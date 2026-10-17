// Bridge to the game page's tap-to-play sound prompt (Tools/Web/hawkes-sound.js, installed by the deploy).
// Used by the playtest bot (SickOfIt.Playtest.PageSound); harmless on a page without the prompt.
mergeInto(LibraryManager.library, {
    HawkesSoundTap: function () {
        return window.hawkesSound && window.hawkesSound.tap() ? 1 : 0;
    },

    HawkesSoundState: function () {
        var state = window.hawkesSound ? window.hawkesSound.state() : "";
        var size = lengthBytesUTF8(state) + 1;
        var buffer = _malloc(size);
        stringToUTF8(state, buffer, size);
        return buffer;
    }
});
