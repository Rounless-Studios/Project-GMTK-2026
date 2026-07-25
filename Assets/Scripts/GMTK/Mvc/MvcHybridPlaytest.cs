using System.Collections;
using System.Text;
using UnityEngine;
using SpinMotion;

namespace GMTK.Mvc
{
    /// <summary>
    /// In-editor verification of the player-only MVC hybrid in the kit race scene: starts a race
    /// and asserts the player spawns as an MVC vehicle (set as the MVC player) while the grid
    /// still registers ai+1 cars for scoring. Add during play and read
    /// <see cref="LastResult"/> ("GMTK-HYBRID: ...").
    /// </summary>
    public class MvcHybridPlaytest : MonoBehaviour
    {
        public static string LastResult = "(not run yet)";
        public int aiCount = 5;

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            var sb = new StringBuilder();
            bool pass = true;
            void Check(string n, bool ok, string d = "")
            {
                if (!ok) pass = false;
                sb.Append(ok ? "[OK] " : "[FAIL] ").Append(n);
                if (d.Length > 0) sb.Append(" (").Append(d).Append(')');
                sb.Append("; ");
            }

            var events = Race.Events;
            if (events == null) { LastResult = "FAIL | no GameEvents"; yield break; }

            Check("mvc manager present", GmtkMvcBridge.HasManager);

            RaceData.AiBotsSelected = aiCount;
            RaceData.LapsSelected = 20;
            events.OnClickPlayRaceEvent.Invoke();

            float t = Time.time;
            while (!Race.IsRaceInProgress && Time.time - t < 15f) yield return null;
            Check("race started", Race.IsRaceInProgress);
            yield return null;

            int cars = Race.CarCount;
            Check("car count == ai+1", cars == aiCount + 1, "cars=" + cars);

            var player = Race.CarByIndex(0);
            Check("player resolved", player != null, player != null ? player.name : "null");
            if (player != null)
            {
                Check("player is MVC vehicle", GmtkMvcBridge.VehicleOf(player) != null);
                Check("player is MVC player target", GmtkMvcBridge.IsMvcPlayer(player));
            }

            double s0 = Race.ScoreOf(0);
            yield return new WaitForSeconds(2f);
            double s1 = Race.ScoreOf(0);
            Check("player score readable", true, "s0=" + s0 + " s1=" + s1);

            LastResult = (pass ? "PASS" : "FAIL") + " | " + sb;
            Debug.Log("GMTK-HYBRID: " + LastResult);
        }
    }
}
