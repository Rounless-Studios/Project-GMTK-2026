using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GMTK.Rendering
{
    /// <summary>
    /// URP never renders the legacy <see cref="LensFlare"/> component, and RCC Pro Lite's
    /// precompiled runtime only knows that legacy component: its editor adds a
    /// LensFlareComponentSRP with intensity 0 that nothing ever raises. This bridge mirrors every
    /// legacy flare found under this object onto an SRP flare driven by the sibling light, so
    /// headlights, brake lights and indicators glow under URP without editing RCC's prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    public class LegacyLensFlareUrpBridge : MonoBehaviour
    {
        [Tooltip("Flare shape used for every mirrored light. Required, otherwise nothing renders.")]
        [SerializeField] private LensFlareDataSRP flareData;

        [Tooltip("Light intensity that maps to a fully bright flare.")]
        [SerializeField] private float referenceLightIntensity = 3f;

        [Tooltip("Flare intensity at (or above) the reference light intensity.")]
        [SerializeField] private float maxFlareIntensity = 0.6f;

        [Tooltip("SRP flares only render in the post-processing pass, so the camera needs it on.")]
        [SerializeField] private bool ensureCameraPostProcessing = true;

        private Light[] lights;
        private LensFlareComponentSRP[] srpFlares;

        private void Awake()
        {
            LensFlare[] legacyFlares = GetComponentsInChildren<LensFlare>(true);
            lights = new Light[legacyFlares.Length];
            srpFlares = new LensFlareComponentSRP[legacyFlares.Length];

            for (int i = 0; i < legacyFlares.Length; i++)
            {
                GameObject host = legacyFlares[i].gameObject;

                // Keep the legacy data intact (it is what RCC drives) but stop it from ticking.
                legacyFlares[i].enabled = false;
                lights[i] = host.GetComponent<Light>();

                LensFlareComponentSRP srpFlare = host.GetComponent<LensFlareComponentSRP>();

                if (srpFlare == null)
                    srpFlare = host.AddComponent<LensFlareComponentSRP>();

                srpFlare.lensFlareData = flareData;
                srpFlare.attenuationByLightShape = false;
                srpFlare.intensity = 0f;
                srpFlares[i] = srpFlare;
            }
        }

        private void Start()
        {
            if (!ensureCameraPostProcessing)
                return;

            Camera main = Camera.main;

            if (main == null)
                return;

            UniversalAdditionalCameraData cameraData = main.GetUniversalAdditionalCameraData();

            if (cameraData != null)
                cameraData.renderPostProcessing = true;
        }

        private void LateUpdate()
        {
            for (int i = 0; i < srpFlares.Length; i++)
            {
                Light light = lights[i];
                float litIntensity = light != null && light.enabled && light.gameObject.activeInHierarchy
                    ? light.intensity
                    : 0f;

                srpFlares[i].intensity = Mathf.Clamp01(litIntensity / referenceLightIntensity) * maxFlareIntensity;
            }
        }
    }
}
