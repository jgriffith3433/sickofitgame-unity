// How the page loaded, for the playtest's phone-load scenario (Assets/Scripts/Playtest/SickOfItPlaytest.PhoneLoad.cs):
// seconds since the page opened, the page's loading bar, and what the browser fetched (Resource Timing). Read only.
mergeInto(LibraryManager.library, {
  // Called as the player starts: keeps every fetch in Resource Timing (the browser keeps 250 by default, and a boot fetches more).
  SoiLoadWatch: function () {
    try {
      if (typeof performance !== 'undefined' && performance.setResourceTimingBufferSize) performance.setResourceTimingBufferSize(20000);
    } catch (e) {}
  },

  // Seconds since the page started loading (performance.now()), or -1.
  SoiLoadPageSeconds: function () {
    return (typeof performance !== 'undefined' && performance.now) ? performance.now() / 1000 : -1;
  },

  // JSON: the page's loading bar (which one, its fill, hidden yet) and the files fetched up to untilSeconds since the page
  // opened (0 or less: all so far), with their bytes (transferSize, else encodedBodySize; 0 when the host hides them).
  SoiLoadPageInfo: function (untilSeconds) {
    var info = { bar: '', barWidth: '', barHidden: false, files: 0, sized: 0, bytes: 0, player: 0, playerFiles: 0, bundles: 0, bundleBytes: 0, catalogs: 0, astc: 0 };
    try {
      var bars = [['#bar > i', '#loading'], ['#unity-progress-bar-full', '#unity-loading-bar']];
      for (var i = 0; i < bars.length; i++) {
        var el = document.querySelector(bars[i][0]);
        if (!el) continue;
        var box = document.querySelector(bars[i][1]);
        info.bar = bars[i][0];
        info.barWidth = el.style.width || '';
        info.barHidden = !!box && (box.style.display === 'none' || (box.classList && box.classList.contains('done')));
        break;
      }
      var until = untilSeconds > 0 ? untilSeconds * 1000 : Infinity;
      var entries = (typeof performance !== 'undefined' && performance.getEntriesByType) ? performance.getEntriesByType('resource') : [];
      for (var j = 0; j < entries.length; j++) {
        var e = entries[j];
        if (e.responseEnd > until) continue;
        var size = e.transferSize || e.encodedBodySize || 0;
        var name = String(e.name || '');
        info.files++;
        info.bytes += size;
        if (size > 0) info.sized++;
        if (/\/Build\/[^\/]+\.(unityweb|wasm|data|js|br|gz)(\?|$)/.test(name)) { info.player += size; info.playerFiles++; }
        if (/\.bundle(\?|$)/.test(name)) { info.bundles++; info.bundleBytes += size; if (name.indexOf('/WebGL_ASTC/') >= 0) info.astc++; }
        if (name.indexOf('catalog_') >= 0) info.catalogs++;
      }
    } catch (err) { info.error = String(err); }
    var s = JSON.stringify(info);
    var n = lengthBytesUTF8(s) + 1;
    var buf = _malloc(n);
    stringToUTF8(s, buf, n);
    return buf;
  }
});
