// "First 5 Days" plan (task N3).
// Keeps the ticked steps in localStorage so a player can close the page,
// play, and continue from the first unfinished step in a later session.
(function () {
    'use strict';

    var STORAGE_KEY = 'survivewiki.guide.doneSteps';

    var root = document.querySelector('[data-guide]');
    if (!root) return;

    var checkboxes = Array.prototype.slice.call(root.querySelectorAll('[data-guide-step]'));
    if (checkboxes.length === 0) return;

    var days = Array.prototype.slice.call(root.querySelectorAll('[data-guide-day]'));
    var bar = root.querySelector('[data-guide-bar]');
    var fill = root.querySelector('[data-guide-fill]');
    var text = root.querySelector('[data-guide-text]');
    var resetButton = root.querySelector('[data-guide-reset]');
    var next = root.querySelector('[data-guide-next]');
    var nextLink = root.querySelector('[data-guide-next-link]');
    var finished = root.querySelector('[data-guide-finished]');
    var storageWarning = root.querySelector('[data-guide-storage-warning]');

    // ---- storage (may be unavailable in private windows) ----

    function loadDoneIds() {
        try {
            var saved = JSON.parse(window.localStorage.getItem(STORAGE_KEY) || '[]');
            return Array.isArray(saved) ? saved.map(String) : [];
        } catch (error) {
            return [];
        }
    }

    function saveDoneIds() {
        var doneIds = checkboxes
            .filter(function (box) { return box.checked; })
            .map(function (box) { return box.getAttribute('data-guide-step'); });
        try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify(doneIds));
            storageWarning.hidden = true;
        } catch (error) {
            storageWarning.hidden = false;
        }
    }

    // ---- display ----

    function stepsOf(day) {
        return Array.prototype.slice.call(day.querySelectorAll('[data-guide-step]'));
    }

    function isDayComplete(day) {
        return stepsOf(day).every(function (box) { return box.checked; });
    }

    function render() {
        var doneCount = 0;

        days.forEach(function (day) {
            var steps = stepsOf(day);
            var doneInDay = steps.filter(function (box) { return box.checked; }).length;
            doneCount += doneInDay;
            day.querySelector('[data-guide-day-count]').textContent = doneInDay + ' / ' + steps.length;
            day.classList.toggle('is-complete', doneInDay === steps.length);
        });

        checkboxes.forEach(function (box) {
            box.closest('.guide-step').classList.toggle('is-done', box.checked);
        });

        var total = checkboxes.length;
        var percent = Math.round((doneCount / total) * 100);
        fill.style.width = percent + '%';
        bar.setAttribute('aria-valuenow', String(doneCount));
        text.textContent = percent + '% complete · ' + doneCount + ' of ' + total + ' steps';
        resetButton.hidden = doneCount === 0;

        var nextBox = checkboxes.filter(function (box) { return !box.checked; })[0];
        if (nextBox) {
            var day = nextBox.closest('[data-guide-day]');
            nextLink.textContent = 'Day ' + day.getAttribute('data-guide-day') + ' — ' +
                nextBox.getAttribute('data-guide-title');
            nextLink.setAttribute('href', '#' + nextBox.closest('.guide-step').id);
            next.hidden = false;
            finished.hidden = true;
        } else {
            next.hidden = true;
            finished.hidden = false;
        }
    }

    function openFirstUnfinishedDay() {
        var target = days.filter(function (day) { return !isDayComplete(day); })[0];
        days.forEach(function (day) { day.open = day === target; });
    }

    // ---- events ----

    checkboxes.forEach(function (box) {
        box.addEventListener('change', function () {
            saveDoneIds();
            render();

            // Finishing a day reveals the next one, without closing what the player is reading.
            var day = box.closest('[data-guide-day]');
            if (box.checked && isDayComplete(day)) {
                var following = days[days.indexOf(day) + 1];
                if (following) following.open = true;
            }
        });
    });

    nextLink.addEventListener('click', function (event) {
        var step = root.querySelector(nextLink.getAttribute('href'));
        if (!step) return;
        event.preventDefault();
        step.closest('[data-guide-day]').open = true;
        step.scrollIntoView({ block: 'center' });
        step.querySelector('[data-guide-step]').focus({ preventScroll: true });
    });

    resetButton.addEventListener('click', function () {
        if (!window.confirm('Clear all ticks and start the plan again from Day 1?')) return;
        checkboxes.forEach(function (box) { box.checked = false; });
        saveDoneIds();
        render();
        openFirstUnfinishedDay();
        window.scrollTo(0, 0);
    });

    // ---- start ----

    var doneIds = loadDoneIds();
    checkboxes.forEach(function (box) {
        box.checked = doneIds.indexOf(box.getAttribute('data-guide-step')) !== -1;
    });
    render();
    openFirstUnfinishedDay();
})();
