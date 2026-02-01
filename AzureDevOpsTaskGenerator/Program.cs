using System.CommandLine;
using System.IO;
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
        // Build DI host
        var host = CreateHostBuilder(args).Build();

        // Simple manual argument parsing to avoid depending on System.CommandLine version differences.
        string? filePath = GetOptionValue(args, "--file");
        string? organization = GetOptionValue(args, "--organization");
        string? project = GetOptionValue(args, "--project");
        string? token = GetOptionValue(args, "--token");
        string? dryRunRaw = GetOptionValue(args, "--dry-run");
        var dryRun = false;

        if (!string.IsNullOrEmpty(dryRunRaw) && bool.TryParse(dryRunRaw, out var parsedDry))
        {
            dryRun = parsedDry;
        }

    static string? GetOptionValue(string[] args, string optionName)
    {
        if (args == null || args.Length == 0)
            return null;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];

            if (string.Equals(a, optionName, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                    return args[i + 1].Trim('"');
                return null;
            }

            // support --option=value
            if (a.StartsWith(optionName + "=", StringComparison.OrdinalIgnoreCase))
            {
                return a.Substring(optionName.Length + 1).Trim('"');
            }
        }

        return null;
    }

        // Validate required args
        var logger = host.Services.GetRequiredService<ILogger<Program>>();

        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(organization) || string.IsNullOrEmpty(project) || string.IsNullOrEmpty(token))
        {
            logger.LogError("Missing required arguments. Usage: --file <path> --organization <orgUrl> --project <project> --token <pat> [--dry-run true|false]");
            return 1;
        }

        var fileInfo = new FileInfo(filePath!);

        await ProcessTaskFile(host.Services, fileInfo, organization!, project!, token!, dryRun);

        return 0;
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