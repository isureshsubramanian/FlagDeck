// FlagDeck — minimal progressive enhancement. The dashboard is fully usable
// without JavaScript (forms post + redirect); this file only adds nice-to-haves.
(function () {
    'use strict';

    // Submit search on Enter without requiring focus changes; also debounce-submit on typing pause.
    var searchForm = document.querySelector('.fd-search');
    if (searchForm) {
        var input = searchForm.querySelector('input[name="q"]');
        var timer;
        if (input) {
            input.addEventListener('input', function () {
                clearTimeout(timer);
                timer = setTimeout(function () { searchForm.requestSubmit ? searchForm.requestSubmit() : searchForm.submit(); }, 350);
            });
        }
    }

    // Keyboard shortcut: "/" focuses the search box.
    document.addEventListener('keydown', function (e) {
        if (e.key === '/' && document.activeElement && document.activeElement.tagName !== 'INPUT') {
            var box = document.querySelector('.fd-search input');
            if (box) {
                e.preventDefault();
                box.focus();
                box.select();
            }
        }
    });
})();
