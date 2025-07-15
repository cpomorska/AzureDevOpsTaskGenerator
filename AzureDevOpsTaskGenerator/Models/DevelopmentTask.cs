namespace AzureDevOpsTaskGenerator.Models;

public class DevelopmentTask
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkItemType Type { get; set; }
    public Priority Priority { get; set; }
    public int StoryPoints { get; set; }
    public List<string> AcceptanceCriteria { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
    public string Theme { get; set; } = string.Empty;
    public string BusinessValue { get; set; } = string.Empty;
    public List<DevelopmentTask> Children { get; set; } = new();
    public DevelopmentTask? Parent { get; set; }
}

public enum WorkItemType
{
    Epic,
    Feature,
    UserStory,
    Task,
    Bug
}

public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}