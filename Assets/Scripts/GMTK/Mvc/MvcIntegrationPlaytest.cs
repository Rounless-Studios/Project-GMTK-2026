using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using MVC;
using MVC.Core;

namespace GMTK.Mvc
{
    /// <summary>
    /// Compiled validation harness for the MVC integration primitives — used because
    /// `unity command eval` cannot reference MVC.dll, but this Assembly-CSharp script can.
    /// Run it by entering play mode in an MVC scene and creating the component, then read
    /// <see cref="LastResult"/> via the CLI:
    ///     new UnityEngine.GameObject().AddComponent&lt;GMTK.Mvc.MvcIntegrationPlaytest&gt;();
    ///     return GMTK.Mvc.MvcIntegrationPlaytest.LastResult;
    /// It validates the two APIs the eval spike could not: runtime path building and
    /// override-driving a vehicle. Note: the Vehicle.InputsAccess.Set* methods are STATIC and
    /// take the target vehicle as the first argument.
    /// </summary>
    public class MvcIntegrationPlaytest : MonoBehaviour
    {
        public static string LastResult = "(not run yet)";
        public static bool testPathBuild = false; // isolate: path building disabled by default

        private StringBuilder sb;
        private bool pass;

        private void Check(string name, bool ok, string detail = "")
        {
            if (!ok) pass = false;
            sb.Append(ok ? "[OK] " : "[FAIL] ").Append(name);
            if (detail.Length > 0) sb.Append(" (").Append(detail).Append(')');
            sb.Append("; ");
        }

        private void Start() => StartCoroutine(Run());

        private IEnumerator Run()
        {
            sb = new StringBuilder();
            pass = true;

            // 1) runtime AI path building — TEMPORARILY DISABLED to isolate a native crash.
            //    Set testPathBuild = true to re-enable once the drive test is confirmed safe.
            if (testPathBuild)
            {
                var pts = new List<Vector3>
                {
                    new Vector3(0, 0, 0), new Vector3(0, 0, 30),
                    new Vector3(30, 0, 30), new Vector3(30, 0, 0)
                };
                var path = MvcAiPathBuilder.BuildPath("GMTK_TestPath", pts, true, 8f);
                Check("path built", path != null);
                if (path != null)
                {
                    Check("path valid", path.IsValid, "curves=" + path.CurvesCount + " spaced=" + path.SpacedPointsCount);
                    Destroy(path.gameObject);
                }
            }

            // 2) override-drive an existing demo vehicle and confirm it accelerates
            var mgr = ToolkitBehaviour.Manager;
            Vehicle v = mgr != null ? mgr.PlayerVehicle : null;
            if (v == null && mgr != null && mgr.ActiveVehicles != null && mgr.ActiveVehicles.Length > 0)
                v = mgr.ActiveVehicles[0];
            Check("vehicle found", v != null);
            if (v != null)
            {
                Vehicle.InputsAccess.SetOverrideInputs(v, true);
                Vehicle.InputsAccess.SetFuelPedal(v, 1f);
                Vehicle.InputsAccess.SetBrakePedal(v, 0f);
                Vehicle.InputsAccess.SetHandbrake(v, 0f);
                float s0 = v.Rigidbody != null ? v.Rigidbody.linearVelocity.magnitude : -1f;
                float t = Time.time;
                while (Time.time - t < 4f) yield return null;
                float s1 = v.Rigidbody != null ? v.Rigidbody.linearVelocity.magnitude : -1f;
                Check("override drive accelerates", s1 > s0 + 1f, "s0=" + s0.ToString("F1") + " s1=" + s1.ToString("F1"));
                Vehicle.InputsAccess.SetOverrideInputs(v, false);
            }

            LastResult = (pass ? "PASS" : "FAIL") + " | " + sb;
            Debug.Log("GMTK-MVC-TEST: " + LastResult);
            yield break;
        }
    }
}
