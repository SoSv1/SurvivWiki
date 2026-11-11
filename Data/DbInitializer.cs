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
