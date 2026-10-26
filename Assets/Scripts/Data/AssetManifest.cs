using System;

namespace SickOfIt.Data
{
    /// <summary>
    /// A player build's map of the art and sound taken out of GameDefinition (Tools/Editor PlayerDataSplit) and what
    /// loads each (Core/Addressables GameAssets). Stored as JSON in <see cref="GameDefinition.assetManifest"/>; the editor
    /// and tests never have one, so there every reference stays in place.
    /// </summary>
    [Serializable]
    public class AssetManifest
    {
        public AssetManifestEntry[] entries = new AssetManifestEntry[0];
        public AssetManifestScope[] scopes = new AssetManifestScope[0];
    }

    /// <summary>One reference the build emptied: where it was in GameDefinition and what to load back into it.</summary>
    [Serializable]
    public class AssetManifestEntry
    {
        public const int Texture = 0, Sprite = 1, SpriteArray = 2, Clip = 3;

        /// <summary>Unity's property path in GameDefinition ("levels.Array.data[3].music").</summary>
        public string path = "";
        /// <summary>The Addressables address: the asset's project path.</summary>
        public string address = "";
        public int kind;
        /// <summary>A texture cut into sprites (its sprites are loaded with it, for GameDefinition.SpritesOf).</summary>
        public bool sliced;
        /// <summary><see cref="Sprite"/>: the sprite's name in its sheet.</summary>
        public string sprite = "";
        /// <summary><see cref="SpriteArray"/>: the sprites' names, in the array's order.</summary>
        public string[] sprites = new string[0];
    }

    /// <summary>
    /// What one moment needs: "core" (loaded at boot, kept), "hero", "level/&lt;levelName&gt;", "scene/&lt;id&gt;",
    /// "video/&lt;videoName&gt;", "death/&lt;deathName&gt;", "sound/&lt;id&gt;", "asset/&lt;registry id&gt;", "shaft",
    /// "yooba", "codex". A scope's own entries plus the scopes it includes (a level includes the content art and the sounds
    /// its script and the content it reaches name).
    /// </summary>
    [Serializable]
    public class AssetManifestScope
    {
        public string id = "";
        public int[] entries = new int[0];
        public string[] include = new string[0];
    }
}
