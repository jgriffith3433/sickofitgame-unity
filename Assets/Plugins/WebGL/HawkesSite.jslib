// The playtest's look at the website around the game (the "site" scenario in SickOfIt.Playtest): opens a site page
// in a full-screen frame over the game, so the recording shows it, and reads its Hawkes credit (the
// "Powered by HAWKES GEN" mark the deploy installs, GameDeployWindow step 7c). Only the playtest calls these.
mergeInto(LibraryManager.library, {
    // Opens 'path' (relative to the site root, "" = the landing page) and starts checking it.
    HawkesSiteOpen: function (pathPtr) {
        var path = UTF8ToString(pathPtr);
        var check = window.hawkesSiteCheck = { path: path, done: false };
        var root = new URL("../", location.href);  // the game is Game/index.html
        var url = new URL(path, root).href;
        check.url = url;

        var frame = document.getElementById("hawkes-site-frame");
        if (!frame) {
            frame = document.createElement("iframe");
            frame.id = "hawkes-site-frame";
            frame.setAttribute("title", "Playtest: site page");
            frame.style.cssText = "position:fixed;left:0;top:0;width:100vw;height:100vh;border:0;background:#000;z-index:100000";
            document.body.appendChild(frame);
        }

        function finish(result) {
            if (window.hawkesSiteCheck !== check || check.done) return;
            for (var key in result) check[key] = result[key];
            check.done = true;
        }

        function read() {
            var doc;
            try { doc = frame.contentDocument; } catch (e) { doc = null; }
            if (!doc || !doc.body) { finish({ error: "the page can't be read" }); return; }
            var view = frame.contentWindow;
            function link(selector) {
                var a = doc.querySelector(selector);
                if (!a) return null;
                var box = a.getBoundingClientRect();
                var style = view.getComputedStyle(a);
                return {
                    href: a.getAttribute("href"),
                    text: (a.textContent || "").replace(/\s+/g, " ").trim(),
                    visible: box.width > 0 && box.height > 0 && style.visibility !== "hidden" && style.display !== "none" && parseFloat(style.opacity || "1") > 0
                };
            }
            var gen = doc.querySelector('a[href^="https://hawkesgen.com"]');
            // Bring the mark into view and ring it for the recording (only in this frame, only now).
            if (gen) {
                try { gen.scrollIntoView({ block: "center" }); } catch (e) { }
                gen.style.outline = "2px solid #2ecc71";
                gen.style.outlineOffset = "4px";
            }
            var parent = doc.querySelector('a[href^="https://hawkesbiomedical.com"]');
            if (parent) {
                parent.style.outline = "2px solid #2ecc71";
                parent.style.outlineOffset = "4px";
            }
            finish({
                title: doc.title || "",
                gen: link('a[href^="https://hawkesgen.com"]'),
                parent: link('a[href^="https://hawkesbiomedical.com"]')
            });
        }

        fetch(url, { cache: "no-store" }).then(function (response) {
            check.status = response.status;
            if (!response.ok) { finish({ error: "HTTP " + response.status }); return; }
            frame.onload = function () { setTimeout(read, 600); };
            frame.src = url;
        }).catch(function (e) {
            finish({ error: "could not load: " + e });
        });
        setTimeout(function () { finish({ error: "timed out" }); }, 15000);
    },

    // The open page's check as JSON once it's done, "" while it's loading.
    HawkesSiteResult: function () {
        var check = window.hawkesSiteCheck;
        var text = check && check.done ? JSON.stringify(check) : "";
        var size = lengthBytesUTF8(text) + 1;
        var buffer = _malloc(size);
        stringToUTF8(text, buffer, size);
        return buffer;
    },

    // Closes the frame: the game is on screen again.
    HawkesSiteClose: function () {
        var frame = document.getElementById("hawkes-site-frame");
        if (frame) frame.parentNode.removeChild(frame);
        window.hawkesSiteCheck = null;
    },

    // How many Hawkes credit links the game page itself has (it should have none: the game screen is unchanged).
    HawkesSiteGamePageCredits: function () {
        return document.querySelectorAll('a[href^="https://hawkesgen.com"], a[href^="https://hawkesbiomedical.com"], .hawkes-credit').length;
    }
});
