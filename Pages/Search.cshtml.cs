using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SurviveWiki.Data;
using SurviveWiki.Models;
using SurviveWiki.Services;

namespace SurviveWiki.Pages;

/// <summary>
/// Tasks N1 and N2. A query is answered in up to three ways:
///   1. a topic ("night", "hungry") shows a quick answer with the items that solve it;
///   2. an exact item name opens that item directly, the fastest path mid-game;
///   3. otherwise a list: names that match first, then pages that mention the words.
/// </summary>
public class SearchModel : PageModel
{
    private const int MaxQueryLength = 60;
    private const int MaxResults = 40;
    private const int MinLengthForTextSearch = 3;

    private readonly SurviveWikiContext _context;
    private readonly SearchTopics _topics;

    public SearchModel(SurviveWikiContext context, SearchTopics topics)
    {
        _context = context;
        _topics = topics;
    }

    [BindProperty(SupportsGet = true)]
    public string Query { get; set; } = string.Empty;

    public List<Item> SearchResults { get; set; } = new();
    public bool HasSearched { get; set; }

    public SearchTopics.Topic? Topic { get; private set; }

    /// <summary>The topic's items with their recipes, in the order the topic lists them.</summary>
    public List<Item> TopicItems { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        Query = (Query ?? string.Empty).Trim();
        if (Query.Length > MaxQueryLength)
        {
            Query = Query[..MaxQueryLength];
        }

        if (Query.Length == 0)
        {
            return Page();
        }

        HasSearched = true;
        var term = Query.ToLower();

        Topic = _topics.Match(Query);
        if (Topic is not null)
        {
            var wanted = Topic.Items.Select(name => name.ToLower()).ToList();
            var found = await _context.Items
                .AsNoTracking()
                .Include(item => item.CraftedFrom)
                    .ThenInclude(recipe => recipe.IngredientItem)
                .Where(item => item.Status == PageStatus.Approved && wanted.Contains(item.Name.ToLower()))
                .ToListAsync();

            TopicItems = found.OrderBy(item => wanted.IndexOf(item.Name.ToLower())).ToList();
        }
        else
        {
            // No topic: an exact item name goes straight to that item.
            var exactMatch = await _context.Items
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Name.ToLower() == term && item.Status == PageStatus.Approved);

            if (exactMatch is not null)
            {
                return RedirectToPage("/Item", new { name = exactMatch.Name });
            }
        }

        // Names first: the exact name, then names that start with the term, then the rest.
        var nameMatches = await _context.Items
            .AsNoTracking()
            .Where(item => item.Status == PageStatus.Approved && item.Name.ToLower().Contains(term))
            .ToListAsync();

        SearchResults = nameMatches
            .OrderBy(item => item.Name.ToLower() == term ? 0 : item.Name.ToLower().StartsWith(term) ? 1 : 2)
            .ThenBy(item => item.Name)
            .Take(MaxResults)
            .ToList();

        // Then pages that mention the term, so a word that is not an item name still finds something.
        if (Query.Length >= MinLengthForTextSearch && SearchResults.Count < MaxResults)
        {
            var textMatches = await _context.Items
                .AsNoTracking()
                .Where(item => item.Status == PageStatus.Approved
                               && !item.Name.ToLower().Contains(term)
                               && (item.Description.ToLower().Contains(term) || item.BeginnerTip.ToLower().Contains(term)))
                .OrderBy(item => item.Name)
                .Take(MaxResults - SearchResults.Count)
                .ToListAsync();

            SearchResults.AddRange(textMatches);
        }

        return Page();
    }
}
