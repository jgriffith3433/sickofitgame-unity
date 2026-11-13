using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core;
using SickOfIt.Core.Audio;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data.Config;

namespace SickOfIt.Gameplay
{
    /// <summary>
    /// Inked HUD widgets (Docs/Story.md "The HUD and menus", art bible): ember vessels for hearts, a spinning tape
    /// reel for rewind juice, a pulsing gem for diamonds, a brass elevator dial for the floor. Grouped in two
    /// corners; wired to the same game state as the old TMP chips. Presentation only.
    /// </summary>
    public sealed partial class GameplayHud
    {
        const float VesselSize = 56f;
        const float VesselGap = 6f;
        const int VesselCap = 8;
        const float CrackSeconds = 0.32f;
        const float HealGlowSeconds = 0.35f;
        const float GemPulseSeconds = 0.2f;
        const float ReelSize = 72f;
        const float DialSize = 88f;
        const float GemIconSize = 48f;
        /// <summary>Phone rewind button floor in screen pixels (acceptance criterion).</summary>
        public const float MinRewindButtonPx = 96f;
        /// <summary>Phone caption under the rewind button: never under this many screen pixels (14 pt at 1x, drawn a little over).</summary>
        public const float MinRewindCaptionPx = 16f;
        TextMeshProUGUI rewindCaption;

        RectTransform leftCluster;
        RectTransform rightCluster;
        readonly List<Image> vesselImages = new List<Image>();
        readonly List<float> vesselCrackAt = new List<float>();
        readonly List<float> vesselHealAt = new List<float>();
        int inkShownHealth = -1;
        int inkShownMax = -1;
        TextMeshProUGUI vesselOverflow;

        RectTransform reelRoot;
        Image reelImage;
        Image reelFill;
        float reelAngle;
        float reelClickAt = -99f;
        float shownReelJuice = -1f;

        Image gemIcon;
        float gemPulseAt = -99f;

        RectTransform dialRoot;
        Image dialFace;
        Image dialNeedle;
        TextMeshProUGUI dialLabel;
        float dialTickAt = -99f;
        int dialShownFloor = -1;

        /// <summary>Screen rects of the two corner clusters (playtest coverage check).</summary>
        public Rect LeftClusterScreenRect => ScreenRect(leftCluster);
        public Rect RightClusterScreenRect => ScreenRect(rightCluster);
        public Rect ReelScreenRect => ScreenRect(reelRoot != null ? reelRoot : leftCluster);
        public Rect DialScreenRect => ScreenRect(dialRoot != null ? dialRoot : floorRoot);
        public Rect GemScreenRect => ScreenRect(gemIcon != null ? gemIcon.rectTransform : diamondsRoot);
        public Rect HealthClusterScreenRect => ScreenRect(healthRoot);

        /// <summary>How long the last lost vessel has been cracking (playtest: >= 0.3 s).</summary>
        public float LastCrackAge { get; private set; }
        /// <summary>True while any vessel is mid-crack.</summary>
        public bool VesselCracking
        {
            get
            {
                float now = Time.unscaledTime;
                for (int i = 0; i < vesselCrackAt.Count; i++)
                    if (vesselCrackAt[i] > 0f && now - vesselCrackAt[i] < CrackSeconds)
                        return true;
                return false;
            }
        }
        /// <summary>Seconds since the gem last pulsed on a diamond pickup.</summary>
        public float GemPulseAge => gemPulseAt < 0f ? 999f : Time.unscaledTime - gemPulseAt;
        /// <summary>Reel spin degrees this frame while rewinding (playtest reads Absolute).</summary>
        public float ReelSpinSpeed { get; private set; }
        /// <summary>Fraction of juice shown on the reel (0-1).</summary>
        public float ReelFillAmount { get; private set; }
        /// <summary>True when the illustrated widgets are up (always, once Build ran).</summary>
        public bool InkHudActive => leftCluster != null;

        void BuildInkClusters()
        {
            leftCluster = MenuGraphics.NewRect("HudLeft", menu.Root);
            leftCluster.anchorMin = leftCluster.anchorMax = leftCluster.pivot = new Vector2(0f, 1f);
            leftCluster.anchoredPosition = Vector2.zero;
            leftCluster.sizeDelta = new Vector2(420f, 160f);

            rightCluster = MenuGraphics.NewRect("HudRight", menu.Root);
            rightCluster.anchorMin = rightCluster.anchorMax = rightCluster.pivot = new Vector2(1f, 1f);
            rightCluster.anchoredPosition = Vector2.zero;
            rightCluster.sizeDelta = new Vector2(320f, 160f);
        }

        void BuildInkHealth()
        {
            healthRoot = MenuGraphics.NewRect("PlayerHealth", leftCluster != null ? leftCluster : menu.Root);
            healthRoot.anchorMin = healthRoot.anchorMax = new Vector2(0f, 1f);
            healthRoot.pivot = new Vector2(0f, 1f);
            healthRoot.anchoredPosition = new Vector2(8f, -8f);
            healthRoot.sizeDelta = new Vector2(VesselCap * (VesselSize + VesselGap) + 80f, VesselSize + 8f);

            for (int i = 0; i < VesselCap; i++)
            {
                var slot = MenuGraphics.NewRect("Vessel" + i, healthRoot);
                slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0f, 0.5f);
                slot.sizeDelta = new Vector2(VesselSize, VesselSize);
                slot.anchoredPosition = new Vector2(i * (VesselSize + VesselGap), 0f);
                var img = slot.gameObject.AddComponent<Image>();
                img.sprite = HudInkAtlas.VesselFull;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = Color.white;
                vesselImages.Add(img);
                vesselCrackAt.Add(-99f);
                vesselHealAt.Add(-99f);
                slot.gameObject.SetActive(false);
            }

            var overflowSlot = MenuGraphics.NewRect("Overflow", healthRoot);
            overflowSlot.anchorMin = overflowSlot.anchorMax = overflowSlot.pivot = new Vector2(0f, 0.5f);
            overflowSlot.sizeDelta = new Vector2(72f, VesselSize);
            vesselOverflow = MenuGraphics.Text(overflowSlot, "Text", "", theme.chipText, menu.Font, 28f, autoSize: false);
            vesselOverflow.enableVertexGradient = false;
            vesselOverflow.color = style.healthFill;
            overflowSlot.gameObject.SetActive(false);

            // Keep a tiny MenuBar off-screen so shop-balance / legacy probes that look for PlayerHealth/MenuBar still find one.
            var legacy = MenuGraphics.NewRect("Panel", healthRoot);
            legacy.gameObject.SetActive(false);
            healthBar = MenuBar.Create(legacy, "Bar");
            healthBar.MaxSegments = style.maxSegments;
            healthCount = vesselOverflow;

            heart = healthRoot; // floaters / heal pop anchor
            menu.AddMotion(healthRoot.gameObject, null, theme.entrance, 0f);

            var landscape = style.landscape != null && style.landscape.health != null && style.landscape.health.use
                ? style.landscape.health
                : MenuPlacement.At(0f, 1f, 24f, -24f, VesselCap * (VesselSize + VesselGap));
            var portrait = style.portrait != null ? style.portrait.health : DefaultPortrait.health;
            MenuResponsive.Track(leftCluster != null ? leftCluster : healthRoot,
                Clone(landscape, new Vector2(Mathf.Max(landscape.size.x, 280f), 150f)),
                portrait != null && portrait.use ? Clone(portrait, new Vector2(Mathf.Max(portrait.size.x, 260f), 150f)) : null,
                MenuElementKind.Panel, menu.Responsive, pivotAtAnchor: true);
        }

        void BuildInkReel()
        {
            reelRoot = MenuGraphics.NewRect("RewindReel", leftCluster != null ? leftCluster : menu.Root);
            reelRoot.anchorMin = reelRoot.anchorMax = reelRoot.pivot = new Vector2(0f, 1f);
            reelRoot.anchoredPosition = new Vector2(8f, -8f - VesselSize - 10f);
            reelRoot.sizeDelta = new Vector2(ReelSize + 100f, ReelSize);

            var disc = MenuGraphics.NewRect("Disc", reelRoot);
            disc.anchorMin = disc.anchorMax = disc.pivot = new Vector2(0f, 0.5f);
            disc.sizeDelta = new Vector2(ReelSize, ReelSize);
            disc.anchoredPosition = Vector2.zero;
            reelImage = disc.gameObject.AddComponent<Image>();
            reelImage.sprite = HudInkAtlas.Reel;
            reelImage.preserveAspect = true;
            reelImage.raycastTarget = false;

            // Tape on the disc: a radial teal wipe over the reel so juice reads as wound tape draining.
            var fillSlot = MenuGraphics.NewRect("Fill", disc);
            MenuGraphics.Fill(fillSlot, 10f);
            reelFill = fillSlot.gameObject.AddComponent<Image>();
            reelFill.sprite = MenuGraphics.Shape(MenuShape.Circle, 0f);
            reelFill.type = Image.Type.Filled;
            reelFill.fillMethod = Image.FillMethod.Radial360;
            reelFill.fillOrigin = (int)Image.Origin360.Top;
            reelFill.fillClockwise = true;
            reelFill.color = new Color(style.rewind.r, style.rewind.g, style.rewind.b, 0.55f);
            reelFill.raycastTarget = false;

            // Juice seconds sit beside the reel (still a TMP readout for rewind-juice contrast checks when progress pill is off).
            var juiceSlot = MenuGraphics.NewRect("Juice", reelRoot);
            juiceSlot.anchorMin = juiceSlot.anchorMax = juiceSlot.pivot = new Vector2(0f, 0.5f);
            juiceSlot.anchoredPosition = new Vector2(ReelSize + 8f, 0f);
            juiceSlot.sizeDelta = new Vector2(120f, 36f);
        }

        void BuildInkDiamonds()
        {
            diamondsRoot = MenuGraphics.NewRect("Diamonds", rightCluster != null ? rightCluster : menu.Root);
            diamondsRoot.anchorMin = diamondsRoot.anchorMax = diamondsRoot.pivot = new Vector2(1f, 1f);
            diamondsRoot.anchoredPosition = new Vector2(-8f, -8f);
            diamondsRoot.sizeDelta = new Vector2(200f, 56f);

            var gemSlot = MenuGraphics.NewRect("Gem", diamondsRoot);
            gemSlot.anchorMin = gemSlot.anchorMax = gemSlot.pivot = new Vector2(0f, 0.5f);
            gemSlot.sizeDelta = new Vector2(GemIconSize, GemIconSize);
            gemSlot.anchoredPosition = new Vector2(4f, 0f);
            gemIcon = gemSlot.gameObject.AddComponent<Image>();
            gemIcon.sprite = HudInkAtlas.Gem;
            gemIcon.preserveAspect = true;
            gemIcon.raycastTarget = false;

            var valueSlot = MenuGraphics.NewRect("ValueSlot", diamondsRoot);
            valueSlot.anchorMin = new Vector2(0f, 0f);
            valueSlot.anchorMax = new Vector2(1f, 1f);
            valueSlot.offsetMin = new Vector2(GemIconSize + 8f, 4f);
            valueSlot.offsetMax = new Vector2(-8f, -4f);
            diamonds = MenuGraphics.Text(valueSlot, "Value", "", theme.chipText, menu.Font, 0f, autoSize: false);
            diamonds.textWrappingMode = TextWrappingModes.NoWrap;
            diamonds.enableVertexGradient = false;
            diamonds.color = theme.accent;
            diamonds.alignment = TextAlignmentOptions.MidlineLeft;
            diamondsTicker = NumberTicker.On(diamonds, v => string.Format(theme.pointsFormat, v));
            menu.AddMotion(diamondsRoot.gameObject, null, theme.entrance, 0f);
            // rightCluster is laid once here (children use local offsets so they stay inside the corner).
            if (rightCluster != null)
                Lay(rightCluster, l => l.diamonds, MenuElementKind.Panel, pivotAtAnchor: true, controlsSize: false);
            else
                Lay(diamondsRoot, l => l.diamonds, MenuElementKind.Panel, pivotAtAnchor: true, controlsSize: false);
        }

        void BuildInkDial()
        {
            if (world.Stage == null || !world.Stage.HasFloors)
                return;
            dialRoot = MenuGraphics.NewRect("FloorDial", rightCluster != null ? rightCluster : menu.Root);
            dialRoot.anchorMin = dialRoot.anchorMax = dialRoot.pivot = new Vector2(1f, 1f);
            dialRoot.anchoredPosition = new Vector2(-8f, -8f - 56f - 8f);
            dialRoot.sizeDelta = new Vector2(DialSize + 20f, DialSize);

            floorRoot = dialRoot;
            var faceSlot = MenuGraphics.NewRect("Face", dialRoot);
            faceSlot.anchorMin = faceSlot.anchorMax = faceSlot.pivot = new Vector2(0.5f, 0.5f);
            faceSlot.sizeDelta = new Vector2(DialSize, DialSize);
            dialFace = faceSlot.gameObject.AddComponent<Image>();
            dialFace.sprite = HudInkAtlas.DialFace;
            dialFace.preserveAspect = true;
            dialFace.raycastTarget = false;

            var needleSlot = MenuGraphics.NewRect("Needle", faceSlot);
            MenuGraphics.Fill(needleSlot);
            dialNeedle = needleSlot.gameObject.AddComponent<Image>();
            dialNeedle.sprite = HudInkAtlas.DialNeedle;
            dialNeedle.preserveAspect = true;
            dialNeedle.raycastTarget = false;
            dialNeedle.color = Color.white;

            dialLabel = MenuGraphics.Text(dialRoot, "Value", "", theme.chipText, menu.TitleFont, 22f, autoSize: false);
            dialLabel.textWrappingMode = TextWrappingModes.NoWrap;
            dialLabel.enableVertexGradient = false;
            dialLabel.color = BrassLabel;
            dialLabel.alignment = TextAlignmentOptions.Center;
            var labelRt = dialLabel.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.sizeDelta = new Vector2(0f, 28f);
            labelRt.anchoredPosition = new Vector2(0f, -2f);
            floorText = dialLabel;

            menu.AddMotion(dialRoot.gameObject, null, theme.entrance, 0f);
            // Dial stays a local child of the right cluster (theme floor placement is absorbed into that corner).
            if (bossRoot != null)
                Lay(bossRoot, l => l.bossBarWithFloor, MenuElementKind.Panel);
        }

        static readonly Color BrassLabel = new Color(0.78f, 0.6f, 0.23f, 1f);

        Image rewindButtonRing;
        float rewindRingShown = 1f;

        /// <summary>0..1 fill on the hold-to-rewind button's ring (drains while held; phone playtest).</summary>
        public float RewindButtonRingFill { get; private set; } = 1f;

        /// <summary>Applies the reel sprite to the hold-to-rewind button (and grows it to ≥72 px on touch).</summary>
        void DressRewindButtonWithReel()
        {
            if (rewindButton == null) return;
            // Icon sits in the button content; keep the circular fill as the hit target.
            if (rewindButton.Content != null)
            {
                var icon = MenuGraphics.NewRect("ReelIcon", rewindButton.Content);
                MenuGraphics.Fill(icon, 6f);
                var img = icon.gameObject.AddComponent<Image>();
                img.sprite = HudInkAtlas.Reel;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = Color.white;
            }
            if (rewindButton.Label != null)
                MenuGraphics.SetText(rewindButton.Label, "");

            // A readable caption above the thumb button (the reel alone read as a tiny icon on a phone).
            if (rewindRoot != null && rewindCaption == null)
            {
                var cap = MenuGraphics.NewRect("RewindCaption", rewindRoot);
                cap.anchorMin = new Vector2(-0.5f, 1f);
                cap.anchorMax = new Vector2(1.5f, 1f);
                cap.pivot = new Vector2(0.5f, 0f);
                cap.anchoredPosition = new Vector2(0f, 14f);
                cap.sizeDelta = new Vector2(0f, 60f);
                rewindCaption = cap.gameObject.AddComponent<TextMeshProUGUI>();
                rewindCaption.text = "HOLD TO REWIND";
                rewindCaption.alignment = TextAlignmentOptions.Bottom;
                rewindCaption.fontStyle = FontStyles.Bold;
                rewindCaption.textWrappingMode = TextWrappingModes.NoWrap;
                rewindCaption.color = style.rewind;
                rewindCaption.raycastTarget = false;
                rewindCaption.fontSize = 28f;
            }

            // Fill ring around the button: drains with juice while rewind is held (phone critic check).
            if (rewindRoot != null && rewindButtonRing == null)
            {
                var ringSlot = MenuGraphics.NewRect("FillRing", rewindRoot);
                ringSlot.anchorMin = Vector2.zero;
                ringSlot.anchorMax = Vector2.one;
                ringSlot.offsetMin = new Vector2(-10f, -10f);
                ringSlot.offsetMax = new Vector2(10f, 10f);
                ringSlot.SetAsFirstSibling();
                rewindButtonRing = ringSlot.gameObject.AddComponent<Image>();
                rewindButtonRing.sprite = MenuGraphics.Shape(MenuShape.Circle, 0f);
                rewindButtonRing.type = Image.Type.Filled;
                rewindButtonRing.fillMethod = Image.FillMethod.Radial360;
                rewindButtonRing.fillOrigin = (int)Image.Origin360.Top;
                rewindButtonRing.fillClockwise = true;
                rewindButtonRing.color = new Color(style.rewind.r, style.rewind.g, style.rewind.b, 0.85f);
                rewindButtonRing.raycastTarget = false;
                // Hollow look: a slightly smaller clear hole is the button face; the ring reads as a rim.
                var hole = MenuGraphics.NewRect("RingHole", ringSlot);
                MenuGraphics.Fill(hole, 14f);
                var holeImg = hole.gameObject.AddComponent<Image>();
                holeImg.sprite = MenuGraphics.Shape(MenuShape.Circle, 0f);
                holeImg.color = new Color(0.1f, 0.07f, 0.1f, 0.92f);
                holeImg.raycastTarget = false;
            }
        }

        void UpdateRewindButtonRing()
        {
            if (rewindButtonRing == null)
                return;
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            var clock = SickOfIt.Core.GameClock.Instance;
            bool rewinding = clock != null && clock.IsRewinding;
            float juice = budget != null ? budget.Juice : 1f;
            float cap = budget != null ? Mathf.Max(0.01f, budget.Max) : 1f;
            float target = Mathf.Clamp01(juice / cap);
            // While held, chase the live juice so the ring visibly drains; otherwise ease to the current fill.
            float speed = rewinding ? 4f : 2f;
            rewindRingShown = Mathf.MoveTowards(rewindRingShown, target, Time.unscaledDeltaTime * speed);
            RewindButtonRingFill = rewindRingShown;
            rewindButtonRing.fillAmount = rewindRingShown;
            rewindButtonRing.color = rewindRingShown < 0.05f
                ? style.rewindEmpty
                : new Color(style.rewind.r, style.rewind.g, style.rewind.b, rewinding ? 1f : 0.75f);
        }

        void UpdateInkHealth()
        {
            var player = world != null ? world.Player : null;
            if (player == null || vesselImages.Count == 0) return;

            int max = Mathf.Max(1, player.MaxHealth);
            int health = Mathf.Clamp(player.Health, 0, max);
            bool rewinding = GameClock.Instance != null && GameClock.Instance.IsRewinding;
            float now = Time.unscaledTime;

            int show = Mathf.Min(max, VesselCap);
            if (max != inkShownMax)
            {
                inkShownMax = max;
                for (int i = 0; i < vesselImages.Count; i++)
                    vesselImages[i].transform.parent.gameObject.SetActive(i < show);
                if (vesselOverflow != null)
                {
                    var slot = vesselOverflow.rectTransform.parent as RectTransform;
                    if (slot != null)
                    {
                        bool overflow = max > VesselCap;
                        slot.gameObject.SetActive(overflow);
                        if (overflow)
                            slot.anchoredPosition = new Vector2(show * (VesselSize + VesselGap), 0f);
                    }
                }
                healthRoot.sizeDelta = new Vector2(show * (VesselSize + VesselGap) + (max > VesselCap ? 80f : 8f), VesselSize + 8f);
            }

            if (vesselOverflow != null && max > VesselCap)
                MenuGraphics.SetText(vesselOverflow, $"{health}/{max}");

            if (inkShownHealth >= 0 && health < inkShownHealth && !rewinding)
            {
                // Crack the vessels that were just lost, from the right.
                for (int lost = health; lost < inkShownHealth && lost < show; lost++)
                {
                    int idx = Mathf.Clamp(lost, 0, show - 1);
                    vesselCrackAt[idx] = now;
                    LastCrackAge = 0f;
                }
                Sfx.Play("hit_pop", 0.7f, 0.85f); // glass/ember crack stand-in (cabinet feel)
            }
            if (inkShownHealth >= 0 && health > inkShownHealth && !rewinding)
            {
                for (int gained = inkShownHealth; gained < health && gained < show; gained++)
                    vesselHealAt[gained] = now;
                healPopAt = now;
                floaters?.Spawn(heart, $"+{health - inkShownHealth}", style.healthFill, 52f);
            }
            inkShownHealth = health;
            shownHealth = health;

            if (healthBar != null)
                healthBar.Set(health, max);

            LastCrackAge = 0f;
            for (int i = 0; i < show; i++)
            {
                var img = vesselImages[i];
                float crackAge = vesselCrackAt[i] > 0f ? now - vesselCrackAt[i] : 999f;
                float healAge = vesselHealAt[i] > 0f ? now - vesselHealAt[i] : 999f;
                if (crackAge < CrackSeconds)
                    LastCrackAge = Mathf.Max(LastCrackAge, crackAge);

                bool filled = i < health;
                if (crackAge < CrackSeconds)
                {
                    img.sprite = HudInkAtlas.VesselCracked;
                    float t = crackAge / CrackSeconds;
                    img.color = Color.Lerp(Color.white, new Color(1f, 1f, 1f, 0.35f), t);
                    float shake = (1f - t) * 6f;
                    ((RectTransform)img.transform).anchoredPosition = new Vector2(
                        i * (VesselSize + VesselGap) + Mathf.Sin(now * 70f) * shake, Mathf.Cos(now * 55f) * shake * 0.5f);
                    img.transform.localScale = Vector3.one * (1f + 0.15f * (1f - t));
                }
                else if (!filled)
                {
                    img.sprite = HudInkAtlas.VesselEmpty;
                    img.color = new Color(1f, 1f, 1f, 0.85f);
                    ((RectTransform)img.transform).anchoredPosition = new Vector2(i * (VesselSize + VesselGap), 0f);
                    img.transform.localScale = Vector3.one;
                }
                else
                {
                    img.sprite = HudInkAtlas.VesselFull;
                    float glow = healAge < HealGlowSeconds ? 0.35f * Mathf.Sin(healAge / HealGlowSeconds * Mathf.PI) : 0f;
                    // Idle ember flicker so full vessels breathe.
                    float flicker = 0.04f + 0.04f * Mathf.Sin(now * (2.1f + i * 0.37f) + i);
                    img.color = Color.Lerp(Color.white, EmberGlow, Mathf.Max(glow, flicker));
                    ((RectTransform)img.transform).anchoredPosition = new Vector2(i * (VesselSize + VesselGap), 0f);
                    float beat = health <= Mathf.CeilToInt(max * style.lowHealthFraction)
                        ? 1f + 0.1f * Mathf.Max(0f, Mathf.Sin(now * 9f)) : 1f;
                    if (healAge < HealGlowSeconds)
                        beat *= 1f + 0.25f * Mathf.Sin(healAge / HealGlowSeconds * Mathf.PI);
                    img.transform.localScale = new Vector3(beat, beat, 1f);
                }
            }
        }

        static readonly Color EmberGlow = new Color(1f, 0.85f, 0.45f, 1f);

        void UpdateInkReel()
        {
            if (reelImage == null) return;
            var budget = SickOfIt.Core.Rewind.RewindBudget.Instance;
            var clock = GameClock.Instance;
            bool rewinding = clock != null && clock.IsRewinding;
            float juice = budget != null ? budget.Juice : 0f;
            float cap = budget != null ? Mathf.Max(1f, budget.Max) : 10f;
            ReelFillAmount = Mathf.Clamp01(juice / cap);
            if (reelFill != null)
            {
                reelFill.fillAmount = ReelFillAmount;
                reelFill.color = ReelFillAmount < 0.05f ? style.rewindEmpty : style.rewind;
            }

            if (rewinding)
            {
                ReelSpinSpeed = 420f * (0.6f + 0.4f * ReelFillAmount);
                reelAngle += ReelSpinSpeed * Time.unscaledDeltaTime;
            }
            else
            {
                ReelSpinSpeed = 0f;
                if (shownReelJuice >= 0f && juice > shownReelJuice + 0.35f)
                {
                    reelClickAt = Time.unscaledTime;
                    Sfx.Play(Sfx.Events.uiClick, 0.85f, 1.15f); // mechanical click as juice refills
                }
            }
            shownReelJuice = juice;
            float clickAge = Time.unscaledTime - reelClickAt;
            float pop = clickAge < 0.18f ? 1f + 0.2f * Mathf.Sin(clickAge / 0.18f * Mathf.PI) : 1f;
            reelImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -reelAngle);
            reelImage.rectTransform.localScale = new Vector3(pop, pop, 1f);
            if (reelFill != null)
                reelFill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -reelAngle);
        }

        void UpdateInkGemPulse(bool gained)
        {
            if (!gained || gemIcon == null) return;
            gemPulseAt = Time.unscaledTime;
        }

        void UpdateInkGem()
        {
            if (gemIcon == null) return;
            float age = Time.unscaledTime - gemPulseAt;
            float pop = age < GemPulseSeconds ? 1f + 0.35f * Mathf.Sin(age / GemPulseSeconds * Mathf.PI) : 1f;
            // Quiet sparkle when idle.
            float sparkle = age > GemPulseSeconds ? 1f + 0.04f * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 3.2f)) : pop;
            gemIcon.rectTransform.localScale = new Vector3(sparkle, sparkle, 1f);
        }

        void UpdateInkDial()
        {
            if (dialNeedle == null || floorText == null) return;
            int floor = world.Stage.Floor;
            if (floor != dialShownFloor)
            {
                if (dialShownFloor >= 0)
                {
                    dialTickAt = Time.unscaledTime;
                    if (GameClock.Instance == null || !GameClock.Instance.IsRewinding)
                        Sfx.Play("shaft_tick", 0.7f, 1f); // brass dial tick
                }
                dialShownFloor = floor;
                MenuGraphics.SetText(dialLabel, string.Format(Texts.floorFormat ?? "FLOOR {0}", floor));
            }
            // Needle sweeps as floors drop (1000 → lower): map a 50-floor window into ~270 degrees.
            float t = Mathf.Repeat((1000 - floor) / 50f, 1f);
            float target = Mathf.Lerp(120f, -120f, t);
            float tickAge = Time.unscaledTime - dialTickAt;
            float punch = tickAge < 0.28f ? (1f - tickAge / 0.28f) * 18f : 0f;
            dialNeedle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, target + punch);
            float scale = tickAge < 0.35f ? 1f + 0.18f * Mathf.Sin(tickAge / 0.35f * Mathf.PI) : 1f;
            dialRoot.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>On touch devices, grow the rewind button to at least <see cref="MinRewindButtonPx"/> screen pixels.</summary>
        void EnsureRewindButtonMinSize()
        {
            if (rewindRoot == null || !rewindRoot.gameObject.activeInHierarchy) return;
            if (!TouchUiOn && Screen.height > 500 && Screen.width > 500) return;
            float h = ScreenRect(rewindRoot).height;
            if (h < 1f) return;
            float scale = rewindRoot.localScale.y;
            if (scale < 0.01f) scale = 1f;
            float want = MinRewindButtonPx / (h / scale);
            if (want > scale + 0.01f)
                rewindRoot.localScale = new Vector3(want, want, 1f);
            if (rewindCaption != null)
            {
                // Font size in canvas units so it measures MinRewindCaptionPx on the screen, whatever the canvas scale.
                float pxPerUnit = rewindRoot.rect.height > 0.01f ? ScreenRect(rewindRoot).height / rewindRoot.rect.height : 1f;
                if (pxPerUnit > 0.001f)
                    rewindCaption.fontSize = MinRewindCaptionPx / pxPerUnit;
                rewindCaption.gameObject.SetActive(TouchUiOn);
            }
        }

        /// <summary>Union of the ink HUD clusters as a fraction of the playfield (phone criterion ≤ 0.12).</summary>
        public float InkHudCoverageOfPlayfield()
        {
            var play = PlayfieldScreenRect();
            if (play.width < 1f || play.height < 1f) return 0f;
            float area = 0f;
            area += RectAreaInside(LeftClusterScreenRect, play);
            area += RectAreaInside(RightClusterScreenRect, play);
            // Rewind button sits outside the playfield on portrait (below it); only count if it overlaps.
            area += RectAreaInside(RewindButtonScreenRect, play);
            return area / (play.width * play.height);
        }

        Rect PlayfieldScreenRect()
        {
            // Portrait: the top band is the playfield; landscape: nearly the full screen inside the safe area.
            var safe = Screen.safeArea;
            if (MenuScreen.Portrait)
            {
                float h = safe.height * 0.6f;
                return new Rect(safe.x, safe.yMax - h, safe.width, h);
            }
            return safe;
        }

        static float RectAreaInside(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            float w = xMax - xMin, h = yMax - yMin;
            return w > 0f && h > 0f ? w * h : 0f;
        }
    }
}
