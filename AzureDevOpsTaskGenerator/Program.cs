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

        if (!TryParseArguments(args, out var file, out var organization, out var project, out var token, out var dryRun))
        {
            PrintUsage();
            return 1;
        }

        await ProcessTaskFile(host.Services, file, organization, project, token, dryRun);
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
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        var ct = cts.Token;

        var logger = services.GetRequiredService<ILogger<Program>>();
        var parser = services.GetRequiredService<ITextFileParser>();
        var taskGenerator = services.GetRequiredService<ITaskGenerator>();
        var azureDevOpsClient = services.GetRequiredService<IAzureDevOpsClient>();

        if (!file.Exists)
        {
            logger.LogError("Task file not found: {File}", file.FullName);
            return;
        }

        try
        {
            logger.LogInformation("Starting task file processing...");
            logger.LogInformation($"File: {file.FullName}");
            logger.LogInformation($"Organization: {organization}");
            logger.LogInformation($"Project: {project}");
            logger.LogInformation($"Dry Run: {dryRun}");

            logger.LogInformation("Parsing task file...");
            var document = await parser.ParseAsync(file.FullName, ct);
            logger.LogInformation($"Parsed document: {document.Title}");
            logger.LogInformation($"Found {document.Tasks.Count} top-level tasks");

            logger.LogInformation("Building work item hierarchy...");
            var hierarchy = await taskGenerator.BuildHierarchyAsync(document, ct);
            logger.LogInformation($"Total work items: {hierarchy.TotalWorkItems}");
            logger.LogInformation($"Total story points: {hierarchy.TotalStoryPoints}");
            logger.LogInformation($"Epics: {hierarchy.Epics.Count}");

            DisplayHierarchy(hierarchy, logger);

            if (dryRun)
            {
                logger.LogInformation("Dry run completed. No work items were created in Azure DevOps.");
                return;
            }

            logger.LogInformation("Authenticating with Azure DevOps...");
            var authenticated = await azureDevOpsClient.AuthenticateAsync(organization, token, ct);

            if (!authenticated)
            {
                logger.LogError("Failed to authenticate with Azure DevOps. Please check your credentials.");
                return;
            }

            logger.LogInformation("Authentication successful!");

            logger.LogInformation("Creating work items in Azure DevOps...");
            var createdIds = await azureDevOpsClient.CreateWorkItemHierarchyAsync(hierarchy, project, ct);

            logger.LogInformation($"Successfully created {createdIds.Count} work items!");
            logger.LogInformation($"Work item IDs: {string.Join(", ", createdIds)}");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Operation was cancelled by the user.");
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

    static bool TryParseArguments(string[] args, out FileInfo file, out string organization, out string project, out string token, out bool dryRun)
    {
        file = null!;
        organization = string.Empty;
        project = string.Empty;
        token = string.Empty;
        dryRun = false;

        if (args == null || args.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < args.Length; i++)
        {
            var argument = args[i];

            if (string.Equals(argument, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
                continue;
            }

            if (string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                return false;
            }

            if (i == args.Length - 1)
            {
                return false;
            }

            var value = args[++i];
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            switch (argument.ToLowerInvariant())
            {
                case "--file":
                    file = new FileInfo(value);
                    break;
                case "--organization":
                    organization = value;
                    break;
                case "--project":
                    project = value;
                    break;
                case "--token":
                    token = value;
                    break;
                default:
                    return false;
            }
        }

        return file != null &&
               !string.IsNullOrWhiteSpace(organization) &&
               !string.IsNullOrWhiteSpace(project) &&
               !string.IsNullOrWhiteSpace(token);
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: AzureDevOpsTaskGenerator --file <path> --organization <url> --project <name> --token <pat> [--dry-run]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --file <path>         (required) Path to the Markdown task file to process.");
        Console.WriteLine("  --organization <url>  (required) Azure DevOps organization URL (e.g. https://dev.azure.com/yourorg).");
        Console.WriteLine("  --project <name>      (required) Target Azure DevOps project name.");
        Console.WriteLine("  --token <pat>         (required) Personal Access Token with work item permissions.");
        Console.WriteLine("  --dry-run             (optional) Parse and log work items without creating them in Azure DevOps.");
    }
}