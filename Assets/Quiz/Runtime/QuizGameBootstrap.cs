using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Gmtk2026.Quiz
{
    public static class QuizGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureQuizSystem()
        {
            EnsureCamera();

            QuizSessionController controller =
                Object.FindFirstObjectByType<QuizSessionController>(FindObjectsInactive.Include);

            if (controller == null)
            {
                GameObject root = new GameObject("QuizSystem");
                Object.DontDestroyOnLoad(root);
                controller = root.AddComponent<QuizSessionController>();
            }

            QuizUiPresenter presenter = controller.GetComponent<QuizUiPresenter>();
            if (presenter == null)
            {
                presenter = controller.gameObject.AddComponent<QuizUiPresenter>();
            }

            presenter.Build(controller);
            EnsureEventSystem();
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
            {
                return;
            }

            Camera existingCamera =
                Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
            if (existingCamera != null)
            {
                existingCamera.tag = "MainCamera";
                return;
            }

            GameObject cameraObject = new GameObject(
                "QuizFallbackCamera",
                typeof(Camera),
                typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 2f, -7f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.5f, 0f));
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            Object.DontDestroyOnLoad(eventSystem);
        }
    }
}
