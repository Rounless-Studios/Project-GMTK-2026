using UnityEngine;
using Utilities.Inputs;

namespace Gmtk2026
{
    /// <summary>
    /// Owns the lifecycle of BxB Studio's static InputsManager.
    /// MVC reads this manager from its vehicle, camera, and demo behaviours but
    /// the getting-started scenes do not contain the package sample bootstrap.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    internal sealed class MVCInputsBootstrap : MonoBehaviour
    {
        private static MVCInputsBootstrap instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance != null)
            {
                return;
            }

            GameObject root = new GameObject(nameof(MVCInputsBootstrap));
            DontDestroyOnLoad(root);
            instance = root.AddComponent<MVCInputsBootstrap>();
        }

        private void Awake()
        {
            EnsureStarted();
        }

        private void Update()
        {
            // Some MVC initialization paths dispose the package's static state
            // while the scene is being assembled. Recover before any regular
            // MVC Update runs instead of leaving every consumer throwing.
            EnsureStarted();
            InputsManager.Update();
        }

        private static void EnsureStarted()
        {
            if (!InputsManager.Started)
            {
                InputsManager.Start();
            }
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            InputsManager.Dispose();
            instance = null;
        }
    }
}
