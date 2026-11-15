// "What can I craft?" (task N4).
// The player taps what they carry on the left; the middle updates at once with
// what they can craft now and what is one ingredient away. No page reload.
(function () {
    'use strict';

    var STORAGE_KEY = 'survivewiki.pack';
    var MAX_ALMOST = 12;

    var root = document.querySelector('[data-lookup]');
    var dataElement = document.getElementById('lookup-recipes');
    if (!root || !dataElement) return;

    var recipes;
    try {
        recipes = JSON.parse(dataElement.textContent);
    } catch (error) {
        recipes = [];
    }

    var chips = Array.prototype.slice.call(root.querySelectorAll('[data-lookup-item]'));
    var groups = Array.prototype.slice.call(root.querySelectorAll('[data-lookup-group]'));
    var filter = root.querySelector('[data-lookup-filter]');
    var filterEmpty = root.querySelector('[data-lookup-filter-empty]');
    var status = root.querySelector('[data-lookup-status]');
    var selectedBar = root.querySelector('[data-lookup-selected]');
    var selectedChips = root.querySelector('[data-lookup-selected-chips]');
    var emptyState = root.querySelector('[data-lookup-empty]');
    var readySection = root.querySelector('[data-lookup-ready]');
    var readyCards = root.querySelector('[data-lookup-ready-cards]');
    var readyCount = root.querySelector('[data-lookup-ready-count]');
    var almostSection = root.querySelector('[data-lookup-almost]');
    var almostCards = root.querySelector('[data-lookup-almost-cards]');
    var almostCount = root.querySelector('[data-lookup-almost-count]');

    var iconByName = {};
    var knownNames = {};
    chips.forEach(function (chip) {
        var name = chip.getAttribute('data-lookup-item');
        knownNames[name.toLowerCase()] = name;
        iconByName[name.toLowerCase()] = chip.getAttribute('data-icon') || '';
    });

    var pack = {}; // lower-case name -> display name

    // ---- storage ----

    function loadPack() {
        try {
            var saved = JSON.parse(window.localStorage.getItem(STORAGE_KEY) || '[]');
            if (!Array.isArray(saved)) return;
            saved.forEach(function (name) {
                var known = knownNames[String(name).toLowerCase()];
                if (known) pack[known.toLowerCase()] = known;
            });
        } catch (error) { /* start with an empty pack */ }
    }

    function savePack() {
        try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(Object.keys(pack).map(function (key) { return pack[key]; })));
        } catch (error) { /* the pack simply will not be remembered */ }
    }

    // ---- small DOM helpers ----

    function element(tag, className, text) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    }

    function icon(name, url, size) {
        if (url) {
            var image = element('img', 'item-icon');
            image.src = url;
            image.alt = '';
            image.width = size;
            image.height = size;
            image.loading = 'lazy';
            return image;
        }
        var placeholder = element('span', 'item-icon item-icon-missing', name.charAt(0).toUpperCase());
        placeholder.style.width = size + 'px';
        placeholder.style.height = size + 'px';
        placeholder.style.fontSize = Math.round(size / 2) + 'px';
        placeholder.setAttribute('aria-hidden', 'true');
        return placeholder;
    }

    function stationLabel(station) {
        if (station === 'None') return 'By hand';
        if (!station || station === 'Not specified') return '';
        return 'Needs ' + station;
    }

    // ---- cards ----

    function card(recipe, missing) {
        var article = element('article', 'lookup-card' + (missing ? ' is-almost' : ''));

        var top = element('div', 'lookup-card-top');
        top.appendChild(icon(recipe.name, recipe.icon, 56));
        var heading = element('div', 'lookup-card-heading');
        var link = element('a', 'lookup-card-name', recipe.name);
        link.href = recipe.url;
        heading.appendChild(link);
        var station = stationLabel(recipe.station);
        if (station) {
            heading.appendChild(element('span', 'lookup-card-station' + (recipe.station === 'None' ? ' is-hand' : ''), station));
        }
        top.appendChild(heading);
        article.appendChild(top);

        if (recipe.does) article.appendChild(element('p', 'lookup-card-does', recipe.does));
        if (recipe.tip) article.appendChild(element('p', 'lookup-card-tip', recipe.tip));

        var list = element('ul', 'lookup-card-ingredients');
        recipe.ingredients.forEach(function (ingredient) {
            var isMissing = missing && ingredient.name.toLowerCase() === missing.name.toLowerCase();
            var item = element('li', isMissing ? 'is-missing' : 'is-owned');
            item.appendChild(icon(ingredient.name, ingredient.icon, 24));
            item.appendChild(element('span', 'lookup-ingredient-name', ingredient.name));
            item.appendChild(element('span', 'lookup-ingredient-quantity', '× ' + ingredient.quantity));
            item.appendChild(element('span', 'lookup-ingredient-state', isMissing ? 'missing' : 'have'));
            list.appendChild(item);
        });
        article.appendChild(list);

        if (missing && knownNames[missing.name.toLowerCase()]) {
            var add = element('button', 'lookup-card-add', '+ I have ' + missing.name);
            add.type = 'button';
            add.addEventListener('click', function () { toggle(missing.name, true); });
            article.appendChild(add);
        }

        return article;
    }

    function fill(container, cards) {
        container.textContent = '';
        cards.forEach(function (node) { container.appendChild(node); });
    }

    // Most useful first: the items every beginner needs, then by how easy they are to make
    // (by hand, then Science Machine, then the rest; fewer ingredients first).
    function byEase(a, b) {
        function rank(recipe) {
            return recipe.station === 'None' ? 0 : recipe.station === 'Science Machine' ? 1 : 2;
        }
        return (b.featured ? 1 : 0) - (a.featured ? 1 : 0)
            || rank(a) - rank(b)
            || a.ingredients.length - b.ingredients.length
            || a.name.localeCompare(b.name);
    }

    // ---- render ----

    function render() {
        var names = Object.keys(pack);

        chips.forEach(function (chip) {
            var on = Object.prototype.hasOwnProperty.call(pack, chip.getAttribute('data-lookup-item').toLowerCase());
            chip.setAttribute('aria-pressed', on ? 'true' : 'false');
        });

        // "In your pack" bar: each chip removes that item.
        selectedChips.textContent = '';
        names.map(function (key) { return pack[key]; }).sort().forEach(function (name) {
            var chip = element('button', 'lookup-selected-chip');
            chip.type = 'button';
            chip.setAttribute('aria-label', 'Remove ' + name);
            chip.appendChild(icon(name, iconByName[name.toLowerCase()], 24));
            chip.appendChild(element('span', null, name));
            chip.appendChild(element('span', 'lookup-selected-remove', '×'));
            chip.addEventListener('click', function () { toggle(name, false); });
            selectedChips.appendChild(chip);
        });

        var hasItems = names.length > 0;
        selectedBar.hidden = !hasItems;
        emptyState.hidden = hasItems;

        if (!hasItems) {
            readySection.hidden = true;
            almostSection.hidden = true;
            status.textContent = 'Pick your items on the left to see what you can make.';
            return;
        }

        var ready = [];
        var almost = [];
        recipes.forEach(function (recipe) {
            var missing = recipe.ingredients.filter(function (ingredient) {
                return !Object.prototype.hasOwnProperty.call(pack, ingredient.name.toLowerCase());
            });
            if (missing.length === 0) {
                ready.push(recipe);
            } else if (missing.length === 1 && recipe.ingredients.length > 1) {
                almost.push({ recipe: recipe, missing: missing[0] });
            }
        });

        ready.sort(byEase);
        almost.sort(function (a, b) { return byEase(a.recipe, b.recipe); });

        readySection.hidden = false;
        readyCount.textContent = String(ready.length);
        if (ready.length > 0) {
            fill(readyCards, ready.map(function (recipe) { return card(recipe, null); }));
        } else {
            fill(readyCards, [element('p', 'lookup-none', 'Nothing yet with only these items. Add more on the left, or look at what is one item short below.')]);
        }

        almostSection.hidden = almost.length === 0;
        almostCount.textContent = String(almost.length);
        fill(almostCards, almost.slice(0, MAX_ALMOST).map(function (entry) { return card(entry.recipe, entry.missing); }));
        if (almost.length > MAX_ALMOST) {
            almostCards.appendChild(element('p', 'lookup-none', 'Showing the ' + MAX_ALMOST + ' easiest of ' + almost.length + '.'));
        }

        status.textContent = ready.length === 1
            ? 'You can craft 1 thing with ' + names.length + (names.length === 1 ? ' item.' : ' items.')
            : 'You can craft ' + ready.length + ' things with ' + names.length + (names.length === 1 ? ' item.' : ' items.');
    }

    function toggle(name, forceOn) {
        var key = name.toLowerCase();
        var known = knownNames[key];
        if (!known) return;
        var on = forceOn === undefined ? !Object.prototype.hasOwnProperty.call(pack, key) : forceOn;
        if (on) pack[key] = known; else delete pack[key];
        savePack();
        render();
    }

    // ---- events ----

    chips.forEach(function (chip) {
        chip.addEventListener('click', function () { toggle(chip.getAttribute('data-lookup-item')); });
    });

    root.querySelector('[data-lookup-clear]').addEventListener('click', function () {
        pack = {};
        savePack();
        render();
    });

    Array.prototype.forEach.call(root.querySelectorAll('[data-lookup-starter]'), function (button) {
        button.addEventListener('click', function () {
            button.getAttribute('data-lookup-starter').split('|').forEach(function (name) {
                var known = knownNames[name.toLowerCase()];
                if (known) pack[known.toLowerCase()] = known;
            });
            savePack();
            render();
        });
    });

    // Typing in the filter narrows the list across every group.
    var openBeforeFilter = null;
    filter.addEventListener('input', function () {
        var term = filter.value.trim().toLowerCase();
        if (term && openBeforeFilter === null) {
            openBeforeFilter = groups.map(function (group) { return group.open; });
        }

        var anyVisible = false;
        groups.forEach(function (group, index) {
            var visibleInGroup = 0;
            Array.prototype.forEach.call(group.querySelectorAll('[data-lookup-item]'), function (chip) {
                var match = !term || chip.getAttribute('data-lookup-item').toLowerCase().indexOf(term) !== -1;
                chip.hidden = !match;
                if (match) visibleInGroup++;
            });
            group.hidden = visibleInGroup === 0;
            if (term) {
                group.open = visibleInGroup > 0;
            } else if (openBeforeFilter) {
                group.open = openBeforeFilter[index];
            }
            if (visibleInGroup > 0) anyVisible = true;
        });

        if (!term) openBeforeFilter = null;
        filterEmpty.hidden = anyVisible;
    });

    // ---- start ----

    loadPack();
    render();
})();
