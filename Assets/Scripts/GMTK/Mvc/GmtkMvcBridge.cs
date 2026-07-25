using UnityEngine;
using MVC;
using MVC.Core;

namespace GMTK.Mvc
{
    /// <summary>
    /// Thin bridge to MVC for the player-only hybrid: the player drives an MVC vehicle (via
    /// MVC's InputsManager), while the AI stay on kit cars. Keeping every MVC reference here lets
    /// the rest of GMTK stay vehicle-agnostic. (Checklist stage 2-3, hybrid path.)
    /// </summary>
    public static class GmtkMvcBridge
    {
        /// <summary>Resolve the MVC Vehicle on a car root (or its children).</summary>
        public static Vehicle VehicleOf(GameObject car)
        {
            if (car == null) return null;
            var v = car.GetComponent<Vehicle>();
            return v != null ? v : car.GetComponentInChildren<Vehicle>(true);
        }

        /// <summary>Make the given car the MVC player, driven by the InputsManager. Returns true if wired.</summary>
        public static bool SetPlayerVehicle(GameObject car)
        {
            var v = VehicleOf(car);
            var mgr = ToolkitBehaviour.Manager;
            if (v == null || mgr == null) return false;
            mgr.PlayerTarget = v;
            mgr.RefreshPlayer();
            return true;
        }

        /// <summary>True if the car is the current MVC player vehicle.</summary>
        public static bool IsMvcPlayer(GameObject car)
        {
            var v = VehicleOf(car);
            var mgr = ToolkitBehaviour.Manager;
            return v != null && mgr != null && mgr.PlayerVehicle == v;
        }

        /// <summary>True if a live MVC VehicleManager exists in the scene.</summary>
        public static bool HasManager => ToolkitBehaviour.Manager != null;
    }
}
