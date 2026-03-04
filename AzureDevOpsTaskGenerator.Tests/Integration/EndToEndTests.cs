using FluentAssertions;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AzureDevOpsTaskGenerator.Interfaces;
using AzureDevOpsTaskGenerator.Parsers;
using AzureDevOpsTaskGenerator.Services;
using AzureDevOpsTaskGenerator.Models;

namespace AzureDevOpsTaskGenerator.Tests.Integration;

public class EndToEndTests
{
    private readonly IServiceProvider _serviceProvider;

    public EndToEndTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddScoped<ITextFileParser, MarkdownTaskParser>();
        services.AddScoped<ITaskGenerator, TaskGeneratorService>();
        services.AddScoped<IAzureDevOpsClient, AzureDevOpsClient>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task CompleteWorkflow_ShouldParseAndGenerateHierarchy_Successfully()
    {
        // Arrange
        var parser = _serviceProvider.GetRequiredService<ITextFileParser>();
        var taskGenerator = _serviceProvider.GetRequiredService<ITaskGenerator>();
        var testFile = Path.Combine("TestData", "sample-tasks.md");

        // Act
        var ct = TestContext.Current.CancellationToken;
        var document = await parser.ParseAsync(testFile, ct);
        var hierarchy = await taskGenerator.BuildHierarchyAsync(document, ct);
        var allTasks = await taskGenerator.ExtractTasksAsync(document, ct);

        // Assert
        document.Should().NotBeNull();
        document.Title.Should().Be("Sample Development Tasks");
        document.Tasks.Should().NotBeEmpty();

        hierarchy.Should().NotBeNull();
        hierarchy.Epics.Should().NotBeEmpty();
        hierarchy.TotalWorkItems.Should().BeGreaterThan(0);
        hierarchy.TotalStoryPoints.Should().BeGreaterThan(0);

        allTasks.Should().NotBeEmpty();
        allTasks.Should().Contain(t => t.Type == WorkItemType.Epic);
        allTasks.Should().Contain(t => t.Type == WorkItemType.Feature);
    }

    [Fact]
    public async Task ParseSampleFile_ShouldExtractCorrectEpics()
    {
        // Arrange
        var parser = _serviceProvider.GetRequiredService<ITextFileParser>();
        var testFile = Path.Combine("TestData", "sample-tasks.md");

        // Act
        var document = await parser.ParseAsync(testFile, TestContext.Current.CancellationToken);

        // Assert
        document.Tasks.Should().HaveCountGreaterThanOrEqualTo(2);
        
        var securityEpic = document.Tasks.FirstOrDefault(t => t.Title.Contains("Implement Proper Authentication"));
        securityEpic.Should().NotBeNull();
        securityEpic!.Type.Should().Be(WorkItemType.Epic);
        securityEpic.Priority.Should().Be(Priority.Critical);
        securityEpic.StoryPoints.Should().Be(21);

        var architectureEpic = document.Tasks.FirstOrDefault(t => t.Title.Contains("Improve DDD"));
        architectureEpic.Should().NotBeNull();
        architectureEpic!.Type.Should().Be(WorkItemType.Epic);
        architectureEpic.Priority.Should().Be(Priority.High);
        architectureEpic.StoryPoints.Should().Be(34);
    }

    [Fact]
    public async Task ParseSampleFile_ShouldExtractFeaturesUnderEpics()
    {
        // Arrange
        var parser = _serviceProvider.GetRequiredService<ITextFileParser>();
        var testFile = Path.Combine("TestData", "sample-tasks.md");

        // Act
        var document = await parser.ParseAsync(testFile, TestContext.Current.CancellationToken);

        // Assert
        var securityEpic = document.Tasks.FirstOrDefault(t => t.Title.Contains("Implement Proper Authentication"));
        securityEpic.Should().NotBeNull();
        securityEpic!.Children.Should().NotBeEmpty();

        var jwtFeature = securityEpic.Children.FirstOrDefault(f => f.Title.Contains("JWT"));
        jwtFeature.Should().NotBeNull();
        jwtFeature!.Type.Should().Be(WorkItemType.Feature);
        jwtFeature.StoryPoints.Should().Be(8);
        jwtFeature.Parent.Should().Be(securityEpic);

        var securityFeature = securityEpic.Children.FirstOrDefault(f => f.Title.Contains("Security Hardening"));
        securityFeature.Should().NotBeNull();
        securityFeature!.StoryPoints.Should().Be(5);
    }

    [Fact]
    public async Task ParseSimpleFile_ShouldHandleBasicStructure()
    {
        // Arrange
        var parser = _serviceProvider.GetRequiredService<ITextFileParser>();
        var testFile = Path.Combine("TestData", "simple-tasks.md");

        // Act
        var document = await parser.ParseAsync(testFile, TestContext.Current.CancellationToken);

        // Assert
        document.Title.Should().Be("Simple Task List");
        document.Tasks.Should().NotBeEmpty();

        var epic = document.Tasks.FirstOrDefault(t => t.Type == WorkItemType.Epic);
        epic.Should().NotBeNull();
        epic!.Title.Should().Be("Basic Improvements");
        epic.StoryPoints.Should().Be(10);
        epic.Priority.Should().Be(Priority.Medium);
    }

    [Fact]
    public async Task BuildHierarchy_ShouldCalculateCorrectTotals()
    {
        // Arrange
        var parser = _serviceProvider.GetRequiredService<ITextFileParser>();
        var taskGenerator = _serviceProvider.GetRequiredService<ITaskGenerator>();
        var testFile = Path.Combine("TestData", "sample-tasks.md");
        var ct = TestContext.Current.CancellationToken;

        // Act
        var document = await parser.ParseAsync(testFile, ct);
        var hierarchy = await taskGenerator.BuildHierarchyAsync(document, ct);

        // Assert
        hierarchy.TotalStoryPoints.Should().BeGreaterThan(30);
        hierarchy.TotalWorkItems.Should().BeGreaterThan(5);
        hierarchy.Epics.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExtractAllTasks_ShouldFlattenHierarchyCorrectly()
    {
        // Arrange
        var parser = _serviceProvider.GetRequiredService<ITextFileParser>();
        var taskGenerator = _serviceProvider.GetRequiredService<ITaskGenerator>();
        var testFile = Path.Combine("TestData", "sample-tasks.md");
        var ct = TestContext.Current.CancellationToken;

        // Act
        var document = await parser.ParseAsync(testFile, ct);
        var allTasks = await taskGenerator.ExtractTasksAsync(document, ct);

        // Assert
        allTasks.Should().NotBeEmpty();
        
        var epics = allTasks.Where(t => t.Type == WorkItemType.Epic).ToList();
        var features = allTasks.Where(t => t.Type == WorkItemType.Feature).ToList();
        var tasks = allTasks.Where(t => t.Type == WorkItemType.Task).ToList();

        epics.Should().NotBeEmpty();
        features.Should().NotBeEmpty();
        tasks.Should().NotBeEmpty();
        
        // Verify parent-child relationships are maintained
        foreach (var feature in features)
        {
            feature.Parent.Should().NotBeNull();
            feature.Parent!.Type.Should().Be(WorkItemType.Epic);
        }
    }

    [Fact]
    public void ServiceRegistration_ShouldResolveAllServices()
    {
        // Arrange & Act
        var parser = _serviceProvider.GetService<ITextFileParser>();
        var taskGenerator = _serviceProvider.GetService<ITaskGenerator>();
        var azureDevOpsClient = _serviceProvider.GetService<IAzureDevOpsClient>();

        // Assert
        parser.Should().NotBeNull();
        parser.Should().BeOfType<MarkdownTaskParser>();

        taskGenerator.Should().NotBeNull();
        taskGenerator.Should().BeOfType<TaskGeneratorService>();

        azureDevOpsClient.Should().NotBeNull();
        azureDevOpsClient.Should().BeOfType<AzureDevOpsClient>();
    }

    [Fact]
    public async Task RealWorldScenario_ShouldHandleFixodaTaskFile()
    {
        // This test would work with the actual fixoda-development-tasks.md file
        // if it exists in the test data directory
        
        // Arrange
        var parser = _serviceProvider.GetRequiredService<ITextFileParser>();
        var taskGenerator = _serviceProvider.GetRequiredService<ITaskGenerator>();
        var fixodaTaskFile = Path.Combine("..", "fixoda-development-tasks.md");

        // Skip test if file doesn't exist
        if (!File.Exists(fixodaTaskFile))
        {
            return; // Skip this test
        }

        // Act
        var ct = TestContext.Current.CancellationToken;
        var document = await parser.ParseAsync(fixodaTaskFile, ct);
        var hierarchy = await taskGenerator.BuildHierarchyAsync(document, ct);

        // Assert
        document.Title.Should().Be("Fixoda Marketplace Development Tasks");
        hierarchy.TotalStoryPoints.Should().BeGreaterThan(50);
        hierarchy.Epics.Should().HaveCount(6); // Security, Architecture, Performance, API, Infrastructure, Testing
        
        var securityEpic = hierarchy.Epics.FirstOrDefault(e => e.Title.Contains("Security"));
        securityEpic.Should().NotBeNull();
        securityEpic!.Priority.Should().Be(Priority.Critical);
    }
}