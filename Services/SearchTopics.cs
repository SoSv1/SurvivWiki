using System.Text.Json;
using System.Text.RegularExpressions;

namespace SurviveWiki.Services;

/// <summary>
/// Task N2: maps the words a beginner types for a problem ("night", "hungry")
/// to a short answer and the items that solve it. The topics are data, kept in
/// Data/search-topics.json, so they can be edited without changing code.
/// </summary>
public partial class SearchTopics
{
    public record Topic(string Title, string Advice, IReadOnlyList<string> Items, IReadOnlyList<string> Keywords);

    private readonly List<Topic> _topics = new();

    public SearchTopics(IWebHostEnvironment environment, ILogger<SearchTopics> logger)
    {
        var path = Path.Combine(environment.ContentRootPath, "Data", "search-topics.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("No search topics at {Path}; search will match item text only.", path);
            return;
        }

        try
        {
            var file = JsonSerializer.Deserialize<TopicFile>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            foreach (var topic in file?.Topics ?? new())
            {
                var keywords = topic.Keywords
                    .Select(keyword => keyword.Trim().ToLowerInvariant())
                    .Where(keyword => keyword.Length > 0)
                    .ToList();

                if (keywords.Count > 0 && topic.Items.Count > 0)
                {
                    _topics.Add(new Topic(topic.Title, topic.Advice, topic.Items, keywords));
                }
            }
        }
        catch (JsonException error)
        {
            // Topics are a shortcut on top of normal search; a broken file must not stop the wiki.
            logger.LogWarning(error, "The search topics file could not be read; search will match item text only.");
        }
    }

    /// <summary>The first topic with a keyword in the query, or null.</summary>
    public Topic? Match(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var normalized = string.Join(' ', Words().Matches(query.ToLowerInvariant()).Select(match => match.Value));
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        return _topics.FirstOrDefault(topic => topic.Keywords.Any(keyword =>
            keyword.Contains(' ')
                ? $" {normalized} ".Contains($" {keyword} ")   // a phrase such as "first night"
                : words.Contains(keyword)));                    // a whole word, so "eat" does not match "meat"
    }

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex Words();

    private sealed class TopicFile
    {
        public List<TopicEntry> Topics { get; set; } = new();
    }

    private sealed class TopicEntry
    {
        public List<string> Keywords { get; set; } = new();
        public string Title { get; set; } = string.Empty;
        public string Advice { get; set; } = string.Empty;
        public List<string> Items { get; set; } = new();
    }
}
