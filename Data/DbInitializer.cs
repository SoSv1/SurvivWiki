using SurviveWiki.Models;

namespace SurviveWiki.Data;

public static class DbInitializer
{
    public static void Initialize(SurviveWikiContext context)
    {
        // Creates the DB if it doesn't exist (good for prototyping)
        context.Database.EnsureCreated();

        // Look for any items. If we have items, the DB is already seeded.
        if (context.Items.Any())
        {
            return;
        }

        // 1. Create the Base Materials (These aren't crafted from anything)
        var flint = new Item { Name = "Flint", Category = "Material", Description = "A sharp rock.", BeginnerTip = "Found scattered on the ground.", Status = PageStatus.Approved };
        var twigs = new Item { Name = "Twigs", Category = "Material", Description = "Small sticks.", BeginnerTip = "Harvested from Saplings.", Status = PageStatus.Approved };
        var cutGrass = new Item { Name = "Cut Grass", Category = "Material", Description = "Common grass.", BeginnerTip = "Harvested from Grass Tufts.", Status = PageStatus.Approved };
        var log = new Item { Name = "Log", Category = "Material", Description = "Wood from a tree.", BeginnerTip = "Chop down trees to get this.", Status = PageStatus.Approved };

        // 2. Create the Craftable Items
        var axe = new Item { Name = "Axe", Category = "Tool", Description = "Used to chop down trees.", BeginnerTip = "Make this immediately on day one.", CraftingStation = "None", Status = PageStatus.Approved };
        var campfire = new Item { Name = "Campfire", Category = "Light", Description = "Provides light and heat.", BeginnerTip = "Build this before night falls so you don't die!", CraftingStation = "None", Status = PageStatus.Approved };

        // Add to context and save so EF Core generates their IDs
        context.Items.AddRange(flint, twigs, cutGrass, log, axe, campfire);
        context.SaveChanges();

        // 3. Create the Recipes (Connecting the items together)
        var recipes = new RecipeIngredient[]
        {
            // Axe requires: 1 Twig, 1 Flint
            new RecipeIngredient { ResultItemId = axe.Id, IngredientItemId = twigs.Id, QuantityRequired = 1 },
            new RecipeIngredient { ResultItemId = axe.Id, IngredientItemId = flint.Id, QuantityRequired = 1 },

            // Campfire requires: 3 Cut Grass, 2 Logs
            new RecipeIngredient { ResultItemId = campfire.Id, IngredientItemId = cutGrass.Id, QuantityRequired = 3 },
            new RecipeIngredient { ResultItemId = campfire.Id, IngredientItemId = log.Id, QuantityRequired = 2 }
        };

        context.RecipeIngredients.AddRange(recipes);
        context.SaveChanges();
    }
}