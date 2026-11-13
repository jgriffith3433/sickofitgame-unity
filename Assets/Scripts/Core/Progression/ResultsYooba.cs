using System;
using UnityEngine;
using SickOfIt.Core.UI.Menus;
using SickOfIt.Data;
using SickOfIt.Data.Config;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Yooba on the level-clear screen (Docs/Story.md, "Yooba between floors"): his talking portrait beside the rank stamp, and the
    /// verdict he gives it. GameplayHud cannot name this assembly, so it asks through <see cref="NextGoalBridge.BuildResultsYooba"/>.
    /// Presentation only: unscaled time, nothing here changes the simulation.
    /// </summary>
    public static class ResultsYooba
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => NextGoalBridge.BuildResultsYooba = Build;

        static Action<string, string> Build(MenuCanvas menu, Transform parent, MenuPlacement landscape, MenuPlacement portrait)
        {
            if (!YoobaPortrait.Enabled || menu == null || parent == null)
                return null;
            var config = GameDefinition.Instance.yooba;
            var view = YoobaPortrait.Create(menu, parent, config, landscape, portrait);
            return (sound, text) =>
            {
                if (view == null)
                    return;
                var line = new YoobaLine { moment = YoobaMoment.Chatter, sound = sound, text = text, clip = "talk", once = false };
                view.StartCoroutine(view.Say(line, null));
            };
        }
    }
}
