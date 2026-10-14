using System;
using UnityEngine;
using SickOfIt.Json;

namespace SickOfIt.Data.Config
{
    /// <summary>
    /// One gameplay content file (an entity, sequence, or status effect), embedded so the game can parse it at runtime.
    /// Authored as Assets/GameConfig/content/{path}; the importer copies it here.
    /// </summary>
    [Serializable]
    public class ContentFile
    {
        [Tooltip("Path under Assets/GameConfig/content, e.g. \"entities/chaser.json\".")]
        public string path;

        [RawJson]
        [Tooltip("The file's JSON, stored compact.")]
        public string json;
    }

    /// <summary>
    /// An asset that gameplay content refers to by id. Built by the config importer from every id the
    /// content uses, so runtime-parsed content can find its sprites, sounds, and frames.
    /// </summary>
    [Serializable]
    public class AssetRegistryEntry
    {
        public string id;
        public UnityEngine.Object asset;
        [Tooltip("For a texture: its sprites in sheet order, for animation.")]
        public Sprite[] sprites;
    }
}
