// What Sick Of It adds to Hawkes Gen's Unity analytics bridge (com.hawkes.analytics, which loads the SDK and sends the
// events; see AnalyticsManager.cs): the build the player is on (window.HAWKES_BUILD), the feedback card in the game's
// look (ask() with a theme), closing it when play goes on (it keeps the keyboard while it is open), and a readout of
// the SDK's debug log for the "analytics" playtest. Nothing here throws into the game.
mergeInto(LibraryManager.library, {
    // version, commit, date from the build (Resources/HawkesBuildInfo, written by the build). The page's own
    // HAWKES_BUILD wins. "+v<n>" when the build was loaded from Game/v<n>/, like the bridge's default.
    SickOfItAnalytics_SetBuild: function (versionPtr, commitPtr, datePtr) {
        try {
            if (window.HAWKES_BUILD) return;
            var version = UTF8ToString(versionPtr), commit = UTF8ToString(commitPtr), date = UTF8ToString(datePtr);
            var m = /\/Game\/v(\d+)\//i.exec(location.pathname);
            var scripts = document.querySelectorAll('script[src]');
            for (var i = 0; !m && i < scripts.length; i++) m = /\/Game\/v(\d+)\//i.exec(scripts[i].src);
            if (m) version = (version ? version + '+' : '') + 'v' + m[1];
            var b = { version: version };
            if (commit) b.commit = commit;
            if (date) b.date = date;
            window.HAWKES_BUILD = b;
        } catch (e) {}
    },

    // The feedback card (the SDK's ask) with the game's theme. Queued for the SDK when it hasn't loaded yet.
    SickOfItAnalytics_Ask: function (jsonPtr) {
        try {
            var o = JSON.parse(UTF8ToString(jsonPtr));
            var a = window.hawkesAnalytics;
            if (a && typeof a.ask === 'function') { a.ask(o); return; }
            (window.hawkesAnalyticsQ = window.hawkesAnalyticsQ || []).push(['ask', o]);
        } catch (e) {}
    },

    // 1 while a feedback card is on the page.
    SickOfItAnalytics_AskOpen: function () {
        try { return document.querySelector('[data-hawkes-ask]') ? 1 : 0; } catch (e) { return 0; }
    },

    // Play goes on: an unanswered card goes away and the keyboard goes back to the game.
    SickOfItAnalytics_CloseAsk: function () {
        try {
            var cards = document.querySelectorAll('[data-hawkes-ask]');
            if (!cards.length) return;
            for (var i = 0; i < cards.length; i++) cards[i].remove();
            var canvas = document.querySelector('#unity-canvas') || document.querySelector('canvas');
            if (canvas) { if (!canvas.hasAttribute('tabindex')) canvas.setAttribute('tabindex', '-1'); canvas.focus({ preventScroll: true }); }
        } catch (e) {}
    },

    // The playtest: keep every event on hawkesAnalytics.debugLog from now on (also ?hawkes-analytics-debug=1).
    SickOfItAnalytics_Debug: function () {
        try {
            var a = window.hawkesAnalytics;
            if (a && typeof a.init === 'function') a.init({ debug: true });
            else (window.hawkesAnalyticsQ = window.hawkesAnalyticsQ || []).push(['init', { debug: true }]);
        } catch (e) {}
    },

    // The playtest: what went out, as JSON { sdk, config, events: [{ name, sent, level, props }] } (the last 200).
    SickOfItAnalytics_DebugInfo: function () {
        var text = '';
        try {
            var a = window.hawkesAnalytics, out = { sdk: !!(a && a.config) };
            if (out.sdk) {
                out.config = a.config();
                out.events = (a.debugLog || []).slice(-200).map(function (e) {
                    return { name: e.name, sent: !!e.sent, level: e.ctx && e.ctx.level, props: e.props };
                });
            }
            text = JSON.stringify(out);
        } catch (e) { text = '{"sdk":false}'; }
        var size = lengthBytesUTF8(text) + 1;
        var buffer = _malloc(size);
        stringToUTF8(text, buffer, size);
        return buffer;
    },
});
