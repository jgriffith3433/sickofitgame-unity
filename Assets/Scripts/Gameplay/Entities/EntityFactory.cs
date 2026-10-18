using UnityEngine;
using SickOfIt.Data;
using SickOfIt.Gameplay.Content;

namespace SickOfIt.Gameplay
{
    /// <summary>A two-sprite world-space health bar that keeps its world size whatever the entity's scale.</summary>
    public sealed class EntityHealthBar : MonoBehaviour
    {
        [SerializeField] SpriteRenderer outline;
        [SerializeField] SpriteRenderer track;
        [SerializeField] SpriteRenderer fill;
        [SerializeField] Vector2 worldOffset;
        [SerializeField] Vector2 worldSize;
        [SerializeField] bool aboveBody;
        Collider2D body;

        public void Init(SpriteRenderer outlineRenderer, SpriteRenderer trackRenderer, SpriteRenderer fillRenderer, Vector2 offset, Vector2 size, bool overBody)
        {
            aboveBody = overBody;
            outline = outlineRenderer;
            track = trackRenderer;
            fill = fillRenderer;
            worldOffset = offset;
            worldSize = size;
        }

        public void Show(float fraction, float parentScale)
        {
            float s = Mathf.Approximately(parentScale, 0f) ? 1f : 1f / parentScale;
            Vector2 offset = worldOffset;
            if (aboveBody && transform.parent != null)
            {
                if (body == null)
                    body = transform.parent.GetComponent<Collider2D>();
                if (body != null && body.enabled)
                    offset.y += body.bounds.max.y - transform.parent.position.y + worldSize.y;
            }
            transform.localPosition = new Vector3(offset.x * s, offset.y * s, 0f);
            transform.localRotation = Quaternion.Inverse(transform.parent != null ? transform.parent.localRotation : Quaternion.identity);
            transform.localScale = new Vector3(s, s, 1f);
            fraction = Mathf.Clamp01(fraction);
            if (outline != null)
            {
                // A dark edge like the HUD bars: as thick as a third of the bar, never thinner than a hairline.
                float edge = Mathf.Max(0.012f, worldSize.y * 0.35f);
                outline.transform.localScale = new Vector3(worldSize.x + edge * 2f, worldSize.y + edge * 2f, 1f);
            }
            if (track != null)
                track.transform.localScale = new Vector3(worldSize.x, worldSize.y, 1f);
            if (fill != null)
            {
                fill.transform.localScale = new Vector3(worldSize.x * fraction, worldSize.y, 1f);
                fill.transform.localPosition = new Vector3(-worldSize.x * (1f - fraction) * 0.5f, 0f, 0f);
            }
            // Only hurt enemies show a bar, so a full screen stays readable.
            gameObject.SetActive(fraction > 0f && fraction < 0.999f);
        }
    }

    /// <summary>
    /// Builds the inactive template GameObject for an entity type; pools copy it. Everything is made in code,
    /// so a new entity defined in JSON works without running Generate Project.
    /// </summary>
    public static class EntityFactory
    {
        static Sprite whiteSquare;
        static Sprite circle;

        public static Sprite WhiteSquare
        {
            get
            {
                if (whiteSquare == null)
                {
                    var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
                    var pixels = new Color[16];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                    tex.SetPixels(pixels);
                    tex.Apply();
                    whiteSquare = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                    whiteSquare.hideFlags = HideFlags.DontSave;
                }
                return whiteSquare;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null)
                {
                    const int size = 64;
                    var tex = new Texture2D(size, size) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
                    var pixels = new Color[size * size];
                    float r = size * 0.5f;
                    for (int y = 0; y < size; y++)
                    {
                        for (int x = 0; x < size; x++)
                        {
                            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                            float a = Mathf.Clamp01(r - d);
                            float edge = d > r - 3f ? 0.65f : 1f;
                            pixels[y * size + x] = new Color(edge, edge, edge, a);
                        }
                    }
                    tex.SetPixels(pixels);
                    tex.Apply();
                    circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                    circle.hideFlags = HideFlags.DontSave;
                }
                return circle;
            }
        }

        public static GameObject BuildTemplate(EntityDef def, GameContent content, Transform parent)
        {
            var go = new GameObject("E_" + def.id);
            go.SetActive(false);
            go.transform.SetParent(parent, false);

            bool shootable = def.team == Team.Enemy && def.health > 0 && def.shootable;
            if (shootable)
            {
                go.tag = "Enemy";
                int enemyLayer = LayerMask.NameToLayer("Enemy");
                if (enemyLayer >= 0)
                    go.layer = enemyLayer;
            }

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = def.body.kind == BodyKind.Dynamic ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            body.gravityScale = def.body.kind == BodyKind.Dynamic ? def.body.gravity : 0f;
            body.useFullKinematicContacts = true;
            body.constraints = def.body.freezeRotation && !def.body.rotateToVelocity ? RigidbodyConstraints2D.FreezeRotation : RigidbodyConstraints2D.None;
            body.interpolation = RigidbodyInterpolation2D.None;

            Collider2D collider;
            if (def.body.shape == ShapeKind.Box)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = def.body.size;
                box.offset = def.body.offset;
                collider = box;
            }
            else
            {
                var c = go.AddComponent<CircleCollider2D>();
                c.radius = def.body.radius;
                c.offset = def.body.offset;
                collider = c;
            }
            collider.isTrigger = !def.body.solid;

            var sprite = go.AddComponent<SpriteRenderer>();
            SpriteLayers.Apply(sprite, def.visual.layer, def.visual.SortingOrder);
            sprite.color = def.visual.color;

            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            go.AddComponent<SimHistory>();
            go.AddComponent<Entity>();

            Sprite first = def.visual.sprite;
            if (first == null && !def.visual.animator.IsEmpty)
            {
                var probe = new EntityAnimation(def.visual.animator, content.Sheets, null, null);
                first = probe.FirstSprite;
            }
            if (first != null)
                sprite.sprite = first;
            else
                AddPlaceholder(go, def, sprite);

            if (def.health > 0 && def.healthBar.show)
                AddHealthBar(go, def);

            return go;
        }

        static void AddPlaceholder(GameObject go, EntityDef def, SpriteRenderer rootSprite)
        {
            if (def.visual.placeholderShape == PlaceholderShape.Box)
            {
                // A plain box the size of the body (floor pieces): named Shape, not Placeholder, since it's their intended look until art.
                var box = new GameObject("Shape");
                box.transform.SetParent(go.transform, false);
                Vector2 boxSize = def.body.shape == ShapeKind.Box ? def.body.size : Vector2.one * def.body.radius * 2f;
                box.transform.localPosition = new Vector3(def.body.offset.x, def.body.offset.y, 0f);
                box.transform.localScale = new Vector3(boxSize.x, boxSize.y, 1f);
                var boxSprite = box.AddComponent<SpriteRenderer>();
                boxSprite.sprite = WhiteSquare;
                boxSprite.color = def.visual.placeholderColor;
                SpriteLayers.Apply(boxSprite, def.visual.layer, def.visual.SortingOrder);
                return;
            }

            // Root sprite stays empty; the placeholder child keeps a fixed world size.
            float scale = Mathf.Max(0.0001f, def.visual.scale);
            var child = new GameObject("Placeholder");
            child.transform.SetParent(go.transform, false);
            float worldSize = def.visual.placeholderSize > 0f ? def.visual.placeholderSize : 0.6f;
            child.transform.localScale = Vector3.one * (worldSize / scale);
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = Circle;
            sr.color = def.visual.placeholderColor;
            SpriteLayers.Apply(sr, def.visual.layer, def.visual.SortingOrder);

            if (!def.visual.placeholderLabel)
                return;
            var label = new GameObject("Label");
            label.transform.SetParent(child.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            var text = label.AddComponent<TextMesh>();
            text.text = def.id;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.035f;
            text.color = Color.white;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                text.font = font;
                var renderer = label.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = font.material;
                renderer.sortingLayerName = def.visual.layer.ToString();
                renderer.sortingOrder = def.visual.SortingOrder + 1;
            }
        }

        static void AddHealthBar(GameObject go, EntityDef def)
        {
            var bar = new GameObject("HealthBar");
            bar.transform.SetParent(go.transform, false);

            var hud = SickOfIt.Data.GameDefinition.Instance != null ? SickOfIt.Data.GameDefinition.Instance.menuTheme.hud : null;
            var outlineGo = new GameObject("Outline");
            outlineGo.transform.SetParent(bar.transform, false);
            var outline = outlineGo.AddComponent<SpriteRenderer>();
            outline.sprite = WhiteSquare;
            outline.color = hud != null ? hud.worldBarOutline : new Color(0.12f, 0.07f, 0.13f, 1f);
            SpriteLayers.Apply(outline, def.visual.layer, SpriteLayers.HealthBar - 1);

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(bar.transform, false);
            var track = trackGo.AddComponent<SpriteRenderer>();
            track.sprite = WhiteSquare;
            track.color = def.healthBar.trackColor;
            SpriteLayers.Apply(track, def.visual.layer, SpriteLayers.HealthBar);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(bar.transform, false);
            var fill = fillGo.AddComponent<SpriteRenderer>();
            fill.sprite = WhiteSquare;
            fill.color = def.healthBar.color;
            SpriteLayers.Apply(fill, def.visual.layer, SpriteLayers.HealthBar + 1);

            var healthBar = bar.AddComponent<EntityHealthBar>();
            healthBar.Init(outline, track, fill, def.healthBar.offset, new Vector2(def.healthBar.width, def.healthBar.height), def.healthBar.aboveBody);
        }
    }
}
