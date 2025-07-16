using System.Text.RegularExpressions;
using AzureDevOpsTaskGenerator.Interfaces;
using AzureDevOpsTaskGenerator.Models;

namespace AzureDevOpsTaskGenerator.Parsers;

public class MarkdownTaskParser : ITextFileParser
{
    public string[] SupportedExtensions => new[] { ".md", ".markdown", ".txt" };

    public async Task<TaskDocument> ParseAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var content = await File.ReadAllTextAsync(filePath);
        return await ParseFromTextAsync(content, Path.GetFileName(filePath));
    }

    public async Task<TaskDocument> ParseFromTextAsync(string content, string fileName = "")
    {
        var document = new TaskDocument
        {
            FilePath = fileName,
            Title = ExtractTitle(content),
            Description = ExtractDescription(content)
        };

        var tasks = await ExtractTasksFromContentAsync(content);
        document.Tasks = tasks;

        return document;
    }

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    private string ExtractTitle(string content)
    {
        var lines = content.Split('\n');
        var titleLine = lines.FirstOrDefault(l => l.StartsWith("# "));
        return titleLine?.Substring(2).Trim() ?? "Development Tasks";
    }

    private string ExtractDescription(string content)
    {
        var lines = content.Split('\n');
        var descriptionLines = new List<string>();
        bool foundFirstHeader = false;
        bool inDescription = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("# "))
            {
                foundFirstHeader = true;
                inDescription = true;
                continue;
            }

            if (foundFirstHeader && line.StartsWith("## "))
            {
                break; // End of description
            }

            if (inDescription && !string.IsNullOrWhiteSpace(line))
            {
                descriptionLines.Add(line.Trim());
            }
        }

        return string.Join(" ", descriptionLines);
    }

    private Task<List<DevelopmentTask>> ExtractTasksFromContentAsync(string content)
    {
        var tasks = new List<DevelopmentTask>();
        var lines = content.Split('\n');
        
        DevelopmentTask? currentEpic = null;
        DevelopmentTask? currentFeature = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Epic detection (## Epic: or ### Epic:)
            if (IsEpicHeader(line))
            {
                currentEpic = CreateEpicFromHeader(line, lines, i);
                tasks.Add(currentEpic);
                currentFeature = null;
                continue;
            }

            // Feature detection (#### Features: or similar)
            if (IsFeatureHeader(line) && currentEpic != null)
            {
                // Features are listed after this header
                continue;
            }

            // Individual feature detection (numbered list under Epic)
            if (IsFeatureItem(line) && currentEpic != null)
            {
                currentFeature = CreateFeatureFromLine(line, lines, i);
                int linesConsumed = 0;
                // Scan next lines for property lines (Effort, Priority, etc.)
                for (int j = i + 1; j < Math.Min(i + 5, lines.Length); j++)
                {
                    var nextLine = lines[j]; 
                    var trimmedLine = nextLine.Trim();
                    // If a new feature or epic starts, stop
                    if (IsFeatureItem(trimmedLine) || IsEpicHeader(trimmedLine) || IsFeatureHeader(trimmedLine))
                        break;
                    // If it's a blank line, skip
                    if (string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        linesConsumed++;
                        continue;
                    }
                    // If it's a property line (not a task), assign effort
                    if (!IsTaskItem(trimmedLine))
                    {
                        int effort = ExtractEffortFromLine(trimmedLine);
                        if (effort > 0) { currentFeature.StoryPoints = effort; linesConsumed++; }
                    }
                    // Otherwise, keep scanning
                }
                i += linesConsumed;
                currentFeature.Parent = currentEpic;
                currentEpic.Children.Add(currentFeature);
                continue;
            }

            // Property line detection (Effort, Priority, etc.)
            if (currentFeature != null && (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ "))) 
            {
                if (!IsTaskItem(line))
                {
                    int effort = ExtractEffortFromLine(line);
                    if (effort > 0) { currentFeature.StoryPoints = effort; continue; }
                }
            }

            // Task/Story detection (- or * bullets)
            if (IsTaskItem(line))
            {
                var task = CreateTaskFromLine(line, lines, i);
                
                if (currentFeature != null)
                {
                    task.Parent = currentFeature;
                    currentFeature.Children.Add(task);
                }
                else if (currentEpic != null)
                {
                    task.Parent = currentEpic;
                    currentEpic.Children.Add(task);
                }
                else
                {
                    tasks.Add(task);
                }
            }
        }

        return Task.FromResult(tasks);
    }

    private static bool IsEpicHeader(string line)
    {
        return line.StartsWith("### Epic:") || line.StartsWith("## Epic:") || 
               (line.StartsWith("##") && line.Contains("Epic"));
    }

    private static bool IsFeatureHeader(string line)
    {
        return line.StartsWith("#### Features:") || line.Contains("Features:");
    }

    private static bool IsFeatureItem(string line)
    {
        // Numbered list items like "1. **Feature Name**"
        return Regex.IsMatch(line, @"^\d+\.\s*\*\*.*\*\*");
    }

    private bool IsTaskItem(string line)
    {
        // Exclude property lines like "**Effort**: ...", "**Priority**: ...", etc.
        var propertyPattern = @"^[-*+]\s*\*\*(Effort|Priority|Business Value)\*\*:\s*.*";
        if (Regex.IsMatch(line, propertyPattern, RegexOptions.IgnoreCase))
            return false;
        return line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ ");
    }

    private DevelopmentTask CreateEpicFromHeader(string line, string[] lines, int startIndex)
    {
        var title = ExtractTitleFromHeader(line);
        var epic = new DevelopmentTask
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Type = WorkItemType.Epic,
            Priority = ExtractPriorityFromContext(lines, startIndex),
            StoryPoints = ExtractEffortFromContext(lines, startIndex),
            BusinessValue = ExtractBusinessValueFromContext(lines, startIndex),
            Description = ExtractDescriptionFromContext(lines, startIndex)
        };

        return epic;
    }

    private DevelopmentTask CreateFeatureFromLine(string line, string[] lines, int startIndex)
    {
        var title = ExtractFeatureTitleFromLine(line);
        var feature = new DevelopmentTask
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Type = WorkItemType.Feature,
            Priority = ExtractPriorityFromContext(lines, startIndex),
            StoryPoints = ExtractEffortFromContext(lines, startIndex),
            Description = ExtractFeatureDescriptionFromContext(lines, startIndex)
        };

        return feature;
    }

    private DevelopmentTask CreateTaskFromLine(string line, string[] lines, int startIndex)
    {
        var title = ExtractTaskTitleFromLine(line);
        var task = new DevelopmentTask
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Type = WorkItemType.Task,
            Priority = Priority.Medium,
            StoryPoints = ExtractEffortFromContext(lines, startIndex),
            Description = ExtractTaskDescriptionFromContext(lines, startIndex)
        };

        return task;
    }

    private string ExtractTitleFromHeader(string line)
    {
        // Remove markdown headers and "Epic:" prefix
        var cleaned = line.Replace("#", "").Replace("Epic:", "").Trim();
        return cleaned;
    }

    private string ExtractFeatureTitleFromLine(string line)
    {
        // Extract from "1. **Feature Name**"
        var match = Regex.Match(line, @"^\d+\.\s*\*\*(.*?)\*\*");
        return match.Success ? match.Groups[1].Value.Trim() : line.Trim();
    }

    private string ExtractTaskTitleFromLine(string line)
    {
        // Remove bullet points and clean up
        return line.Replace("-", "").Replace("*", "").Replace("+", "").Trim();
    }

    private Priority ExtractPriorityFromContext(string[] lines, int startIndex)
    {
        // Look for priority indicators in nearby lines
        for (int i = startIndex; i < Math.Min(startIndex + 5, lines.Length); i++)
        {
            var line = lines[i].ToLowerInvariant();
            if (line.Contains("critical")) return Priority.Critical;
            if (line.Contains("high")) return Priority.High;
            if (line.Contains("medium")) return Priority.Medium;
            if (line.Contains("low")) return Priority.Low;
        }
        return Priority.Medium;
    }

    private int ExtractEffortFromContext(string[] lines, int startIndex)
    {
        // Look for effort indicators like "21 story points" or "Effort**: 8 SP"
        for (int i = startIndex; i < Math.Min(startIndex + 10, lines.Length); i++)
        {
            var effort = ExtractEffortFromLine(lines[i]);
            if (effort > 0) return effort;
        }
        return 0;
    }

    private static int ExtractEffortFromLine(string line)
    {
        // Robustly match effort in lines like '- **Effort**: 8 SP', '- 8 SP', '- 8 story points', etc.
        var patterns = new[]
        {
            @"[-*+]\s*\*\*Effort\*\*:\s*(\d+)(\s*SP)?", // - **Effort**: 8 SP
            @"[-*+]\s*(\d+)\s*SP\b",                     // - 8 SP
            @"[-*+]\s*(\d+)\s*story\s*points?",           // - 8 story points
            @"(\d+)\s*SP\b",                              // 8 SP
            @"(\d+)\s*story\s*points?",                   // 8 story points
            @"Effort.*?(\d+)"                               // Effort: 8
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(line, pattern, RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int effort))
            {
                return effort;
            }
        }

        return 0;
    }

    private static string ExtractBusinessValueFromContext(string[] lines, int startIndex)
    {
        for (int i = startIndex; i < Math.Min(startIndex + 10, lines.Length); i++)
        {
            var line = lines[i];
            // Support both plain and markdown bold for Business Value
            var match = Regex.Match(line, @"(?:\*\*|)Business Value(?:\*\*|):\s*(.+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }
        return "";
    }

    private string ExtractDescriptionFromContext(string[] lines, int startIndex)
    {
        var description = new List<string>();
        
        for (int i = startIndex + 1; i < Math.Min(startIndex + 10, lines.Length); i++)
        {
            var line = lines[i].Trim();
            
            // Stop at next header or major section
            if (line.StartsWith("#") || line.StartsWith("### Epic:") || line.StartsWith("## "))
                break;
                
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("-") && !line.StartsWith("*"))
            {
                description.Add(line);
            }
        }

        return string.Join(" ", description);
    }

    private string ExtractFeatureDescriptionFromContext(string[] lines, int startIndex)
    {
        var description = new List<string>();
        
        for (int i = startIndex + 1; i < Math.Min(startIndex + 5, lines.Length); i++)
        {
            var line = lines[i].Trim();
            
            if (line.StartsWith("**Effort**:") || line.StartsWith("-") || 
                line.StartsWith("*") || Regex.IsMatch(line, @"^\d+\."))
                break;
                
            if (!string.IsNullOrWhiteSpace(line))
            {
                description.Add(line);
            }
        }

        return string.Join(" ", description);
    }

    private string ExtractTaskDescriptionFromContext(string[] lines, int startIndex)
    {
        // For tasks, description is usually on the same line or next line
        var line = lines[startIndex];
        var parts = line.Split(new[] { '-', '*', '+' }, 2);
        
        if (parts.Length > 1)
        {
            return parts[1].Trim();
        }

        return "";
    }
}