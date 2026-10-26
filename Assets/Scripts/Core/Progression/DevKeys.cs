using UnityEngine;
using UnityEngine.InputSystem;
using SickOfIt.Data;

namespace SickOfIt.Core.Progression
{
    /// <summary>
    /// Developer keys, only with <c>dev=1</c> in the page's URL (and in the editor): + gives 10 diamonds, - takes 10 away
    /// (hold Ctrl for 100). Straight into the save (RogueliteManager.SetPoints: no achievement counts), shown for a moment
    /// in the corner. Never on for players.
    /// </summary>
    public sealed class DevKeys : MonoBehaviour
    {
        string shown = "";
        float shownUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Application.isEditor && !DevUrl(Application.absoluteURL))
                return;
            var go = new GameObject("DevKeys") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<DevKeys>();
        }

        /// <summary>The query has dev=1 (not "nodev=1", not in the path).</summary>
        public static bool DevUrl(string url)
        {
            int query = url != null ? url.IndexOf('?') : -1;
            if (query < 0)
                return false;
            foreach (string part in url.Substring(query + 1).Split('&', '#'))
            {
                if (part == "dev=1")
                    return true;
            }
            return false;
        }

        void Update()
        {
            var keys = Keyboard.current;
            if (keys == null)
                return;
            int step = keys.ctrlKey.isPressed ? 100 : 10;
            if (keys.equalsKey.wasPressedThisFrame || keys.numpadPlusKey.wasPressedThisFrame)
                Change(step);
            else if (keys.minusKey.wasPressedThisFrame || keys.numpadMinusKey.wasPressedThisFrame)
                Change(-step);
        }

        void Change(int amount)
        {
            var roguelite = RogueliteManager.Instance;
            if (roguelite == null)
                return;
            int now = Mathf.Max(0, roguelite.GetPoints() + amount);
            roguelite.SetPoints(now);
            shown = $"DEV  {(amount > 0 ? "+" : "")}{amount} ◆  =  {now}";
            shownUntil = Time.unscaledTime + 1.5f;
            Debug.Log("[DevKeys] diamonds " + now);
        }

        void OnGUI()
        {
            if (Time.unscaledTime > shownUntil)
                return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(Mathf.Max(16f, Screen.height * 0.028f)), alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            GUI.Box(new Rect(Screen.width - 360f, Screen.height - 70f, 340f, 50f), shown, style);
        }
    }
}
