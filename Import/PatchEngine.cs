using System.Globalization;
using System.Reflection;
using Core;

namespace Import;

public static class PatchEngine
{
    public static List<Part> ApplyPatches(List<Part> baseParts, List<PatchNode> allPatches)
    {
        var discoveredMods = baseParts.Select(p => p.ModOrigin)
            .Concat(allPatches.Select(p => p.ModOrigin))
            .Where(m => !string.IsNullOrEmpty(m))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var activePatches = allPatches
            .Where(p => EvaluateNeedsCondition(p.NeedsCondition, discoveredMods))
            .ToList();

        var orderedPatches = activePatches
            .OrderBy(p => GetPhaseWeight(p.RunPhase))
            .ThenBy(p => p.PhaseModTarget, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var partMap = baseParts
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var patch in orderedPatches)
        {
            if (!string.Equals(patch.NodeType, "PART", StringComparison.OrdinalIgnoreCase)) continue;

            List<Part> targets = [];
            
            if (patch.TargetName.Contains('*'))
            {
                string prefix = patch.TargetName.Replace("*", "");
                targets = baseParts.Where(p => p.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else if (partMap.TryGetValue(patch.TargetName, out var match))
            {
                targets.Add(match);
            }

            foreach (var target in targets)
            {
                ExecutePatchOnPart(target, patch);
            }
        }

        return baseParts;
    }
    
    private static int GetPhaseWeight(string phase) => phase.ToUpperInvariant() switch
    {
        "FIRST"  => 1,
        "LEGACY" => 2,
        "BEFORE" => 3,
        "FOR"    => 4,
        "AFTER"  => 5,
        "FINAL"  => 6,
        _        => 2
    };
    
    private static bool EvaluateNeedsCondition(List<string> needs, HashSet<string> activeMods)
    {
        return !(from need in needs let invert = need.StartsWith('!') let targetMod = invert ? need[1..].Trim() : need let exists = activeMods.Contains(targetMod) where (invert && exists) || (!invert && !exists) select invert).Any();
    }
    
    private static void ExecutePatchOnPart(Part part, PatchNode patch)
    {
        if (patch.Operator == PatchOperator.Delete) return; 
        part.PatchLog.Add($"[{patch.RunPhase}] Patch applied by mod [{patch.ModOrigin}] from {Path.GetFileName(patch.SourceFilePath)}");

        foreach (var (fieldName, fieldInfo) in patch.Fields)
        {
            ApplyFieldMutation(part, fieldName, fieldInfo.Op, fieldInfo.Value);
        }

        foreach (var subPatch in patch.SubNodes)
        {
            if (string.Equals(subPatch.NodeType, "MODULE", StringComparison.OrdinalIgnoreCase))
            {
                ApplyModuleMutation(part, subPatch);
            }
            else if (string.Equals(subPatch.NodeType, "RESOURCE", StringComparison.OrdinalIgnoreCase))
            {
                ApplyResourceMutation(part, subPatch);
            }
        }
    }
    
    private static void ApplyFieldMutation(Part part, string fieldName, PatchOperator op, string value)
    {
        var prop = typeof(Part).GetProperty(fieldName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (prop == null || !prop.CanWrite) return;

        try
        {
            if (prop.PropertyType == typeof(string))
            {
                prop.SetValue(part, value);
            }
            else if (prop.PropertyType == typeof(float) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                prop.SetValue(part, f);
            }
            else if (prop.PropertyType == typeof(int) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
            {
                prop.SetValue(part, i);
            }
        }
        catch { /* Diagnostic suppression */ }
    }
    
    private static void ApplyModuleMutation(Part part, PatchNode subPatch)
    {
        var existingModule = part.Modules.FirstOrDefault(m => string.Equals(m.Name, subPatch.TargetName, StringComparison.OrdinalIgnoreCase));

        if (subPatch.Operator == PatchOperator.Delete)
        {
            if (existingModule != null) part.Modules.Remove(existingModule);
            return;
        }

        if (existingModule == null)
        {
            if (subPatch.Operator == PatchOperator.Modify) return; 
            existingModule = new PartModule { Name = subPatch.TargetName };
            part.Modules.Add(existingModule);
        }

        foreach (var (key, fieldInfo) in subPatch.Fields)
        {
            switch (fieldInfo.Op)
            {
                case PatchOperator.Delete:
                    existingModule.Fields.Remove(key);
                    break;
                case PatchOperator.Modify:
                case PatchOperator.Copy:
                case PatchOperator.Create:
                case PatchOperator.Insert:
                    break;
                default:
                    existingModule.Fields[key] = fieldInfo.Value;
                    break;
            }
        }
    }
    
    private static void ApplyResourceMutation(Part part, PatchNode subPatch)
    {
        var existingResource = part.Resources.FirstOrDefault(r => string.Equals(r.Name, subPatch.TargetName, StringComparison.OrdinalIgnoreCase));

        if (subPatch.Operator == PatchOperator.Delete)
        {
            if (existingResource != null) part.Resources.Remove(existingResource);
            return;
        }

        if (existingResource == null)
        {
            if (subPatch.Operator == PatchOperator.Modify) return;
            existingResource = new PartResource { Name = subPatch.TargetName };
            part.Resources.Add(existingResource);
        }

        foreach (var (key, fieldInfo) in subPatch.Fields)
        {
            var normKey = key.ToLowerInvariant();
            switch (normKey)
            {
                case "amount" when float.TryParse(fieldInfo.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var amt):
                    existingResource.Amount = amt;
                    break;
                case "maxamount" when float.TryParse(fieldInfo.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var max):
                    existingResource.MaxAmount = max;
                    break;
            }
        }
    }
}