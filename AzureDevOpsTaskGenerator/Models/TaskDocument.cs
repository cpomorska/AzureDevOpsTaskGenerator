namespace AzureDevOpsTaskGenerator.Models;

public class TaskDocument
{
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<DevelopmentTask> Tasks { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;
}

public class WorkItemHierarchy
{
    public List<DevelopmentTask> Epics { get; set; } = new();
    public Dictionary<string, List<DevelopmentTask>> EpicToFeatures { get; set; } = new();
    public Dictionary<string, List<DevelopmentTask>> FeatureToStories { get; set; } = new();
    public int TotalStoryPoints { get; set; }
    public int TotalWorkItems { get; set; }
}