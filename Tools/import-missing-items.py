"""Add items and recipes that the first import missed.

Run from the project folder:

    python Tools/import-missing-items.py

Data/wiki-items.json was built by scanning wiki categories, so it misses objects
that are not filed under them (Cut Stone, Crock Pot), objects that share a page
with another one (Fire Pit lives on the Campfire page), and some recipes.

This script reads every wiki page that uses the "Object Infobox" template, which
is where the wiki keeps an object's recipe, and writes Data/wiki-items-extra.json
in the same shape as the main catalog. That file holds:
  * objects missing from the main catalog that can be crafted, or that are an
    ingredient of some recipe (creatures and scenery are left out), and
  * objects that are in it but have no recipe or no crafting station there.

The main catalog is never changed. The app loads the extra file after it and only
uses it to add what is missing (see Data/DbInitializer.cs).

Text comes from https://dontstarve.wiki.gg, licensed CC BY-SA 3.0.
"""

import collections
import json
import re
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

API = "https://dontstarve.wiki.gg/api.php"
WIKI = "https://dontstarve.wiki.gg/wiki/"
USER_AGENT = "Mozilla/5.0 (compatible; course-project-importer)"
TEMPLATE = "Object Infobox"
BATCH_SIZE = 50
PAUSE_SECONDS = 0.1

ROOT = Path(__file__).resolve().parent.parent
CATALOG = ROOT / "Data" / "wiki-items.json"
OUTPUT = ROOT / "Data" / "wiki-items-extra.json"

# The infobox "tier" says which station unlocks the recipe. Checked against known
# recipes: Dark Sword (Shadow Manipulator) is tier 3, Meat Effigy (Prestihatitator) is tier 4.
# Other tiers belong to event and DLC stations and are left as "Not specified".
STATION_BY_TIER = {
    "0": "None",
    "1": "Science Machine",
    "2": "Alchemy Engine",
    "3": "Shadow Manipulator",
    "4": "Prestihatitator",
    "5": "Ancient Pseudoscience Station",
    "6": "Ancient Pseudoscience Station",
}


def station_for(tier):
    if "always available" in tier.lower():
        return "None"
    return STATION_BY_TIER.get(tier, "Not specified")


def api(**params):
    params["format"] = "json"
    request = urllib.request.Request(
        f"{API}?{urllib.parse.urlencode(params)}", headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=60) as response:
        return json.loads(response.read())


def pages_using_template():
    titles, more = [], {}
    while True:
        data = api(action="query", list="embeddedin", eititle=f"Template:{TEMPLATE}",
                   einamespace=0, eilimit=500, eifilterredir="nonredirects", **more)
        titles += [page["title"] for page in data["query"]["embeddedin"]]
        if "continue" not in data:
            return sorted(set(titles))
        more = data["continue"]
        time.sleep(PAUSE_SECONDS)


def wikitext_of(titles):
    texts = {}
    for start in range(0, len(titles), BATCH_SIZE):
        data = api(action="query", prop="revisions", rvprop="content", rvslots="main",
                   titles="|".join(titles[start:start + BATCH_SIZE]))
        for page in data["query"]["pages"].values():
            revisions = page.get("revisions")
            if revisions:
                texts[page["title"]] = revisions[0]["slots"]["main"]["*"]
        time.sleep(PAUSE_SECONDS)
        print(f"  read {min(start + BATCH_SIZE, len(titles))} / {len(titles)} pages", end="\r")
    print()
    return texts


def template_blocks(text, name):
    """Yield the inside of every {{name ...}} in the text, matching nested braces."""
    pattern = re.compile(r"\{\{\s*" + re.escape(name) + r"\s*(?=[|}\n])", re.IGNORECASE)
    for match in pattern.finditer(text):
        depth, position = 2, match.end()
        while position < len(text) and depth > 0:
            if text.startswith("{{", position):
                depth += 2
                position += 2
            elif text.startswith("}}", position):
                depth -= 2
                position += 2
            else:
                position += 1
        yield text[match.end():position - 2]


def parameters(block):
    """Split 'a=1|b=[[x|y]]' on the pipes that are not inside links or templates."""
    parts, current, depth = [], "", 0
    position = 0
    while position < len(block):
        pair = block[position:position + 2]
        if pair in ("{{", "[["):
            depth += 1
            current += pair
            position += 2
        elif pair in ("}}", "]]"):
            depth -= 1
            current += pair
            position += 2
        elif block[position] == "|" and depth == 0:
            parts.append(current)
            current = ""
            position += 1
        else:
            current += block[position]
            position += 1
    parts.append(current)

    result = {}
    for part in parts:
        if "=" in part:
            key, value = part.split("=", 1)
            result[key.strip().lower()] = value.strip()
    return result


def plain(value):
    """Turn wiki markup into plain text."""
    value = re.sub(r"<!--.*?-->", "", value, flags=re.S)
    value = re.sub(r"<ref[^>]*>.*?</ref>|<ref[^>]*/>", "", value, flags=re.S)
    value = re.sub(r"\[\[(?:File|Image):[^\]]*\]\]", "", value, flags=re.I)
    for _ in range(3):
        value = re.sub(r"\{\{[^{}]*\}\}", "", value)
    value = re.sub(r"\[\[[^\]|]*\|([^\]]*)\]\]", r"\1", value)
    value = re.sub(r"\[\[([^\]]*)\]\]", r"\1", value)
    value = re.sub(r"<br\s*/?>", " ", value, flags=re.I)
    value = re.sub(r"<[^>]+>", "", value)
    value = value.replace("'''", "").replace("''", "").replace("&times;", "×").replace("&nbsp;", " ")
    return re.sub(r"\s+", " ", value).strip()


def ingredient_name(value):
    link = re.search(r"\[\[([^\]|#]+)", value)
    return (link.group(1) if link else plain(value)).replace("_", " ").strip()


def quantity(value):
    number = re.search(r"\d+", plain(value or ""))
    return max(1, int(number.group())) if number else 1


def objects_on(title, text):
    """Every object described on a page.

    A page can hold several: "Bed Roll" describes the Straw Roll and the Fur Roll,
    and "Campfire" also describes the Fire Pit. Each infobox names its own object;
    one without a name describes the page itself.
    """
    suffix = "/DST" if title.endswith("/DST") else ""
    for index, block in enumerate(template_blocks(text, TEMPLATE)):
        fields = parameters(block)
        name = plain(fields.get("name", "")).replace("_", " ").strip()
        if not name:
            if index > 0:
                continue
            name = title
        elif suffix and not name.endswith(suffix):
            name += suffix

        ingredients = []
        for number in range(1, 9):
            raw = fields.get(f"ingredient{number}", "")
            if not raw:
                continue
            ingredient = ingredient_name(raw)
            if ingredient and not any(existing["name"].lower() == ingredient.lower() for existing in ingredients):
                ingredients.append({"name": ingredient, "quantity": quantity(fields.get(f"multiplier{number}"))})

        station = station_for(plain(fields.get("tier", ""))) if ingredients else "Not specified"
        category = plain(fields.get("tab", "")).split(",")[0].strip() or "Item"

        yield {
            "name": name,
            "category": category[:1].upper() + category[1:],
            "description": plain(fields.get("description", "")),
            "beginnerTip": plain(fields.get("perk", "")),
            "craftingStation": station,
            "ingredients": ingredients,
            "sourceUrl": WIKI + urllib.parse.quote(title.replace(" ", "_"), safe="/_'()"),
        }


def main():
    catalog = {item["name"].lower(): item for item in json.loads(CATALOG.read_text(encoding="utf-8"))["items"]}

    print(f"Finding pages that use the {TEMPLATE} template...")
    titles = pages_using_template()
    print(f"Reading {len(titles)} pages...")
    texts = wikitext_of(titles)

    found = {}
    for title in titles:
        for item in objects_on(title, texts.get(title, "")):
            known = found.get(item["name"].lower())
            # A page may describe an object twice (placed and as an item); keep the one with the recipe.
            if known is None or (item["ingredients"] and not known["ingredients"]):
                found[item["name"].lower()] = item

    # Only craftable things and their materials belong in a crafting wiki.
    used_as_ingredient = {
        ingredient["name"].lower()
        for item in list(found.values()) + list(catalog.values())
        for ingredient in item["ingredients"]
    }

    extra, new_items, new_recipes, new_stations = [], 0, 0, 0
    for key, item in sorted(found.items()):
        existing = catalog.get(key)
        if existing is None:
            if not item["ingredients"] and key not in used_as_ingredient:
                continue
            new_items += 1
            extra.append(item)
        else:
            adds_recipe = bool(item["ingredients"]) and not existing["ingredients"]
            adds_station = item["craftingStation"] != "Not specified" and existing["craftingStation"] == "Not specified"
            if adds_recipe or adds_station:
                new_recipes += adds_recipe
                new_stations += adds_station
                extra.append(item)

    OUTPUT.write_text(json.dumps({
        "source": "Don't Starve Wiki",
        "license": "CC BY-SA 3.0",
        "licenseUrl": "https://creativecommons.org/licenses/by-sa/3.0/",
        "note": "Generated by Tools/import-missing-items.py. Loaded after wiki-items.json to add what it is missing.",
        "items": extra,
    }, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    stations = collections.Counter(item["craftingStation"] for item in extra if item["ingredients"])
    print(f"{len(found)} objects found on the wiki; the main catalog has {len(catalog)}.")
    print(f"Wrote {len(extra)} entries: {new_items} new objects, "
          f"{new_recipes} recipes and {new_stations} stations for objects already in the catalog.")
    print(f"New objects with a recipe: {sum(1 for item in extra if item['name'].lower() not in catalog and item['ingredients'])}")
    print(f"Stations among entries with a recipe: {dict(stations)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
