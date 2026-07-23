using System.Collections;
using UnityEngine;
using SpinMotion;

namespace GMTK
{
    /// <summary>
    /// Blows a car up: cuts its control, launches it with an explosion impulse, spawns a
    /// quick flash + light, then removes the wreck so it doesn't block the track.
    /// Added to a car by EliminationManager when that car is eliminated.
    /// </summary>
    public class CarExplosion : MonoBehaviour
    {
        [Header("Blast")]
        public float upwardForce = 9f;
        public float explosionForce = 1600f;
        public float explosionRadius = 6f;
        public float torque = 25f;
        public float wreckLingerSeconds = 2.5f;

        [Header("Flash")]
        public Color flashColor = new Color(1f, 0.55f, 0.1f);
        public float flashStartScale = 0.5f;
        public float flashEndScale = 6f;
        public float flashDuration = 0.45f;
        public float flashLightRange = 18f;
        public float flashLightIntensity = 30f;

        private bool exploded;

        public void Explode()
        {
            if (exploded) return;
            exploded = true;
            StartCoroutine(ExplodeRoutine());
        }

        private IEnumerator ExplodeRoutine()
        {
            // 1. stop the car from driving itself
            foreach (var ai in GetComponentsInChildren<CarAIControl>()) ai.enabled = false;
            foreach (var user in GetComponentsInChildren<CarUserControl>()) user.enabled = false;

            // 2. physical blast
            Vector3 origin = transform.position - transform.forward * 0.5f;
            foreach (var rb in GetComponentsInChildren<Rigidbody>())
            {
                if (rb == null) continue;
                rb.AddExplosionForce(explosionForce, origin, explosionRadius, upwardForce, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * torque, ForceMode.Impulse);
            }

            // 3. visual flash
            SpawnFlash(transform.position + Vector3.up * 0.5f);

            // 4. remove the wreck after a moment
            yield return new WaitForSeconds(wreckLingerSeconds);
            gameObject.SetActive(false);
        }

        private void SpawnFlash(Vector3 position)
        {
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "GMTK_ExplosionFlash";
            var col = flash.GetComponent<Collider>();
            if (col != null) Destroy(col);
            flash.transform.position = position;
            flash.transform.localScale = Vector3.one * flashStartScale;

            var renderer = flash.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", flashColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", flashColor);
            renderer.material = mat;

            var light = flash.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = flashColor;
            light.range = flashLightRange;
            light.intensity = flashLightIntensity;

            var anim = flash.AddComponent<ExplosionFlashAnim>();
            anim.startScale = flashStartScale;
            anim.endScale = flashEndScale;
            anim.duration = flashDuration;
            anim.startIntensity = flashLightIntensity;
        }
    }

    /// <summary>Expands and fades the explosion flash sphere, then destroys it.</summary>
    public class ExplosionFlashAnim : MonoBehaviour
    {
        public float startScale = 0.5f;
        public float endScale = 6f;
        public float duration = 0.45f;
        public float startIntensity = 30f;

        private float life;
        private Renderer rend;
        private Light flashLight;

        private void Awake()
        {
            rend = GetComponent<Renderer>();
            flashLight = GetComponent<Light>();
        }

        private void Update()
        {
            life += Time.deltaTime;
            float t = Mathf.Clamp01(life / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            if (flashLight != null) flashLight.intensity = Mathf.Lerp(startIntensity, 0f, t);
            if (rend != null && rend.material.HasProperty("_BaseColor"))
            {
                var c = rend.material.GetColor("_BaseColor");
                c.a = Mathf.Lerp(1f, 0f, t);
                rend.material.SetColor("_BaseColor", c);
            }
            if (life >= duration) Destroy(gameObject);
        }
    }
}
