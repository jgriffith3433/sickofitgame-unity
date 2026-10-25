using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SickOfIt.Core.Input
{
    /// <summary>One coloured block of a held weapon's prop, in aim space: x runs along the aim from the hero's shoulder, y across it.</summary>
    public struct HeldPart
    {
        public Vector2 offset;
        public Vector2 size;
        public float angle;
        public Color color;
    }

    /// <summary>What a weapon looks like in his hands (content/weapons <c>look</c>): the prop's blocks and the flash of a shot or swing.</summary>
    public sealed class HeldLook
    {
        public HeldPart[] parts = new HeldPart[0];
        /// <summary>Where the shoulder is, from the hero's feet (mirrored with his facing).</summary>
        public Vector2 shoulder = new Vector2(0.02f, 0.22f);
        /// <summary>Where a shot leaves or a swing lands, in aim space.</summary>
        public Vector2 muzzle = new Vector2(0.45f, 0f);
        public Color flash = new Color(1f, 0.85f, 0.45f, 1f);
        public Vector2 flashSize = new Vector2(0.18f, 0.18f);
        /// <summary>How far the prop kicks back (a gun) or lunges forward (a swing) when used.</summary>
        public float kick = 0.08f;
        /// <summary>A signature of the shape and colours, so a playtest can tell two props apart without reading pixels.</summary>
        public string Signature
        {
            get
            {
                if (signature == null)
                {
                    var sb = new StringBuilder();
                    foreach (var p in parts)
                        sb.Append(p.offset.x.ToString("0.00")).Append(',').Append(p.offset.y.ToString("0.00")).Append(',')
                          .Append(p.size.x.ToString("0.00")).Append('x').Append(p.size.y.ToString("0.00")).Append('@').Append(p.angle.ToString("0"))
                          .Append('#').Append(ColorUtility.ToHtmlStringRGB(p.color)).Append(';');
                    sb.Append("flash#").Append(ColorUtility.ToHtmlStringRGB(flash)).Append(flashSize.x.ToString("0.00")).Append('x').Append(flashSize.y.ToString("0.00"));
                    signature = sb.ToString();
                }
                return signature;
            }
        }
        string signature;
    }

    /// <summary>
    /// The prop in the hero's hands: a few coloured blocks per weapon (weapon JSON <c>look</c>; a stand-in until the weapon has real art)
    /// drawn over the hero's own sprite, turned to his aim, kicking back with a flash when he fires or swings. The sheets stay one set:
    /// the weapon is this overlay, not seven sheet sets. Pure presentation; it only reads the controller and its armory.
    /// </summary>
    public sealed class HeldWeaponProp : MonoBehaviour
    {
        const float KickSeconds = 0.14f;
        static Sprite square;

        PlayerController controller;
        SpriteRenderer heroRenderer;
        readonly List<SpriteRenderer> blocks = new List<SpriteRenderer>();
        SpriteRenderer flashBlock;
        string builtFor;
        HeldLook look;
        int seenVolleys = -1;
        int seenSwings;
        float usedAt = -99f;

        /// <summary>The weapon whose prop is on screen now, or null when none is shown (a scene clip, a swap, a hurt flinch, the ladder).</summary>
        public string Shown { get; private set; }
        /// <summary>The shown prop's shape and colours (<see cref="HeldLook.Signature"/>).</summary>
        public string Signature => look != null && Shown != null ? look.Signature : null;
        /// <summary>Seconds since the weapon was last fired or swung (the flash is on while this is under <see cref="KickSeconds"/>).</summary>
        public float UsedAge => Time.time - usedAt;
        public bool FlashOn => Shown != null && UsedAge < KickSeconds;
        /// <summary>Shots and swings the prop has reacted to.</summary>
        public int Uses { get; private set; }

        public static HeldWeaponProp For(PlayerController pc)
        {
            if (pc == null)
                return null;
            return pc.TryGetComponent(out HeldWeaponProp prop) ? prop : null;
        }

        static Sprite Square()
        {
            if (square == null)
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "HeldPropSquare" };
                var white = new Color32[16];
                for (int i = 0; i < white.Length; i++)
                    white[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(white);
                tex.Apply(false, true);
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                square.name = "HeldPropSquare";
            }
            return square;
        }

        void Awake()
        {
            controller = GetComponent<PlayerController>();
        }

        void LateUpdate()
        {
            if (controller == null)
                return;
            string id = null;
            HeldLook wanted = null;
            if (controller.Armory != null && controller.Armory.TryGetCurrent(out var stats) && stats.look != null && stats.look.parts.Length > 0
                && controller.PropVisible)
            {
                id = stats.id;
                wanted = stats.look;
            }
            if (id == null)
            {
                Hide();
                return;
            }
            if (heroRenderer == null)
                heroRenderer = controller.GetComponentInChildren<SpriteRenderer>();
            if (builtFor != id || look != wanted)
                Build(id, wanted);
            Shown = id;
            React();
            Place();
        }

        void Hide()
        {
            Shown = null;
            for (int i = 0; i < blocks.Count; i++)
                if (blocks[i] != null && blocks[i].enabled)
                    blocks[i].enabled = false;
            if (flashBlock != null)
                flashBlock.enabled = false;
        }

        void Build(string id, HeldLook wanted)
        {
            builtFor = id;
            look = wanted;
            for (int i = 0; i < blocks.Count; i++)
                if (blocks[i] != null)
                    Destroy(blocks[i].gameObject);
            blocks.Clear();
            if (flashBlock != null)
                Destroy(flashBlock.gameObject);
            for (int i = 0; i < wanted.parts.Length; i++)
                blocks.Add(NewBlock("Prop" + i, wanted.parts[i].color));
            flashBlock = NewBlock("Flash", wanted.flash);
            flashBlock.enabled = false;
        }

        SpriteRenderer NewBlock(string blockName, Color color)
        {
            var go = new GameObject(blockName);
            go.transform.SetParent(null, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Square();
            sr.color = color;
            if (heroRenderer != null)
            {
                sr.sortingLayerID = heroRenderer.sortingLayerID;
                sr.sortingOrder = heroRenderer.sortingOrder + 2;
            }
            // Lives in the hero's scene so it unloads with him.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            return sr;
        }

        void React()
        {
            int volleys = controller.VolleysFired;
            int swings = controller.Armory != null && controller.Armory.Swinging(GameClock.Instance != null ? GameClock.Instance.Time : Time.time) ? 1 : 0;
            bool fired = seenVolleys >= 0 && volleys != seenVolleys;
            bool swung = swings == 1 && seenSwings == 0;
            seenVolleys = volleys;
            seenSwings = swings;
            if (fired || swung)
            {
                usedAt = Time.time;
                Uses++;
            }
        }

        void Place()
        {
            Vector2 aim = controller.AimDirection;
            float side = aim.x < 0f ? -1f : 1f;
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            var rot = Quaternion.Euler(0f, 0f, angle);
            float age = Time.time - usedAt;
            float used = age < KickSeconds ? 1f - age / KickSeconds : 0f;
            // A gun kicks back along the aim; a swing (fists) lunges forward.
            bool melee = controller.Armory != null && controller.Armory.TryGetCurrent(out var s) && s.melee;
            float push = (melee ? 1f : -1f) * look.kick * used;
            Vector3 origin = transform.position + new Vector3(look.shoulder.x * (controller.FacingRight ? 1f : -1f), look.shoulder.y, 0f);
            Vector3 along = rot * Vector3.right * push;
            for (int i = 0; i < blocks.Count; i++)
            {
                var p = look.parts[i];
                // Aim space -> world; below the horizontal on the left the y is mirrored so a prop is never upside down.
                Vector2 local = new Vector2(p.offset.x, p.offset.y * side);
                var t = blocks[i].transform;
                t.position = origin + along + rot * local;
                t.rotation = Quaternion.Euler(0f, 0f, angle + p.angle * side);
                t.localScale = new Vector3(p.size.x, p.size.y, 1f);
                blocks[i].enabled = true;
            }
            if (flashBlock != null)
            {
                bool on = used > 0f;
                flashBlock.enabled = on;
                if (on)
                {
                    var t = flashBlock.transform;
                    t.position = origin + rot * new Vector3(look.muzzle.x, look.muzzle.y * side, 0f);
                    t.rotation = rot;
                    t.localScale = new Vector3(look.flashSize.x * (0.6f + 0.6f * used), look.flashSize.y * (0.6f + 0.6f * used), 1f);
                    var c = look.flash;
                    c.a = Mathf.Clamp01(used * 1.4f);
                    flashBlock.color = c;
                }
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < blocks.Count; i++)
                if (blocks[i] != null)
                    Destroy(blocks[i].gameObject);
            if (flashBlock != null)
                Destroy(flashBlock.gameObject);
        }

        void OnDisable()
        {
            Hide();
        }
    }
}
