using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Data.Config;
using SickOfIt.VideoFrameExtractor.Runtime;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Builds the sprites listed on a level or the main menu.
    /// </summary>
    public static class PlacedSpriteBuilder
    {
        public static void SpawnAll(Transform parent, List<PlacedSpriteConfig> sprites)
        {
            if (sprites == null)
                return;

            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] != null)
                    Spawn(parent, sprites[i]);
            }
        }

        /// <summary>
        /// Moves scene objects that already have the config name. Spawns the rest when allowed.
        /// </summary>
        public static void Bind(List<PlacedSpriteConfig> sprites)
        {
            if (sprites == null)
                return;

            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sprites.Count; i++)
            {
                var cfg = sprites[i];
                if (cfg == null || string.IsNullOrEmpty(cfg.name))
                    continue;

                Transform existing = null;
                for (int t = 0; t < transforms.Length; t++)
                {
                    if (transforms[t] != null && transforms[t].name == cfg.name)
                    {
                        existing = transforms[t];
                        break;
                    }
                }

                if (existing != null)
                    Apply(existing.gameObject, cfg, replaceSprite: cfg.sprite != null && existing.GetComponent<SpriteFramePlayer>() == null);
                else if (cfg.spawnIfMissing)
                    Spawn(null, cfg);
            }
        }

        public static GameObject Spawn(Transform parent, PlacedSpriteConfig cfg)
        {
            var go = new GameObject(string.IsNullOrEmpty(cfg.name) ? "Sprite" : cfg.name);
            if (parent != null)
                go.transform.SetParent(parent, false);

            go.AddComponent<SpriteRenderer>();
            Apply(go, cfg, replaceSprite: true);

            var frames = cfg.spriteFramesData as SpriteFramesData;
            if (frames != null)
            {
                var player = go.AddComponent<SpriteFramePlayer>();
                player.Bind(frames, cfg.useGameClock);
            }

            if (!cfg.startActive)
                go.SetActive(false);

            return go;
        }

        static void Apply(GameObject go, PlacedSpriteConfig cfg, bool replaceSprite)
        {
            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = go.AddComponent<SpriteRenderer>();

            SpriteLayers.Apply(renderer, cfg.layer, cfg.sortingOrder);
            renderer.flipX = cfg.flipX;
            if (replaceSprite && cfg.sprite != null)
                renderer.sprite = cfg.sprite;

            float scaleX = Mathf.Approximately(cfg.scale.x, 0f) ? 1f : cfg.scale.x;
            float scaleY = Mathf.Approximately(cfg.scale.y, 0f) ? 1f : cfg.scale.y;
            var transform = go.transform;
            transform.localScale = new Vector3(scaleX, scaleY, 1f);
            transform.localPosition = new Vector3(cfg.position.x, cfg.position.y, SpriteLayers.Depth(cfg.layer));
        }
    }
}
