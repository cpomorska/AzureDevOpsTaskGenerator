using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AzureDevOpsTaskGenerator.Interfaces;
using AzureDevOpsTaskGenerator.Models;
using AzureDevOpsTaskGenerator.Parsers;
using AzureDevOpsTaskGenerator.Services;

namespace AzureDevOpsTaskGenerator;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        var rootCommand = new RootCommand("Azure DevOps Task Generator - Convert text files to Azure DevOps work items");

        var fileOption = new Option<FileInfo>(
            name: "--file",
            description: "Path to the task file to parse")
        {
            IsRequired = true
        };

        var organizationOption = new Option<string>(
            name: "--organization",
            description: "Azure DevOps organization URL (e.g., https://dev.azure.com/yourorg)")
        {
            IsRequired = true
        };

        var projectOption = new Option<string>(
            name: "--project",
            description: "Azure DevOps project name")
        {
            IsRequired = true
        };

        var tokenOption = new Option<string>(
            name: "--token",
            description: "Personal Access Token for Azure DevOps")
        {
            IsRequired = true
        };

        var dryRunOption = new Option<bool>(
            name: "--dry-run",
            description: "Parse and display work items without creating them in Azure DevOps",
            getDefaultValue: () => false);

        rootCommand.AddOption(fileOption);
        rootCommand.AddOption(organizationOption);
        rootCommand.AddOption(projectOption);
        rootCommand.AddOption(tokenOption);
        rootCommand.AddOption(dryRunOption);

        rootCommand.SetHandler(async (file, organization, project, token, dryRun) =>
        {
            await ProcessTaskFile(host.Services, file, organization, project, token, dryRun);
        }, fileOption, organizationOption, projectOption, tokenOption, dryRunOption);

        return await rootCommand.InvokeAsync(args);
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder => builder.AddConsole());
                services.AddScoped<ITextFileParser, MarkdownTaskParser>();
                services.AddScoped<ITaskGenerator, TaskGeneratorService>();
                services.AddScoped<IAzureDevOpsClient, AzureDevOpsClient>();
            });

    static async Task ProcessTaskFile(IServiceProvider services, FileInfo file, string organization, 
        string project, string token, bool dryRun)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        var parser = services.GetRequiredService<ITextFileParser>();
        var taskGenerator = services.GetRequiredService<ITaskGenerator>();
        var azureDevOpsClient = services.GetRequiredService<IAzureDevOpsClient>();

        try
        {
            logger.LogInformation("Starting task file processing...");
            logger.LogInformation($"File: {file.FullName}");
            logger.LogInformation($"Organization: {organization}");
            logger.LogInformation($"Project: {project}");
            logger.LogInformation($"Dry Run: {dryRun}");

            // Parse the task file
            logger.LogInformation("Parsing task file...");
            var document = await parser.ParseAsync(file.FullName);
            logger.LogInformation($"Parsed document: {document.Title}");
            logger.LogInformation($"Found {document.Tasks.Count} top-level tasks");

            // Build hierarchy
            logger.LogInformation("Building work item hierarchy...");
            var hierarchy = await taskGenerator.BuildHierarchyAsync(document);
            logger.LogInformation($"Total work items: {hierarchy.TotalWorkItems}");
            logger.LogInformation($"Total story points: {hierarchy.TotalStoryPoints}");
            logger.LogInformation($"Epics: {hierarchy.Epics.Count}");

            // Display hierarchy
            DisplayHierarchy(hierarchy, logger);

            if (dryRun)
            {
                logger.LogInformation("Dry run completed. No work items were created in Azure DevOps.");
                return;
            }

            // Authenticate with Azure DevOps
            logger.LogInformation("Authenticating with Azure DevOps...");
            var authenticated = await azureDevOpsClient.AuthenticateAsync(organization, token);
            
            if (!authenticated)
            {
                logger.LogError("Failed to authenticate with Azure DevOps. Please check your credentials.");
                return;
            }

            logger.LogInformation("Authentication successful!");

            // Create work items
            logger.LogInformation("Creating work items in Azure DevOps...");
            var createdIds = await azureDevOpsClient.CreateWorkItemHierarchyAsync(hierarchy, project);
            
            logger.LogInformation($"Successfully created {createdIds.Count} work items!");
            logger.LogInformation($"Work item IDs: {string.Join(", ", createdIds)}");

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while processing the task file");
        }
    }

    static void DisplayHierarchy(WorkItemHierarchy hierarchy, ILogger logger)
    {
        logger.LogInformation("\n=== WORK ITEM HIERARCHY ===");
        
        foreach (var epic in hierarchy.Epics)
        {
            logger.LogInformation($"📋 EPIC: {epic.Title} ({epic.StoryPoints} SP, {epic.Priority} priority)");
            
            foreach (var feature in epic.Children.Where(c => c.Type == Models.WorkItemType.Feature))
            {
                logger.LogInformation($"  🎯 FEATURE: {feature.Title} ({feature.StoryPoints} SP)");
                
                foreach (var story in feature.Children)
                {
                    var icon = story.Type == Models.WorkItemType.UserStory ? "📖" : "⚙️";
                    logger.LogInformation($"    {icon} {story.Type.ToString().ToUpper()}: {story.Title} ({story.StoryPoints} SP)");
                }
            }

            // Direct children of epic (not under features)
            foreach (var child in epic.Children.Where(c => c.Type != Models.WorkItemType.Feature))
            {
                var icon = child.Type == Models.WorkItemType.UserStory ? "📖" : "⚙️";
                logger.LogInformation($"  {icon} {child.Type.ToString().ToUpper()}: {child.Title} ({child.StoryPoints} SP)");
            }
        }

        logger.LogInformation("=== END HIERARCHY ===\n");
    }
}