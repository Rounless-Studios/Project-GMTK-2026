using UnityEngine;

namespace Gmtk2026.Quiz
{
    // BxB's third-person camera also moves during LateUpdate. Run after ordinary
    // camera scripts so the phone never follows the previous frame's camera pose.
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class WorldSpaceQuizCanvasFollower : MonoBehaviour
    {
        private const float HiddenForwardDistance = 1.2f;
        private const float HiddenHorizontalOffset = 2.7f;
        private const float HiddenVerticalOffset = -2.8f;

        // Keep the phone in the camera's right-side safe area. The former position
        // (3.2m forward, 0.62m right) sat directly over a third-person vehicle.
        [SerializeField, Min(0.5f)] private float distance = 2.4f;
        [SerializeField] private float horizontalOffset = 1.45f;
        [SerializeField] private float verticalOffset = 0.32f;
        [SerializeField, Min(0.05f)] private float showDuration = 0.28f;
        [SerializeField, Min(0.05f)] private float hideDuration = 0.20f;

        private Canvas targetCanvas;
        private Camera targetCamera;
        private Renderer[] phoneRenderers;
        private bool isVisible;
        private bool visualsEnabled;
        private bool visualStateInitialized;
        private float presentation;

        public Camera TargetCamera => targetCamera;
        public float Distance => distance;
        public float Presentation => presentation;

        public void Configure(Canvas canvas)
        {
            targetCanvas = canvas;
            phoneRenderers = GetComponentsInChildren<Renderer>(true);
            RefreshCamera();
            SnapToCamera();
            SetVisualsEnabled(isVisible);
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            if (visible)
            {
                SetVisualsEnabled(true);
            }

            if (targetCanvas != null)
            {
                targetCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>().enabled = visible;
            }
        }

        private void OnEnable()
        {
            Application.onBeforeRender += ApplyPoseBeforeRender;
            RefreshCamera();
            SnapToCamera();
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= ApplyPoseBeforeRender;
        }

        private void LateUpdate()
        {
            if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            {
                RefreshCamera();
            }

            if (targetCamera == null)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            presentation = AdvancePresentation(
                presentation,
                isVisible,
                deltaTime,
                showDuration,
                hideDuration);

            if (!isVisible && presentation <= 0f)
            {
                SetVisualsEnabled(false);
            }

            ApplyPose();
        }

        private void ApplyPoseBeforeRender()
        {
            // Camera packages can finalize damping after another LateUpdate callback.
            // Applying the same camera-relative pose immediately before rendering
            // removes the one-frame discrepancy without adding follow smoothing.
            if (isActiveAndEnabled)
            {
                ApplyPose();
            }
        }

        private void ApplyPose()
        {
            if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            {
                RefreshCamera();
            }

            if (targetCamera == null) return;

            float easedPresentation = Mathf.SmoothStep(0f, 1f, presentation);
            Transform cameraTransform = targetCamera.transform;
            Vector3 shownPosition = GetTargetPosition(
                cameraTransform,
                distance,
                horizontalOffset,
                verticalOffset);
            Vector3 hiddenPosition = GetTargetPosition(
                cameraTransform,
                HiddenForwardDistance,
                HiddenHorizontalOffset,
                HiddenVerticalOffset);
            Vector3 targetPosition = Vector3.Lerp(hiddenPosition, shownPosition, easedPresentation);

            Quaternion shownRotation =
                cameraTransform.rotation * Quaternion.Euler(2f, -7f, -3f);
            Quaternion hiddenRotation =
                cameraTransform.rotation * Quaternion.Euler(28f, -48f, -18f);
            Quaternion targetRotation =
                Quaternion.Slerp(hiddenRotation, shownRotation, easedPresentation);

            // Keep the phone locked to the moving camera. Only the presentation
            // progress is animated, avoiding a second follow-lag that causes jitter.
            transform.SetPositionAndRotation(targetPosition, targetRotation);
        }

        public void SnapToCamera()
        {
            if (targetCamera == null)
            {
                RefreshCamera();
            }

            if (targetCamera == null)
            {
                return;
            }

            Transform cameraTransform = targetCamera.transform;
            Vector3 position = isVisible
                ? GetTargetPosition(cameraTransform, distance, horizontalOffset, verticalOffset)
                : GetTargetPosition(
                    cameraTransform,
                    HiddenForwardDistance,
                    HiddenHorizontalOffset,
                    HiddenVerticalOffset);
            Quaternion rotation = isVisible
                ? cameraTransform.rotation * Quaternion.Euler(2f, -7f, -3f)
                : cameraTransform.rotation * Quaternion.Euler(28f, -48f, -18f);
            transform.SetPositionAndRotation(
                position,
                rotation);
        }

        public static Vector3 GetTargetPosition(
            Transform cameraTransform,
            float forwardDistance,
            float rightOffset,
            float upOffset)
        {
            return cameraTransform.position +
                   cameraTransform.forward * forwardDistance +
                   cameraTransform.right * rightOffset +
                   cameraTransform.up * upOffset;
        }

        public static float AdvancePresentation(
            float current,
            bool visible,
            float deltaTime,
            float showDuration,
            float hideDuration)
        {
            float target = visible ? 1f : 0f;
            float duration = visible ? showDuration : hideDuration;
            float speed = 1f / Mathf.Max(0.05f, duration);
            return Mathf.MoveTowards(
                Mathf.Clamp01(current),
                target,
                Mathf.Max(0f, deltaTime) * speed);
        }

        private void RefreshCamera()
        {
            targetCamera = Camera.main;
            if (targetCanvas != null)
            {
                targetCanvas.worldCamera = targetCamera;
            }
        }

        private void SetVisualsEnabled(bool enabled)
        {
            if (visualStateInitialized && visualsEnabled == enabled)
            {
                return;
            }

            visualStateInitialized = true;
            visualsEnabled = enabled;
            if (phoneRenderers != null)
            {
                foreach (Renderer phoneRenderer in phoneRenderers)
                {
                    if (phoneRenderer != null)
                    {
                        phoneRenderer.enabled = enabled;
                    }
                }
            }

            if (targetCanvas != null)
            {
                targetCanvas.enabled = enabled;
            }
        }
    }
}
