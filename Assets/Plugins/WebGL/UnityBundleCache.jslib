/**
 * Asks Unity's WebGL cache (Cache Storage + IndexedDB) whether a bundle URL
 * is already stored. Addressables.GetDownloadSizeAsync cannot see that cache.
 */
mergeInto(LibraryManager.library, {
    WebGL_IsBundleCached: function (requestId, urlPtr, callback) {
        var url = UTF8ToString(urlPtr);

        function finish(isCached) {
            {{{ makeDynCall('vii', 'callback') }}}(requestId, isCached);
        }

        try {
            if (!Module.UnityCache || typeof Module.UnityCache.getInstance !== 'function') {
                finish(0);
                return;
            }

            // Same absolute-URL normalization Unity's loader uses as the cache key.
            var link = document.createElement('a');
            link.href = url;

            Module.UnityCache.getInstance().loadRequest(link.href).then(function (entry) {
                var version = Module.productVersion;
                var hit = !!(entry && entry.metaData &&
                    (!version || entry.metaData.version === version));
                finish(hit ? 1 : 0);
            }).catch(function () {
                finish(0);
            });
        } catch (error) {
            finish(0);
        }
    }
});
