using System.Text;
using System.Text.RegularExpressions;
using AzureDevOpsTaskGenerator.Interfaces;
using AzureDevOpsTaskGenerator.Models;

namespace AzureDevOpsTaskGenerator.Parsers;

public class MarkdownTaskParser : ITextFileParser
{
    public string[] SupportedExtensions => [".md", ".markdown", ".txt"];

    // #18 – explicit UTF-8 with BOM detection
    public async Task<TaskDocument> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, cancellationToken);
        return ParseFromText(content, Path.GetFileName(filePath));
    }

    public bool CanParse(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(extension);
    }

    // internal – no longer part of the public interface
    public TaskDocument ParseFromText(string content, string fileName = "")
    {
        // #17 – strip YAML front-matter and load into Metadata
        var (stripped, metadata) = StripFrontMatter(content);

        var document = new TaskDocument
        {
            FilePath = fileName,
            Title = ExtractTitle(stripped),
            Description = ExtractDescription(stripped),
            Metadata = metadata
        };

        document.Tasks = ExtractTasksFromContent(stripped);
        return document;
    }

    // kept for test compatibility – delegates to ParseFromText
    public Task<TaskDocument> ParseFromTextAsync(string content, string fileName = "")
        => Task.FromResult(ParseFromText(content, fileName));

    // ── title / description ─────────────────────────────────────────────────

    private static string ExtractTitle(string content)
    {
        var line = content.Split('\n').FirstOrDefault(l => l.StartsWith("# "));
        return line?.Substring(2).Trim() ?? "Development Tasks";
    }

    private static string ExtractDescription(string content)
    {
        var lines = content.Split('\n');
        var result = new List<string>();
        bool inDesc = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("# ")) { inDesc = true; continue; }
            if (inDesc && line.StartsWith("## ")) break;
            if (inDesc && !string.IsNullOrWhiteSpace(line))
                result.Add(line.Trim());
        }

        return string.Join(" ", result);
    }

    // ── YAML front-matter (#17) ──────────────────────────────────────────────

    private static (string content, Dictionary<string, string> metadata) StripFrontMatter(string content)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!content.StartsWith("---")) return (content, metadata);

        var end = content.IndexOf("\n---", 3);
        if (end < 0) return (content, metadata);

        var block = content[3..end];
        foreach (var line in block.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (!string.IsNullOrEmpty(key)) metadata[key] = value;
        }

        return (content[(end + 4)..].TrimStart(), metadata);
    }

    // ── main parser (#2 #4 #5) ───────────────────────────────────────────────

    private List<DevelopmentTask> ExtractTasksFromContent(string content)
    {
        var tasks = new List<DevelopmentTask>();
        var lines = content.Split('\n');

        DevelopmentTask? currentEpic = null;
        DevelopmentTask? currentFeature = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var line = raw.Trim();

            // #4 #5 – every ## header starts a new Epic (with or without "Epic:" label)
            if (IsEpicHeader(line))
            {
                currentEpic = CreateEpicFromHeader(line, lines, i);
                tasks.Add(currentEpic);
                currentFeature = null;
                continue;
            }

            if (IsFeatureHeader(line))
                continue;

            // numbered feature item
            if (IsFeatureItem(line) && currentEpic != null)
            {
                currentFeature = CreateFeatureFromLine(line, lines, i);
                currentFeature.Parent = currentEpic;
                currentEpic.Children.Add(currentFeature);
                continue;
            }

            // property lines belonging to current feature (Effort, Priority …)
            if (currentFeature != null && IsPropertyLine(line))
            {
                ApplyPropertyToTask(line, currentFeature);
                continue;
            }

            // #2 – task/story bullets – stay under currentFeature until next feature/epic
            if (IsTaskItem(line))
            {
                var task = CreateTaskFromLine(line);

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

        return tasks;
    }

    // ── detection helpers ───────────────────────────────────────────────────

    // #4 – ## headers are always Epics; ### only if they carry the "Epic:" label
    private static bool IsEpicHeader(string line)
        => line.StartsWith("## ") ||
           line.StartsWith("## Epic:") ||
           line.StartsWith("### Epic:");

    private static bool IsFeatureHeader(string line)
        => line.StartsWith("####") && line.Contains("Features");

    private static bool IsFeatureItem(string line)
        => Regex.IsMatch(line, @"^\d+\.\s*\*\*.*\*\*");

    private static bool IsPropertyLine(string line)
    {
        var propPattern = @"^[-*+]\s*\*\*(Effort|Priority|Business Value|Tags|Dependencies|Theme)\*\*:\s*.*";
        return Regex.IsMatch(line, propPattern, RegexOptions.IgnoreCase);
    }

    private static bool IsTaskItem(string line)
    {
        if (IsPropertyLine(line)) return false;
        return line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ ");
    }

    // ── creators ─────────────────────────────────────────────────────────────

    private DevelopmentTask CreateEpicFromHeader(string line, string[] lines, int idx)
    {
        return new DevelopmentTask
        {
            Id = Guid.NewGuid().ToString(),
            Title = ExtractTitleFromHeader(line),
            Type = WorkItemType.Epic,
            Priority = ExtractPriorityFromBlock(lines, idx),
            StoryPoints = ExtractEffortFromBlock(lines, idx, idx),
            BusinessValue = ExtractFieldFromBlock(lines, idx, "Business Value"),
            Description = ExtractDescriptionFromBlock(lines, idx),
            Tags = ExtractTagsFromBlock(lines, idx),
            Theme = ExtractFieldFromBlock(lines, idx, "Theme")
        };
    }

    private DevelopmentTask CreateFeatureFromLine(string line, string[] lines, int idx)
    {
        return new DevelopmentTask
        {
            Id = Guid.NewGuid().ToString(),
            Title = ExtractFeatureTitleFromLine(line),
            Type = WorkItemType.Feature,
            Priority = ExtractPriorityFromBlock(lines, idx),
            // #3 – search only within this feature's own block
            StoryPoints = ExtractEffortFromBlock(lines, idx + 1, idx),
            Description = ExtractFeatureDescriptionFromBlock(lines, idx),
            Tags = ExtractTagsFromBlock(lines, idx)
        };
    }

    // #1 – detect UserStory vs Task
    private static DevelopmentTask CreateTaskFromLine(string line)
    {
        var title = ExtractTaskTitleFromLine(line);
        var type = DetermineTaskType(title, line);
        return new DevelopmentTask
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Type = type,
            Priority = Priority.Medium,
            StoryPoints = 0,
            Description = title
        };
    }

    // ── type detection (#1) ──────────────────────────────────────────────────

    private static WorkItemType DetermineTaskType(string title, string raw)
    {
        var lower = title.ToLowerInvariant();
        if (lower.StartsWith("as a ") ||
            lower.Contains("user story") ||
            lower.Contains("story") ||
            Regex.IsMatch(raw, @"\bUS\b", RegexOptions.IgnoreCase))
            return WorkItemType.UserStory;

        return WorkItemType.Task;
    }

    // ── property extraction (#3 – scoped to block) ────────────────────────────

    private static int ExtractEffortFromBlock(string[] lines, int searchFrom, int headerIdx)
    {
        int limit = FindNextHeaderOrFeatureIndex(lines, headerIdx + 1);
        for (int i = searchFrom; i < Math.Min(limit, lines.Length); i++)
        {
            var effort = ExtractEffortFromLine(lines[i]);
            if (effort > 0) return effort;
        }
        return 0;
    }

    private static Priority ExtractPriorityFromBlock(string[] lines, int startIdx)
    {
        int limit = FindNextHeaderOrFeatureIndex(lines, startIdx + 1);
        for (int i = startIdx; i < Math.Min(limit, lines.Length); i++)
        {
            var l = lines[i].ToLowerInvariant();
            if (l.Contains("critical")) return Priority.Critical;
            if (l.Contains("high")) return Priority.High;
            if (l.Contains("medium")) return Priority.Medium;
            if (l.Contains("low")) return Priority.Low;
        }
        return Priority.Medium;
    }

    private static string ExtractFieldFromBlock(string[] lines, int startIdx, string fieldName)
    {
        int limit = FindNextHeaderOrFeatureIndex(lines, startIdx + 1);
        var pattern = $@"(?:\*\*|){Regex.Escape(fieldName)}(?:\*\*|):\s*(.+)";
        for (int i = startIdx; i < Math.Min(limit, lines.Length); i++)
        {
            var m = Regex.Match(lines[i], pattern, RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.Trim();
        }
        return "";
    }

    // #9 – Tags parsing
    private static List<string> ExtractTagsFromBlock(string[] lines, int startIdx)
    {
        var raw = ExtractFieldFromBlock(lines, startIdx, "Tags");
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return [.. raw.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0)];
    }

    private static string ExtractDescriptionFromBlock(string[] lines, int startIdx)
    {
        var result = new List<string>();
        int limit = FindNextHeaderOrFeatureIndex(lines, startIdx + 1);
        for (int i = startIdx + 1; i < Math.Min(limit, lines.Length); i++)
        {
            var l = lines[i].Trim();
            if (!string.IsNullOrWhiteSpace(l) && !l.StartsWith("-") && !l.StartsWith("*") && !l.StartsWith("#"))
                result.Add(l);
        }
        return string.Join(" ", result);
    }

    private static string ExtractFeatureDescriptionFromBlock(string[] lines, int startIdx)
    {
        var result = new List<string>();
        int limit = Math.Min(startIdx + 6, lines.Length);
        for (int i = startIdx + 1; i < limit; i++)
        {
            var l = lines[i].Trim();
            if (l.StartsWith("-") || l.StartsWith("*") || Regex.IsMatch(l, @"^\d+\.") || l.StartsWith("#"))
                break;
            if (!string.IsNullOrWhiteSpace(l))
                result.Add(l);
        }
        return string.Join(" ", result);
    }

    private static int FindNextHeaderOrFeatureIndex(string[] lines, int from)
    {
        for (int i = from; i < lines.Length; i++)
        {
            var l = lines[i].Trim();
            if (l.StartsWith("## ") || l.StartsWith("### ") || IsFeatureItemStatic(l))
                return i;
        }
        return lines.Length;
    }

    private static bool IsFeatureItemStatic(string line)
        => Regex.IsMatch(line, @"^\d+\.\s*\*\*.*\*\*");

    // ── property applier (#9 #10) ────────────────────────────────────────────

    private static void ApplyPropertyToTask(string line, DevelopmentTask task)
    {
        var effort = ExtractEffortFromLine(line);
        if (effort > 0) { task.StoryPoints = effort; return; }

        var m = Regex.Match(line, @"\*\*(Tags)\*\*:\s*(.+)", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            task.Tags = [.. m.Groups[2].Value.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0)];
            return;
        }

        m = Regex.Match(line, @"\*\*(Dependencies)\*\*:\s*(.+)", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            task.Dependencies = [.. m.Groups[2].Value.Split(',').Select(d => d.Trim()).Where(d => d.Length > 0)];
            return;
        }

        m = Regex.Match(line, @"\*\*(Theme)\*\*:\s*(.+)", RegexOptions.IgnoreCase);
        if (m.Success) { task.Theme = m.Groups[1].Value.Trim(); return; }

        m = Regex.Match(line, @"(?:\*\*|)Business Value(?:\*\*|):\s*(.+)", RegexOptions.IgnoreCase);
        if (m.Success) { task.BusinessValue = m.Groups[1].Value.Trim(); }
    }

    // ── title extraction ─────────────────────────────────────────────────────

    private static string ExtractTitleFromHeader(string line)
        => Regex.Replace(line, @"^#+\s*(Epic:\s*)?", "").Trim();

    private static string ExtractFeatureTitleFromLine(string line)
    {
        var m = Regex.Match(line, @"^\d+\.\s*\*\*(.*?)\*\*");
        return m.Success ? m.Groups[1].Value.Trim() : line.Trim();
    }

    // #11 – only strip leading bullet character, keep inner content intact
    private static string ExtractTaskTitleFromLine(string line)
        => Regex.Replace(line, @"^[-*+]\s+", "").Trim();

    // ── effort line parser ───────────────────────────────────────────────────

    private static int ExtractEffortFromLine(string line)
    {
        var patterns = new[]
        {
            @"\*\*Effort\*\*:\s*(\d+)",      // **Effort**: 8
            @"[-*+]\s*(\d+)\s*SP\b",         // - 8 SP
            @"[-*+]\s*(\d+)\s*story\s*points?",
            @"(\d+)\s*SP\b",
            @"(\d+)\s*story\s*points?",
            @"Effort.*?(\d+)"
        };

        foreach (var p in patterns)
        {
            var m = Regex.Match(line, p, RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int v)) return v;
        }
        return 0;
    }
}

