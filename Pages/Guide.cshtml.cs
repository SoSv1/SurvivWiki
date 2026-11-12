using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SurviveWiki.Data;
using SurviveWiki.Models;

namespace SurviveWiki.Pages;

/// <summary>
/// Task N3: an ordered plan for the first five days that the player ticks off.
/// The server only supplies the steps. Progress is kept in the player's browser
/// (see wwwroot/js/guide.js) so no account is needed.
/// </summary>
public partial class GuideModel : PageModel
{
    private readonly SurviveWikiContext _context;

    public GuideModel(SurviveWikiContext context)
    {
        _context = context;
    }

    /// <summary>A piece of instruction text. ItemName is set when the piece links to an item page.</summary>
    public record Segment(string Text, string? ItemName);

    public record StepView(int Id, string Title, IReadOnlyList<Segment> Instructions);

    public record DayView(int Number, string Theme, IReadOnlyList<StepView> Steps);

    public List<DayView> Days { get; private set; } = new();

    public int TotalSteps => Days.Sum(day => day.Steps.Count);

    private static readonly Dictionary<int, string> DayThemes = new()
    {
        [1] = "Tools and your first night",
        [2] = "Unlock better recipes",
        [3] = "Set up camp and gear",
        [4] = "Secure your food",
        [5] = "Prepare for what comes next",
    };

    // Matches [[Item Name]] and [[Item Name|shown text]].
    [GeneratedRegex(@"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]")]
    private static partial Regex WikiLink();

    public async Task OnGetAsync()
    {
        var steps = await _context.GuideSteps
            .AsNoTracking()
            .OrderBy(step => step.DayNumber)
            .ThenBy(step => step.Id)
            .ToListAsync();

        // Only link to items that really have an approved page, so a step never leads to a 404.
        var mentionedNames = steps
            .SelectMany(step => WikiLink().Matches(step.Instructions))
            .Select(match => match.Groups[1].Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var linkableNames = (await _context.Items
                .AsNoTracking()
                .Where(item => item.Status == PageStatus.Approved && mentionedNames.Contains(item.Name))
                .Select(item => item.Name)
                .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Days = steps
            .GroupBy(step => step.DayNumber)
            .Select(day => new DayView(
                day.Key,
                DayThemes.GetValueOrDefault(day.Key, string.Empty),
                day.Select(step => new StepView(
                    step.Id,
                    step.Title,
                    ParseInstructions(step.Instructions, linkableNames))).ToList()))
            .ToList();
    }

    private static List<Segment> ParseInstructions(string text, HashSet<string> linkableNames)
    {
        var segments = new List<Segment>();
        var position = 0;

        foreach (Match match in WikiLink().Matches(text))
        {
            if (match.Index > position)
            {
                segments.Add(new Segment(text[position..match.Index], null));
            }

            var itemName = match.Groups[1].Value.Trim();
            var shownText = match.Groups[2].Success ? match.Groups[2].Value.Trim() : itemName;
            segments.Add(new Segment(shownText, linkableNames.Contains(itemName) ? itemName : null));

            position = match.Index + match.Length;
        }

        if (position < text.Length)
        {
            segments.Add(new Segment(text[position..], null));
        }

        return segments;
    }
}
