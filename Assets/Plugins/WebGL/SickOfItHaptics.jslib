// Haptics for Android browsers (navigator.vibrate). iOS Safari has no vibrate; desktop has none worth buzzing.
mergeInto(LibraryManager.library, {
  SickOfItHaptics_Supported: function () {
    try {
      var ua = (navigator.userAgent || "");
      return (/Android/i.test(ua) && typeof navigator.vibrate === "function") ? 1 : 0;
    } catch (e) { return 0; }
  },
  SickOfItHaptics_Vibrate: function (ms) {
    try { if (typeof navigator.vibrate === "function") navigator.vibrate(ms); } catch (e) {}
  }
});
