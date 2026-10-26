// The page's memory, for the memory readout (Core/Addressables/MemoryReport): the WebAssembly heap (it only grows; a
// phone's browser kills the page past its limit) and, where the browser tells (Chrome), the JavaScript heap, which holds
// the decoded audio. Megabytes; -1 when unknown.
mergeInto(LibraryManager.library, {
    SoiMemoryHeapMB: function () {
        try { return Math.round(HEAPU8.length / 1048576); } catch (e) { return -1; }
    },

    SoiMemoryJsMB: function () {
        try {
            var m = window.performance && window.performance.memory;
            return m ? Math.round(m.usedJSHeapSize / 1048576) : -1;
        } catch (e) { return -1; }
    },
});
