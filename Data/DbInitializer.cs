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

    private static void SeedWikiItems(SurviveWikiContext context, string contentRootPath)
    {
        var catalogPath = Path.Combine(contentRootPath, "Data", "wiki-items.json");
        var catalogJson = File.ReadAllText(catalogPath);
        var catalog = JsonSerializer.Deserialize<WikiCatalog>(
            catalogJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"Could not read the wiki item catalog at {catalogPath}.");

        var itemsByName = context.Items.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var wikiItem in catalog.Items)
        {
            if (!itemsByName.TryGetValue(wikiItem.Name, out var item))
            {
                item = new Item { Name = wikiItem.Name };
                context.Items.Add(item);
                itemsByName.Add(item.Name, item);
            }

            item.Category = wikiItem.Category;
            item.Description = wikiItem.Description;
            item.BeginnerTip = wikiItem.BeginnerTip;
            item.CraftingStation = wikiItem.CraftingStation;
            item.SourceUrl = wikiItem.SourceUrl;
            item.Status = PageStatus.Approved;
        }

        context.SaveChanges();

        var recipes = context.RecipeIngredients
            .Select(recipe => new { recipe.ResultItemId, recipe.IngredientItemId })
            .AsEnumerable()
            .Select(recipe => (recipe.ResultItemId, recipe.IngredientItemId))
            .ToHashSet();

        foreach (var wikiItem in catalog.Items)
        {
            var resultItem = itemsByName[wikiItem.Name];
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

        context.SaveChanges();
    }

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
