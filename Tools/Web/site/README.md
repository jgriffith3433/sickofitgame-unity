# Website content owned by sickofitgame-unity

Copied over the website (`sickofitgamewebsite`) at the same paths by the deploy, step 7d (`SiteContent` in
`Assets/Scripts/Tools/Editor/`), before step 7c adds the "Powered by HAWKES GEN" credit. Not copied: this file,
`site.json` and `_inserts/`.

- `blog/lore.html`: the Descent, the lore hub (scroll down into Hell, floor counter 0 to 1,000).
- `blog/lore/**`: the lore pages, their shared `assets/descent.css`/`descent.js` and `img/` (from `Tools/Web/site-images.sh`).
- `blog/posts/the-engine.html`: the dev post about the engine.
- `site.json` + `_inserts/`: snippets put into website pages this repo doesn't own (marked `<!-- Hawkes site: id -->`).

Story first (`Docs/Story.md`), then the page. Check: `node Tools/Web/site-check.mjs <website checkout>`.
