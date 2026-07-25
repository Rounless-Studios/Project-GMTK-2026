using UnityEngine;
using MVC;
using MVC.AI;
using MVC.Core;

namespace GMTK.Mvc
{
    /// <summary>
    /// Thin bridge to MVC: the player drives an MVC vehicle via MVC's InputsManager and the AI
    /// drive MVC vehicles via VehicleAIPathFollower along a scene VehicleAIPath. Keeping every
    /// MVC reference here lets the rest of GMTK stay vehicle-agnostic. (Checklist stage 2-3.)
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

        /// <summary>Point an MVC AI car's path follower at the given VehicleAIPath object. Returns true if wired.</summary>
        public static bool SetupAiFollower(GameObject aiCar, GameObject pathObject)
        {
            if (aiCar == null || pathObject == null) return false;
            var follower = aiCar.GetComponent<VehicleAIPathFollower>();
            if (follower == null) follower = aiCar.GetComponentInChildren<VehicleAIPathFollower>(true);
            var path = pathObject.GetComponent<VehicleAIPath>();
            if (follower == null || path == null) return false;
            follower.path = path;
            return true;
        }

        /// <summary>Re-scan the scene so the manager registers runtime-spawned MVC vehicles.</summary>
        public static void RefreshVehicles()
        {
            var mgr = ToolkitBehaviour.Manager;
            if (mgr != null) mgr.RefreshVehicles();
        }
    }
}
