namespace SickOfIt.Core.Audio
{
    /// <summary>
    /// One helper for every menu, Settings, shop, death and results button: hover, press, back, toggle, panel open,
    /// shop open, purchase, can't afford, unlock, level-clear, rank stamp, new-best and death sting. Always plays
    /// through <see cref="Sfx"/> onto the UI bus (audio.json <c>bus: Ui</c>), never a per-button AudioSource.
    /// While a kit clip is still pending Gen, falls back to an existing audible cousin so menus never go mute.
    /// Presentation only.
    /// </summary>
    public static class UiSounds
    {
        public static void Hover() => PlayOr("ui_hover", Sfx.Events.uiHover);
        public static void Press() => PlayOr("ui_click", Sfx.Events.uiClick);
        public static void Back() => PlayOr("ui_back", Sfx.Events.uiBack);
        public static void Toggle() => PlayOr(Sfx.Events.uiToggle, Sfx.Events.uiClick);
        public static void PanelOpen() => PlayOr(Sfx.Events.uiPanelOpen, Sfx.Events.pause);
        public static void ShopOpen() => PlayOr(Sfx.Events.uiShopOpen, Sfx.Events.pause);
        public static void Purchase() => PlayOr(Sfx.Events.uiPurchase, Sfx.Events.upgradePick);
        public static void CantAfford() => PlayOr(Sfx.Events.uiCantAfford, Sfx.Events.uiBack);
        public static void Unlock() => PlayOr(Sfx.Events.uiUnlock, Sfx.Events.gradeReveal);
        public static void LevelClear() => PlayOr(Sfx.Events.levelClear, Sfx.Events.gradeReveal);
        public static void RankStamp() => PlayOr(Sfx.Events.uiRankStamp, Sfx.Events.gradeReveal);
        public static void NewBest() => PlayOr(Sfx.Events.uiNewBest, Sfx.Events.gradeReveal);
        public static void DeathSting() => PlayOr(Sfx.Events.uiDeathSting, Sfx.Events.deathWarning);

        static void PlayOr(string preferred, string fallback)
        {
            if (!string.IsNullOrEmpty(preferred) && Sfx.ClipOf(preferred) != null)
                Sfx.Play(preferred);
            else if (!string.IsNullOrEmpty(fallback))
                Sfx.Play(fallback);
        }
    }
}
