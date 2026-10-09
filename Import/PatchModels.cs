namespace Import;

public enum PatchOperator
{
    Modify, // @
    Copy,   // +
    Delete, // !
    Create, // %
    Insert  // Default
}

public class PatchNode
{
    public PatchOperator Operator { get; set; } = PatchOperator.Insert;
    public string NodeType { get; set; } = string.Empty;
    public string TargetName { get; set; } = string.Empty; 
    public string RunPhase { get; set; } = "LEGACY"; 
    public string PhaseModTarget { get; set; } = string.Empty; 
    public List<string> NeedsCondition { get; set; } = []; 
    public Dictionary<string, (PatchOperator Op, string Value)> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<PatchNode> SubNodes { get; set; } = [];
    public string SourceFilePath { get; set; } = string.Empty;
    public string ModOrigin { get; set; } = string.Empty;
}