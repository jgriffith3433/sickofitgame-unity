using System.Collections.Generic;
using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// The world of a travel stage: ground that runs along with the camera, platforms streamed in segment by segment
    /// ahead of it and removed behind, passing parallax props and floor signs. Everything here is a pure function of
    /// the camera position and the level seed, so it needs no saved state: a rewind moves the camera back and the
    /// same platforms and props reappear.
    /// </summary>
    public sealed class StageTrack : MonoBehaviour
    {
        const int GroundOrder = -20;
        const float StreamMargin = 2f;

        GameplayWorld world;
        TrackDef track;
        List<PropDef> props;
        FloorsDef floors;
        WorldDef worldDef;
        uint seed;
        bool up;
        Vector2 dir;
        Vector2 origin;
        float halfAlong;
        float halfAcross;

        Transform ground;
        readonly Dictionary<int, GameObject> platforms = new Dictionary<int, GameObject>();
        readonly List<int> scratch = new List<int>();
        readonly Dictionary<long, SpriteRenderer> propInstances = new Dictionary<long, SpriteRenderer>();
        readonly List<long> propScratch = new List<long>();
        readonly HashSet<long> propWanted = new HashSet<long>();
        readonly Stack<SpriteRenderer> propPool = new Stack<SpriteRenderer>();
        readonly Dictionary<int, GameObject> signs = new Dictionary<int, GameObject>();

        public int PlatformCount => platforms.Count;
        public int PropCount => propInstances.Count;
        public int SignCount => signs.Count;

        public static StageTrack Create(GameplayWorld world, LevelScript script, CircleDef circle, Vector2 cameraStart)
        {
            var go = new GameObject("StageTrack");
            go.transform.SetParent(world.transform, false);
            var track = go.AddComponent<StageTrack>();
            track.Init(world, script, circle, cameraStart);
            return track;
        }

        void Init(GameplayWorld owner, LevelScript script, CircleDef circle, Vector2 cameraStart)
        {
            world = owner;
            worldDef = script.world;
            floors = script.floors;
            track = circle != null ? circle.track : new TrackDef();
            props = circle != null ? circle.props : new List<PropDef>();
            seed = world.Seed;
            up = worldDef.travelDirection == TravelDirection.Up;
            dir = up ? Vector2.up : Vector2.right;
            origin = cameraStart;
            var view = world.View;
            halfAlong = (up ? view.height : view.width) * 0.5f;
            halfAcross = (up ? view.width : view.height) * 0.5f;
            BuildGround();
        }

        float Along(Vector2 p) => Vector2.Dot(p - origin, dir);

        Vector2 Point(float along, float across) =>
            up ? new Vector2(origin.x + across, origin.y + along) : new Vector2(origin.x + along, across);

        // =====================================================================
        // GROUND
        // =====================================================================

        void BuildGround()
        {
            // Right: one slab that slides along under the camera, so the ground never ends.
            // Up: a slab at the bottom of the climb only.
            float width = (up ? halfAcross : halfAlong) * 2f + 8f;
            ground = new GameObject("TravelGround").transform;
            ground.SetParent(transform, false);
            ground.position = new Vector3(origin.x, track.groundY, 0f);

            var solid = new GameObject("Surface");
            solid.transform.SetParent(ground, false);
            solid.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            SetLayer(solid, "Platform");
            solid.AddComponent<BoxCollider2D>().size = new Vector2(width, 1f);

            // Dropping through platforms ignores the whole Platform layer; this keeps the player on the ground anyway.
            var floor = new GameObject("Bedrock");
            floor.transform.SetParent(ground, false);
            floor.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            SetLayer(floor, "Boundary");
            floor.AddComponent<BoxCollider2D>().size = new Vector2(width, 1f);

            float depth = halfAcross * 2f + 4f;
            Quad(ground, "Fill", new Vector2(0f, -depth * 0.5f), new Vector2(width, depth), track.groundColor, GroundOrder);
            if (track.groundEdgeColor.a > 0f)
                Quad(ground, "Edge", new Vector2(0f, -0.03f), new Vector2(width, 0.06f), track.groundEdgeColor, GroundOrder + 1);
        }

        static SpriteRenderer Quad(Transform parent, string name, Vector2 local, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = EntityFactory.WhiteSquare;
            sr.color = color;
            SpriteLayers.Apply(sr, SpriteLayer.Player, order);
            go.transform.localPosition = new Vector3(local.x, local.y, go.transform.localPosition.z);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            return sr;
        }

        static void SetLayer(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
                go.layer = layer;
        }

        // =====================================================================
        // STREAMING (simulation: called after the camera moves, and after a rewind)
        // =====================================================================

        /// <summary>Puts the ground under the camera and streams platforms in and out around it.</summary>
        public void Refresh(Vector2 cameraCenter)
        {
            if (ground == null)
                return;
            if (!up)
                ground.position = new Vector3(cameraCenter.x, track.groundY, 0f);

            float segment = Mathf.Max(1f, track.segmentLength);
            float a = Along(cameraCenter);
            int first = Mathf.FloorToInt((a - halfAlong - StreamMargin) / segment);
            int last = Mathf.FloorToInt((a + halfAlong + StreamMargin) / segment);

            scratch.Clear();
            foreach (var pair in platforms)
            {
                if (pair.Key < first || pair.Key > last)
                    scratch.Add(pair.Key);
            }
            foreach (int i in scratch)
            {
                if (platforms[i] != null)
                    Destroy(platforms[i]);
                platforms.Remove(i);
            }
            for (int i = Mathf.Max(0, first); i <= last; i++)
            {
                if (!platforms.ContainsKey(i))
                    platforms[i] = BuildSegment(i, segment);
            }
        }

        /// <summary>A segment's platform, decided by the seed and the segment number alone (null = none).</summary>
        GameObject BuildSegment(int index, float segment)
        {
            var random = DeterministicRandom.Derive(seed, 0x7EA40000u + (uint)index);
            if (up)
            {
                // A ledge every segment, somewhere across the shaft.
                float x = random.Range(-halfAcross + 1.4f, halfAcross - 1.4f);
                float width = random.Range(track.platformWidth.x, track.platformWidth.y);
                return Platform(index, Point((index + 1) * segment + (track.groundY - origin.y), x), width);
            }
            if ((index + 1) * segment <= track.clearStart || random.Value() >= track.platformChance)
                return null;
            float along = (index + 0.2f + 0.6f * random.Value()) * segment;
            float height = random.Range(track.platformHeight.x, track.platformHeight.y);
            float w = random.Range(track.platformWidth.x, track.platformWidth.y);
            return Platform(index, Point(along, track.groundY + height), w);
        }

        GameObject Platform(int index, Vector2 at, float width)
        {
            var definition = GameDefinition.Instance;
            Color color = track.platformColor.a > 0f ? track.platformColor
                : definition != null && definition.presentation != null ? definition.presentation.platformColor : new Color(0.4f, 0.4f, 0.4f, 1f);
            var prefab = definition != null ? definition.platformPrefab : null;
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, new Vector3(at.x, at.y, 0f), Quaternion.identity, transform);
                go.transform.localScale = new Vector3(width, 0.3f, 1f);
                if (go.TryGetComponent<SpriteRenderer>(out var sr))
                    sr.color = color;
                var controller = world.Player != null ? world.Player.GetComponent<SickOfIt.Core.Input.PlayerController>() : null;
                if (controller != null && go.TryGetComponent<SickOfIt.Core.Input.DropThroughPlatform>(out var drop))
                    drop.SetPlayerController(controller);
            }
            else
            {
                go = new GameObject();
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(at.x, at.y, 0f);
                go.transform.localScale = new Vector3(width, 0.3f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = EntityFactory.WhiteSquare;
                sr.color = color;
                SpriteLayers.Apply(sr, SpriteLayer.Player, SpriteLayers.Platform);
                go.AddComponent<BoxCollider2D>();
                SetLayer(go, "Platform");
            }
            go.name = "Platform_" + index;
            return go;
        }

        // =====================================================================
        // SCENERY (cosmetic: every frame)
        // =====================================================================

        void LateUpdate()
        {
            if (world == null)
                return;
            RefreshScenery(world.CameraCenter);
        }

        public void RefreshScenery(Vector2 cameraCenter)
        {
            float a = Along(cameraCenter);
            RefreshProps(a);
            RefreshSigns(a);
        }

        void RefreshProps(float a)
        {
            propScratch.Clear();
            foreach (var key in propInstances.Keys)
                propScratch.Add(key);
            var wanted = propWanted;
            wanted.Clear();

            for (int p = 0; p < props.Count; p++)
            {
                var prop = props[p];
                float spacing = Mathf.Max(0.25f, prop.spacing);
                float reach = halfAlong + Mathf.Max(prop.size.x, prop.size.y) * (1f + prop.sizeVariation) + 1f;
                // Along the track, a prop at slot position u sits at a * parallax + u.
                float shift = a * prop.parallax;
                int first = Mathf.FloorToInt((a - reach - shift) / spacing) - 1;
                int last = Mathf.FloorToInt((a + reach - shift) / spacing) + 1;
                for (int cell = first; cell <= last; cell++)
                {
                    var random = DeterministicRandom.Derive(seed ^ 0x51A7u, (uint)(p * 100003 + cell * 7 + 0x40000000));
                    if (random.Value() >= prop.chance)
                        continue;
                    float u = (cell + 0.5f + random.Range(-0.35f, 0.35f)) * spacing;
                    float along = shift + u;
                    if (Mathf.Abs(along - a) > reach)
                        continue;
                    long key = ((long)p << 32) | (uint)cell;
                    wanted.Add(key);
                    if (!propInstances.TryGetValue(key, out var sr))
                    {
                        sr = TakeProp(prop);
                        float w = prop.size.x * (1f + random.Range(-prop.sizeVariation, prop.sizeVariation));
                        float h = prop.size.y * (1f + random.Range(-prop.sizeVariation, prop.sizeVariation));
                        bool flip = random.Value() < 0.5f;
                        Vector2 spriteSize = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;
                        sr.transform.localScale = new Vector3(w / Mathf.Max(0.01f, spriteSize.x) * (flip ? -1f : 1f), h / Mathf.Max(0.01f, spriteSize.y), 1f);
                        propInstances[key] = sr;
                    }
                    Vector2 at = Point(along, prop.across);
                    sr.transform.position = new Vector3(at.x, at.y, SpriteLayers.Depth(prop.layer));
                }
            }

            foreach (var key in propScratch)
            {
                if (wanted.Contains(key))
                    continue;
                var sr = propInstances[key];
                propInstances.Remove(key);
                if (sr != null)
                {
                    sr.gameObject.SetActive(false);
                    propPool.Push(sr);
                }
            }
        }

        SpriteRenderer TakeProp(PropDef prop)
        {
            SpriteRenderer sr = null;
            while (sr == null && propPool.Count > 0)
                sr = propPool.Pop();
            if (sr == null)
            {
                var go = new GameObject("Prop");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
            }
            sr.gameObject.SetActive(true);
            sr.gameObject.name = string.IsNullOrEmpty(prop.name) ? "Prop" : prop.name;
            sr.sprite = prop.sprite != null ? prop.sprite : PropShapes.Get(prop.shape);
            sr.color = prop.sprite != null && prop.color == new PropDef().color ? Color.white : prop.color;
            SpriteLayers.Apply(sr, prop.layer, prop.sortingOrder);
            return sr;
        }

        void RefreshSigns(float a)
        {
            if (!floors.signs || floors.start <= 0 || floors.unitsPerFloor <= 0f)
                return;
            float u = floors.unitsPerFloor;
            int first = Mathf.Max(1, Mathf.FloorToInt((a - halfAlong - 2f) / u));
            int last = Mathf.FloorToInt((a + halfAlong + 2f + worldDef.travelLead) / u);
            if (floors.end > 0)
                last = Mathf.Min(last, floors.start - floors.end);

            scratch.Clear();
            foreach (var pair in signs)
            {
                if (pair.Key < first || pair.Key > last)
                    scratch.Add(pair.Key);
            }
            foreach (int k in scratch)
            {
                if (signs[k] != null)
                    Destroy(signs[k]);
                signs.Remove(k);
            }
            for (int k = first; k <= last; k++)
            {
                if (!signs.ContainsKey(k))
                    signs[k] = BuildSign(k);
            }
        }

        /// <summary>A post with the floor number, standing where the counter ticks over to it.</summary>
        GameObject BuildSign(int k)
        {
            int floor = floors.start - k;
            // The camera leads the player, so the floor ticks when the player is about 'travelLead' behind this point.
            float along = k * floors.unitsPerFloor - worldDef.travelLead;
            Vector2 at = up ? Point(along, halfAcross - 0.6f) : Point(along, track.groundY);

            var go = new GameObject("FloorSign_" + floor);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var post = Quad(go.transform, "Post", new Vector2(0f, 0.45f), new Vector2(0.09f, 0.9f), new Color(0.1f, 0.06f, 0.06f, 1f), GroundOrder + 2);
            SpriteLayers.Apply(post, SpriteLayer.Background2, 900);
            post.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            var board = Quad(go.transform, "Board", new Vector2(0f, 0.95f), new Vector2(0.78f, 0.36f), new Color(0.16f, 0.07f, 0.05f, 1f), GroundOrder + 3);
            SpriteLayers.Apply(board, SpriteLayer.Background2, 901);
            board.transform.localPosition = new Vector3(0f, 0.95f, 0f);

            var textGo = new GameObject("Number");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            var text = textGo.AddComponent<TMPro.TextMeshPro>();
            var theme = GameDefinition.Instance != null ? GameDefinition.Instance.menuTheme : null;
            text.font = SickOfIt.Core.UI.Menus.MenuGraphics.FontAsset(theme != null ? theme.font : null);
            text.text = floor.ToString();
            text.alignment = TMPro.TextAlignmentOptions.Center;
            text.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            text.fontSize = 2.4f;
            text.fontStyle = TMPro.FontStyles.Bold;
            text.color = track.groundEdgeColor.a > 0f ? Color.Lerp(track.groundEdgeColor, Color.white, 0.35f) : new Color(1f, 0.8f, 0.5f, 1f);
            text.rectTransform.sizeDelta = new Vector2(0.78f, 0.36f);
            text.sortingLayerID = SortingLayer.NameToID(SpriteLayer.Background2.ToString());
            text.sortingOrder = 902;
            return go;
        }

        /// <summary>Built-in silhouettes for props without art (white, tinted by the prop color, pivot at the base).</summary>
        static class PropShapes
        {
            static readonly Dictionary<PropShape, Sprite> cache = new Dictionary<PropShape, Sprite>();

            public static Sprite Get(PropShape shape)
            {
                if (cache.TryGetValue(shape, out var sprite) && sprite != null)
                    return sprite;
                const int w = 64, h = 128;
                var tex = new Texture2D(w, h) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var pixels = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float fx = (x + 0.5f) / w * 2f - 1f;   // -1..1 across
                        float fy = (y + 0.5f) / h;            // 0 at the base, 1 at the top
                        pixels[y * w + x] = new Color(1f, 1f, 1f, Alpha(shape, fx, fy));
                    }
                }
                tex.SetPixels(pixels);
                tex.Apply();
                sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
                sprite.hideFlags = HideFlags.DontSave;
                // One unit wide, two tall at this pixels-per-unit; the prop's size scales it.
                cache[shape] = sprite;
                return sprite;
            }

            static float Alpha(PropShape shape, float fx, float fy)
            {
                switch (shape)
                {
                    case PropShape.Spire:
                    {
                        // A jagged spike: narrows toward the top with a little wobble on the edges.
                        float half = (1f - fy) * (0.85f + 0.12f * Mathf.Sin(fy * 23f) + 0.06f * Mathf.Sin(fy * 57f));
                        return Edge(half - Mathf.Abs(fx));
                    }
                    case PropShape.Rock:
                    {
                        float top = 0.45f + 0.06f * Mathf.Sin(fx * 7f) + 0.04f * Mathf.Sin(fx * 13f + 1f);
                        float r = Mathf.Sqrt(fx * fx + (fy / top) * (fy / top));
                        return Edge((1f - r) * 0.5f);
                    }
                    case PropShape.Glow:
                    {
                        float r = Mathf.Sqrt(fx * fx + ((fy - 0.1f) * 6f) * ((fy - 0.1f) * 6f));
                        return Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r);
                    }
                    default:
                        return 1f;
                }
            }

            static float Edge(float distance) => Mathf.Clamp01(distance * 40f);
        }
    }
}
