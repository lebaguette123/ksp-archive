using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using Core;

namespace Import;

public static partial class PartScanner
{
    private const string PreferredLocalizationLanguage = "en-us";
    private static readonly Regex UnicodeEscapePattern = MyRegex();

    static PartScanner()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private sealed class ParseDiagnostics
    {
        public long UnresolvedLocalizationKeys;
        public long SuspiciousTextValues;
        public long EncodingFallbackReads;
    }

    private sealed class FileParseResult
    {
        public List<Part> Parts { get; set; } = [];
        public List<PatchNode> Patches { get; set; } = [];
    }

    public static List<Part> ScanGameData(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return [];

        var files = Directory.EnumerateFiles(folderPath, "*.cfg", SearchOption.AllDirectories).ToArray();
        var diagnostics = new ParseDiagnostics();
        var localizationByLanguage = BuildLocalizationMapByLanguage(files, diagnostics);
        var localizationMap = BuildPreferredLocalizationMap(localizationByLanguage, PreferredLocalizationLanguage);
        
        var baseParts = new ConcurrentBag<Part>();
        var rawPatches = new ConcurrentBag<PatchNode>();
        
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount)
        };

        Parallel.ForEach(files, parallelOptions, filePath =>
        {
            string modOrigin = ExtractModOrigin(filePath, folderPath);
            var result = ParseConfigFile(filePath, modOrigin, localizationMap, diagnostics);
            
            foreach (var part in result.Parts) baseParts.Add(part);
            foreach (var patch in result.Patches) rawPatches.Add(patch);
        });

        // Execute the Patch Resolution Pipeline
        List<Part> finalizedParts = PatchEngine.ApplyPatches(baseParts.ToList(), rawPatches.ToList());

        if (diagnostics.UnresolvedLocalizationKeys != 0 || diagnostics.SuspiciousTextValues != 0 || diagnostics.EncodingFallbackReads != 0)
        {
            Console.WriteLine(
                $"Localization diagnostics: unresolved={diagnostics.UnresolvedLocalizationKeys}, suspiciousText={diagnostics.SuspiciousTextValues}, encodingFallbacks={diagnostics.EncodingFallbackReads}");
        }

        return finalizedParts;
    }

    private static string ExtractModOrigin(string filePath, string folderPath)
    {
        string relativePath = Path.GetRelativePath(folderPath, filePath);
        string[] segments = relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return segments.Length > 0 ? segments[0] : "Squad";
    }

    private static PatchNode? ParsePatchHeader(string line, string modOrigin, string filePath)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        char opChar = line[0];
        PatchOperator op = PatchOperator.Insert;
        string remaining = line;

        if (opChar == '@') { op = PatchOperator.Modify; remaining = line[1..]; }
        else if (opChar == '+') { op = PatchOperator.Copy; remaining = line[1..]; }
        else if (opChar == '!') { op = PatchOperator.Delete; remaining = line[1..]; }
        else if (opChar == '%') { op = PatchOperator.Create; remaining = line[1..]; }

        int openBracket = remaining.IndexOf('[');
        int closeBracket = remaining.IndexOf(']');
        if (openBracket <= 0 || closeBracket <= openBracket) return null;

        string nodeType = remaining[..openBracket].Trim();
        string targetName = remaining[(openBracket + 1)..closeBracket].Trim();

        var node = new PatchNode
        {
            Operator = op,
            NodeType = nodeType.ToUpperInvariant(),
            TargetName = targetName,
            SourceFilePath = filePath,
            ModOrigin = modOrigin
        };

        string suffixes = remaining[(closeBracket + 1)..].Trim();
        if (!string.IsNullOrEmpty(suffixes))
        {
            var parts = suffixes.Split(':', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                int oB = part.IndexOf('[');
                int cB = part.IndexOf(']');
                if (oB > 0 && cB > oB)
                {
                    string directive = part[..oB].ToUpperInvariant();
                    string content = part[(oB + 1)..cB].Trim();

                    if (directive == "NEEDS")
                    {
                        node.NeedsCondition.AddRange(content.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                    }
                    else if (directive == "BEFORE" || directive == "FOR" || directive == "AFTER")
                    {
                        node.RunPhase = directive;
                        node.PhaseModTarget = content;
                    }
                }
                else if (part.ToUpperInvariant() == "FINAL")
                {
                    node.RunPhase = "FINAL";
                }
                else if (part.ToUpperInvariant() == "FIRST")
                {
                    node.RunPhase = "FIRST";
                }
            }
        }

        return node;
    }

    private static FileParseResult ParseConfigFile(
        string filePath,
        string modOrigin,
        IReadOnlyDictionary<string, string> localizationMap,
        ParseDiagnostics diagnostics)
    {
        var result = new FileParseResult();
        Part? activePart = null;
        var moduleStack = new Stack<PartModule?>();
        var resourceStack = new Stack<PartResource?>();
        var patchStack = new Stack<PatchNode>();

        var contextStack = new Stack<string>();
        contextStack.Push("NONE"); 

        var pendingHeader = "NONE";
        PatchNode? pendingPatchNode = null;

        try
        {
            foreach (var rawLine in ReadLinesWithFallback(filePath, diagnostics))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//")) continue;

                if (line.StartsWith("PART", StringComparison.Ordinal)) { pendingHeader = "PART"; continue; }
                if (line.StartsWith("MODULE", StringComparison.Ordinal)) { pendingHeader = "MODULE"; continue; }
                if (line.StartsWith("RESOURCE", StringComparison.Ordinal)) { pendingHeader = "RESOURCE"; continue; }
                
                bool isPatchHeader = line.StartsWith('@') || line.StartsWith('+') || line.StartsWith('!') || line.StartsWith('%') || line.Contains('[');
                if (isPatchHeader)
                {
                    var parsedPatch = ParsePatchHeader(line, modOrigin, filePath);
                    if (parsedPatch != null)
                    {
                        pendingHeader = "PATCH";
                        pendingPatchNode = parsedPatch;
                        continue;
                    }
                }

                if (!line.Contains('=') && !line.Contains('{') && !line.Contains('}') && line.Split(' ').Length == 1)
                {
                    pendingHeader = "UNKNOWN";
                    continue;
                }

                if (line.Contains('{'))
                {
                    var parentContext = contextStack.Peek();
                    contextStack.Push(pendingHeader);
                    
                    switch (pendingHeader)
                    {
                        case "PART":
                            if (activePart != null && IsValidPart(activePart)) result.Parts.Add(activePart);
                            activePart = new Part { FilePath = filePath, ModOrigin = modOrigin };
                            moduleStack.Clear();
                            resourceStack.Clear();
                            break;
                        case "MODULE" when activePart != null:
                            moduleStack.Push(parentContext == "PART" ? new PartModule() : null);
                            break;
                        case "RESOURCE" when activePart != null:
                            resourceStack.Push(parentContext == "PART" ? new PartResource() : null);
                            break;
                        case "PATCH":
                            if (pendingPatchNode != null) patchStack.Push(pendingPatchNode);
                            break;
                    }

                    pendingHeader = "UNKNOWN"; 
                    pendingPatchNode = null;
                    continue;
                }

                if (line == "}")
                {
                    if (contextStack.Count > 1)
                    {
                        var poppedContext = contextStack.Pop();

                        switch (poppedContext)
                        {
                            case "PART":
                                if (activePart != null && IsValidPart(activePart)) result.Parts.Add(activePart);
                                activePart = null;
                                moduleStack.Clear();
                                resourceStack.Clear();
                                break;
                            case "MODULE" when moduleStack.Count != 0:
                                var completedModule = moduleStack.Pop();
                                if (completedModule != null && !string.IsNullOrWhiteSpace(completedModule.Name))
                                {
                                    activePart?.Modules.Add(completedModule);
                                }
                                break;
                            case "RESOURCE" when resourceStack.Count != 0:
                                var completedResource = resourceStack.Pop();
                                if (completedResource != null && !string.IsNullOrWhiteSpace(completedResource.Name))
                                {
                                    activePart?.Resources.Add(completedResource);
                                }
                                break;
                            case "PATCH" when patchStack.Count != 0:
                                var completedPatch = patchStack.Pop();
                                if (patchStack.Count > 0) patchStack.Peek().SubNodes.Add(completedPatch);
                                else result.Patches.Add(completedPatch);
                                break;
                        }
                    }
                    continue;
                }

                if (line.Contains('='))
                {
                    var tokens = line.Split('=', 2);
                    var key = tokens[0].Trim();
                    var value = tokens[1].Trim();
                    var normalizedKey = key.ToLowerInvariant();

                    if (value.Contains("//")) value = value.Split("//")[0].Trim();
                    value = SanitizeText(value, diagnostics);

                    var currentRoom = contextStack.Peek();

                    if (currentRoom == "PATCH" && patchStack.Count > 0)
                    {
                        var activePatch = patchStack.Peek();
                        PatchOperator fieldOp = PatchOperator.Insert;
                        if (key.StartsWith('@')) { fieldOp = PatchOperator.Modify; key = key[1..].Trim(); }
                        else if (key.StartsWith('+')) { fieldOp = PatchOperator.Copy; key = key[1..].Trim(); }
                        else if (key.StartsWith('!')) { fieldOp = PatchOperator.Delete; key = key[1..].Trim(); }
                        else if (key.StartsWith('%')) { fieldOp = PatchOperator.Create; key = key[1..].Trim(); }

                        activePatch.Fields[key] = (fieldOp, value);
                    }
                    else if (activePart != null)
                    {
                        switch (currentRoom)
                        {
                            case "PART":
                                switch (normalizedKey)
                                {
                                    case "name": activePart.Name = value; break;
                                    case "title": activePart.Title = ResolveLocalization(value, localizationMap, diagnostics); break;
                                    case "description": activePart.Description = ResolveLocalization(value, localizationMap, diagnostics); break;
                                    case "category": activePart.Category = ResolveLocalization(value, localizationMap, diagnostics); break;
                                    case "mass": float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var m); activePart.Mass = m; break;
                                    case "cost": int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c); activePart.Cost = c; break;
                                    case "crewcapacity": int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cc); activePart.CrewCapacity = cc; break;
                                    case "maxtemp": float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mt); activePart.MaxTemp = mt; break;
                                    case "skinmaxtemp": float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var smt); activePart.SkinMaxTemp = smt; break;
                                }
                                break;

                            case "MODULE":
                                if (!moduleStack.TryPeek(out var currentModule) || currentModule == null) break;
                                if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase)) currentModule.Name = value;
                                else currentModule.Fields[key] = value;
                                break;

                            case "RESOURCE":
                                if (!resourceStack.TryPeek(out var currentResource) || currentResource == null) break;
                                switch (normalizedKey)
                                {
                                    case "name": currentResource.Name = value; break;
                                    case "amount": float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var amt); currentResource.Amount = amt; break;
                                    case "maxamount": float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var max); currentResource.MaxAmount = max; break;
                                }
                                break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
            return result;
        }

        if (activePart != null && IsValidPart(activePart)) result.Parts.Add(activePart);
        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> BuildLocalizationMapByLanguage(IEnumerable<string> cfgFiles, ParseDiagnostics diagnostics)
    {
        var localizationEntriesByLanguage = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var localizationFiles = cfgFiles.Where(path => path.Contains($"{Path.DirectorySeparatorChar}Localization{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        Parallel.ForEach(localizationFiles, filePath =>
        {
            try
            {
                var currentLanguage = string.Empty;
                var pendingHeader = string.Empty;
                var contextDepth = 0;
                var localizationDepth = -1;
                var languageDepth = -1;

                foreach (var rawLine in ReadLinesWithFallback(filePath, diagnostics))
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal)) continue;
                    if (line.StartsWith("Localization", StringComparison.OrdinalIgnoreCase)) { pendingHeader = "LOCALIZATION"; continue; }
                    if (!line.Contains('=') && !line.Contains('{') && !line.Contains('}') && line.Split(' ').Length == 1) { pendingHeader = line.ToLowerInvariant(); continue; }

                    if (line.Contains('{'))
                    {
                        contextDepth++;
                        if (pendingHeader == "LOCALIZATION") localizationDepth = contextDepth;
                        else if (localizationDepth > 0 && contextDepth == localizationDepth + 1) { currentLanguage = pendingHeader; languageDepth = contextDepth; }
                        pendingHeader = string.Empty;
                        continue;
                    }
                    if (line == "}")
                    {
                        if (contextDepth == languageDepth) { currentLanguage = string.Empty; languageDepth = -1; }
                        else if (contextDepth == localizationDepth) localizationDepth = -1;
                        contextDepth = Math.Max(0, contextDepth - 1);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(currentLanguage) || !IsLocalizationToken(line)) continue;
                    var separatorIndex = line.IndexOf('=');
                    if (separatorIndex <= 0) continue;

                    var key = line[..separatorIndex].Trim();
                    var value = line[(separatorIndex + 1)..].Trim();
                    if (value.Contains("//")) value = value.Split("//")[0].Trim();
                    value = SanitizeText(value, diagnostics);

                    var languageEntries = localizationEntriesByLanguage.GetOrAdd(currentLanguage, _ => new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase));
                    languageEntries.TryAdd(key, value);
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error reading localization file {filePath}: {ex.Message}"); }
        });

        return localizationEntriesByLanguage.ToDictionary(pair => pair.Key, pair => (IReadOnlyDictionary<string, string>)pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> BuildPreferredLocalizationMap(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> entriesByLanguage, string preferredLanguage)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var precedence = new List<string>();

        if (!string.IsNullOrWhiteSpace(preferredLanguage)) precedence.Add(preferredLanguage.ToLowerInvariant());
        if (!precedence.Contains("en-us", StringComparer.OrdinalIgnoreCase)) precedence.Add("en-us");

        foreach (var availableLanguage in entriesByLanguage.Keys.OrderBy(l => l, StringComparer.OrdinalIgnoreCase))
        {
            if (!precedence.Contains(availableLanguage, StringComparer.OrdinalIgnoreCase)) precedence.Add(availableLanguage);
        }

        foreach (var language in precedence)
        {
            if (!entriesByLanguage.TryGetValue(language, out var languageEntries)) continue;
            foreach (var (key, value) in languageEntries) merged.TryAdd(key, value);
        }

        return merged;
    }

    private static string[] ReadLinesWithFallback(string filePath, ParseDiagnostics diagnostics)
    {
        try { return File.ReadAllLines(filePath, new UTF8Encoding(false, true)); }
        catch (DecoderFallbackException)
        {
            Interlocked.Increment(ref diagnostics.EncodingFallbackReads);
            try { return File.ReadAllLines(filePath, Encoding.GetEncoding(1252)); }
            catch (ArgumentException) { return File.ReadAllLines(filePath, Encoding.Latin1); }
        }
    }

    private static bool IsLocalizationToken(string value) => value.StartsWith("#LOC_", StringComparison.OrdinalIgnoreCase) || value.StartsWith("#autoLOC_", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeText(string value, ParseDiagnostics diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var cleaned = UnicodeEscapePattern.Replace(value, match => ((char)int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString()).Normalize(NormalizationForm.FormC);
        if (cleaned.Any(ch => ch == '\uFFFD')) Interlocked.Increment(ref diagnostics.SuspiciousTextValues);
        return new string(cleaned.Where(ch => !char.IsControl(ch) || ch == '\t').ToArray()).Trim();
    }

    private static string ResolveLocalization(string value, IReadOnlyDictionary<string, string> localizationMap, ParseDiagnostics diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value) || !IsLocalizationToken(value)) return value;
        if (localizationMap.TryGetValue(value, out var localized)) return localized;
        Interlocked.Increment(ref diagnostics.UnresolvedLocalizationKeys);
        return value;
    }

    private static bool IsValidPart(Part part) => !string.IsNullOrWhiteSpace(part.Name) || !string.IsNullOrWhiteSpace(part.Title);
    [GeneratedRegex(@"\\u([0-9a-fA-F]{4})", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}