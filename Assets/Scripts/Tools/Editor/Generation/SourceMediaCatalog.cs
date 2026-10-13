#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Tools.Editor.Generation
{
    /// <summary>
    /// The mp4s and stills the game is authored from.
    /// Clips: {root}/{Id}/Output/{Id}.mp4
    /// Stills: {imageRoot}/{Category}/{File}
    /// </summary>
    public sealed class SourceMediaEntry
    {
        public string Group;
        public string Label;
        public string Path;
        public bool Exists;
    }

    public static class SourceMediaCatalog
    {
        public static List<SourceMediaEntry> Collect(GameDefinition gd)
        {
            var entries = new List<SourceMediaEntry>();
            if (gd == null)
                return entries;

            var media = gd.sourceMedia ?? new SourceMediaConfig();
            var art = gd.characterArt ?? new CharacterArtConfig();
            string videos = Root(media.videoRoot, SourceMediaConfigDefaults.VideoRoot);
            string objects = Root(media.objectRoot, SourceMediaConfigDefaults.ObjectRoot);
            string images = Root(media.imageRoot, SourceMediaConfigDefaults.ImageRoot);
            string player = Root(art.playerSourceRoot, CharacterSpriteBaker.PlayerSourceRoot);
            string enemies = Root(art.enemySourceRoot, CharacterSpriteBaker.EnemySourceRoot);

            AddVideos(entries, gd, videos);
            AddObjects(entries, gd, objects);
            AddImages(entries, gd, images);
            AddPlayer(entries, gd, player);
            AddEnemies(entries, gd, enemies);
            return entries;
        }

        public static void CreateFolders(IList<SourceMediaEntry> entries)
        {
            if (entries == null)
                return;
            for (int i = 0; i < entries.Count; i++)
            {
                string path = entries[i].Path;
                if (string.IsNullOrEmpty(path))
                    continue;
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);
            }
        }

        static void AddVideos(List<SourceMediaEntry> entries, GameDefinition gd, string root)
        {
            if (gd.videos != null)
            {
                for (int i = 0; i < gd.videos.Count; i++)
                {
                    var video = gd.videos[i];
                    if (video == null || string.IsNullOrEmpty(video.videoName))
                        continue;
                    AddClip(entries, "Videos", video.videoName, root);
                }
            }

            if (gd.deaths != null)
            {
                for (int i = 0; i < gd.deaths.Count; i++)
                {
                    var death = gd.deaths[i];
                    if (death == null || string.IsNullOrEmpty(death.deathName))
                        continue;
                    AddClip(entries, "Deaths", death.deathName, root);
                }
            }

            if (gd.levels != null)
            {
                for (int i = 0; i < gd.levels.Count; i++)
                {
                    var level = gd.levels[i];
                    if (level == null)
                        continue;
                    int count = level.backgroundLayers != null && level.backgroundLayers.Count > 0
                        ? level.backgroundLayers.Count
                        : level.backgrounds != null ? level.backgrounds.Count : 0;
                    int number = level.levelNumber > 0 ? level.levelNumber : i + 1;
                    for (int layer = 0; layer < count; layer++)
                        AddClip(entries, "Backgrounds", "Level" + number + "_Background" + (layer + 1), root);
                }
            }

            var menu = gd.mainMenuConfig;
            if (menu != null && menu.placedSprites != null)
            {
                for (int i = 0; i < menu.placedSprites.Count; i++)
                {
                    var sprite = menu.placedSprites[i];
                    if (sprite == null || sprite.spriteFramesData == null)
                        continue;
                    string id = MenuClipId(sprite.name);
                    if (!string.IsNullOrEmpty(id))
                        AddClip(entries, "Backgrounds", id, root);
                }
            }
        }

        static void AddObjects(List<SourceMediaEntry> entries, GameDefinition gd, string root)
        {
            if (gd.pickupConfig != null)
            {
                string id = TextureId(gd.pickupConfig.spriteSheet);
                if (string.IsNullOrEmpty(id))
                    id = gd.pickupConfig.displayName;
                if (!string.IsNullOrEmpty(id))
                    AddClip(entries, "Objects", id, root);
            }

            if (gd.projectileConfigs == null)
                return;
            for (int i = 0; i < gd.projectileConfigs.Count; i++)
            {
                var projectile = gd.projectileConfigs[i];
                if (projectile == null)
                    continue;
                string id = TextureId(projectile.spriteSheet);
                if (!string.IsNullOrEmpty(id))
                    AddClip(entries, "Objects", id, root);
            }
        }

        static void AddImages(List<SourceMediaEntry> entries, GameDefinition gd, string root)
        {
            var menu = gd.mainMenuConfig;
            bool hasHouse = menu != null && (
                !string.IsNullOrEmpty(menu.houseSpriteName) ||
                HasPlacedSprite(menu, "MainMenu_LeftPartOfHouse"));
            if (hasHouse)
                AddFile(entries, "Images", "MainMenu/LeftPartOfHouse", Path.Combine(root, "MainMenu", "LeftPartOfHouse.png"));

            AddFile(entries, "Images", "UI/IconRewind", Path.Combine(root, "UI", "IconRewind.png"));
            AddFile(entries, "Images", "UI/IconSkip", Path.Combine(root, "UI", "IconSkip.png"));
        }

        static void AddPlayer(List<SourceMediaEntry> entries, GameDefinition gd, string root)
        {
            var states = gd.player != null && gd.player.animationConfig != null
                ? gd.player.animationConfig.states
                : null;
            if (states == null)
                return;
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (state == null || string.IsNullOrEmpty(state.stateName))
                    continue;
                AddClip(entries, "Player", state.stateName, root);
            }
        }

        static void AddEnemies(List<SourceMediaEntry> entries, GameDefinition gd, string root)
        {
            if (gd.enemyTypes == null)
                return;
            for (int i = 0; i < gd.enemyTypes.Count; i++)
            {
                var enemy = gd.enemyTypes[i];
                if (enemy == null || enemy.animationConfig == null || enemy.animationConfig.states == null)
                    continue;
                string type = enemy.type.ToString();
                for (int s = 0; s < enemy.animationConfig.states.Count; s++)
                {
                    var state = enemy.animationConfig.states[s];
                    if (state == null || string.IsNullOrEmpty(state.stateName))
                        continue;
                    string path = Path.Combine(root, type, state.stateName, "Output", state.stateName + ".mp4");
                    AddFile(entries, "Enemies", type + "/" + state.stateName, path);
                }
            }
        }

        static string MenuClipId(string placedName)
        {
            if (string.IsNullOrEmpty(placedName))
                return null;
            if (placedName == "MainMenu_Background" || placedName == "MainMenu_Background1")
                return "MainMenu_Background";
            if (placedName.Contains("Explode"))
                return "MainMenu_Explode";
            return null;
        }

        static bool HasPlacedSprite(MainMenuConfig menu, string name)
        {
            if (menu.placedSprites == null)
                return false;
            for (int i = 0; i < menu.placedSprites.Count; i++)
            {
                if (menu.placedSprites[i] != null && menu.placedSprites[i].name == name)
                    return true;
            }
            return false;
        }

        static string TextureId(Texture2D texture)
        {
            if (texture == null)
                return null;
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
                return texture.name;
            return Path.GetFileNameWithoutExtension(path);
        }

        static void AddClip(List<SourceMediaEntry> entries, string group, string id, string root)
        {
            AddFile(entries, group, id, Path.Combine(root, id, "Output", id + ".mp4"));
        }

        static void AddFile(List<SourceMediaEntry> entries, string group, string label, string path)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].Path, path, System.StringComparison.OrdinalIgnoreCase))
                    return;
            }

            entries.Add(new SourceMediaEntry
            {
                Group = group,
                Label = label,
                Path = path,
                Exists = File.Exists(path)
            });
        }

        static string Root(string configured, string fallback)
        {
            return string.IsNullOrEmpty(configured) ? fallback : configured;
        }
    }

    static class SourceMediaConfigDefaults
    {
        public const string VideoRoot = @"C:\Projects\SickOfItGameVideos";
        public const string ObjectRoot = @"C:\Projects\SickOfItGameObjects";
        public const string ImageRoot = @"C:\Projects\SickOfItGameImages";
    }
}
#endif
