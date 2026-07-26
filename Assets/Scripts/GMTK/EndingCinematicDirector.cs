using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GMTK
{
    /// <summary>
    /// Plays the hospital ending once: sound over black, a waking ceiling POV,
    /// a clear view of the breathless patient's face, a wider room reveal,
    /// and the final end card.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndingCinematicDirector : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Camera cinematicCamera;
        [SerializeField] private GameObject patient;
        [SerializeField] private AudioClip monitorClip;
        [SerializeField] private AudioClip breathingClip;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float blackAudioLeadSeconds = 2f;
        [SerializeField, Min(0.1f)] private float eyeOpenSeconds = 1f;
        [SerializeField, Min(0f)] private float ceilingHoldSeconds;
        [SerializeField, Min(0.1f)] private float patientShotSeconds = 2f;
        [SerializeField, Min(0.1f)] private float wideRoomShotSeconds = 1.5f;
        [SerializeField, Min(0.1f)] private float wideMoveSeconds = 0.6f;
        [SerializeField, Min(0.1f)] private float endCardFadeSeconds = 1.2f;
        [SerializeField, Range(0f, 1f)] private float finalOverlayAlpha = 0.58f;

        [Header("End Card")]
        [SerializeField] private string endTitle = "THE END";
        [SerializeField] private string endSubtitle = "THANK YOU FOR PLAYING";

        [Header("End Card Style")]
        [Tooltip("Leave empty to use the TextMeshPro default font asset.")]
        [SerializeField] private TMP_FontAsset endCardFont;
        [SerializeField, Min(12)] private int endTitleFontSize = 92;
        [SerializeField, Min(12)] private int endSubtitleFontSize = 28;
        [SerializeField] private FontStyles endTitleFontStyle = FontStyles.Bold;
        [SerializeField] private FontStyles endSubtitleFontStyle = FontStyles.Normal;
        [SerializeField] private Color endTextColor = new(0.9f, 0.94f, 0.92f, 1f);
        [SerializeField] private Color endTextOutlineColor = new(0f, 0f, 0f, 0.8f);
        [SerializeField, Range(0f, 0.5f)] private float endTextOutlineWidth = 0.16f;
        [SerializeField] private Vector2 endTitlePosition = new(0f, 45f);
        [SerializeField] private Vector2 endSubtitlePosition = new(0f, -62f);

        private readonly Vector3 ceilingPosition = new(-10.64f, 1.35f, 13.51f);
        private readonly Vector3 ceilingTarget = new(-10.58f, 3.01f, 13.51f);
        private readonly Vector3 patientPosition = new(-10.95f, 2.4f, 13.53f);
        private readonly Vector3 patientTarget = new(-10.95f, 0.85f, 13.53f);
        private readonly Vector3 wideRoomPosition = new(-10.95f, 3.05f, 13.53f);
        private readonly Vector3 wideRoomTarget = new(-10.95f, 0.85f, 13.53f);

        private CanvasGroup blackout;
        private CanvasGroup endCard;
        private AudioSource monitorSource;
        private AudioSource breathingSource;

        private void Awake()
        {
            if (cinematicCamera == null)
                cinematicCamera = Camera.main;

            if (patient == null)
                patient = GameObject.Find("EndingPatient_Brian");

            if (cinematicCamera == null)
            {
                Debug.LogError("[Ending] Main Camera was not found. Ending cinematic cannot play.", this);
                enabled = false;
                return;
            }

            BuildOverlay();
            BuildAudio();

            if (patient != null)
            {
                patient.SetActive(true);
                Animator patientAnimator = patient.GetComponent<Animator>();
                if (patientAnimator != null)
                    patientAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            SetShot(ceilingPosition, ceilingTarget, Vector3.forward, 52f);
            blackout.alpha = 1f;
            endCard.alpha = 0f;
        }

        private IEnumerator Start()
        {
            if (!enabled)
                yield break;

            monitorSource.Play();
            breathingSource.Play();

            yield return Wait(blackAudioLeadSeconds);
            yield return Fade(blackout, 1f, 0f, eyeOpenSeconds);
            yield return Wait(ceilingHoldSeconds);

            yield return Fade(blackout, 0f, 1f, 0.3f);
            SetShot(patientPosition, patientTarget, Vector3.right, 45f);
            yield return Fade(blackout, 1f, 0f, 0.5f);
            yield return Wait(patientShotSeconds);

            float moveDuration = Mathf.Min(wideMoveSeconds, wideRoomShotSeconds);
            yield return MoveCameraToShot(
                wideRoomPosition,
                wideRoomTarget,
                Vector3.right,
                82f,
                moveDuration);
            yield return Wait(Mathf.Max(0f, wideRoomShotSeconds - moveDuration));

            yield return FadeToOverlayAndSilence(finalOverlayAlpha, 1.2f);
            yield return Fade(endCard, 0f, 1f, endCardFadeSeconds);
        }

        private void BuildOverlay()
        {
            var canvasRoot = new GameObject(
                "EndingCinematicOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);

            Canvas canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var blackObject = new GameObject(
                "Blackout",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            blackObject.transform.SetParent(canvasRoot.transform, false);
            Stretch(blackObject.GetComponent<RectTransform>());
            Image blackImage = blackObject.GetComponent<Image>();
            blackImage.color = Color.black;
            blackImage.raycastTarget = false;
            blackout = blackObject.GetComponent<CanvasGroup>();
            blackout.interactable = false;
            blackout.blocksRaycasts = false;

            var endObject = new GameObject(
                "EndCard",
                typeof(RectTransform),
                typeof(CanvasGroup));
            endObject.transform.SetParent(canvasRoot.transform, false);
            Stretch(endObject.GetComponent<RectTransform>());
            endCard = endObject.GetComponent<CanvasGroup>();
            endCard.interactable = false;
            endCard.blocksRaycasts = false;

            CreateText(
                endObject.transform,
                "Title",
                endTitle,
                endTitleFontSize,
                endTitlePosition,
                endTitleFontStyle);
            CreateText(
                endObject.transform,
                "Subtitle",
                endSubtitle,
                endSubtitleFontSize,
                endSubtitlePosition,
                endSubtitleFontStyle);
        }

        private void CreateText(
            Transform parent,
            string objectName,
            string content,
            int fontSize,
            Vector2 anchoredPosition,
            FontStyles fontStyle)
        {
            var textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.35f);
            rect.anchorMax = new Vector2(0.9f, 0.65f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = anchoredPosition;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            TMP_FontAsset resolvedFont = endCardFont != null
                ? endCardFont
                : TMP_Settings.defaultFontAsset;
            if (resolvedFont != null)
                text.font = resolvedFont;

            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAlignmentOptions.Center;
            text.color = endTextColor;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(12, fontSize / 2);
            text.fontSizeMax = fontSize;
            text.outlineColor = endTextOutlineColor;
            text.outlineWidth = endTextOutlineWidth;
            text.raycastTarget = false;
        }

        private void BuildAudio()
        {
            monitorSource = gameObject.AddComponent<AudioSource>();
            monitorSource.playOnAwake = false;
            monitorSource.loop = true;
            monitorSource.spatialBlend = 0f;
            monitorSource.volume = 0.22f;
            monitorSource.priority = 32;

            breathingSource = gameObject.AddComponent<AudioSource>();
            breathingSource.playOnAwake = false;
            breathingSource.loop = true;
            breathingSource.spatialBlend = 0f;
            breathingSource.volume = 0.32f;
            breathingSource.priority = 31;

            monitorSource.clip = monitorClip;
            breathingSource.clip = breathingClip;
        }

        private IEnumerator FadeToOverlayAndSilence(float targetAlpha, float duration)
        {
            float elapsed = 0f;
            float monitorStart = monitorSource.volume;
            float breathingStart = breathingSource.volume;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Smooth(Mathf.Clamp01(elapsed / duration));
                blackout.alpha = Mathf.Lerp(0f, targetAlpha, progress);
                monitorSource.volume = Mathf.Lerp(monitorStart, 0f, progress);
                breathingSource.volume = Mathf.Lerp(breathingStart, 0f, progress);
                yield return null;
            }

            blackout.alpha = targetAlpha;
            monitorSource.Stop();
            breathingSource.Stop();
        }

        private static IEnumerator Fade(
            CanvasGroup group,
            float from,
            float to,
            float duration)
        {
            group.alpha = from;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Smooth(Mathf.Clamp01(elapsed / duration));
                group.alpha = Mathf.LerpUnclamped(from, to, progress);
                yield return null;
            }

            group.alpha = to;
        }

        private static IEnumerator Wait(float duration)
        {
            float endTime = Time.realtimeSinceStartup + duration;
            while (Time.realtimeSinceStartup < endTime)
                yield return null;
        }

        private void SetShot(
            Vector3 position,
            Vector3 target,
            Vector3 screenUp,
            float fieldOfView)
        {
            cinematicCamera.transform.SetPositionAndRotation(
                position,
                LookAtRotation(position, target, screenUp));
            cinematicCamera.fieldOfView = fieldOfView;
        }

        private IEnumerator MoveCameraToShot(
            Vector3 position,
            Vector3 target,
            Vector3 screenUp,
            float fieldOfView,
            float duration)
        {
            Transform cameraTransform = cinematicCamera.transform;
            Vector3 startPosition = cameraTransform.position;
            Quaternion startRotation = cameraTransform.rotation;
            float startFieldOfView = cinematicCamera.fieldOfView;
            Quaternion targetRotation = LookAtRotation(position, target, screenUp);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Smooth(Mathf.Clamp01(elapsed / duration));
                cameraTransform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(startPosition, position, progress),
                    Quaternion.SlerpUnclamped(startRotation, targetRotation, progress));
                cinematicCamera.fieldOfView =
                    Mathf.LerpUnclamped(startFieldOfView, fieldOfView, progress);
                yield return null;
            }

            cameraTransform.SetPositionAndRotation(position, targetRotation);
            cinematicCamera.fieldOfView = fieldOfView;
        }

        private static Quaternion LookAtRotation(
            Vector3 position,
            Vector3 target,
            Vector3 screenUp)
        {
            Vector3 direction = target - position;
            return direction.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(direction.normalized, screenUp)
                : Quaternion.identity;
        }

        private static float Smooth(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

    }
}
