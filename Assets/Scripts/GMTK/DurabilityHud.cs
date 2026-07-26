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

        private DurabilityController target;
        private Image fillImage;
        private float nextRefreshAt;

        private void Awake()
        {
            if (fill != null)
                fillImage = fill.GetComponent<Image>();
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextRefreshAt)
            {
                nextRefreshAt = Time.unscaledTime + 0.5f;
                RefreshTarget();
            }

            if (target == null || fillImage == null) return;
            // the car's own ceiling, so an AI bonus cannot push the bar past full
            float maximum = Mathf.Max(1f, target.State != null
                ? target.State.MaximumDurability
                : GameBalance.Current.damage.maximumDurability);
            float ratio = Mathf.Clamp01(target.Durability / maximum);
            fillImage.fillAmount = ratio;

            if (valueLabel != null)
                valueLabel.text = $"DUR {Mathf.CeilToInt(target.Durability):000}";
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
