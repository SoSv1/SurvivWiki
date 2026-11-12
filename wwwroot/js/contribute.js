// Contributor form (task C1): ingredient rows, live duplicate check,
// and moving the contributor to the first problem or to the preview.
(function () {
    'use strict';

    var form = document.querySelector('[data-contribute]');
    if (!form) return;

    // ---- ingredient rows ----

    var rowsHost = form.querySelector('[data-contribute-rows]');
    var addButton = form.querySelector('[data-contribute-add]');
    var maxRows = parseInt(rowsHost.getAttribute('data-max'), 10) || 8;

    function rows() {
        return Array.prototype.slice.call(rowsHost.querySelectorAll('[data-contribute-row]'));
    }

    // The server reads rows by position (Input.Ingredients[0], [1], ...),
    // so the numbers must stay continuous after a row is added or removed.
    function renumber() {
        rows().forEach(function (row, index) {
            Array.prototype.forEach.call(row.querySelectorAll('input, span, button'), function (element) {
                // Hidden helper inputs carry the field name in their value.
                var attributes = ['name', 'id', 'data-valmsg-for'];
                if (element.type === 'hidden') attributes.push('value');
                attributes.forEach(function (attribute) {
                    var value = element.getAttribute(attribute);
                    if (!value) return;
                    element.setAttribute(attribute, value
                        .replace(/Ingredients\[\d+\]/, 'Ingredients[' + index + ']')
                        .replace(/Ingredients_\d+__/, 'Ingredients_' + index + '__'));
                });
                var label = element.getAttribute('aria-label');
                if (label) element.setAttribute('aria-label', label.replace(/\d+/, String(index + 1)));
            });
        });
        addButton.hidden = rows().length >= maxRows;
    }

    function clearRow(row) {
        Array.prototype.forEach.call(row.querySelectorAll('input:not([type="hidden"])'), function (input) {
            input.value = input.type === 'number' ? '1' : '';
            input.classList.remove('input-validation-error');
        });
        Array.prototype.forEach.call(row.querySelectorAll('.contribute-error'), function (message) {
            message.textContent = '';
        });
    }

    addButton.addEventListener('click', function () {
        if (rows().length >= maxRows) return;
        var row = rows()[0].cloneNode(true);
        clearRow(row);
        rowsHost.appendChild(row);
        renumber();
        row.querySelector('input').focus();
    });

    rowsHost.addEventListener('click', function (event) {
        var button = event.target.closest('[data-contribute-remove]');
        if (!button) return;
        var row = button.closest('[data-contribute-row]');
        if (rows().length > 1) {
            row.parentNode.removeChild(row);
        } else {
            clearRow(row); // Always keep one row to type into.
        }
        renumber();
    });

    renumber();

    // ---- live duplicate check on the name ----

    var nameInput = form.querySelector('[data-contribute-name]');
    var nameStatus = form.querySelector('[data-contribute-name-status]');
    var timer = null;
    var latestRequest = 0;

    function showNameStatus(kind, message, link) {
        nameStatus.className = 'contribute-name-status' + (kind ? ' is-' + kind : '');
        nameStatus.textContent = message;
        if (link) {
            nameStatus.appendChild(document.createTextNode(' '));
            var anchor = document.createElement('a');
            anchor.href = link.href;
            anchor.textContent = link.text;
            nameStatus.appendChild(anchor);
        }
    }

    function checkName() {
        var name = nameInput.value.trim();
        if (name.length < 2) {
            showNameStatus('', '');
            return;
        }

        var request = ++latestRequest;
        fetch('?handler=CheckName&name=' + encodeURIComponent(name), { headers: { Accept: 'application/json' } })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (result) {
                if (!result || request !== latestRequest) return; // An older answer arrived late.
                if (result.status === 'exists') {
                    showNameStatus('taken', 'A page for ' + result.name + ' already exists.',
                        { href: '/Item/' + encodeURIComponent(result.name), text: 'Open it' });
                } else if (result.status === 'waiting') {
                    showNameStatus('taken', result.name + ' was already submitted and is waiting for review.');
                } else {
                    showNameStatus('free', 'No page with this name yet – good to go.');
                }
            })
            .catch(function () {
                showNameStatus('', ''); // The server checks again on preview, so stay quiet.
            });
    }

    nameInput.addEventListener('input', function () {
        window.clearTimeout(timer);
        timer = window.setTimeout(checkName, 350);
    });
    if (nameInput.value.trim()) checkName();

    // ---- after the server answers ----

    var firstError = form.querySelector('.input-validation-error');
    var preview = document.getElementById('preview');
    if (firstError) {
        firstError.focus();
    } else if (preview) {
        preview.scrollIntoView({ block: 'start' });
        preview.focus({ preventScroll: true });
    }
})();
