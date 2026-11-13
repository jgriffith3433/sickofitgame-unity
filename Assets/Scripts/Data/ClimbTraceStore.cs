using System;
using UnityEngine;

namespace SickOfIt.Data
{
    /// <summary>The save side of the climb's trace (scorch per death, seam per rewind): the PlayerPrefs keys, cleared with the run.</summary>
    public static class ClimbTraceStore
    {
        public const string Prefix = "Trace_F";

        /// <summary>Raised after a reset so the Gameplay layer can zero its live counters.</summary>
        public static event Action Cleared;

        public static string[] KnownFloors() =>
            PlayerPrefs.GetString(Prefix + "List", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        public static void Reset()
        {
            foreach (var f in KnownFloors())
                PlayerPrefs.DeleteKey(Prefix + f);
            PlayerPrefs.DeleteKey(Prefix + "List");
            Cleared?.Invoke();
        }
    }
}
