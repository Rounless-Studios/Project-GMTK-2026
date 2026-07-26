using UnityEngine;
using UnityEngine.InputSystem;

namespace GMTK.Rccp
{
    /// <summary>
    /// Unbinds RCCP input actions that GMTK does not use. Shift is the boost key
    /// (<see cref="BoostController"/>) and RCCP binds it to a manual upshift, so both fired at
    /// once; the gearbox runs in automatic mode and never needs the manual shift anyway.
    /// RCCP's action asset lives in a third-party folder, so the bindings are overridden at
    /// runtime instead of being edited out of the asset.
    /// </summary>
    public static class GmtkRccpInputOverrides
    {
        // RCCP may hand out a clone of the asset, which Unity names "RCCP_InputActions(Clone)"
        private const string AssetNamePrefix = "RCCP_InputActions";

        private static readonly string[] UnboundActions = { "Gear Shift Up" };

        /// <summary>
        /// Applies the overrides to every RCCP action asset currently loaded. Cheap enough to
        /// repeat; call it again whenever RCCP may have created a fresh action instance.
        /// </summary>
        public static void Apply()
        {
            InputActionAsset[] assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();

            foreach (InputActionAsset asset in assets)
            {
                if (asset == null || !asset.name.StartsWith(AssetNamePrefix))
                    continue;

                foreach (string actionName in UnboundActions)
                    Unbind(asset.FindAction(actionName));
            }
        }

        /// <summary>
        /// An empty override path leaves the binding in place but bound to nothing, which
        /// survives RCCP enabling the action map again.
        /// </summary>
        private static void Unbind(InputAction action)
        {
            if (action == null)
                return;

            for (int i = 0; i < action.bindings.Count; i++)
                action.ApplyBindingOverride(i, new InputBinding { overridePath = string.Empty });
        }
    }
}
