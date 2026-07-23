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
        public float upwardForce = 9f;
        public float explosionForce = 1600f;
        public float torque = 25f;
        public float wreckLingerSeconds = 2.5f;

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
            var bodies = GetComponentsInChildren<Rigidbody>();
            Vector3 origin = transform.position - transform.forward * 0.5f;
            foreach (var rb in bodies)
            {
                if (rb == null) continue;
                rb.AddExplosionForce(explosionForce, origin, 6f, upwardForce, ForceMode.Impulse);
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
            flash.transform.localScale = Vector3.one * 0.5f;

            var renderer = flash.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            var color = new Color(1f, 0.55f, 0.1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            renderer.material = mat;

            var light = flash.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 18f;
            light.intensity = 30f;

            flash.AddComponent<ExplosionFlashAnim>();
        }
    }

    /// <summary>Expands and fades the explosion flash sphere, then destroys it.</summary>
    public class ExplosionFlashAnim : MonoBehaviour
    {
        private float life;
        private const float Duration = 0.45f;
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
            float t = Mathf.Clamp01(life / Duration);
            transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 6f, t);
            if (flashLight != null) flashLight.intensity = Mathf.Lerp(30f, 0f, t);
            if (rend != null && rend.material.HasProperty("_BaseColor"))
            {
                var c = rend.material.GetColor("_BaseColor");
                c.a = Mathf.Lerp(1f, 0f, t);
                rend.material.SetColor("_BaseColor", c);
            }
            if (life >= Duration) Destroy(gameObject);
        }
    }
}
