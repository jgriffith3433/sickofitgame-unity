mergeInto(LibraryManager.library, {
    IsMobileBrowser: function() {
        // Enhanced mobile detection that includes iPads with iPadOS 13+
        var isMobile = /iPhone|iPad|iPod|Android/i.test(navigator.userAgent) ||
                       (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1) || // iPad with iPadOS 13+
                       ('ontouchstart' in window); // Any touch-capable device
        
        return isMobile;
    },

    // TouchDevice.IsPrimary: the main pointer is a finger. Same rule as the game page's guide (?touch=1/0 force it; a phone,
    // an iPad that says it is a Mac, or a coarse main pointer). Unlike IsMobileBrowser, a touch laptop with a mouse is not one.
    SickOfItTouchPrimary: function() {
        try {
            var q = location.search || '';
            if (/[?&]touch=1/.test(q)) return 1;
            if (/[?&]touch=0/.test(q)) return 0;
            if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) return 1;
            if (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1) return 1;
            return (window.matchMedia && window.matchMedia('(pointer: coarse)').matches) ? 1 : 0;
        } catch (e) {
            return 0;
        }
    }
});
