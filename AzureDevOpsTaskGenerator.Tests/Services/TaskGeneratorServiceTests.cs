using FluentAssertions;
using Xunit;
using AzureDevOpsTaskGenerator.Services;
using AzureDevOpsTaskGenerator.Models;

namespace AzureDevOpsTaskGenerator.Tests.Services;

public class TaskGeneratorServiceTests
{
    private readonly TaskGeneratorService _service;

    public TaskGeneratorServiceTests()
    {
        _service = new TaskGeneratorService();
    }

    [Fact]
    public async Task BuildHierarchyAsync_ShouldCreateCorrectHierarchy_WithEpicsAndFeatures()
    {
        // Arrange
        var epic1 = new DevelopmentTask
        {
            Id = "epic1",
            Title = "Security Epic",
            Type = WorkItemType.Epic,
            StoryPoints = 21
        };

        var feature1 = new DevelopmentTask
        {
            Id = "feature1",
            Title = "JWT Authentication",
            Type = WorkItemType.Feature,
            StoryPoints = 8,
            Parent = epic1
        };

        var feature2 = new DevelopmentTask
        {
            Id = "feature2",
            Title = "API Security",
            Type = WorkItemType.Feature,
            StoryPoints = 5,
            Parent = epic1
        };

        epic1.Children.AddRange(new[] { feature1, feature2 });

        var document = new TaskDocument
        {
            Title = "Test Document",
            Tasks = new List<DevelopmentTask> { epic1 }
        };

        // Act
        var hierarchy = await _service.BuildHierarchyAsync(document);

        // Assert
        hierarchy.Epics.Should().HaveCount(1);
        hierarchy.Epics.First().Should().Be(epic1);
        
        hierarchy.EpicToFeatures.Should().ContainKey("epic1");
        hierarchy.EpicToFeatures["epic1"].Should().HaveCount(2);
        hierarchy.EpicToFeatures["epic1"].Should().Contain(feature1);
        hierarchy.EpicToFeatures["epic1"].Should().Contain(feature2);
        
        hierarchy.TotalStoryPoints.Should().Be(34); // 21 + 8 + 5
        hierarchy.TotalWorkItems.Should().Be(3);
    }

    [Fact]
    public async Task BuildHierarchyAsync_ShouldHandleFeaturesToStoriesMapping()
    {
        // Arrange
        var epic = new DevelopmentTask
        {
            Id = "epic1",
            Title = "Test Epic",
            Type = WorkItemType.Epic,
            StoryPoints = 20
        };

        var feature = new DevelopmentTask
        {
            Id = "feature1",
            Title = "Test Feature",
            Type = WorkItemType.Feature,
            StoryPoints = 10,
            Parent = epic
        };

        var story1 = new DevelopmentTask
        {
            Id = "story1",
            Title = "User Story 1",
            Type = WorkItemType.UserStory,
            StoryPoints = 3,
            Parent = feature
        };

        var task1 = new DevelopmentTask
        {
            Id = "task1",
            Title = "Task 1",
            Type = WorkItemType.Task,
            StoryPoints = 2,
            Parent = feature
        };

        feature.Children.AddRange(new[] { story1, task1 });
        epic.Children.Add(feature);

        var document = new TaskDocument
        {
            Tasks = new List<DevelopmentTask> { epic }
        };

        // Act
        var hierarchy = await _service.BuildHierarchyAsync(document);

        // Assert
        hierarchy.FeatureToStories.Should().ContainKey("feature1");
        hierarchy.FeatureToStories["feature1"].Should().HaveCount(2);
        hierarchy.FeatureToStories["feature1"].Should().Contain(story1);
        hierarchy.FeatureToStories["feature1"].Should().Contain(task1);
        
        hierarchy.TotalStoryPoints.Should().Be(35); // 20 + 10 + 3 + 2
        hierarchy.TotalWorkItems.Should().Be(4);
    }

    [Fact]
    public async Task BuildHierarchyAsync_ShouldHandleMultipleEpics()
    {
        // Arrange
        var epic1 = new DevelopmentTask
        {
            Id = "epic1",
            Title = "Security Epic",
            Type = WorkItemType.Epic,
            StoryPoints = 21
        };

        var epic2 = new DevelopmentTask
        {
            Id = "epic2",
            Title = "Performance Epic",
            Type = WorkItemType.Epic,
            StoryPoints = 13
        };

        var document = new TaskDocument
        {
            Tasks = new List<DevelopmentTask> { epic1, epic2 }
        };

        // Act
        var hierarchy = await _service.BuildHierarchyAsync(document);

        // Assert
        hierarchy.Epics.Should().HaveCount(2);
        hierarchy.Epics.Should().Contain(epic1);
        hierarchy.Epics.Should().Contain(epic2);
        hierarchy.TotalStoryPoints.Should().Be(34);
        hierarchy.TotalWorkItems.Should().Be(2);
    }

    [Fact]
    public async Task ExtractTasksAsync_ShouldFlattenHierarchy()
    {
        // Arrange
        var epic = new DevelopmentTask
        {
            Id = "epic1",
            Title = "Test Epic",
            Type = WorkItemType.Epic
        };

        var feature = new DevelopmentTask
        {
            Id = "feature1",
            Title = "Test Feature",
            Type = WorkItemType.Feature,
            Parent = epic
        };

        var story = new DevelopmentTask
        {
            Id = "story1",
            Title = "Test Story",
            Type = WorkItemType.UserStory,
            Parent = feature
        };

        var task = new DevelopmentTask
        {
            Id = "task1",
            Title = "Test Task",
            Type = WorkItemType.Task,
            Parent = story
        };

        story.Children.Add(task);
        feature.Children.Add(story);
        epic.Children.Add(feature);

        var document = new TaskDocument
        {
            Tasks = new List<DevelopmentTask> { epic }
        };

        // Act
        var allTasks = await _service.ExtractTasksAsync(document);

        // Assert
        allTasks.Should().HaveCount(4);
        allTasks.Should().Contain(epic);
        allTasks.Should().Contain(feature);
        allTasks.Should().Contain(story);
        allTasks.Should().Contain(task);
    }

    [Fact]
    public async Task ExtractTasksAsync_ShouldHandleEmptyDocument()
    {
        // Arrange
        var document = new TaskDocument
        {
            Tasks = new List<DevelopmentTask>()
        };

        // Act
        var allTasks = await _service.ExtractTasksAsync(document);

        // Assert
        allTasks.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildHierarchyAsync_ShouldCalculateCorrectTotals_WithNestedStructure()
    {
        // Arrange
        var epic = new DevelopmentTask
        {
            Id = "epic1",
            Title = "Test Epic",
            Type = WorkItemType.Epic,
            StoryPoints = 0 // Epic itself has no points
        };

        var feature1 = new DevelopmentTask
        {
            Id = "feature1",
            Title = "Feature 1",
            Type = WorkItemType.Feature,
            StoryPoints = 8,
            Parent = epic
        };

        var feature2 = new DevelopmentTask
        {
            Id = "feature2",
            Title = "Feature 2",
            Type = WorkItemType.Feature,
            StoryPoints = 5,
            Parent = epic
        };

        var story1 = new DevelopmentTask
        {
            Id = "story1",
            Title = "Story 1",
            Type = WorkItemType.UserStory,
            StoryPoints = 3,
            Parent = feature1
        };

        var story2 = new DevelopmentTask
        {
            Id = "story2",
            Title = "Story 2",
            Type = WorkItemType.UserStory,
            StoryPoints = 2,
            Parent = feature2
        };

        feature1.Children.Add(story1);
        feature2.Children.Add(story2);
        epic.Children.AddRange(new[] { feature1, feature2 });

        var document = new TaskDocument
        {
            Tasks = new List<DevelopmentTask> { epic }
        };

        // Act
        var hierarchy = await _service.BuildHierarchyAsync(document);

        // Assert
        hierarchy.TotalStoryPoints.Should().Be(18); // 0 + 8 + 5 + 3 + 2
        hierarchy.TotalWorkItems.Should().Be(5); // epic + 2 features + 2 stories
    }
}