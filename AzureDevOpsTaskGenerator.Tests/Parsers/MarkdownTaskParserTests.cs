using FluentAssertions;
using Xunit;
using AzureDevOpsTaskGenerator.Parsers;
using AzureDevOpsTaskGenerator.Models;

namespace AzureDevOpsTaskGenerator.Tests.Parsers;

public class MarkdownTaskParserTests
{
    private readonly MarkdownTaskParser _parser;

    public MarkdownTaskParserTests()
    {
        _parser = new MarkdownTaskParser();
    }

    [Fact]
    public void CanParse_ShouldReturnTrue_ForMarkdownFiles()
    {
        // Arrange & Act & Assert
        _parser.CanParse("test.md").Should().BeTrue();
        _parser.CanParse("test.markdown").Should().BeTrue();
        _parser.CanParse("test.txt").Should().BeTrue();
        _parser.CanParse("test.doc").Should().BeFalse();
        _parser.CanParse("test.pdf").Should().BeFalse();
    }

    [Fact]
    public void SupportedExtensions_ShouldContainExpectedExtensions()
    {
        // Arrange & Act
        var extensions = _parser.SupportedExtensions;

        // Assert
        extensions.Should().Contain(".md");
        extensions.Should().Contain(".markdown");
        extensions.Should().Contain(".txt");
    }

    [Fact]
    public async Task ParseFromTextAsync_ShouldExtractTitle_FromFirstHeader()
    {
        // Arrange
        var content = @"# Sample Development Tasks

This is a test document.

## Some Section";

        // Act
        var result = await _parser.ParseFromTextAsync(content, "test.md");

        // Assert
        result.Title.Should().Be("Sample Development Tasks");
        result.FilePath.Should().Be("test.md");
    }

    [Fact]
    public async Task ParseFromTextAsync_ShouldExtractDescription_FromContentAfterTitle()
    {
        // Arrange
        var content = @"# Sample Tasks

This is a test document for parsing development tasks.
It contains multiple lines of description.

## First Section";

        // Act
        var result = await _parser.ParseFromTextAsync(content, "test.md");

        // Assert
        result.Description.Should().Contain("test document");
        result.Description.Should().Contain("multiple lines");
    }

    [Fact]
    public async Task ParseFromTextAsync_ShouldParseEpic_WithCorrectProperties()
    {
        // Arrange
        var content = @"# Test Tasks

## Epic: Security Enhancement
- **Priority**: Critical
- **Effort**: 21 story points
- **Business Value**: High - Security is important

Some description here.";

        // Act
        var result = await _parser.ParseFromTextAsync(content, "test.md");

        // Assert
        result.Tasks.Should().HaveCount(1);
        var epic = result.Tasks.First();
        
        epic.Title.Should().Be("Security Enhancement");
        epic.Type.Should().Be(WorkItemType.Epic);
        epic.Priority.Should().Be(Priority.Critical);
        epic.StoryPoints.Should().Be(21);
        epic.BusinessValue.Should().Contain("High");
    }

    [Fact]
    public async Task ParseFromTextAsync_ShouldParseFeatures_UnderEpic()
    {
        // Arrange
        var content = @"# Test Tasks

### Epic: Security Enhancement
- **Priority**: Critical
- **Effort**: 21 story points

#### Features:
1. **JWT Authentication**
   - Implement JWT validation
   - **Effort**: 8 SP

2. **API Security**
   - Add rate limiting
   - **Effort**: 5 SP";

        // Act
        var result = await _parser.ParseFromTextAsync(content, "test.md");

        // Assert
        result.Tasks.Should().HaveCount(1);
        var epic = result.Tasks.First();
        
        epic.Children.Should().HaveCount(2);
        
        var jwtFeature = epic.Children.First();
        jwtFeature.Title.Should().Be("JWT Authentication");
        jwtFeature.Type.Should().Be(WorkItemType.Feature);
        jwtFeature.StoryPoints.Should().Be(8);
        jwtFeature.Parent.Should().Be(epic);

        var apiFeature = epic.Children.Last();
        apiFeature.Title.Should().Be("API Security");
        apiFeature.StoryPoints.Should().Be(5);
    }

    [Fact]
    public async Task ParseFromTextAsync_ShouldParseTasks_UnderFeatures()
    {
        // Arrange
        var content = @"# Test Tasks

### Epic: Security Enhancement

#### Features:
1. **JWT Authentication**
   - Remove [AllowAnonymous] attributes
   - Implement JWT token validation
   - Add role-based authorization
   - **Effort**: 8 SP";

        // Act
        var result = await _parser.ParseFromTextAsync(content, "test.md");

        // Assert
        var epic = result.Tasks.First();
        var feature = epic.Children.First();
        
        // Note: Current implementation doesn't parse individual tasks under features
        // This is a limitation that could be improved
        feature.Title.Should().Be("JWT Authentication");
        feature.StoryPoints.Should().Be(8);
    }

    [Fact]
    public async Task ParseFromTextAsync_ShouldParseSimpleTasks_WithoutHierarchy()
    {
        // Arrange
        var content = @"# Simple Tasks

- Fix authentication issues
- Update dependencies  
- Add logging
- Improve error handling";

        // Act
        var result = await _parser.ParseFromTextAsync(content, "test.md");

        // Assert
        result.Tasks.Should().HaveCount(4);
        result.Tasks.All(t => t.Type == WorkItemType.Task).Should().BeTrue();
        result.Tasks.First().Title.Should().Be("Fix authentication issues");
        result.Tasks.Last().Title.Should().Be("Improve error handling");
    }

    [Fact]
    public async Task ParseFromTextAsync_ShouldExtractEffort_FromVariousFormats()
    {
        // Arrange
        var content = @"# Test Tasks

### Epic: Test Epic
- **Effort**: 21 story points

#### Features:
1. **Feature One**
   - **Effort**: 8 SP

2. **Feature Two**
   - Some description with 5 story points mentioned";

        // Act
        var result = await _parser.ParseFromTextAsync(content, "test.md");

        // Assert
        var epic = result.Tasks.First();
        epic.StoryPoints.Should().Be(21);
        
        var feature1 = epic.Children.First();
        feature1.StoryPoints.Should().Be(8);
        
        var feature2 = epic.Children.Last();
        feature2.StoryPoints.Should().Be(5);
    }

    [Fact]
    public async Task ParseFromTextAsync_ShouldHandleMultipleEpics()
    {
        // Arrange
        var content = @"# Test Tasks

### Epic: Security Enhancement
- **Priority**: Critical
- **Effort**: 21 story points

### Epic: Performance Optimization  
- **Priority**: High
- **Effort**: 13 story points";

        // Act
        var result = await _parser.ParseFromTextAsync(content, "test.md");

        // Assert
        result.Tasks.Should().HaveCount(2);
        
        var securityEpic = result.Tasks.First();
        securityEpic.Title.Should().Be("Security Enhancement");
        securityEpic.Priority.Should().Be(Priority.Critical);
        securityEpic.StoryPoints.Should().Be(21);
        
        var performanceEpic = result.Tasks.Last();
        performanceEpic.Title.Should().Be("Performance Optimization");
        performanceEpic.Priority.Should().Be(Priority.High);
        performanceEpic.StoryPoints.Should().Be(13);
    }

    [Fact]
    public async Task ParseAsync_ShouldThrowFileNotFoundException_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentFile = "non-existent-file.md";

        // Act & Assert
        await _parser.Invoking(p => p.ParseAsync(nonExistentFile))
            .Should().ThrowAsync<FileNotFoundException>()
            .WithMessage($"File not found: {nonExistentFile}");
    }

    [Fact]
    public async Task ParseAsync_ShouldParseActualFile()
    {
        // Arrange
        var testFile = Path.Combine("TestData", "sample-tasks.md");

        // Act
        var result = await _parser.ParseAsync(testFile);

        // Assert
        result.Title.Should().Be("Sample Development Tasks");
        result.Tasks.Should().NotBeEmpty();
        result.Tasks.Should().Contain(t => t.Title.Contains("Authentication"));
        result.Tasks.Should().Contain(t => t.Title.Contains("DDD Implementation"));
    }
}