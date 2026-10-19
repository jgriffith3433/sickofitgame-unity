// Start points for the playtest (AGENTS.md "Playtests", "Start points"; SickOfIt.Playtest PlaytestStart.cs).
// com.hawkes.playtest's bridge passes only seed and speed from hawkesPlaytest.start(id, options) to Unity, so this keeps
// the contract's optional "at" (a floor or level) for the game to read. A page started with ?playtest=<id> takes
// &at= from the URL. Nothing happens unless a playtest scenario starts.
mergeInto(LibraryManager.library, {
    SickOfItPlaytestWatchStart: function () {
        var w = window;
        var pt = w.hawkesPlaytest;
        if (!pt || pt.__sickOfItAt) return;
        var start = pt.start;
        pt.start = function (id, options) {
            var at = options && options.at != null && options.at !== '' ? String(options.at) : null;
            w.__sickOfItPlaytestAt = { id: String(id), at: at };
            return start.apply(this, arguments);
        };
        pt.__sickOfItAt = true;
    },

    SickOfItPlaytestStartAt: function (idPtr) {
        var id = UTF8ToString(idPtr);
        var w = window;
        var at = null;
        var asked = w.__sickOfItPlaytestAt;
        if (asked && asked.id === id) {
            at = asked.at;
        } else {
            try {
                var q = new URLSearchParams(location.search);
                if (q.get('playtest') === id) at = q.get('at');
            } catch (e) {}
        }
        if (at == null || at === '') return null;
        var size = lengthBytesUTF8(at) + 1;
        var buffer = _malloc(size);
        stringToUTF8(at, buffer, size);
        return buffer;
    }
});
