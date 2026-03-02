/**
 * Unity WebGL JavaScript Plugin for Umami Analytics
 * This file bridges Unity C# code with the website's JavaScript analytics functions.
 * 
 * Uses a single generic trackEvent function that dynamically calls window[functionName]().
 * Backward compatible — still calls the same window.trackLevel1Start(), etc.
 * Also falls back to window.trackEvent(name) if the specific function is not defined.
 * 
 * For new levels/videos, just define the corresponding window.trackLevelNStart() on the website,
 * or use the generic window.trackEvent(name) handler.
 */

mergeInto(LibraryManager.library, {

    // ============================================================
    // Generic Event Tracking — replaces all per-level/video functions
    // ============================================================

    trackEvent: function(eventNamePtr) {
        var eventName = UTF8ToString(eventNamePtr);

        // Try the specific window function first (e.g. window.trackLevel1Start)
        if (typeof window[eventName] === 'function') {
            window[eventName]();
        }
        // Fall back to a generic handler if the website provides one
        else if (typeof window.trackEvent === 'function') {
            window.trackEvent(eventName);
        }
        else {
            console.warn('[Unity Analytics] window.' + eventName + ' is not defined');
        }
    },

    // ============================================================
    // Event Tracking with a string parameter (e.g. death cause)
    // ============================================================

    trackEventWithData: function(eventNamePtr, dataPtr) {
        var eventName = UTF8ToString(eventNamePtr);
        var data = UTF8ToString(dataPtr);

        if (typeof window[eventName] === 'function') {
            window[eventName](data);
        }
        else if (typeof window.trackEventWithData === 'function') {
            window.trackEventWithData(eventName, data);
        }
        else {
            console.warn('[Unity Analytics] window.' + eventName + ' is not defined');
        }
    }

});
