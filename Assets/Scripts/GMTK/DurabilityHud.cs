using Gmtk2026.GameBalance;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GMTK
{
    /// <summary>
    /// Updates the durability UI serialized under BxB's existing SpeedMeter Canvas.
    /// In MVC demo scenes several selectable vehicles stay active, so the readout
    /// follows the durability controller nearest to the third-person camera.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DurabilityHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;

        private DurabilityController target;
        private float nextRefreshAt;

        private void Update()
        {
            if (Time.unscaledTime >= nextRefreshAt)
            {
                nextRefreshAt = Time.unscaledTime + 0.5f;
                RefreshTarget();
            }

            if (target == null || valueLabel == null || fill == null) return;
            float maximum = Mathf.Max(1f, GameBalance.Current.damage.maximumDurability);
            float ratio = Mathf.Clamp01(target.Durability / maximum);
            Vector2 anchors = fill.anchorMax;
            anchors.x = ratio;
            fill.anchorMax = anchors;
            valueLabel.text = $"DUR {Mathf.CeilToInt(target.Durability):000}";

            if (fillImage != null)
            {
                fillImage.color = ratio <= 0.3f
                    ? new Color(0.92f, 0.16f, 0.14f, 1f)
                    : ratio <= 0.6f
                        ? new Color(1f, 0.62f, 0.08f, 1f)
                        : new Color(0.12f, 0.82f, 0.44f, 1f);
            }
        }

        private void RefreshTarget()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            DurabilityController closest = null;
            float closestDistance = float.PositiveInfinity;
            foreach (DurabilityController candidate in
                     FindObjectsByType<DurabilityController>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                float distance =
                    (candidate.transform.position - camera.transform.position).sqrMagnitude;
                if (distance >= closestDistance) continue;
                closest = candidate;
                closestDistance = distance;
            }
            target = closest;
        }

    }
}
