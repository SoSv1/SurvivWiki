// "First 5 Days" plan (task N3), shown as a road with a flag for each day.
// Tapping a flag opens that day's jobs. Ticks are kept in localStorage, so the
// player can close the page, play, and carry on from where they stopped.
(function () {
    'use strict';

    var STORAGE_KEY = 'survivewiki.guide.doneSteps';

    var root = document.querySelector('[data-guide]');
    if (!root) return;

    var checkboxes = Array.prototype.slice.call(root.querySelectorAll('[data-guide-step]'));
    if (checkboxes.length === 0) return;

    var days = Array.prototype.slice.call(root.querySelectorAll('[data-guide-day]'));
    var flags = Array.prototype.slice.call(root.querySelectorAll('[data-guide-flag]'));
    var road = root.querySelector('[data-guide-road]');
    var walked = root.querySelector('[data-guide-walked]');
    var you = root.querySelector('[data-guide-you]');
    var pulse = root.querySelector('[data-guide-pulse]');
    var text = root.querySelector('[data-guide-text]');
    var daysDone = root.querySelector('[data-guide-days-done]');
    var resetButton = root.querySelector('[data-guide-reset]');
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

    // ---- the road ----

    // How far along the road each flag stands, found by walking the path once.
    var roadLength = road && road.getTotalLength ? road.getTotalLength() : 0;
    var flagDistances = flags.map(function (flag) {
        var x = parseFloat(flag.getAttribute('data-x'));
        var y = parseFloat(flag.getAttribute('data-y'));
        var best = 0;
        var bestGap = Infinity;
        for (var distance = 0; distance <= roadLength; distance += 2) {
            var point = road.getPointAtLength(distance);
            var gap = (point.x - x) * (point.x - x) + (point.y - y) * (point.y - y);
            if (gap < bestGap) {
                bestGap = gap;
                best = distance;
            }
        }
        return best;
    });

    function stepsOf(day) {
        return Array.prototype.slice.call(day.querySelectorAll('[data-guide-step]'));
    }

    function doneIn(day) {
        return stepsOf(day).filter(function (box) { return box.checked; }).length;
    }

    function isComplete(day) {
        return doneIn(day) === stepsOf(day).length;
    }

    function firstUnfinishedIndex() {
        for (var index = 0; index < days.length; index++) {
            if (!isComplete(days[index])) return index;
        }
        return -1;
    }

    // The walked part of the road reaches each finished day's flag, then moves
    // toward the next flag as that day's jobs get ticked.
    function walkedDistance() {
        if (!roadLength || flagDistances.length === 0) return 0;
        var index = firstUnfinishedIndex();
        if (index === -1) return flagDistances[flagDistances.length - 1];
        if (index >= flagDistances.length) return flagDistances[flagDistances.length - 1];
        var from = index === 0 ? 0 : flagDistances[index - 1];
        var share = doneIn(days[index]) / stepsOf(days[index]).length;
        return from + (flagDistances[index] - from) * share;
    }

    // ---- display ----

    function render() {
        var totalDone = 0;
        var finishedDays = 0;
        var current = firstUnfinishedIndex();

        days.forEach(function (day, index) {
            var steps = stepsOf(day);
            var done = doneIn(day);
            var complete = done === steps.length;
            totalDone += done;
            if (complete) finishedDays++;

            day.querySelector('[data-guide-day-count]').textContent = done + ' of ' + steps.length + ' done';
            Array.prototype.forEach.call(day.querySelectorAll('[data-guide-pip]'), function (pip, pipIndex) {
                pip.classList.toggle('is-on', pipIndex < done);
            });
            day.querySelector('[data-guide-day-done]').hidden = !complete;

            var flag = flags[index];
            if (flag) {
                flag.querySelector('[data-guide-flag-count]').textContent = done + '/' + steps.length;
                flag.classList.toggle('is-done', complete);
                flag.classList.toggle('is-current', index === current);
            }
        });

        checkboxes.forEach(function (box) {
            box.closest('.trail-step').classList.toggle('is-done', box.checked);
        });

        daysDone.textContent = String(finishedDays);
        text.textContent = totalDone + ' of ' + checkboxes.length + ' jobs done';
        resetButton.hidden = totalDone === 0;

        if (roadLength) {
            var distance = walkedDistance();
            walked.style.strokeDasharray = distance + ' ' + (roadLength + 10);
            var point = road.getPointAtLength(distance);
            [you, pulse].forEach(function (marker) {
                marker.setAttribute('cx', point.x);
                marker.setAttribute('cy', point.y);
            });
        }
    }

    function openDay(number, moveFocus) {
        var target = null;
        days.forEach(function (day) {
            var isTarget = day.getAttribute('data-guide-day') === String(number);
            day.hidden = !isTarget;
            if (isTarget) target = day;
        });
        flags.forEach(function (flag) {
            var isOpen = flag.getAttribute('data-guide-flag') === String(number);
            flag.classList.toggle('is-open', isOpen);
            flag.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        });

        if (target && moveFocus) {
            // On a narrow screen the jobs are below the map, so bring them into view.
            if (target.getBoundingClientRect().top > window.innerHeight * 0.6 || target.getBoundingClientRect().top < 0) {
                target.scrollIntoView({ block: 'start' });
            }
            target.focus({ preventScroll: true });
        }
    }

    function celebrate(index) {
        var flag = flags[index];
        if (!flag) return;
        flag.classList.remove('just-done');
        void flag.offsetWidth; // restart the animation if it is already running
        flag.classList.add('just-done');
    }

    // ---- events ----

    flags.forEach(function (flag) {
        flag.addEventListener('click', function () {
            openDay(flag.getAttribute('data-guide-flag'), true);
        });
    });

    Array.prototype.forEach.call(root.querySelectorAll('[data-guide-go]'), function (button) {
        button.addEventListener('click', function () {
            openDay(button.getAttribute('data-guide-go'), true);
        });
    });

    checkboxes.forEach(function (box) {
        box.addEventListener('change', function () {
            var day = box.closest('[data-guide-day]');
            var wasComplete = isComplete(day) && !box.checked ? false : null;
            saveDoneIds();
            render();
            if (box.checked && isComplete(day) && wasComplete === null) {
                celebrate(days.indexOf(day));
            }
        });
    });

    resetButton.addEventListener('click', function () {
        if (!window.confirm('Clear all ticks and start the plan again from Day 1?')) return;
        checkboxes.forEach(function (box) { box.checked = false; });
        saveDoneIds();
        render();
        openDay(days[0].getAttribute('data-guide-day'), false);
    });

    // ---- start ----

    var doneIds = loadDoneIds();
    checkboxes.forEach(function (box) {
        box.checked = doneIds.indexOf(box.getAttribute('data-guide-step')) !== -1;
    });
    render();

    var startIndex = firstUnfinishedIndex();
    openDay(days[startIndex === -1 ? days.length - 1 : startIndex].getAttribute('data-guide-day'), false);
})();
