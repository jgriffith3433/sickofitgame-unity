using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.Input;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.UI.Portraits
{
    /// <summary>
    /// The speaker's painted portrait (Docs/Story.md "Portraits", ui/portraits.json): whenever a voiced line of the hero, the Overlord,
    /// Yooba, Rook or Wick plays, wherever it is (a beat, the Shaft, a death, the shop, a level), the character's card slides in at a
    /// screen edge in 0.12 s, shows the line's mood, flaps its mouth with the clip's loudness and, a moment after the line ends, slides
    /// away. It sits at mid-height on the left (the right when the hero is there), clear of the top HUD rows, the touch stick and the
    /// bottom buttons, and is never as small as 72 px. A character with no painting yet is a drawn stand-in bust. Own overlay canvas,
    /// unscaled time, never takes a press. Presentation only.
    /// </summary>
    public sealed class SpeakerPortraits : MonoBehaviour
    {
        /// <summary>Playtest and log: (kind, detail).</summary>
        public static event Action<string, string> Reported;

        static SpeakerPortraits instance;
        static readonly Dictionary<string, int> claims = new Dictionary<string, int>();

        /// <summary>A widget that already shows this character's portrait (Yooba's shop frame) takes the overlay's place while it is open.</summary>
        public static void Claim(string characterId) => claims[characterId] = (claims.TryGetValue(characterId, out var n) ? n : 0) + 1;
        public static void Release(string characterId)
        {
            if (claims.TryGetValue(characterId, out var n))
                claims[characterId] = Mathf.Max(0, n - 1);
        }
        static bool Claimed(string id) => claims.TryGetValue(id, out var n) && n > 0;

        public static SpeakerPortraits Instance => instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Hook()
        {
            instance = null;
            claims.Clear();
            PortraitVoice.Started -= OnLine;
            PortraitVoice.Started += OnLine;
        }

        static void OnLine(SoundDef sound)
        {
            var character = PortraitLibrary.ForVoice(sound.voice);
            if (character == null)
                return;
            if (Claimed(character.id))
            {
                if (instance != null)
                    instance.Dismiss();
                Report("claimed", $"{character.id}: shown by its own frame");
                return;
            }
            if (instance == null)
                instance = Create();
            instance.Show(character, sound);
        }

        // ─── What the playtest reads ──────────────────────────────────

        /// <summary>The card is on screen (sliding in, held or sliding out).</summary>
        public bool Visible => state != State.Hidden;
        public string CharacterId => character != null ? character.id : "";
        public string Mood => mood;
        public bool MouthIsOpen { get; private set; }
        /// <summary>The painting on screen is a stand-in drawn from shapes.</summary>
        public bool StandIn { get; private set; }
        public string SpriteName => art != null && art.sprite != null ? art.sprite.name : "";
        /// <summary>Seconds from the line's start to the card at its place (0.12 by config).</summary>
        public float SlideInSeconds { get; private set; }
        public int Shown { get; private set; }
        /// <summary>The line this card shows.</summary>
        public string Sound => lineId;
        /// <summary>The card's rectangle in screen pixels (y up).</summary>
        public Rect ScreenRect => card != null ? new Rect(cardMin, card.sizeDelta) : Rect.zero;

        enum State { Hidden, In, Held, Out }
        State state = State.Hidden;
        PortraitCharacter character;
        string mood = PortraitMood.Neutral, lineId = "";
        string builtFor = "";
        RectTransform card;
        Image rim, plate, art;
        TextMeshProUGUI nameText;
        RectTransform standIn;
        Image standBody, standEyeL, standEyeR, standBrowL, standBrowR, standMouth;
        CanvasGroup group;
        float slideStart, outStart, shownAt, popAt;
        bool onLeft = true;
        Vector2 cardMin;
        float height;

        static SpeakerPortraits Create()
        {
            var go = new GameObject("SpeakerPortraits", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 3800; // over the HUD and the Shaft, under the floor card, the toasts and the screen transition
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var view = go.AddComponent<SpeakerPortraits>();
            view.Build();
            return view;
        }

        void Build()
        {
            card = MenuGraphics.NewRect("Card", transform);
            card.anchorMin = card.anchorMax = Vector2.zero;
            card.pivot = Vector2.zero;
            group = card.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            rim = card.gameObject.AddComponent<Image>();
            rim.raycastTarget = false;
            MenuGraphics.Apply(rim, MenuShape.Rounded, 16f);
            plate = MenuGraphics.NewImage(card, "Plate", new Color(0.078f, 0.043f, 0.051f, 0.92f));
            plate.raycastTarget = false;
            MenuGraphics.Apply(plate, MenuShape.Rounded, 12f);
            MenuGraphics.Fill(plate.rectTransform, 3f);

            var artRect = MenuGraphics.NewRect("Art", card);
            MenuGraphics.Fill(artRect, 5f);
            artRect.offsetMax = new Vector2(-5f, -5f);
            art = artRect.gameObject.AddComponent<Image>();
            art.preserveAspect = true;
            art.raycastTarget = false;
            BuildStandIn(artRect);

            var chip = MenuGraphics.NewRect("NameChip", card);
            chip.anchorMin = new Vector2(0.08f, 0f);
            chip.anchorMax = new Vector2(0.92f, 0.17f);
            chip.offsetMin = chip.offsetMax = Vector2.zero;
            var chipBack = chip.gameObject.AddComponent<Image>();
            chipBack.color = new Color(0.078f, 0.043f, 0.051f, 0.85f);
            chipBack.raycastTarget = false;
            var textRect = MenuGraphics.NewRect("Name", chip);
            MenuGraphics.Fill(textRect);
            nameText = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 8f;
            nameText.fontSizeMax = 40f;
            nameText.color = Color.white;
            nameText.raycastTarget = false;
            card.gameObject.SetActive(false);
        }

        /// <summary>The stand-in bust: a rounded body, a head, two eyes with brows and a mouth that opens. For a character with no painting yet.</summary>
        void BuildStandIn(RectTransform parent)
        {
            standIn = MenuGraphics.NewRect("StandIn", parent);
            MenuGraphics.Fill(standIn);
            standBody = Piece("Body", new Vector2(0.12f, 0f), new Vector2(0.88f, 0.45f), MenuShape.Rounded, 40f);
            var head = Piece("Head", new Vector2(0.22f, 0.3f), new Vector2(0.78f, 0.95f), MenuShape.Rounded, 50f);
            head.color = Color.white;
            standEyeL = Piece("EyeL", new Vector2(0.32f, 0.62f), new Vector2(0.43f, 0.73f), MenuShape.Circle, 0f);
            standEyeR = Piece("EyeR", new Vector2(0.57f, 0.62f), new Vector2(0.68f, 0.73f), MenuShape.Circle, 0f);
            standBrowL = Piece("BrowL", new Vector2(0.3f, 0.75f), new Vector2(0.45f, 0.78f), MenuShape.Rounded, 4f);
            standBrowR = Piece("BrowR", new Vector2(0.55f, 0.75f), new Vector2(0.7f, 0.78f), MenuShape.Rounded, 4f);
            standMouth = Piece("Mouth", new Vector2(0.4f, 0.45f), new Vector2(0.6f, 0.48f), MenuShape.Rounded, 6f);
            standEyeL.color = standEyeR.color = standBrowL.color = standBrowR.color = standMouth.color = new Color(0.078f, 0.043f, 0.051f, 1f);
            standHead = head;
        }
        Image standHead;

        Image Piece(string name, Vector2 min, Vector2 max, MenuShape shape, float corner)
        {
            var rect = MenuGraphics.NewRect(name, standIn);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.gameObject.AddComponent<Image>();
            MenuGraphics.Apply(image, shape, corner);
            image.raycastTarget = false;
            return image;
        }

        // ─── Showing a line ───────────────────────────────────────────

        void Show(PortraitCharacter who, SoundDef sound)
        {
            bool fresh = state == State.Hidden || state == State.Out || who != character;
            character = who;
            mood = PortraitVoice.Mood;
            lineId = sound.id;
            if (builtFor != who.id)
            {
                builtFor = who.id;
                rim.color = who.color;
                nameText.text = string.IsNullOrEmpty(who.displayName) ? who.id.ToUpperInvariant() : who.displayName;
                standBody.color = who.color;
                standHead.color = Color.Lerp(who.color, Color.white, 0.55f);
            }
            card.gameObject.SetActive(true);
            if (fresh)
            {
                state = State.In;
                slideStart = Time.unscaledTime;
                SlideInSeconds = 0f;
                onLeft = true;
                Layout(true);
            }
            else
            {
                state = state == State.Held ? State.Held : State.In;
                popAt = Time.unscaledTime;
            }
            shownAt = Time.unscaledTime;
            Shown++;
            Report("show", $"{who.id} {mood} ({sound.id})");
        }

        /// <summary>Takes the card away at once (a widget that shows this character itself has opened).</summary>
        public void Dismiss()
        {
            state = State.Hidden;
            if (card != null)
                card.gameObject.SetActive(false);
        }

        void Update()
        {
            if (state == State.Hidden)
                return;
            var cfg = PortraitLibrary.Config;
            if (cfg == null)
            {
                Dismiss();
                return;
            }
            float now = Time.unscaledTime;
            bool speaking = PortraitVoice.Speaking;
            MouthIsOpen = speaking && PortraitVoice.MouthOpen;
            if (speaking)
                mood = PortraitVoice.Mood;
            Paint();

            float slide = Mathf.Max(0.01f, cfg.slideSeconds);
            float k;
            switch (state)
            {
                case State.In:
                    k = Mathf.Clamp01((now - slideStart) / slide);
                    if (k >= 1f)
                    {
                        state = State.Held;
                        SlideInSeconds = now - slideStart;
                        Report("slid-in", $"{character.id} in {SlideInSeconds:0.000} s");
                    }
                    break;
                case State.Out:
                    k = 1f - Mathf.Clamp01((now - outStart) / Mathf.Max(0.01f, cfg.slideOutSeconds));
                    if (k <= 0f)
                    {
                        Dismiss();
                        return;
                    }
                    break;
                default:
                    k = 1f;
                    if (!speaking && PortraitVoice.SinceEnd >= cfg.holdSeconds)
                    {
                        state = State.Out;
                        outStart = now;
                    }
                    break;
            }
            if (state == State.Held && speaking)
                k = 1f;
            Layout(false, k);
        }

        /// <summary>The painting for the mood and mouth now: the painted frame, else the stand-in.</summary>
        void Paint()
        {
            var sprite = PortraitLibrary.Pick(character, mood, MouthIsOpen);
            StandIn = sprite == null;
            art.enabled = !StandIn;
            standIn.gameObject.SetActive(StandIn);
            if (!StandIn)
            {
                art.sprite = sprite;
                return;
            }
            float open = MouthIsOpen ? 1f : 0f;
            var mouth = standMouth.rectTransform;
            mouth.anchorMin = new Vector2(0.4f, 0.47f - 0.1f * open);
            mouth.anchorMax = new Vector2(0.6f, 0.5f);
            bool shocked = mood == PortraitMood.Shocked, laughing = mood == PortraitMood.Laughing, smug = mood == PortraitMood.Smug;
            float eye = shocked ? 0.13f : laughing ? 0.05f : 0.11f;
            SetEye(standEyeL, 0.375f, eye);
            SetEye(standEyeR, 0.625f, eye);
            standBrowL.rectTransform.localRotation = Quaternion.Euler(0f, 0f, smug ? -14f : shocked ? 8f : 0f);
            standBrowR.rectTransform.localRotation = Quaternion.Euler(0f, 0f, smug ? 14f : shocked ? -8f : 0f);
        }

        static void SetEye(Image eye, float cx, float size)
        {
            var r = eye.rectTransform;
            r.anchorMin = new Vector2(cx - size * 0.5f, 0.675f - size * 0.5f);
            r.anchorMax = new Vector2(cx + size * 0.5f, 0.675f + size * 0.5f);
        }

        /// <summary>Sizes the card and puts it at its edge, slid 'k' of the way in (0 off-screen, 1 at rest).</summary>
        void Layout(bool choose, float k = 0f)
        {
            var cfg = PortraitLibrary.Config;
            if (cfg == null)
                return;
            float w = Screen.width, h = Screen.height;
            bool portrait = h > w;
            height = Mathf.Clamp(Mathf.Min(w, h) * cfg.heightFraction, cfg.minHeight, cfg.maxHeight);
            card.sizeDelta = new Vector2(height, height);
            var safe = Screen.safeArea;
            float margin = Mathf.Max(8f, height * 0.08f);
            float cy = h * (portrait ? cfg.heightPortrait : cfg.heightLandscape);
            float y = Mathf.Clamp(cy - height * 0.5f, safe.yMin + margin, safe.yMax - height - margin);

            float leftRest = safe.xMin + margin;
            float rightRest = safe.xMax - margin - height;
            if (choose || state == State.Held)
                onLeft = HeroClear(leftRest, y) || !HeroClear(rightRest, y);
            float rest = onLeft ? leftRest : rightRest;
            float off = onLeft ? -height - 4f : w + 4f;
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(k), 3f);
            float x = Mathf.Lerp(off, rest, ease);
            card.anchoredPosition = new Vector2(x, y);
            cardMin = new Vector2(x, y);
            float pop = 1f + 0.06f * Mathf.Clamp01(1f - (Time.unscaledTime - popAt) / 0.12f);
            card.localScale = new Vector3(pop, pop, 1f);
            group.alpha = Mathf.Clamp01(k * 4f);
        }

        /// <summary>True when the hero's body on screen is not under a card whose lower-left corner is (x, y).</summary>
        bool HeroClear(float x, float y)
        {
            var hero = PlayerController.Active;
            var cam = Camera.main;
            if (hero == null || cam == null)
                return true;
            Vector3 sp = cam.WorldToScreenPoint(hero.transform.position);
            if (sp.z < 0f)
                return true;
            float pad = height * 0.35f;
            return !(sp.x > x - pad && sp.x < x + height + pad && sp.y > y - pad && sp.y < y + height + pad);
        }

        static void Report(string kind, string detail)
        {
            Debug.Log($"[Portrait] {kind}: {detail}");
            try
            {
                Reported?.Invoke(kind, detail);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
