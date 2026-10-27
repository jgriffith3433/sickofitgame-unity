using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Core.Flow;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Manages the Level Selection scene: a card per level (thumbnail, name, locked/cleared state and the health
    /// the hero finished it with), built at runtime from levelSelectionConfig.layout and ui/theme.json. Pages like the
    /// Codex: as many cards as the list holds without scrolling, previous / page / next under it (arrow keys too), opening
    /// on the page with the furthest level reached. Every card is built (hidden off its page), so the order is all there.
    /// </summary>
    public class LevelSelectionManager : MonoBehaviour
    {
        [Header("Scene Loading")]
        [SerializeField] private SceneLoadingManager sceneLoadingManager;

        private MenuCanvas menu;
        private bool leaving;

        private readonly List<GameObject> cards = new List<GameObject>();
        private SelectionSceneConfig config;
        private int page, perPage;
        private bool lastPortrait;
        private MenuButton previous, next;
        private TextMeshProUGUI pageText;

        private void Start()
        {
            AnalyticsManager.Screen("level_select");
            var gd = GameDefinition.Instance;
            if (gd == null)
            {
                Debug.LogError("LevelSelectionManager: GameDefinition not found in Resources!");
                return;
            }

            config = gd.levelSelectionConfig ?? new SelectionSceneConfig();
            menu = MenuCanvas.Create("LevelSelectUI", gd.menuTheme, config.layout, gameObject);
            var list = menu.AddList(config.layout.list, menu.Theme.card.size);

            int highestReached = GameProgressManager.Instance.GetHighestLevelReached();
            for (int i = 1; i <= gd.TotalLevels; i++)
            {
                int levelNumber = i;
                var level = gd.GetLevel(levelNumber);
                bool completed = GameProgressManager.Instance.IsLevelCompleted(levelNumber);
                bool unlocked = levelNumber <= highestReached;
                int endHealth = RogueliteManager.Instance.GetLevelEndHealth(levelNumber);
                int maxHealth = RogueliteManager.Instance.GetLevelMaxHealth(levelNumber);

                var data = new MenuCardData
                {
                    Picture = level != null ? level.thumbnail : null,
                    PictureArea = true,
                    Badge = levelNumber.ToString(),
                    Caption = string.Format(config.captionFormat, levelNumber),
                    Title = level != null && !string.IsNullOrEmpty(level.displayName) ? level.displayName : $"Level {levelNumber}",
                    Status = completed
                        ? config.clearedText + (endHealth >= 0 ? "  " + Hearts(endHealth, maxHealth) : "")
                        : config.playText,
                    StatusColor = completed ? menu.Theme.positive : menu.Theme.accent,
                    LockedStamp = unlocked ? null : config.lockedText,
                };
                var card = MenuCard.Add(menu, list, data, () => OnLevelClicked(levelNumber));
                cards.Add(card.Button.transform.parent.gameObject);
                // Cards past the first page are built hidden: their entrance stagger must not climb with them, or the
                // pager and Back (built last) would wait seconds to appear, and a card paged to would still be invisible.
                if (cards.Count >= PerPage())
                    menu.ResetEntranceCascade();
            }
            menu.ResetEntranceCascade();
            BuildPager(gd);
            lastPortrait = MenuScreen.Portrait;
            perPage = PerPage();
            page = Mathf.Max(0, Mathf.Min(highestReached, cards.Count) - 1) / perPage;
            ShowPage();

            if (config.showCumulativeHealth)
            {
                int maxHealth = RogueliteManager.Instance.GetMaxHealth();
                int health = RogueliteManager.Instance.GetCumulativeHealth(gd.TotalLevels);
                menu.AddChip(string.Format(menu.Theme.healthFormat, health, maxHealth));
            }
            menu.AddBackButton(OnBackClicked);
        }

        // ─── Pages ────────────────────────────────────────────────────

        /// <summary>The cards the list shows at once on this screen shape: its columns times the rows that fit.</summary>
        int PerPage()
        {
            var list = config.layout.list;
            bool portrait = MenuScreen.Portrait && list.portrait != null && list.portrait.use;
            Vector2 size = portrait ? list.portrait.size : list.size;
            Vector2 item = portrait ? list.portrait.itemSize : list.itemSize;
            Vector2 spacing = portrait ? list.portrait.spacing : list.spacing;
            int columns = portrait ? list.portrait.columns : list.columns;
            if (item.x <= 0f || item.y <= 0f)
                item = menu.Theme.card.size;
            if (columns <= 0)
                columns = Mathf.Max(1, Mathf.FloorToInt((size.x + spacing.x) / (item.x + spacing.x)));
            int rows = Mathf.Max(1, Mathf.FloorToInt((size.y + spacing.y) / (item.y + spacing.y)));
            return Mathf.Max(1, columns * rows);
        }

        /// <summary>Previous, "PAGE n OF m" and next under the list, in the Codex's buttons and words.</summary>
        void BuildPager(GameDefinition gd)
        {
            var codex = gd.codex ?? new CodexConfig();
            var style = menu.Theme.smallButton.Clone();
            style.size = new Vector2(110f, 70f);
            float y = 120f, portraitY = 230f;
            previous = menu.AddButtonAt(new Vector2(0.5f, 0f), new Vector2(-300f, y), codex.previous, style, () => Turn(-1), null,
                new MenuPlacement { use = true, anchor = new Vector2(0.5f, 0f), offset = new Vector2(-300f, portraitY) });
            next = menu.AddButtonAt(new Vector2(0.5f, 0f), new Vector2(300f, y), codex.next, style, () => Turn(1), null,
                new MenuPlacement { use = true, anchor = new Vector2(0.5f, 0f), offset = new Vector2(300f, portraitY) });
            var slot = MenuGraphics.NewRect("Page", menu.Root);
            var size = new Vector2(420f, 70f);
            MenuGraphics.Place(slot, new Vector2(0.5f, 0f), new Vector2(0f, y), size);
            MenuResponsive.Track(slot, MenuResponsive.From(new Vector2(0.5f, 0f), new Vector2(0f, y), size),
                new MenuPlacement { use = true, anchor = new Vector2(0.5f, 0f), offset = new Vector2(0f, portraitY), size = size },
                MenuElementKind.Panel, menu.Responsive);
            pageText = MenuGraphics.Text(slot, "Text", "", menu.Theme.heading, menu.Font, 32f);
        }

        void Turn(int step)
        {
            int pages = Pages();
            if (pages <= 1)
                return;
            page = Mathf.Clamp(page + step, 0, pages - 1);
            ShowPage();
        }

        int Pages() => Mathf.Max(1, Mathf.CeilToInt(cards.Count / (float)perPage));

        void ShowPage()
        {
            int pages = Pages();
            page = Mathf.Clamp(page, 0, pages - 1);
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                    cards[i].SetActive(i / perPage == page);
            }
            MenuGraphics.SetText(pageText, string.Format(config.pageFormat, page + 1, pages));
            if (previous != null) previous.interactable = page > 0;
            if (next != null) next.interactable = page < pages - 1;
            bool paged = pages > 1;
            if (previous != null) previous.gameObject.SetActive(paged);
            if (next != null) next.gameObject.SetActive(paged);
            if (pageText != null) pageText.gameObject.SetActive(paged);
        }

        private void Update()
        {
            if (menu == null || cards.Count == 0 || leaving)
                return;
            // A phone turned round holds a different number of cards: keep the first card shown on screen.
            if (MenuScreen.Portrait != lastPortrait)
            {
                lastPortrait = MenuScreen.Portrait;
                int first = page * perPage;
                perPage = PerPage();
                page = first / perPage;
                ShowPage();
            }
            var keys = Keyboard.current;
            if (keys != null && (keys.leftArrowKey.wasPressedThisFrame || keys.pageUpKey.wasPressedThisFrame))
                Turn(-1);
            else if (keys != null && (keys.rightArrowKey.wasPressedThisFrame || keys.pageDownKey.wasPressedThisFrame))
                Turn(1);
        }

        /// <summary>Health as hearts, lost ones faded (falls back to "♥ 3/5" for big totals).</summary>
        public static string Hearts(int health, int max)
        {
            if (max <= 0 || max > 8)
                return $"♥ {health}/{max}";
            health = Mathf.Clamp(health, 0, max);
            return new string('♥', health) + "<alpha=#40>" + new string('♥', max - health) + "<alpha=#FF>";
        }

        private void OnLevelClicked(int levelNumber)
        {
            if (leaving) return;
            leaving = true;
            AnalyticsManager.Choice("level_select", levelNumber.ToString());

            // For Level 1, check if it's the first time visiting
            if (levelNumber == 1 && !HasVisitedLevel(1))
            {
                // Mark as visited and play Video1 first
                MarkLevelAsVisited(1);
                PlayerPrefs.SetInt("ReturnToLevel", 1);
                PlayerPrefs.Save();

                if (BundleDownloadManager.Instance != null)
                    BundleDownloadManager.Instance.StartDownloadForLevel(1);

                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadVideo(1);
                else
                    SceneTransitionManager.Instance.LoadVideo(1);
            }
            else
            {
                if (BundleDownloadManager.Instance != null)
                    BundleDownloadManager.Instance.StartDownloadForLevel(levelNumber);

                if (sceneLoadingManager != null)
                    sceneLoadingManager.LoadLevel(levelNumber);
                else
                    SceneTransitionManager.Instance.LoadLevel(levelNumber);
            }
        }

        private bool HasVisitedLevel(int levelNumber)
        {
            return PlayerPrefs.GetInt($"Level_{levelNumber}_Visited", 0) == 1;
        }

        private void MarkLevelAsVisited(int levelNumber)
        {
            PlayerPrefs.SetInt($"Level_{levelNumber}_Visited", 1);
            PlayerPrefs.Save();
        }

        private void OnBackClicked()
        {
            if (leaving) return;
            leaving = true;
            SceneLoadingManager.ShowLoadingUI();
            SceneTransitionManager.Instance.LoadMainMenu();
        }
    }
}
