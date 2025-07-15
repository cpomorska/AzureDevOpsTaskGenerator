using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using AzureDevOpsTaskGenerator.Interfaces;
using AzureDevOpsTaskGenerator.Models;

namespace AzureDevOpsTaskGenerator.Services;

public class AzureDevOpsClient : IAzureDevOpsClient
{
    private VssConnection? _connection;
    private WorkItemTrackingHttpClient? _workItemClient;
    private string _organizationUrl = string.Empty;

    public async Task<bool> AuthenticateAsync(string organizationUrl, string personalAccessToken)
    {
        try
        {
            _organizationUrl = organizationUrl;
            var credentials = new VssBasicCredential(string.Empty, personalAccessToken);
            _connection = new VssConnection(new Uri(organizationUrl), credentials);
            
            await _connection.ConnectAsync();
            _workItemClient = _connection.GetClient<WorkItemTrackingHttpClient>();
            
            return await TestConnectionAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            if (_workItemClient == null) return false;
            
            // Try to get work item types to test connection
            var projects = await _connection!.GetClient<Microsoft.TeamFoundation.Core.WebApi.ProjectHttpClient>()
                .GetProjects();
            
            return projects.Any();
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> CreateWorkItemAsync(DevelopmentTask task, string project, int? parentId = null)
    {
        if (_workItemClient == null)
            throw new InvalidOperationException("Not authenticated. Call AuthenticateAsync first.");

        var workItemType = MapToAzureDevOpsWorkItemType(task.Type);
        var patchDocument = new JsonPatchDocument();

        // Required fields
        patchDocument.Add(new JsonPatchOperation()
        {
            Operation = Operation.Add,
            Path = "/fields/System.Title",
            Value = task.Title
        });

        patchDocument.Add(new JsonPatchOperation()
        {
            Operation = Operation.Add,
            Path = "/fields/System.Description",
            Value = task.Description
        });

        // Priority
        patchDocument.Add(new JsonPatchOperation()
        {
            Operation = Operation.Add,
            Path = "/fields/Microsoft.VSTS.Common.Priority",
            Value = MapPriority(task.Priority)
        });

        // Story Points (for User Stories and Features)
        if (task.Type == Models.WorkItemType.UserStory || task.Type == Models.WorkItemType.Feature)
        {
            if (task.StoryPoints > 0)
            {
                patchDocument.Add(new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/Microsoft.VSTS.Scheduling.StoryPoints",
                    Value = task.StoryPoints
                });
            }
        }

        // Effort (for Tasks)
        if (task.Type == Models.WorkItemType.Task && task.StoryPoints > 0)
        {
            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Operation.Add,
                Path = "/fields/Microsoft.VSTS.Scheduling.OriginalEstimate",
                Value = task.StoryPoints
            });
        }

        // Business Value
        if (!string.IsNullOrEmpty(task.BusinessValue))
        {
            var mappedBusinessValue = MapBusinessValue(task.BusinessValue);
            if (mappedBusinessValue.HasValue)
            {
                patchDocument.Add(new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/Microsoft.VSTS.Common.BusinessValue",
                    Value = mappedBusinessValue.Value
                });
            }
        }

        // Tags
        if (task.Tags.Any())
        {
            patchDocument.Add(new JsonPatchOperation()
            {
                Operation = Operation.Add,
                Path = "/fields/System.Tags",
                Value = string.Join("; ", task.Tags)
            });
        }

        // Acceptance Criteria (in description for now)
        if (task.AcceptanceCriteria.Any())
        {
            var fullDescription = task.Description;
            if (!string.IsNullOrEmpty(fullDescription))
                fullDescription += "<br/><br/>";
            
            fullDescription += "<strong>Acceptance Criteria:</strong><br/>";
            fullDescription += string.Join("<br/>", task.AcceptanceCriteria.Select((ac, i) => $"{i + 1}. {ac}"));
            
            patchDocument[1].Value = fullDescription; // Update description
        }

        try
        {
            var workItem = await _workItemClient.CreateWorkItemAsync(patchDocument, project, workItemType);
            
            // Create parent-child relationship if parentId is provided
            if (parentId.HasValue && workItem.Id.HasValue)
            {
                await CreateParentChildRelationship(workItem.Id.Value, parentId.Value);
            }

            return workItem.Id ?? 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create work item '{task.Title}': {ex.Message}");
            throw;
        }
    }

    public async Task<List<int>> CreateWorkItemHierarchyAsync(WorkItemHierarchy hierarchy, string project)
    {
        if (_workItemClient == null)
            throw new InvalidOperationException("Not authenticated. Call AuthenticateAsync first.");

        var createdWorkItemIds = new List<int>();

        // Create Epics first
        foreach (var epic in hierarchy.Epics)
        {
            var epicId = await CreateWorkItemAsync(epic, project);
            createdWorkItemIds.Add(epicId);

            // Create Features under this Epic
            foreach (var feature in epic.Children.Where(c => c.Type == Models.WorkItemType.Feature))
            {
                var featureId = await CreateWorkItemAsync(feature, project, epicId);
                createdWorkItemIds.Add(featureId);

                // Create User Stories/Tasks under this Feature
                foreach (var story in feature.Children)
                {
                    var storyId = await CreateWorkItemAsync(story, project, featureId);
                    createdWorkItemIds.Add(storyId);

                    // Create Tasks under User Stories if any
                    foreach (var task in story.Children.Where(c => c.Type == Models.WorkItemType.Task))
                    {
                        var taskId = await CreateWorkItemAsync(task, project, storyId);
                        createdWorkItemIds.Add(taskId);
                    }
                }
            }

            // Create direct children (User Stories/Tasks) under Epic if no Features
            foreach (var child in epic.Children.Where(c => c.Type != Models.WorkItemType.Feature))
            {
                var childId = await CreateWorkItemAsync(child, project, epicId);
                createdWorkItemIds.Add(childId);
            }
        }

        return createdWorkItemIds;
    }

    private async Task CreateParentChildRelationship(int childId, int parentId)
    {
        var patchDocument = new JsonPatchDocument();
        
        patchDocument.Add(new JsonPatchOperation()
        {
            Operation = Operation.Add,
            Path = "/relations/-",
            Value = new
            {
                rel = "System.LinkTypes.Hierarchy-Reverse",
                url = $"{_organizationUrl}/_apis/wit/workItems/{parentId}"
            }
        });

        await _workItemClient!.UpdateWorkItemAsync(patchDocument, childId);
    }

    private string MapToAzureDevOpsWorkItemType(Models.WorkItemType type)
    {
        return type switch
        {
            Models.WorkItemType.Epic => "Epic",
            Models.WorkItemType.Feature => "Feature",
            Models.WorkItemType.UserStory => "User Story",
            Models.WorkItemType.Task => "Task",
            Models.WorkItemType.Bug => "Bug",
            _ => "Task"
        };
    }

    private int? MapBusinessValue(string value)
    {
        if (int.TryParse(value, out int result))
            return result;
        switch (value.Trim().ToLowerInvariant())
        {
            case "high": return 100;
            case "medium": return 50;
            case "low": return 10;
            default: return null;
        }
    }

    private int MapPriority(Priority priority)
    {
        switch (priority)
        {
            case Priority.Critical:
            case Priority.High:
                return 1;
            case Priority.Medium:
                return 2;
            case Priority.Low:
                return 3;
            default:
                return 2; // Default to Medium if unknown
        }
    }
}