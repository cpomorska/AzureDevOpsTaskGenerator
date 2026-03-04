using AzureDevOpsTaskGenerator.Models;

namespace AzureDevOpsTaskGenerator.Interfaces;

public interface ITextFileParser
{
    Task<TaskDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default);
    bool CanParse(string filePath);
    string[] SupportedExtensions { get; }
}

public interface ITaskGenerator
{
    Task<WorkItemHierarchy> BuildHierarchyAsync(TaskDocument document, CancellationToken cancellationToken = default);
    Task<List<DevelopmentTask>> ExtractTasksAsync(TaskDocument document, CancellationToken cancellationToken = default);
}

public interface IAzureDevOpsClient
{
    Task<bool> AuthenticateAsync(string organizationUrl, string personalAccessToken, CancellationToken cancellationToken = default);
    Task<int> CreateWorkItemAsync(DevelopmentTask task, string project, int? parentId = null, CancellationToken cancellationToken = default);
    Task<List<int>> CreateWorkItemHierarchyAsync(WorkItemHierarchy hierarchy, string project, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}
