using AzureDevOpsTaskGenerator.Interfaces;
using AzureDevOpsTaskGenerator.Models;

namespace AzureDevOpsTaskGenerator.Services;

public class TaskGeneratorService : ITaskGenerator
{
    public Task<WorkItemHierarchy> BuildHierarchyAsync(TaskDocument document)
    {
        var hierarchy = new WorkItemHierarchy();
        
        // Extract epics from the document
        var epics = document.Tasks.Where(t => t.Type == WorkItemType.Epic).ToList();
        hierarchy.Epics = epics;

        // Build Epic to Features mapping
        foreach (var epic in epics)
        {
            var features = epic.Children.Where(c => c.Type == WorkItemType.Feature).ToList();
            hierarchy.EpicToFeatures[epic.Id] = features;

            // Build Feature to Stories mapping
            foreach (var feature in features)
            {
                var stories = feature.Children.Where(c => c.Type == WorkItemType.UserStory || c.Type == WorkItemType.Task).ToList();
                hierarchy.FeatureToStories[feature.Id] = stories;
            }
        }

        // Calculate totals
        hierarchy.TotalStoryPoints = CalculateTotalStoryPoints(document.Tasks);
        hierarchy.TotalWorkItems = CountAllWorkItems(document.Tasks);

        return Task.FromResult(hierarchy);
    }

    public Task<List<DevelopmentTask>> ExtractTasksAsync(TaskDocument document)
    {
        var allTasks = new List<DevelopmentTask>();
        
        // Flatten the hierarchy to get all tasks
        foreach (var task in document.Tasks)
        {
            allTasks.Add(task);
            AddChildrenRecursively(task, allTasks);
        }

        return Task.FromResult(allTasks);
    }

    private void AddChildrenRecursively(DevelopmentTask parent, List<DevelopmentTask> allTasks)
    {
        foreach (var child in parent.Children)
        {
            allTasks.Add(child);
            AddChildrenRecursively(child, allTasks);
        }
    }

    private int CalculateTotalStoryPoints(List<DevelopmentTask> tasks)
    {
        int total = 0;
        
        foreach (var task in tasks)
        {
            total += task.StoryPoints;
            total += CalculateTotalStoryPoints(task.Children);
        }

        return total;
    }

    private int CountAllWorkItems(List<DevelopmentTask> tasks)
    {
        int count = tasks.Count;
        
        foreach (var task in tasks)
        {
            count += CountAllWorkItems(task.Children);
        }

        return count;
    }
}