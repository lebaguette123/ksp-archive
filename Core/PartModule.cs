namespace Core;

public class PartModule
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}