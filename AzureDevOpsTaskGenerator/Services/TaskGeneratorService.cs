using AzureDevOpsTaskGenerator.Interfaces;
using AzureDevOpsTaskGenerator.Models;

namespace AzureDevOpsTaskGenerator.Services;

public class TaskGeneratorService : ITaskGenerator
{
    // #20 – CancellationToken on public methods
    public Task<WorkItemHierarchy> BuildHierarchyAsync(TaskDocument document,
        CancellationToken cancellationToken = default)
    {
        var hierarchy = new WorkItemHierarchy();

        var epics = document.Tasks.Where(t => t.Type == WorkItemType.Epic).ToList();
        hierarchy.Epics = epics;

        foreach (var epic in epics)
        {
            var features = epic.Children.Where(c => c.Type == WorkItemType.Feature).ToList();
            hierarchy.EpicToFeatures[epic.Id] = features;

            foreach (var feature in features)
            {
                var stories = feature.Children
                    .Where(c => c.Type is WorkItemType.UserStory or WorkItemType.Task)
                    .ToList();
                hierarchy.FeatureToStories[feature.Id] = stories;
            }
        }

        // #12 – only sum leaf nodes to avoid double-counting
        hierarchy.TotalStoryPoints = CalculateLeafStoryPoints(document.Tasks);
        hierarchy.TotalWorkItems = CountAllWorkItems(document.Tasks);

        return Task.FromResult(hierarchy);
    }

    public Task<List<DevelopmentTask>> ExtractTasksAsync(TaskDocument document,
        CancellationToken cancellationToken = default)
    {
        var allTasks = new List<DevelopmentTask>();
        foreach (var task in document.Tasks)
        {
            allTasks.Add(task);
            AddChildrenRecursively(task, allTasks);
        }
        return Task.FromResult(allTasks);
    }

    private static void AddChildrenRecursively(DevelopmentTask parent, List<DevelopmentTask> allTasks)
    {
        foreach (var child in parent.Children)
        {
            allTasks.Add(child);
            AddChildrenRecursively(child, allTasks);
        }
    }

    // #12 – sum leaf nodes; if a parent's children all have 0 SP, use the parent's own points
    private static int CalculateLeafStoryPoints(List<DevelopmentTask> tasks)
    {
        int total = 0;
        foreach (var task in tasks)
        {
            if (task.Children.Count == 0)
            {
                total += task.StoryPoints;
            }
            else
            {
                var childTotal = CalculateLeafStoryPoints(task.Children);
                total += childTotal > 0 ? childTotal : task.StoryPoints;
            }
        }
        return total;
    }

    private static int CountAllWorkItems(List<DevelopmentTask> tasks)
    {
        int count = tasks.Count;
        foreach (var task in tasks)
            count += CountAllWorkItems(task.Children);
        return count;
    }
}
