using AzureDevOpsTaskGenerator.Models;

namespace AzureDevOpsTaskGenerator.Interfaces;

public interface ITextFileParser
{
    Task<TaskDocument> ParseAsync(string filePath);
    Task<TaskDocument> ParseFromTextAsync(string content, string fileName = "");
    bool CanParse(string filePath);
    string[] SupportedExtensions { get; }
}

public interface ITaskGenerator
{
    Task<WorkItemHierarchy> BuildHierarchyAsync(TaskDocument document);
    Task<List<DevelopmentTask>> ExtractTasksAsync(TaskDocument document);
}

public interface IAzureDevOpsClient
{
    Task<bool> AuthenticateAsync(string organizationUrl, string personalAccessToken);
    Task<int> CreateWorkItemAsync(DevelopmentTask task, string project, int? parentId = null);
    Task<List<int>> CreateWorkItemHierarchyAsync(WorkItemHierarchy hierarchy, string project);
    Task<bool> TestConnectionAsync();
}