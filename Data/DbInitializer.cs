using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SurviveWiki.Models;

namespace SurviveWiki.Data;

public static class DbInitializer
{
    public static void Initialize(SurviveWikiContext context, string contentRootPath)
    {
        context.Database.Migrate();
        SeedWikiItems(context, contentRootPath);
        ApplyItemNotes(context, contentRootPath);
        SeedGuideSteps(context, contentRootPath);
    }

    /// <summary>
    /// Loads the "First 5 Days" plan (task N3) from Data/guide-steps.json.
    /// Steps are matched by day and title, so existing rows keep their Id.
    /// The browser stores progress by step Id, so Ids must not change between runs.
    /// A missing or broken file only disables the guide; it never stops the app.
    /// </summary>
    private static void SeedGuideSteps(SurviveWikiContext context, string contentRootPath)
    {
        var planPath = Path.Combine(contentRootPath, "Data", "guide-steps.json");
        if (!File.Exists(planPath))
        {
            return;
        }

        GuidePlan? plan;
        try
        {
            plan = JsonSerializer.Deserialize<GuidePlan>(
                File.ReadAllText(planPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return;
        }

        if (plan is null)
        {
            return;
        }

        var existingSteps = context.GuideSteps.ToList();
        foreach (var planStep in plan.Steps)
        {
            if (planStep.Day < 1 || string.IsNullOrWhiteSpace(planStep.Title))
            {
                continue;
            }

            var step = existingSteps.FirstOrDefault(s =>
                s.DayNumber == planStep.Day &&
                string.Equals(s.Title, planStep.Title, StringComparison.OrdinalIgnoreCase));

            if (step is null)
            {
                step = new GuideStep { DayNumber = planStep.Day, Title = planStep.Title.Trim() };
                context.GuideSteps.Add(step);
                existingSteps.Add(step);
            }

            step.Instructions = planStep.Instructions;
        }

        context.SaveChanges();
    }

    /// <summary>
    /// Replaces the imported description and tip of the items a beginner meets first
    /// with the hand-written notes in Data/item-notes.json. A note also covers the
    /// item's "/DST" variant. Runs after the catalog seed, which resets those fields.
    /// A missing or broken file leaves the imported text in place.
    /// </summary>
    private static void ApplyItemNotes(SurviveWikiContext context, string contentRootPath)
    {
        var notesPath = Path.Combine(contentRootPath, "Data", "item-notes.json");
        if (!File.Exists(notesPath))
        {
            return;
        }

        ItemNotes? notes;
        try
        {
            notes = JsonSerializer.Deserialize<ItemNotes>(
                File.ReadAllText(notesPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return;
        }

        if (notes is null)
        {
            return;
        }

        var itemsByName = context.Items
            .Where(item => item.SourceUrl != null && item.SourceUrl != "")
            .ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var note in notes.Notes)
        {
            foreach (var name in new[] { note.Name, note.Name + "/DST" })
            {
                if (!itemsByName.TryGetValue(name, out var item))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(note.Does))
                {
                    item.Description = note.Does.Trim();
                }

                if (!string.IsNullOrWhiteSpace(note.BestFor))
                {
                    item.BeginnerTip = note.BestFor.Trim();
                }
            }
        }

        context.SaveChanges();
    }

    private sealed class ItemNotes
    {
        public List<ItemNote> Notes { get; set; } = new();
    }

    private sealed class ItemNote
    {
        public string Name { get; set; } = string.Empty;
        public string Does { get; set; } = string.Empty;
        public string BestFor { get; set; } = string.Empty;
    }

    private sealed class GuidePlan
    {
        public List<GuidePlanStep> Steps { get; set; } = new();
    }

    private sealed class GuidePlanStep
    {
        public int Day { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
    }

    /// <summary>
    /// Loads the item catalog. Data/wiki-items.json is the main import and always wins:
    /// its text and recipes are applied on every start. Data/wiki-items-extra.json
    /// (from Tools/import-missing-items.py) is loaded after it and only fills gaps:
    /// items the main file lacks, plus a recipe or crafting station where it has none.
    /// </summary>
    private static void SeedWikiItems(SurviveWikiContext context, string contentRootPath)
    {
        var catalogPath = Path.Combine(contentRootPath, "Data", "wiki-items.json");
        var catalog = ReadCatalog(catalogPath)
            ?? throw new InvalidDataException($"Could not read the wiki item catalog at {catalogPath}.");

        // The extra file is optional. Without it the wiki still works, with fewer items.
        var extraPath = Path.Combine(contentRootPath, "Data", "wiki-items-extra.json");
        WikiCatalog? extra = null;
        if (File.Exists(extraPath))
        {
            try
            {
                extra = ReadCatalog(extraPath);
            }
            catch (JsonException)
            {
                extra = null;
            }
        }

        var mainByName = catalog.Items
            .GroupBy(wikiItem => wikiItem.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        // Entries of the extra file that describe an item the main file does not have.
        var extraOnly = (extra?.Items ?? new())
            .Where(wikiItem => !string.IsNullOrWhiteSpace(wikiItem.Name) && !mainByName.ContainsKey(wikiItem.Name))
            .GroupBy(wikiItem => wikiItem.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var itemsByName = context.Items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);

        void Apply(WikiItem wikiItem)
        {
            if (!itemsByName.TryGetValue(wikiItem.Name, out var item))
            {
                item = new Item { Name = wikiItem.Name };
                context.Items.Add(item);
                itemsByName.Add(item.Name, item);
            }

            item.Category = wikiItem.Category;
            item.Description = WikiText.Clean(wikiItem.Description);
            item.BeginnerTip = wikiItem.BeginnerTip;
            item.CraftingStation = wikiItem.CraftingStation;
            item.SourceUrl = wikiItem.SourceUrl;
            item.Status = PageStatus.Approved;
        }

        foreach (var wikiItem in catalog.Items)
        {
            Apply(wikiItem);
        }

        foreach (var wikiItem in extraOnly)
        {
            // A page a contributor wrote under the same name is theirs; the import must not replace it.
            var contributed = itemsByName.TryGetValue(wikiItem.Name, out var existing)
                              && string.IsNullOrEmpty(existing.SourceUrl);
            if (!contributed)
            {
                Apply(wikiItem);
            }
        }

        // Where the main file does not say which station an item needs, take it from the extra file.
        foreach (var wikiItem in extra?.Items ?? new())
        {
            if (mainByName.TryGetValue(wikiItem.Name, out var mainItem)
                && mainItem.CraftingStation == "Not specified"
                && wikiItem.CraftingStation != "Not specified"
                && itemsByName.TryGetValue(wikiItem.Name, out var item))
            {
                item.CraftingStation = wikiItem.CraftingStation;
            }
        }

        context.SaveChanges();

        var recipes = context.RecipeIngredients
            .Select(recipe => new { recipe.ResultItemId, recipe.IngredientItemId })
            .AsEnumerable()
            .Select(recipe => (recipe.ResultItemId, recipe.IngredientItemId))
            .ToHashSet();

        void AddRecipe(WikiItem wikiItem)
        {
            if (!itemsByName.TryGetValue(wikiItem.Name, out var resultItem) || string.IsNullOrEmpty(resultItem.SourceUrl))
            {
                return;
            }

            foreach (var ingredient in wikiItem.Ingredients)
            {
                if (!itemsByName.TryGetValue(ingredient.Name, out var ingredientItem))
                {
                    continue;
                }

                if (recipes.Add((resultItem.Id, ingredientItem.Id)))
                {
                    context.RecipeIngredients.Add(new RecipeIngredient
                    {
                        ResultItemId = resultItem.Id,
                        IngredientItemId = ingredientItem.Id,
                        QuantityRequired = ingredient.Quantity
                    });
                }
            }
        }

        foreach (var wikiItem in catalog.Items)
        {
            AddRecipe(wikiItem);
        }

        foreach (var wikiItem in extra?.Items ?? new())
        {
            // Use the extra recipe only when the main file has none for that item.
            var mainHasRecipe = mainByName.TryGetValue(wikiItem.Name, out var mainItem) && mainItem.Ingredients.Count > 0;
            if (!mainHasRecipe)
            {
                AddRecipe(wikiItem);
            }
        }

        context.SaveChanges();
    }

    private static WikiCatalog? ReadCatalog(string path) =>
        JsonSerializer.Deserialize<WikiCatalog>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    private sealed class WikiCatalog
    {
        public List<WikiItem> Items { get; set; } = new();
    }

    private sealed class WikiItem
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = "Item";
        public string Description { get; set; } = string.Empty;
        public string BeginnerTip { get; set; } = string.Empty;
        public string CraftingStation { get; set; } = "Not specified";
        public List<WikiIngredient> Ingredients { get; set; } = new();
        public string SourceUrl { get; set; } = string.Empty;
    }

    private sealed class WikiIngredient
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}
