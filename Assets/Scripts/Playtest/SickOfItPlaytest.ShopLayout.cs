using System.Collections;
using System.Collections.Generic;
using Hawkes.Playtest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SickOfIt.Core.Progression;
using SickOfIt.Core.UI.Menus;

namespace SickOfIt.Playtest
{
    public static partial class SickOfItPlaytest
    {
        sealed partial class Session
        {
            /// <summary>
            /// Yooba's shop at 1280x720 and 390x844 (a window resize, setup shortcut; WebGL keeps the runner's device):
            /// pip strips clear of each card's title/description, the list clear of the Decoration footnote, and the last
            /// row fully visible after scrolling to the bottom. Scenes name each size so the recording keeps frames.
            /// </summary>
            IEnumerator ShopLayoutSizes()
            {
                var shop = UpgradeShopManager.OverShaft ?? Object.FindAnyObjectByType<UpgradeShopManager>();
                if (shop == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, "shop layout: no shop open to size-check");
                    yield break;
                }
                if (!CanResizeWindow)
                {
                    Run.Event("layout-size", $"shop kept at {Screen.width}x{Screen.height}: a WebGL page's size is the browser window's, set by the runner's device");
                    yield return ShopLayoutPass($"shop {Screen.width}x{Screen.height}", shop);
                    yield break;
                }

                int w0 = Screen.width, h0 = Screen.height;
                var sizes = new (string name, int w, int h)[]
                {
                    ("desktop 1280x720", 1280, 720),
                    ("phone portrait 390x844", 390, 844),
                };
                foreach (var size in sizes)
                {
                    Screen.SetResolution(size.w, size.h, false);
                    yield return Run.Until(() => Screen.width == size.w && Screen.height == size.h, 6f, $"the window to resize to {size.w}x{size.h}");
                    if (Run.Done) yield break;
                    // Let MenuListLayout re-Apply for portrait/landscape columns and item sizes.
                    yield return WaitReal(0.6f);
                    if (Run.Done) yield break;
                    Run.Event("layout-size", $"shop at {size.name}: {Screen.width}x{Screen.height}");
                    yield return ShopLayoutPass("Yooba's shop " + size.name, shop);
                    if (Run.Done) yield break;
                }
                Screen.SetResolution(w0, h0, false);
                yield return Run.Until(() => Screen.width == w0 && Screen.height == h0, 6f, "the window to return to its size");
                yield return WaitReal(0.4f);
            }

            /// <summary>Top of the list, then scrolled to the bottom: MenuLayoutAudit plus pip/title checks and a full last row.</summary>
            IEnumerator ShopLayoutPass(string scene, UpgradeShopManager shop)
            {
                Run.Scene(scene + " (top)");
                yield return WaitReal(0.8f);
                if (Run.Done) yield break;
                input.MoveMouse(new Vector2(1f, 1f));
                yield return WaitReal(0.3f);
                if (Run.Done) yield break;

                ShopPipChecks(scene + " top");
                yield return Audit(scene + " top", 0, 0, 0.4f, progress: false);
                if (Run.Done) yield break;

                var scroll = ShopScroll(shop);
                if (scroll == null)
                {
                    Run.Problem(PlaytestSeverity.Bug, $"{scene}: no scroll list on the shop");
                    yield break;
                }
                // Scroll to the end the way a drag would leave it (normalized 0 = bottom of a vertical list).
                scroll.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                yield return WaitReal(0.5f);
                if (Run.Done) yield break;

                Run.Scene(scene + " (bottom)");
                ShopPipChecks(scene + " bottom");
                ShopBottomRowCheck(scene, scroll);
                float shortest = ShortestVisibleShopCardPx(scroll);
                bool tallEnough = shortest >= 48f || Mathf.Min(Screen.width, Screen.height) > 900f;
                Run.Event("check", $"{scene}: shortest visible card {shortest:0} px ({(tallEnough ? "at least 48 px" : "UNDER 48 px")})");
                if (!tallEnough)
                    Run.Problem(PlaytestSeverity.Bug, $"{scene}: shop cards are {shortest:0} px tall, under 48");
                // Phone: two columns of three rows, six cards a page (the tabs hold the rest).
                int visibleCards = 0;
                foreach (Transform child in scroll.content)
                    if (child.gameObject.activeSelf)
                        visibleCards++;
                Run.Metric("shop.visibleCards", visibleCards);
                Run.Event("check", $"{scene}: {visibleCards} cards on the page ({(visibleCards <= 6 ? "at most 6" : "MORE THAN 6")})");
                if (visibleCards > 6)
                    Run.Problem(PlaytestSeverity.Bug, $"{scene}: {visibleCards} shop cards at once, more than 6");
                if (MenuScreen.Portrait)
                {
                    int columns = ShopColumns(scroll);
                    Run.Event("check", $"{scene}: portrait columns {columns} ({(columns <= 2 ? "at most two columns" : "TOO MANY columns")})");
                    if (columns > 2)
                        Run.Problem(PlaytestSeverity.Bug, $"{scene}: portrait shop is {columns} columns, want at most 2");
                }
                yield return Audit(scene + " bottom", 0, 0, 0.4f, progress: false);
            }

            /// <summary>Each visible shop card's pip Caption must not overlap its Title or Description (screen space).</summary>
            void ShopPipChecks(string where)
            {
                int checkedCards = 0, overlaps = 0;
                foreach (var button in Object.FindObjectsByType<MenuButton>(FindObjectsSortMode.None))
                {
                    if (button == null || !button.isActiveAndEnabled)
                        continue;
                    var caption = FindCardText(button.transform, "Caption");
                    var title = FindCardText(button.transform, "Title");
                    var description = FindCardText(button.transform, "Description");
                    if (caption == null || title == null)
                        continue;
                    // Shop goods only: a pip strip is ● characters (or empty while Coming soon).
                    string raw = caption.text ?? "";
                    if (raw.Length > 0 && raw.IndexOf('●') < 0 && raw.IndexOf('•') < 0)
                        continue;
                    if (!TextScreenRect(caption, out var cap) || !TextScreenRect(title, out var tit))
                        continue;
                    checkedCards++;
                    bool hitTitle = OverlapsPx(cap, tit);
                    bool hitDesc = description != null && TextScreenRect(description, out var desc) && OverlapsPx(cap, desc);
                    if (hitTitle || hitDesc)
                    {
                        overlaps++;
                        string name = title.text ?? button.name;
                        string which = hitTitle ? "title" : "description";
                        Rect other = tit;
                        if (!hitTitle && description != null && TextScreenRect(description, out var descHit))
                            other = descHit;
                        Run.Problem(PlaytestSeverity.Bug,
                            $"{where}: pip strip overlaps {which} on \"{name}\" (pips {DescribeRect(cap)}, text {DescribeRect(other)})");
                    }
                }
                Run.Metric("shop.pipCardsChecked", checkedCards);
                Run.Metric("shop.pipOverlaps", overlaps);
                Run.Event("check", $"{where}: pip strips vs name/description — {checkedCards} cards, {overlaps} overlap(s)");
            }

            /// <summary>After scrolling to the end, every card that belongs on the last row is fully inside the viewport.</summary>
            void ShopBottomRowCheck(string where, ScrollRect scroll)
            {
                if (scroll == null || scroll.viewport == null || scroll.content == null)
                    return;
                if (!MenuLayoutAuditScreenRect(scroll.viewport, out var clip, true))
                    return;
                float bottomY = float.MaxValue;
                var lastRow = new List<RectTransform>();
                foreach (Transform child in scroll.content)
                {
                    if (!child.gameObject.activeSelf || !(child is RectTransform slot))
                        continue;
                    if (!MenuLayoutAuditScreenRect(slot, out var screen, false))
                        continue;
                    if (screen.yMin < bottomY - 2f)
                    {
                        bottomY = screen.yMin;
                        lastRow.Clear();
                        lastRow.Add(slot);
                    }
                    else if (Mathf.Abs(screen.yMin - bottomY) <= 2f)
                        lastRow.Add(slot);
                }
                int clipped = 0;
                foreach (var slot in lastRow)
                {
                    if (!MenuLayoutAuditScreenRect(slot, out var screen, false))
                        continue;
                    bool inside = screen.yMin >= clip.yMin - 2f && screen.yMax <= clip.yMax + 2f
                                  && screen.xMin >= clip.xMin - 2f && screen.xMax <= clip.xMax + 2f;
                    if (!inside)
                        clipped++;
                }
                Run.Event("check", $"{where}: last row after scroll — {lastRow.Count} card(s), {clipped} clipped");
                if (clipped > 0)
                    Run.Problem(PlaytestSeverity.Bug, $"{where}: {clipped} of the last-row cards are cut off by the list mask");
                Run.Metric("shop.lastRowCards", lastRow.Count);
                Run.Metric("shop.lastRowClipped", clipped);
            }

            static ScrollRect ShopScroll(UpgradeShopManager shop)
            {
                if (shop == null)
                    return null;
                foreach (var scroll in shop.GetComponentsInChildren<ScrollRect>(true))
                    if (scroll != null && scroll.content != null && scroll.content.childCount > 0)
                        return scroll;
                return null;
            }

            static int ShopColumns(ScrollRect scroll)
            {
                if (scroll == null || scroll.content == null)
                    return 0;
                if (scroll.content.TryGetComponent<GridLayoutGroup>(out var grid) && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                    return grid.constraintCount;
                // Infer from the first row's active siblings sharing a y.
                float top = float.MinValue;
                int cols = 0;
                foreach (Transform child in scroll.content)
                {
                    if (!child.gameObject.activeSelf || !(child is RectTransform slot))
                        continue;
                    float y = slot.anchoredPosition.y;
                    if (y > top + 1f)
                    {
                        top = y;
                        cols = 1;
                    }
                    else if (Mathf.Abs(y - top) <= 1f)
                        cols++;
                }
                return cols;
            }

            static float ShortestVisibleShopCardPx(ScrollRect scroll)
            {
                float min = float.MaxValue;
                if (scroll == null || scroll.viewport == null || scroll.content == null)
                    return 0f;
                if (!MenuLayoutAuditScreenRect(scroll.viewport, out var clip, true))
                    return 0f;
                foreach (Transform child in scroll.content)
                {
                    if (!child.gameObject.activeSelf || !(child is RectTransform slot))
                        continue;
                    if (!MenuLayoutAuditScreenRect(slot, out var screen, false))
                        continue;
                    float xMin = Mathf.Max(screen.xMin, clip.xMin), yMin = Mathf.Max(screen.yMin, clip.yMin);
                    float xMax = Mathf.Min(screen.xMax, clip.xMax), yMax = Mathf.Min(screen.yMax, clip.yMax);
                    if (xMax - xMin < 4f || yMax - yMin < 4f)
                        continue;
                    // Fully visible cards only: a peeking row's clipped height is not its real size.
                    if (screen.yMin < clip.yMin - 1f || screen.yMax > clip.yMax + 1f)
                        continue;
                    min = Mathf.Min(min, screen.height);
                }
                return min < float.MaxValue ? min : 0f;
            }

            static TextMeshProUGUI FindCardText(Transform card, string slotName)
            {
                foreach (var text in card.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    if (text == null || text.transform.parent == null)
                        continue;
                    if (text.transform.parent.name == slotName)
                        return text;
                }
                return null;
            }

            static bool TextScreenRect(TextMeshProUGUI text, out Rect screen)
            {
                screen = default;
                if (text == null)
                    return false;
                // Prefer the glyphs' own bounds so empty padding does not count as an overlap.
                text.ForceMeshUpdate();
                var bounds = text.textBounds;
                if (bounds.size.x < 0.5f || bounds.size.y < 0.5f)
                    return MenuLayoutAuditScreenRect((RectTransform)text.transform.parent, out screen, true);
                var canvas = text.canvas != null ? text.canvas.rootCanvas : null;
                if (canvas == null)
                    return false;
                var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector3 worldMin = text.transform.TransformPoint(bounds.min);
                Vector3 worldMax = text.transform.TransformPoint(bounds.max);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, worldMin);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, worldMax);
                screen = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                return screen.width > 0.5f && screen.height > 0.5f;
            }

            static bool OverlapsPx(Rect a, Rect b)
            {
                float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                return w > 2f && h > 2f;
            }

            static string DescribeRect(Rect r) => $"x {r.xMin:0}..{r.xMax:0}, y {r.yMin:0}..{r.yMax:0}";

            /// <summary>Same rest-rectangle rule as <see cref="MenuLayoutAudit"/> (ignore the button's spring scale).</summary>
            static bool MenuLayoutAuditScreenRect(RectTransform rect, out Rect screen, bool includeOwnScale)
            {
                screen = default;
                if (rect == null)
                    return false;
                var canvas = rect.GetComponentInParent<Canvas>();
                if (canvas == null)
                    return false;
                canvas = canvas.rootCanvas;
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var local = rect.rect;
                var parent = rect.parent;
                Vector3 origin = rect.localPosition;
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
                var corners = new[]
                {
                    new Vector2(local.xMin, local.yMin), new Vector2(local.xMin, local.yMax),
                    new Vector2(local.xMax, local.yMin), new Vector2(local.xMax, local.yMax),
                };
                foreach (var corner in corners)
                {
                    Vector3 world = includeOwnScale || parent == null
                        ? rect.TransformPoint(corner)
                        : parent.TransformPoint(origin + (Vector3)corner);
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, world);
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
                screen = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                return screen.width > 0.5f && screen.height > 0.5f;
            }
        }
    }
}
