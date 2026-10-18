// The playtest's look at the website around the game (the "site" scenario in SickOfIt.Playtest): opens a site page
// in a full-screen frame over the game, so the recording shows it, and reads its Hawkes credit (the
// "Powered by HAWKES GEN" mark the deploy installs, GameDeployWindow step 7c), checks its layout at the frame's size
// (on device "phone", 390 wide: links and buttons overlapping each other or leaving the screen, a page wider than the
// screen) and, on the lore's Descent, the floor counter once scrolled to the bottom. Only the playtest calls these.
mergeInto(LibraryManager.library, {
    // Opens 'path' (relative to the site root, "" = the landing page) and starts checking it. 'expect' (may be
    // empty) is a CSS selector the page should have, e.g. a link to a new post.
    HawkesSiteOpen: function (pathPtr, expectPtr) {
        var path = UTF8ToString(pathPtr);
        var expect = expectPtr ? UTF8ToString(expectPtr) : "";
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
            // Layout at the top of the page, as a visitor first sees it.
            function scrolls(el) {
                for (var p = el.parentElement; p && p !== doc.body; p = p.parentElement) {
                    var o = view.getComputedStyle(p).overflowX;
                    if (o === "auto" || o === "scroll" || o === "hidden") return true;
                }
                return false;
            }
            function label(el) {
                return (el.tagName.toLowerCase() + " \"" + (el.textContent || el.getAttribute("aria-label") || "").replace(/\s+/g, " ").trim().slice(0, 40) + "\"");
            }
            var width = view.innerWidth, items = [], overlaps = [], offscreen = [];
            doc.querySelectorAll('a[href], button, [role="button"], input, select').forEach(function (el) {
                var style = view.getComputedStyle(el);
                if (style.visibility === "hidden" || style.display === "none" || parseFloat(style.opacity || "1") === 0) return;
                var rects = Array.prototype.filter.call(el.getClientRects(), function (r) { return r.width > 1 && r.height > 1; });
                if (!rects.length) return;
                items.push({ el: el, rects: rects });
                if (!scrolls(el) && rects.some(function (r) { return r.right > width + 1 || r.left < -1; }))
                    offscreen.push(label(el));
            });
            for (var i = 0; i < items.length && overlaps.length < 8; i++) {
                for (var j = i + 1; j < items.length && overlaps.length < 8; j++) {
                    var a = items[i], b = items[j];
                    if (a.el.contains(b.el) || b.el.contains(a.el)) continue;
                    var hit = a.rects.some(function (r) {
                        return b.rects.some(function (q) {
                            return Math.min(r.right, q.right) - Math.max(r.left, q.left) > 2 && Math.min(r.bottom, q.bottom) - Math.max(r.top, q.top) > 2;
                        });
                    });
                    if (hit) overlaps.push(label(a.el) + " / " + label(b.el));
                }
            }
            var layout = {
                width: width,
                pageWidth: doc.documentElement.scrollWidth,
                overflowX: doc.documentElement.scrollWidth > width + 1,
                overlaps: overlaps,
                offscreen: offscreen.slice(0, 8)
            };
            var expected = !expect || !!doc.querySelector(expect);

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
            // The Descent counts floors as it scrolls: at the credit (the bottom of the page) it should read 1,000.
            setTimeout(function () {
                var floor = -1;
                try { if (view.hellDescent) floor = view.hellDescent.floor(); } catch (e) { }
                finish({
                    title: doc.title || "",
                    gen: link('a[href^="https://hawkesgen.com"]'),
                    parent: link('a[href^="https://hawkesbiomedical.com"]'),
                    layout: layout,
                    expected: expected,
                    floor: floor
                });
            }, 700);
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
