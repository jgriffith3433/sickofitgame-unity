using System;
using System.Collections;
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
    /// The level-clear celebration around the tally (Docs/Story.md, "The level-clear screen"): the rank stamp's slam (shake,
    /// a burst of embers, the stamp sound, Yooba's voiced verdict), the NEW BEST banner and the earned diamonds flying into the
    /// total. All presentation on unscaled time (the world is paused behind it): nothing here touches the simulation.
    /// </summary>
    public sealed partial class GameplayHud
    {
        /// <summary>Seconds after the results opened that the stamp slammed (-1 = not yet), for the playtest.</summary>
        public float ResultsStampSeconds { get; private set; } = -1f;
        /// <summary>The rank letter on the stamp ("" on Chapter One's card).</summary>
        public string ResultsRank => stampLetter;
        /// <summary>The sound id of Yooba's verdict for this clear ("" if none).</summary>
        public string ResultsVerdict => verdictId;
        /// <summary>The NEW BEST banner has been shown.</summary>
        public bool ResultsNewBestShown { get; private set; }
        /// <summary>Embers thrown by the slam (for the playtest).</summary>
        public int ResultsEmbers { get; private set; }
        /// <summary>Diamonds that flew into the total and the total shown now (-1 = no total chip).</summary>
        public int ResultsDiamondsFlown { get; private set; }
        public int ResultsTotalShown => totalTicker != null ? totalTicker.Shown : -1;
        /// <summary>Continue's height on screen in pixels (0 = not shown), for the phone check.</summary>
        public float ResultsContinuePixels
        {
            get
            {
                if (continueButton == null || !continueButton.gameObject.activeInHierarchy)
                    return 0f;
                var corners = new Vector3[4];
                ((RectTransform)continueButton.transform).GetWorldCorners(corners);
                return Mathf.Abs(corners[1].y - corners[0].y);
            }
        }

        /// <summary>
        /// The visible texts of the results screen whose drawn words leave 'safe' (screen pixels, 1 px of slack); "" when everything is
        /// inside. The playtest's phone check (390x844: one column in the safe area).
        /// </summary>
        public string ResultsOutsideSafeArea(Rect safe)
        {
            if (resultsRoot == null)
                return "no results screen";
            var worst = new List<string>();
            var corners = new Vector3[4];
            foreach (var text in resultsRoot.GetComponentsInChildren<TMP_Text>(false))
            {
                if (string.IsNullOrWhiteSpace(text.text) || text.name == "FlyingDiamond")
                    continue;
                var b = text.textBounds;
                var t = text.transform;
                corners[0] = t.TransformPoint(new Vector3(b.min.x, b.min.y, 0f));
                corners[2] = t.TransformPoint(new Vector3(b.max.x, b.max.y, 0f));
                var lo = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                var hi = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                if (lo.x < safe.xMin - 1f || hi.x > safe.xMax + 1f || lo.y < safe.yMin - 1f || hi.y > safe.yMax + 1f)
                    worst.Add($"\"{text.text.Replace('\n', ' ')}\" ({lo.x:0},{lo.y:0})-({hi.x:0},{hi.y:0})");
            }
            return string.Join("; ", worst);
        }

        string verdictId = "";
        Action<string, string> verdictSpeak;
        RectTransform stampRect, diamondsRow, bannerRect, totalRect;
        TextMeshProUGUI totalText;
        NumberTicker totalTicker;
        float bannerAt = -1f;
        bool newBestWanted;
        int diamondsEarned, totalBefore;
        float flightsAt = -1f;
        int flightsSpawned;
        static readonly Dictionary<string, int> lastVerdicts = new Dictionary<string, int>();

        sealed class Ember
        {
            public RectTransform rect;
            public Image image;
            public Vector2 velocity;
            public float born, life, size;
        }

        sealed class Flight
        {
            public RectTransform rect;
            public Vector3 from;
            public float start;
            public int share;
        }

        readonly List<Ember> embers = new List<Ember>();
        readonly List<Flight> flights = new List<Flight>();
        float totalPulseAt = -9f;

        void ResetResultsFx()
        {
            ResultsStampSeconds = -1f;
            ResultsNewBestShown = false;
            ResultsEmbers = 0;
            ResultsDiamondsFlown = 0;
            embers.Clear();
            flights.Clear();
            stampRect = diamondsRow = bannerRect = totalRect = null;
            totalText = null;
            totalTicker = null;
            bannerAt = -1f;
            flightsAt = -1f;
            flightsSpawned = 0;
            newBestWanted = false;
            diamondsEarned = 0;
            verdictId = "";
            verdictSpeak = null;
        }

        /// <summary>Two voiced verdicts a rank (vo_yooba_rank_s_1 ...): the one not heard last time for this rank, a coin flip the first time.</summary>
        static string PickVerdict(string letter)
        {
            string key = letter == "S" || letter == "A" || letter == "B" ? letter.ToLowerInvariant() : "c";
            int pick = lastVerdicts.TryGetValue(key, out int last) ? (last == 1 ? 2 : 1) : UnityEngine.Random.Range(1, 3);
            lastVerdicts[key] = pick;
            return "vo_yooba_rank_" + key + "_" + pick;
        }

        /// <summary>The NEW BEST banner, the diamond total and its place (called while the results are built; non-Chapter-One only).</summary>
        void BuildResultsExtras(Transform inner, MenuPortraitScreens screens, bool newBest, int gained, int totalNow)
        {
            newBestWanted = newBest;
            diamondsEarned = gained;
            totalBefore = Mathf.Max(0, totalNow - gained);

            // NEW BEST: a hot pill that slams down beside the stamp.
            var banner = MenuGraphics.NewRect("NewBest", inner);
            MenuGraphics.Place(banner, new Vector2(0.9f, 0.7f), Vector2.zero, new Vector2(420f, 100f));
            MenuResponsive.Track(banner, MenuPlacement.At(0.72f, 0.74f, 0f, 0f, 460f, 110f), screens.resultsBanner, MenuElementKind.Panel, menu.Responsive);
            var slam = MenuGraphics.NewRect("Slam", banner);
            MenuGraphics.Fill(slam);
            MenuGraphics.Surface(slam, new MenuSurfaceStyle
            {
                shape = MenuShape.Pill,
                fill = new Color(0.95f, 0.2f, 0.12f, 1f),
                borderColor = theme.title.outlineColor,
                borderWidth = 7f,
                shadowColor = theme.title.outlineColor,
                shadowOffset = new Vector2(5f, -7f),
            });
            var words = MenuGraphics.Text(slam, "Text", Texts.resultsNewBest, theme.title, menu.TitleFont, 64f);
            words.enableVertexGradient = false;
            words.color = new Color(1f, 0.97f, 0.88f, 1f);
            words.alignment = TextAlignmentOptions.Center;
            slam.localRotation = Quaternion.Euler(0f, 0f, -7f);
            banner.gameObject.SetActive(false);
            bannerRect = banner;

            // The total the earned diamonds fly into.
            var total = menu.AddText(inner, "Total", "♦ " + (totalBefore), theme.heading, new Vector2(0.44f, 0.245f), Vector2.zero,
                new Vector2(560f, 80f), out _, 52f, null, theme.entrance);
            MenuResponsive.Track((RectTransform)total.transform.parent, MenuPlacement.At(0.44f, 0.245f, 0f, 0f, 560f, 80f),
                screens.resultsTotal, MenuElementKind.Text, menu.Responsive);
            total.enableVertexGradient = false;
            total.color = new Color(0.55f, 0.95f, 1f, 1f);
            totalText = total;
            totalRect = (RectTransform)total.transform.parent;
            totalTicker = NumberTicker.On(total, v => "♦ " + v);
            totalTicker.Snap(totalBefore);
        }

        /// <summary>The slam: shake, an ember burst off the stamp, the sound, then Yooba says his verdict.</summary>
        void FireStamp()
        {
            if (stampFired)
                return;
            stampFired = true;
            ResultsStampSeconds = Time.unscaledTime - resultsOpenedAt;
            world.Telemetry?.Record("signal", "results:stamp", Mathf.RoundToInt(ResultsStampSeconds * 1000f), stampLetter.Length > 0 ? stampLetter : "I");
            world.Effects.Shake(0.5f, 0.2f);
            ThrowEmbers();
            if (verdictId.Length > 0)
            {
                string text = Sfx.SpokenText(verdictId);
                world.Telemetry?.Record("signal", "results:verdict", 1, verdictId);
                if (verdictSpeak != null)
                    verdictSpeak(verdictId, text);
                else
                    Sfx.Play(verdictId);
            }
            if (newBestWanted)
                world.Telemetry?.Record("signal", "results:newBest", 1, "level " + world.LevelNumber);
            if (stampLetter.Length > 0)
                flightsAt = Time.unscaledTime + 0.45f;
        }

        IEnumerator NewBestAfterStamp()
        {
            yield return new WaitForSecondsRealtime(0.35f);
            if (resultsRoot == null || bannerRect == null)
                yield break;
            UiSounds.NewBest();
            bannerRect.gameObject.SetActive(true);
            bannerAt = Time.unscaledTime;
            ResultsNewBestShown = true;
        }

        void ThrowEmbers()
        {
            if (stampRect == null)
                return;
            var parent = stampRect.parent;
            float scale = Mathf.Max(0.0001f, stampRect.lossyScale.x);
            for (int i = 0; i < 36; i++)
            {
                var image = MenuGraphics.NewImage(parent, "Ember", Color.white);
                MenuGraphics.Apply(image, MenuShape.Circle, 0f);
                var rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                float size = UnityEngine.Random.Range(12f, 26f);
                rect.sizeDelta = new Vector2(size, size);
                rect.position = stampRect.position;
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float speed = UnityEngine.Random.Range(450f, 1300f);
                image.color = Color.Lerp(new Color(1f, 0.85f, 0.25f), new Color(1f, 0.35f, 0.08f), UnityEngine.Random.value);
                embers.Add(new Ember
                {
                    rect = rect, image = image, size = size, born = Time.unscaledTime,
                    life = UnityEngine.Random.Range(0.55f, 1.0f),
                    velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * scale,
                });
            }
            ResultsEmbers += 36;
        }

        /// <summary>A tap skipped the tally: the diamonds are in the total at once, the banner and the embers are where they would be.</summary>
        void SkipResultsFx()
        {
            foreach (var f in flights)
                if (f.rect != null)
                    Destroy(f.rect.gameObject);
            flights.Clear();
            flightsAt = -1f;
            flightsSpawned = int.MaxValue;
            ResultsDiamondsFlown = diamondsEarned;
            totalTicker?.Snap(totalBefore + diamondsEarned);
        }

        void UpdateResultsFx()
        {
            if (resultsRoot == null)
            {
                if (embers.Count > 0 || flights.Count > 0)
                {
                    embers.Clear();
                    flights.Clear();
                }
                return;
            }
            float now = Time.unscaledTime;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

            // Embers: thrown out of the stamp, pulled down, fading.
            for (int i = embers.Count - 1; i >= 0; i--)
            {
                var e = embers[i];
                float age = now - e.born;
                if (e.rect == null || age >= e.life)
                {
                    if (e.rect != null)
                        Destroy(e.rect.gameObject);
                    embers.RemoveAt(i);
                    continue;
                }
                float scale = Mathf.Max(0.0001f, e.rect.lossyScale.x);
                e.velocity += new Vector2(0f, -1500f * scale) * dt;
                e.rect.position += (Vector3)(e.velocity * dt);
                float k = 1f - age / e.life;
                var c = e.image.color;
                c.a = Mathf.Clamp01(k * 1.6f);
                e.image.color = c;
                e.rect.localScale = Vector3.one * (0.4f + 0.6f * k);
            }

            // NEW BEST: slams down from big to size.
            if (bannerAt >= 0f && bannerRect != null)
            {
                float t = Mathf.Clamp01((now - bannerAt) / 0.2f);
                float s = 1f + 1.6f * (1f - t) * (1f - t);
                bannerRect.localScale = Vector3.one * s;
            }

            // Diamonds: after the stamp they leave the diamonds row one by one and land in the total.
            if (flightsAt >= 0f && now >= flightsAt && diamondsRow != null && totalRect != null && diamondsEarned > 0)
            {
                int count = Mathf.Min(diamondsEarned, 10);
                int next = flightsSpawned;
                if (next < count && now >= flightsAt + next * 0.07f)
                {
                    int share = diamondsEarned / count + (next < diamondsEarned % count ? 1 : 0);
                    var text = MenuGraphics.Text(diamondsRow.parent, "FlyingDiamond", "♦", theme.heading, menu.Font, 56f);
                    text.enableVertexGradient = false;
                    text.color = new Color(0.55f, 0.95f, 1f, 1f);
                    text.raycastTarget = false;
                    var rect = text.rectTransform;
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(70f, 70f);
                    rect.position = diamondsRow.position;
                    flights.Add(new Flight { rect = rect, from = diamondsRow.position, start = now, share = share });
                    flightsSpawned++;
                    Sfx.Play("ui_tally_tick", 0.8f, 1.2f + next * 0.05f);
                }
            }
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                if (f.rect == null)
                {
                    flights.RemoveAt(i);
                    continue;
                }
                float t = Mathf.Clamp01((now - f.start) / 0.5f);
                float eased = t * t;
                var to = totalRect.position;
                var pos = Vector3.Lerp(f.from, to, eased);
                pos.y += Mathf.Sin(t * Mathf.PI) * 60f * Mathf.Max(0.0001f, f.rect.lossyScale.x);
                f.rect.position = pos;
                if (t >= 1f)
                {
                    ResultsDiamondsFlown += f.share;
                    totalTicker?.CountTo(totalBefore + ResultsDiamondsFlown, 0.15f);
                    totalPulseAt = now;
                    Destroy(f.rect.gameObject);
                    flights.RemoveAt(i);
                }
            }
            if (totalRect != null)
            {
                float pulse = Mathf.Clamp01(1f - (now - totalPulseAt) / 0.18f);
                totalRect.localScale = Vector3.one * (1f + 0.18f * pulse);
            }
        }
    }
}
