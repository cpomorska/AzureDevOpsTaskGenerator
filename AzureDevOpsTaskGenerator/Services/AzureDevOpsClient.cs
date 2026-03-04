using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using AzureDevOpsTaskGenerator.Interfaces;
using AzureDevOpsTaskGenerator.Models;
using WorkItemType = AzureDevOpsTaskGenerator.Models.WorkItemType;

namespace AzureDevOpsTaskGenerator.Services;

public class AzureDevOpsClient : IAzureDevOpsClient
{
    private VssConnection? _connection;
    private WorkItemTrackingHttpClient? _workItemClient;
    private string _organizationUrl = string.Empty;

    // #20 – CancellationToken throughout
    public async Task<bool> AuthenticateAsync(string organizationUrl, string personalAccessToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _organizationUrl = organizationUrl;
            var credentials = new VssBasicCredential(string.Empty, personalAccessToken);
            _connection = new VssConnection(new Uri(organizationUrl), credentials);

            await _connection.ConnectAsync();
            _workItemClient = await _connection.GetClientAsync<WorkItemTrackingHttpClient>();

            return await TestConnectionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Authentication failed: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_workItemClient == null) return false;
            var projects = await _connection!
                .GetClient<Microsoft.TeamFoundation.Core.WebApi.ProjectHttpClient>()
                .GetProjects();
            return projects.Any();
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> CreateWorkItemAsync(DevelopmentTask task, string project,
        int? parentId = null, CancellationToken cancellationToken = default)
    {
        if (_workItemClient == null)
            throw new InvalidOperationException("Not authenticated. Call AuthenticateAsync first.");

        // #13 – validate title before sending
        if (string.IsNullOrWhiteSpace(task.Title))
            throw new ArgumentException($"Work item title must not be empty. Task ID: {task.Id}");

        var workItemType = MapToAzureDevOpsWorkItemType(task.Type);
        var patchDocument = new JsonPatchDocument();

        patchDocument.Add(new JsonPatchOperation
        {
            Operation = Operation.Add,
            Path = "/fields/System.Title",
            Value = task.Title
        });

        // #8 – hold reference to description operation for later update
        var descriptionOp = new JsonPatchOperation
        {
            Operation = Operation.Add,
            Path = "/fields/System.Description",
            Value = BuildDescription(task)
        };
        patchDocument.Add(descriptionOp);

        // #6 – Critical → priority 1 + Severity; High → priority 1; others mapped normally
        patchDocument.Add(new JsonPatchOperation
        {
            Operation = Operation.Add,
            Path = "/fields/Microsoft.VSTS.Common.Priority",
            Value = MapPriority(task.Priority)
        });

        if (task.Priority == Priority.Critical)
        {
            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/Microsoft.VSTS.Common.Severity",
                Value = "1 - Critical"
            });
        }

        // #7 – Story Points for Epic, Feature and UserStory
        if (task.Type is WorkItemType.Epic or WorkItemType.Feature or WorkItemType.UserStory)
        {
            if (task.StoryPoints > 0)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/Microsoft.VSTS.Scheduling.StoryPoints",
                    Value = task.StoryPoints
                });
            }
        }

        if (task.Type == WorkItemType.Task && task.StoryPoints > 0)
        {
            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/Microsoft.VSTS.Scheduling.OriginalEstimate",
                Value = task.StoryPoints
            });
        }

        if (!string.IsNullOrEmpty(task.BusinessValue))
        {
            var bv = MapBusinessValue(task.BusinessValue);
            if (bv.HasValue)
            {
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = "/fields/Microsoft.VSTS.Common.BusinessValue",
                    Value = bv.Value
                });
            }
        }

        // combined tags: explicit tags + theme (#10)
        var allTags = new List<string>(task.Tags);
        if (!string.IsNullOrWhiteSpace(task.Theme))
            allTags.Add(task.Theme);

        if (allTags.Count > 0)
        {
            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/fields/System.Tags",
                Value = string.Join("; ", allTags)
            });
        }

        // #14 – include parent relation directly in the creation request
        if (parentId.HasValue)
        {
            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Add,
                Path = "/relations/-",
                Value = new
                {
                    rel = "System.LinkTypes.Hierarchy-Reverse",
                    url = $"{_organizationUrl}/_apis/wit/workItems/{parentId.Value}"
                }
            });
        }

        try
        {
            var workItem = await _workItemClient.CreateWorkItemAsync(
                patchDocument, project, workItemType, cancellationToken: cancellationToken);
            return workItem.Id ?? 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create work item '{task.Title}': {ex.Message}");
            throw;
        }
    }

    public async Task<List<int>> CreateWorkItemHierarchyAsync(WorkItemHierarchy hierarchy, string project,
        CancellationToken cancellationToken = default)
    {
        if (_workItemClient == null)
            throw new InvalidOperationException("Not authenticated. Call AuthenticateAsync first.");

        var createdIds = new List<int>();

        foreach (var epic in hierarchy.Epics)
        {
            // #15 – per-item error handling
            int epicId = 0;
            try
            {
                epicId = await CreateWorkItemAsync(epic, project, null, cancellationToken);
                createdIds.Add(epicId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Skipping Epic '{epic.Title}': {ex.Message}");
                continue;
            }

            foreach (var feature in epic.Children.Where(c => c.Type == WorkItemType.Feature))
            {
                int featureId = 0;
                try
                {
                    featureId = await CreateWorkItemAsync(feature, project, epicId, cancellationToken);
                    createdIds.Add(featureId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Skipping Feature '{feature.Title}': {ex.Message}");
                    continue;
                }

                foreach (var story in feature.Children)
                {
                    int storyId = 0;
                    try
                    {
                        storyId = await CreateWorkItemAsync(story, project, featureId, cancellationToken);
                        createdIds.Add(storyId);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Skipping Story/Task '{story.Title}': {ex.Message}");
                        continue;
                    }

                    foreach (var child in story.Children.Where(c => c.Type == WorkItemType.Task))
                    {
                        try
                        {
                            var childId = await CreateWorkItemAsync(child, project, storyId, cancellationToken);
                            createdIds.Add(childId);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Skipping Task '{child.Title}': {ex.Message}");
                        }
                    }
                }
            }

            foreach (var child in epic.Children.Where(c => c.Type != WorkItemType.Feature))
            {
                try
                {
                    var childId = await CreateWorkItemAsync(child, project, epicId, cancellationToken);
                    createdIds.Add(childId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Skipping '{child.Title}': {ex.Message}");
                }
            }
        }

        return createdIds;
    }

    // #8 – description built once, reference kept
    private static string BuildDescription(DevelopmentTask task)
    {
        var desc = task.Description ?? "";

        if (task.AcceptanceCriteria.Count > 0)
        {
            if (!string.IsNullOrEmpty(desc)) desc += "<br/><br/>";
            desc += "<strong>Acceptance Criteria:</strong><br/>";
            desc += string.Join("<br/>", task.AcceptanceCriteria.Select((ac, i) => $"{i + 1}. {ac}"));
        }

        return desc;
    }

    private static string MapToAzureDevOpsWorkItemType(WorkItemType type) => type switch
    {
        WorkItemType.Epic => "Epic",
        WorkItemType.Feature => "Feature",
        WorkItemType.UserStory => "User Story",
        WorkItemType.Task => "Task",
        WorkItemType.Bug => "Bug",
        _ => "Task"
    };

    private static int? MapBusinessValue(string value)
    {
        if (int.TryParse(value, out int r)) return r;
        return value.Trim().ToLowerInvariant() switch
        {
            "high" => 100,
            "medium" => 50,
            "low" => 10,
            _ => null
        };
    }

    // #6 – Critical and High both map to ADO priority 1; Severity distinguishes them
    private static int MapPriority(Priority priority) => priority switch
    {
        Priority.Critical => 1,
        Priority.High => 1,
        Priority.Medium => 2,
        Priority.Low => 3,
        _ => 2
    };
}

