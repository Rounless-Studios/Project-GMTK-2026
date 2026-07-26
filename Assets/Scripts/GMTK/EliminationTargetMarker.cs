using UnityEngine;
using UnityEngine.UI;

namespace GMTK
{
    /// <summary>
    /// Screen-space marker for the currently condemned racer. It follows the car when visible and
    /// clamps to the screen edge as a directional indicator when the target is off camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EliminationTargetMarker : MonoBehaviour
    {
        private RectTransform marker;
        private RectTransform arrow;
        private Image markerBackground;
        private Text label;
        private int targetIndex = -1;
        private EliminationWarningLevel level;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            GameObject host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<EliminationTargetMarker>() == null)
                host.AddComponent<EliminationTargetMarker>();
        }

        private void Awake()
        {
            GameObject canvasObject = new(
                "Elimination Marker Canvas",
                typeof(Canvas),
                typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 420;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject markerObject = new(
                "Marked Racer",
                typeof(RectTransform),
                typeof(Image));
            markerObject.transform.SetParent(canvasObject.transform, false);
            marker = markerObject.GetComponent<RectTransform>();
            marker.anchorMin = marker.anchorMax = Vector2.zero;
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(310f, 62f);
            markerBackground = markerObject.GetComponent<Image>();
            markerBackground.color = new Color(0.04f, 0.01f, 0.01f, 0.86f);
            markerBackground.raycastTarget = false;

            GameObject arrowObject = new(
                "Direction",
                typeof(RectTransform),
                typeof(Text));
            arrowObject.transform.SetParent(markerObject.transform, false);
            arrow = arrowObject.GetComponent<RectTransform>();
            arrow.anchorMin = new Vector2(0f, 0.5f);
            arrow.anchorMax = new Vector2(0f, 0.5f);
            arrow.pivot = new Vector2(0.5f, 0.5f);
            arrow.anchoredPosition = new Vector2(28f, 0f);
            arrow.sizeDelta = new Vector2(42f, 42f);
            Text arrowText = arrowObject.GetComponent<Text>();
            arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrowText.fontSize = 32;
            arrowText.fontStyle = FontStyle.Bold;
            arrowText.alignment = TextAnchor.MiddleCenter;
            arrowText.color = new Color(1f, 0.12f, 0.06f);
            arrowText.text = "►";
            arrowText.raycastTarget = false;

            GameObject labelObject = new(
                "Target Identity",
                typeof(RectTransform),
                typeof(Text));
            labelObject.transform.SetParent(markerObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(52f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);
            label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 19;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(1f, 0.12f, 0.06f);
            label.raycastTarget = false;
            markerObject.SetActive(false);
        }

        private void OnEnable()
        {
            EliminationManager.WarningChanged += OnWarningChanged;
            EliminationManager.CarEliminated += OnCarEliminated;
            EliminationManager.FinalDuelStarted += Hide;
        }

        private void OnDisable()
        {
            EliminationManager.WarningChanged -= OnWarningChanged;
            EliminationManager.CarEliminated -= OnCarEliminated;
            EliminationManager.FinalDuelStarted -= Hide;
        }

        private void Update()
        {
            if (targetIndex < 0 || marker == null) return;
            GameObject target = Race.CarByIndex(targetIndex);
            Camera camera = Camera.main;
            if (target == null || camera == null)
            {
                Hide();
                return;
            }

            Vector3 point = camera.WorldToScreenPoint(target.transform.position + Vector3.up * 2f);
            bool behind = point.z <= 0f;
            Vector2 screenCenter = new(Screen.width * 0.5f, Screen.height * 0.5f);
            if (behind)
            {
                point.x = Screen.width - point.x;
                point.y = Screen.height - point.y;
            }

            const float horizontalMargin = 175f;
            const float verticalMargin = 95f;
            bool clamped = behind ||
                           point.x < horizontalMargin ||
                           point.x > Screen.width - horizontalMargin ||
                           point.y < verticalMargin ||
                           point.y > Screen.height - verticalMargin;
            Vector2 unclampedPoint = new(point.x, point.y);
            point.x = Mathf.Clamp(point.x, horizontalMargin, Screen.width - horizontalMargin);
            point.y = Mathf.Clamp(point.y, verticalMargin, Screen.height - verticalMargin);
            marker.position = point;

            float pulseSpeed = level >= EliminationWarningLevel.Intense ? 10f : 5f;
            float alpha = 0.65f + Mathf.PingPong(Time.unscaledTime * pulseSpeed, 0.35f);
            Color color = label.color;
            color.a = alpha;
            label.color = color;
            Color backgroundColor = markerBackground.color;
            backgroundColor.a = 0.62f + alpha * 0.24f;
            markerBackground.color = backgroundColor;

            string identity = targetIndex == 0 ? "YOU" : RaceSessionStats.NameOf(targetIndex);
            label.text = targetIndex == 0
                ? "YOU ARE LAST\nPURGE TARGET"
                : $"{identity} — LAST PLACE\nPURGE TARGET";

            arrow.gameObject.SetActive(true);
            if (clamped)
            {
                Vector2 direction = unclampedPoint - screenCenter;
                if (direction.sqrMagnitude < 0.01f)
                    direction = Vector2.up;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                arrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            else
            {
                // On screen, the arrow points down from the label to the target car.
                arrow.localRotation = Quaternion.Euler(0f, 0f, -90f);
            }
        }

        private void OnWarningChanged(int raceIndex, EliminationWarningLevel warning)
        {
            level = warning;
            if (warning == EliminationWarningLevel.None)
            {
                Hide();
                return;
            }
            targetIndex = raceIndex;
            marker.gameObject.SetActive(true);
        }

        private void OnCarEliminated(int raceIndex)
        {
            if (raceIndex == targetIndex) Hide();
        }

        private void Hide()
        {
            targetIndex = -1;
            level = EliminationWarningLevel.None;
            if (marker != null) marker.gameObject.SetActive(false);
        }
    }
}
