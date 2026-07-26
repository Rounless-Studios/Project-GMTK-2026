using System.Collections;
using Gmtk2026.GameBalance;
using Gmtk2026.Quiz;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

namespace GMTK
{
    /// <summary>
    /// GDD execution presentation:
    /// - the player's RCCP chase camera remains the main, fully controllable view;
    /// - three seconds before zero, a Cinemachine CCTV inset follows the live last place;
    /// - at zero, EliminationManager revalidates and locks the actual explosion target;
    /// - after the wreck shot, the CCTV inset closes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExecutionCctvDirector : MonoBehaviour
    {
        private const float HideBeforeWreckVanishes = 0.1f;

        [Header("Execution shot")]
        [SerializeField] private Vector3 followOffset = new(0f, 2.4f, -6.2f);
        [SerializeField] private Vector3 targetOffset = new(0f, 1.1f, 0f);
        [SerializeField, Range(20f, 90f)] private float fieldOfView = 45f;

        private Camera executionCamera;
        private CinemachineCamera virtualCamera;
        private CinemachineRotationComposer composer;

        private Camera playerCamera;
        private Rect playerOriginalRect;
        private float playerOriginalDepth;
        private bool playerCameraCaptured;

        private GameObject overlay;
        private GameObject cctvFrame;
        private Text targetLabel;
        private Coroutine cctvViewportRoutine;
        private Coroutine hideRoutine;
        private int shownRaceIndex = -1;
        private bool targetLocked;
        private Transform lockedTargetAnchor;
        private bool quizVisible;
        private QuizSessionController quizSession;

        private CameraSettings CameraConfig => GameBalance.Current.camera;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            GameObject host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<ExecutionCctvDirector>() == null)
                host.AddComponent<ExecutionCctvDirector>();
        }

        private void Awake()
        {
            BuildCameraRig();
            BuildOverlay();
            HideImmediately();
        }

        private void OnEnable()
        {
            EliminationManager.ExecutionTargetChanged += ShowLiveTarget;
            EliminationManager.EliminationTargetLocked += LockTarget;
            EliminationManager.CarEliminated += OnCarEliminated;
            EliminationManager.FinalDuelStarted += OnFinalDuelStarted;
        }

        private void Start()
        {
            quizSession =
                FindFirstObjectByType<QuizSessionController>(FindObjectsInactive.Include);
            if (quizSession != null)
            {
                quizSession.QuestionStarted += OnQuizStarted;
                quizSession.QuizClosed += OnQuizClosed;
                quizVisible = quizSession.State != QuizSessionState.Waiting;
                if (quizVisible)
                    SuspendPresentationForQuiz();
            }

            if (Race.Events != null)
            {
                Race.Events.RaceStartedEvent.AddListener(HideImmediately);
                Race.Events.RestartRaceEvent.AddListener(HideImmediately);
            }
        }

        private void OnDisable()
        {
            HideImmediately();

            EliminationManager.ExecutionTargetChanged -= ShowLiveTarget;
            EliminationManager.EliminationTargetLocked -= LockTarget;
            EliminationManager.CarEliminated -= OnCarEliminated;
            EliminationManager.FinalDuelStarted -= OnFinalDuelStarted;

            if (quizSession != null)
            {
                quizSession.QuestionStarted -= OnQuizStarted;
                quizSession.QuizClosed -= OnQuizClosed;
                quizSession = null;
            }

            if (Race.Events != null)
            {
                Race.Events.RaceStartedEvent.RemoveListener(HideImmediately);
                Race.Events.RestartRaceEvent.RemoveListener(HideImmediately);
            }
        }

        private void ShowLiveTarget(int raceIndex)
        {
            bool presentationWasHidden = shownRaceIndex < 0;
            bool targetChanged = shownRaceIndex != raceIndex;

            if (presentationWasHidden && !quizVisible)
                BeginExecutionPresentation();

            if (targetChanged)
                SetCinemachineTarget(raceIndex);

            shownRaceIndex = raceIndex;
            targetLocked = false;
            UpdateTargetLabel();
        }

        private void LockTarget(int raceIndex)
        {
            if (shownRaceIndex < 0 && !quizVisible)
                BeginExecutionPresentation();

            if (shownRaceIndex != raceIndex)
                SetCinemachineTarget(raceIndex);

            shownRaceIndex = raceIndex;
            targetLocked = true;
            LockCameraAtTarget(raceIndex);
            UpdateTargetLabel();
        }

        private void OnCarEliminated(int raceIndex)
        {
            if (raceIndex != shownRaceIndex)
                return;

            if (hideRoutine != null)
                StopCoroutine(hideRoutine);

            float linger = GameBalance.Current.presentation.wreckLingerSeconds;
            float visibleSeconds = Mathf.Max(0.25f, linger - HideBeforeWreckVanishes);
            hideRoutine = StartCoroutine(HideAfter(visibleSeconds));
        }

        private void OnFinalDuelStarted()
        {
            // The final regular execution can be the one that leaves two racers.
            // Keep its explosion shot alive; only hide when no execution is active.
            if (shownRaceIndex < 0)
                HideImmediately();
        }

        private void BeginExecutionPresentation()
        {
            if (quizVisible)
                return;

            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            CapturePlayerCamera();
            Rect cctvViewport = CameraConfig.executionCctvViewportRect;
            Rect collapsedViewport = CollapseRect(cctvViewport);
            executionCamera.rect = collapsedViewport;
            ConfigureCctvFrame(collapsedViewport);

            executionCamera.enabled = true;
            virtualCamera.enabled = true;
            overlay.SetActive(true);

            if (cctvFrame != null)
                cctvFrame.SetActive(true);

            if (playerCameraCaptured)
                executionCamera.depth = playerOriginalDepth + 1f;
            else
                executionCamera.depth = 100f;

            StartCctvViewportTransition(
                collapsedViewport,
                cctvViewport,
                CameraConfig.executionTransitionSeconds,
                false);
        }

        private void OnQuizStarted(QuizQuestion _)
        {
            quizVisible = true;
            SuspendPresentationForQuiz();
        }

        private void OnQuizClosed()
        {
            quizVisible = false;

            // The purge countdown keeps running during the driving quiz. If its target is still
            // active when the phone closes, restore the CCTV with its latest target.
            if (shownRaceIndex < 0)
                return;

            BeginExecutionPresentation();
            SetCinemachineTarget(shownRaceIndex);
            UpdateTargetLabel();
        }

        private void SuspendPresentationForQuiz()
        {
            if (cctvViewportRoutine != null)
            {
                StopCoroutine(cctvViewportRoutine);
                cctvViewportRoutine = null;
            }

            if (overlay != null)
                overlay.SetActive(false);
            if (executionCamera != null)
                executionCamera.enabled = false;
            if (virtualCamera != null)
                virtualCamera.enabled = false;
        }

        private void SetCinemachineTarget(int raceIndex)
        {
            GameObject car = Race.CarByIndex(raceIndex);
            if (car == null)
                return;

            DestroyLockedTargetAnchor();
            Transform target = car.transform;
            virtualCamera.Follow = target;
            virtualCamera.LookAt = target;
            composer.TargetOffset = targetOffset;

            Vector3 initialPosition = target.TransformPoint(followOffset);
            Quaternion initialRotation = Quaternion.LookRotation(
                target.TransformPoint(targetOffset) - initialPosition,
                Vector3.up);

            virtualCamera.PreviousStateIsValid = false;
            virtualCamera.ForceCameraPosition(initialPosition, initialRotation);
        }

        private void LockCameraAtTarget(int raceIndex)
        {
            GameObject car = Race.CarByIndex(raceIndex);
            if (car == null)
                return;

            DestroyLockedTargetAnchor();
            GameObject anchorObject = new("Execution CCTV Locked Target");
            anchorObject.transform.SetParent(transform, true);
            anchorObject.transform.SetPositionAndRotation(
                car.transform.position,
                car.transform.rotation);
            lockedTargetAnchor = anchorObject.transform;

            virtualCamera.Follow = lockedTargetAnchor;
            virtualCamera.LookAt = lockedTargetAnchor;
            composer.TargetOffset = targetOffset;
        }

        private void DestroyLockedTargetAnchor()
        {
            if (lockedTargetAnchor == null)
                return;
            Destroy(lockedTargetAnchor.gameObject);
            lockedTargetAnchor = null;
        }

        private void CapturePlayerCamera()
        {
            if (playerCameraCaptured && playerCamera != null)
                return;

            playerCamera = ResolvePlayerCamera();
            playerCameraCaptured = playerCamera != null;

            if (!playerCameraCaptured)
                return;

            playerOriginalRect = playerCamera.rect;
            playerOriginalDepth = playerCamera.depth;
        }

        private Camera ResolvePlayerCamera()
        {
            RCCP_Camera rccpCamera =
                FindAnyObjectByType<RCCP_Camera>(FindObjectsInactive.Include);

            if (rccpCamera != null &&
                rccpCamera.actualCamera != null &&
                rccpCamera.actualCamera != executionCamera)
            {
                return rccpCamera.actualCamera;
            }

            Camera main = Camera.main;
            if (main != null && main != executionCamera)
                return main;

            Camera[] cameras = FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Camera candidate in cameras)
            {
                if (candidate != executionCamera && candidate.targetTexture == null)
                    return candidate;
            }

            Debug.LogWarning(
                "Execution CCTV: no player camera was found; showing only the execution camera.",
                this);
            return null;
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            hideRoutine = null;
            BeginReturn();
        }

        private void BeginReturn()
        {
            if (shownRaceIndex < 0)
                return;

            if (executionCamera != null)
            {
                StartCctvViewportTransition(
                    executionCamera.rect,
                    CollapseRect(CameraConfig.executionCctvViewportRect),
                    CameraConfig.executionReturnSeconds,
                    true);
            }
            else
            {
                CompleteHide();
            }
        }

        private void StartCctvViewportTransition(
            Rect from,
            Rect to,
            float duration,
            bool hideWhenComplete)
        {
            if (cctvViewportRoutine != null)
                StopCoroutine(cctvViewportRoutine);

            cctvViewportRoutine = StartCoroutine(
                AnimateCctvViewport(from, to, duration, hideWhenComplete));
        }

        private IEnumerator AnimateCctvViewport(
            Rect from,
            Rect to,
            float duration,
            bool hideWhenComplete)
        {
            if (executionCamera == null)
            {
                cctvViewportRoutine = null;
                if (hideWhenComplete)
                    CompleteHide();
                yield break;
            }

            if (duration <= 0f)
            {
                SetCctvViewport(to);
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration && executionCamera != null)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    t = t * t * (3f - 2f * t);
                    SetCctvViewport(LerpRect(from, to, t));
                    yield return null;
                }

                if (executionCamera != null)
                    SetCctvViewport(to);
            }

            cctvViewportRoutine = null;
            if (hideWhenComplete)
                CompleteHide();
        }

        private void HideImmediately()
        {
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            if (cctvViewportRoutine != null)
            {
                StopCoroutine(cctvViewportRoutine);
                cctvViewportRoutine = null;
            }

            if (playerCameraCaptured && playerCamera != null)
            {
                playerCamera.rect = playerOriginalRect;
                playerCamera.depth = playerOriginalDepth;
            }

            CompleteHide();
        }

        private void CompleteHide()
        {
            shownRaceIndex = -1;
            targetLocked = false;

            if (overlay != null)
                overlay.SetActive(false);

            if (executionCamera != null)
                executionCamera.enabled = false;

            if (virtualCamera != null)
            {
                virtualCamera.enabled = false;
                virtualCamera.Follow = null;
                virtualCamera.LookAt = null;
            }

            DestroyLockedTargetAnchor();
            playerCamera = null;
            playerCameraCaptured = false;
        }

        private void BuildCameraRig()
        {
            GameObject cameraObject = new(
                "Execution CCTV Output Camera",
                typeof(Camera),
                typeof(CinemachineBrain));
            cameraObject.transform.SetParent(transform, false);

            executionCamera = cameraObject.GetComponent<Camera>();
            executionCamera.rect = new Rect(0f, 0f, 1f, 1f);
            executionCamera.clearFlags = CameraClearFlags.Skybox;
            executionCamera.nearClipPlane = 0.1f;
            executionCamera.farClipPlane = 2000f;
            executionCamera.allowHDR = true;
            executionCamera.enabled = false;

            CinemachineBrain brain = cameraObject.GetComponent<CinemachineBrain>();
            brain.ChannelMask = OutputChannels.Channel01;
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.Cut,
                0f);

            GameObject virtualCameraObject = new("Execution CCTV Cinemachine Camera");
            virtualCameraObject.transform.SetParent(transform, false);

            virtualCamera = virtualCameraObject.AddComponent<CinemachineCamera>();
            virtualCamera.OutputChannel = OutputChannels.Channel01;
            virtualCamera.Priority = 100;

            LensSettings lens = virtualCamera.Lens;
            lens.FieldOfView = fieldOfView;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 2000f;
            virtualCamera.Lens = lens;

            CinemachineFollow follow =
                virtualCameraObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = followOffset;

            composer =
                virtualCameraObject.AddComponent<CinemachineRotationComposer>();
            composer.TargetOffset = targetOffset;
            composer.Damping = new Vector2(0.2f, 0.2f);

            virtualCamera.enabled = false;
        }

        private void BuildOverlay()
        {
            GameObject canvasObject = new(
                "Execution CCTV Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            overlay = CreateUiObject("Execution CCTV Overlay", canvasObject.transform);
            Stretch(overlay.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            cctvFrame = CreateUiObject("Execution CCTV Frame", overlay.transform);
            AddFrameBorder(cctvFrame.transform, 5f);

            GameObject cctvHeader =
                CreateUiObject("CCTV Header", cctvFrame.transform, typeof(Image));
            RectTransform cctvHeaderRect = cctvHeader.GetComponent<RectTransform>();
            cctvHeaderRect.anchorMin = new Vector2(0f, 1f);
            cctvHeaderRect.anchorMax = new Vector2(1f, 1f);
            cctvHeaderRect.pivot = new Vector2(0.5f, 1f);
            cctvHeaderRect.anchoredPosition = new Vector2(0f, -5f);
            cctvHeaderRect.sizeDelta = new Vector2(-10f, 28f);
            cctvHeader.GetComponent<Image>().color =
                new Color(0.02f, 0.02f, 0.02f, 0.84f);

            Text header = CreateLabel("CCTV Label", cctvHeader.transform);
            Stretch(header.rectTransform, 8f, 0f, 8f, 0f);
            header.fontSize = 15;
            header.alignment = TextAnchor.MiddleLeft;
            header.color = new Color(1f, 0.22f, 0.18f);
            header.text = "● LIVE // EXECUTION CCTV";

            targetLabel = CreateLabel("Target", cctvHeader.transform);
            Stretch(targetLabel.rectTransform, 8f, 0f, 8f, 0f);
            targetLabel.fontSize = 15;
            targetLabel.alignment = TextAnchor.MiddleRight;
            targetLabel.color = new Color(0.72f, 0.95f, 0.78f);
            targetLabel.text = "CURRENT LAST --";
        }

        private void ConfigureCctvFrame(Rect viewport)
        {
            if (cctvFrame == null)
                return;

            RectTransform rect = cctvFrame.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(viewport.xMin, viewport.yMin);
            rect.anchorMax = new Vector2(viewport.xMax, viewport.yMax);
            rect.offsetMin = new Vector2(4f, 4f);
            rect.offsetMax = new Vector2(-4f, -4f);
        }

        private void SetCctvViewport(Rect viewport)
        {
            executionCamera.rect = viewport;
            ConfigureCctvFrame(viewport);
        }

        private void UpdateTargetLabel()
        {
            if (targetLabel == null)
                return;

            string state = targetLocked ? "LOCKED" : "CURRENT LAST";
            targetLabel.text = $"{state}  {shownRaceIndex + 1:00}";
        }

        private static Rect LerpRect(Rect from, Rect to, float t)
        {
            return new Rect(
                Mathf.Lerp(from.x, to.x, t),
                Mathf.Lerp(from.y, to.y, t),
                Mathf.Lerp(from.width, to.width, t),
                Mathf.Lerp(from.height, to.height, t));
        }

        private static Rect CollapseRect(Rect viewport)
        {
            const float collapsedSize = 0.02f;
            float width = Mathf.Min(collapsedSize, viewport.width);
            float height = Mathf.Min(collapsedSize, viewport.height);
            return new Rect(
                viewport.center.x - width * 0.5f,
                viewport.center.y - height * 0.5f,
                width,
                height);
        }

        private static void AddFrameBorder(Transform parent, float thickness)
        {
            Color color = new(0.75f, 0.04f, 0.03f, 0.96f);

            CreateBorderLine(
                "Left", parent,
                new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(thickness, 0f), new Vector2(thickness * 0.5f, 0f),
                color);
            CreateBorderLine(
                "Right", parent,
                new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(thickness, 0f), new Vector2(-thickness * 0.5f, 0f),
                color);
            CreateBorderLine(
                "Bottom", parent,
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, thickness), new Vector2(0f, thickness * 0.5f),
                color);
            CreateBorderLine(
                "Top", parent,
                new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, thickness), new Vector2(0f, -thickness * 0.5f),
                color);
        }

        private static void CreateBorderLine(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 sizeDelta,
            Vector2 anchoredPosition,
            Color color)
        {
            GameObject line = CreateUiObject(name, parent, typeof(Image));
            RectTransform rect = line.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = anchoredPosition;
            line.GetComponent<Image>().color = color;
        }

        private static GameObject CreateUiObject(
            string name,
            Transform parent,
            params System.Type[] components)
        {
            GameObject result = new(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);

            foreach (System.Type component in components)
                result.AddComponent(component);

            return result;
        }

        private static Text CreateLabel(string name, Transform parent)
        {
            GameObject labelObject = CreateUiObject(name, parent, typeof(Text));
            Text label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            return label;
        }

        private static void Stretch(
            RectTransform rect,
            float left,
            float bottom,
            float right,
            float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
