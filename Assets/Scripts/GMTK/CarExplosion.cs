using System.Collections;
using System.Collections.Generic;
using Gmtk2026.GameBalance;
using UnityEngine;

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

        [Header("Breakable wreck")]
        [Tooltip("Optional override. When empty, the GameBalance presentation prefab is used.")]
        public GameObject breakableVehiclePrefab;

        [Header("Flash")]
        public Color flashColor = new Color(1f, 0.55f, 0.1f);
        public float flashStartScale = 0.5f;
        public float flashEndScale = 6f;
        public float flashDuration = 0.45f;
        public float flashLightRange = 18f;
        public float flashLightIntensity = 30f;

        private bool exploded;
        private GameObject breakableInstance;
        private readonly List<Renderer> hiddenOriginalRenderers = new();
        private readonly List<RigidbodyState> frozenOriginalBodies = new();
        private readonly List<ColliderState> disabledOriginalColliders = new();

        private struct RigidbodyState
        {
            public Rigidbody body;
            public bool wasKinematic;
        }

        private struct ColliderState
        {
            public Collider collider;
            public bool wasEnabled;
        }

        public void Explode()
        {
            if (exploded) return;
            exploded = true;
            StartCoroutine(ExplodeRoutine());
        }

        private IEnumerator ExplodeRoutine()
        {
            // 1. stop the car from driving itself
            var vehicleAdapter = GetComponent<GmtkVehicleAdapter>();
            if (vehicleAdapter != null) vehicleAdapter.SetControlsEnabled(false);
            FreezeOriginalVehicle();

            // 2. replace the intact shell with the authored breakable Skyline. Its ten body
            // pieces receive independent rigidbodies so the execution visibly tears it apart.
            bool spawnedBreakable = SpawnBreakableWreck();
            if (spawnedBreakable)
                HideOriginalVehicle();

            // The breakable prefab owns three authored explosion particles and starts exactly
            // one at random. Retain the procedural flash as a safe fallback for missing config.
            if (!spawnedBreakable)
                SpawnFlash(transform.position + Vector3.up * 0.5f);

            // 3. remove the wreck after the execution camera has lingered on it.
            yield return new WaitForSeconds(wreckLingerSeconds);
            gameObject.SetActive(false);
        }

        private bool SpawnBreakableWreck()
        {
            GameObject prefab = breakableVehiclePrefab;
            if (prefab == null)
                prefab = GameBalance.Current.presentation.executionBreakableVehiclePrefab;
            if (prefab == null)
            {
                Debug.LogWarning(
                    "Execution explosion: no breakable vehicle prefab is configured.",
                    this);
                return false;
            }

            breakableInstance = Instantiate(prefab, transform.position, transform.rotation);
            breakableInstance.name = $"{prefab.name} (Execution Wreck)";
            breakableInstance.transform.localScale = transform.lossyScale;
            AlignBreakableWreck();

            List<Transform> explosionRoots = new();
            foreach (Transform child in breakableInstance.transform)
            {
                if (child.name.StartsWith("Explosion") &&
                    child.GetComponentInChildren<ParticleSystem>(true) != null)
                {
                    explosionRoots.Add(child);
                    foreach (ParticleSystem particle in
                             child.GetComponentsInChildren<ParticleSystem>(true))
                        particle.Stop(
                            true,
                            ParticleSystemStopBehavior.StopEmittingAndClear);
                    child.gameObject.SetActive(false);
                }
            }

            if (explosionRoots.Count > 0)
            {
                Transform selected = explosionRoots[Random.Range(0, explosionRoots.Count)];
                selected.gameObject.SetActive(true);
                foreach (ParticleSystem particle in
                         selected.GetComponentsInChildren<ParticleSystem>(true))
                    particle.Play(true);
            }

            Vector3 origin = transform.position - transform.forward * 0.5f;
            HashSet<GameObject> physicalPieces = new();
            List<Renderer> pieceRenderers = new();
            foreach (Renderer renderer in
                     breakableInstance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer ||
                    !physicalPieces.Add(renderer.gameObject))
                    continue;
                pieceRenderers.Add(renderer);
            }

            Rigidbody vehicleBody = GetComponentInChildren<Rigidbody>();
            float vehicleMass = vehicleBody != null ? vehicleBody.mass : 1200f;
            float pieceMass = Mathf.Max(20f, vehicleMass / Mathf.Max(1, pieceRenderers.Count));

            foreach (Renderer renderer in pieceRenderers)
            {
                BoxCollider pieceCollider = renderer.gameObject.GetComponent<BoxCollider>();
                if (pieceCollider == null)
                    pieceCollider = renderer.gameObject.AddComponent<BoxCollider>();
                ConfigurePieceCollider(renderer, pieceCollider);

                Rigidbody pieceBody = renderer.gameObject.GetComponent<Rigidbody>();
                if (pieceBody == null)
                    pieceBody = renderer.gameObject.AddComponent<Rigidbody>();
                pieceBody.mass = pieceMass;
                pieceBody.interpolation = RigidbodyInterpolation.Interpolate;
                pieceBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                pieceBody.AddExplosionForce(
                    explosionForce,
                    origin,
                    explosionRadius,
                    upwardForce,
                    ForceMode.Impulse);
                pieceBody.AddTorque(Random.insideUnitSphere * torque, ForceMode.Impulse);
            }

            Debug.Log(
                $"Execution wreck spawned: {pieceRenderers.Count} pieces, " +
                $"{pieceMass:0.#}kg each, particle={GetActiveExplosionName(explosionRoots)}.",
                this);
            Destroy(breakableInstance, wreckLingerSeconds);
            return true;
        }

        private static void ConfigurePieceCollider(Renderer renderer, BoxCollider collider)
        {
            Bounds bounds = renderer.localBounds;
            collider.center = bounds.center;
            collider.size = bounds.size;

            // Model_Bottom spans nearly the complete footprint of the car. A box using its
            // full visual bounds starts partly inside the road and is then trapped between
            // the road and the intact vehicle's collision shell. Preserve its upper face,
            // but pull the collision bottom well above the authored visual bottom.
            if (renderer.gameObject.name.IndexOf(
                    "Bottom",
                    System.StringComparison.OrdinalIgnoreCase) < 0)
                return;

            const float heightScale = 0.35f;
            const float horizontalScale = 0.92f;
            Vector3 reducedSize = bounds.size;
            reducedSize.x *= horizontalScale;
            reducedSize.y = Mathf.Max(0.05f, bounds.size.y * heightScale);
            reducedSize.z *= horizontalScale;

            Vector3 raisedCenter = bounds.center;
            raisedCenter.y += (bounds.size.y - reducedSize.y) * 0.5f;
            collider.center = raisedCenter;
            collider.size = reducedSize;
        }

        private void AlignBreakableWreck()
        {
            if (!TryGetVisualBounds(gameObject, out Bounds originalBounds) ||
                !TryGetVisualBounds(breakableInstance, out Bounds breakableBounds))
                return;

            breakableInstance.transform.position +=
                originalBounds.center - breakableBounds.center;
        }

        private static bool TryGetVisualBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer)
                    continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return found;
        }

        private static string GetActiveExplosionName(List<Transform> explosionRoots)
        {
            foreach (Transform root in explosionRoots)
            {
                if (root.gameObject.activeSelf)
                    return root.name;
            }
            return "none";
        }

        private void HideOriginalVehicle()
        {
            hiddenOriginalRenderers.Clear();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled)
                    continue;
                hiddenOriginalRenderers.Add(renderer);
                renderer.enabled = false;
            }
        }

        private void FreezeOriginalVehicle()
        {
            frozenOriginalBodies.Clear();
            disabledOriginalColliders.Clear();

            // The breakable wreck is spawned at this vehicle's pose. Leaving the invisible
            // original collision shell enabled makes the new debris overlap an immovable
            // kinematic body, with the wide bottom piece suffering the worst jitter.
            foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            {
                if (collider == null)
                    continue;

                disabledOriginalColliders.Add(new ColliderState
                {
                    collider = collider,
                    wasEnabled = collider.enabled,
                });
                collider.enabled = false;
            }

            foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
            {
                if (body == null)
                    continue;

                frozenOriginalBodies.Add(new RigidbodyState
                {
                    body = body,
                    wasKinematic = body.isKinematic,
                });

                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                }
            }
        }

        public void RestoreVehicle()
        {
            if (breakableInstance != null)
                Destroy(breakableInstance);
            breakableInstance = null;

            foreach (Renderer renderer in hiddenOriginalRenderers)
            {
                if (renderer != null)
                    renderer.enabled = true;
            }
            hiddenOriginalRenderers.Clear();

            foreach (ColliderState state in disabledOriginalColliders)
            {
                if (state.collider != null)
                    state.collider.enabled = state.wasEnabled;
            }
            disabledOriginalColliders.Clear();

            foreach (RigidbodyState state in frozenOriginalBodies)
            {
                if (state.body == null)
                    continue;
                state.body.isKinematic = state.wasKinematic;
                if (!state.wasKinematic)
                {
                    state.body.linearVelocity = Vector3.zero;
                    state.body.angularVelocity = Vector3.zero;
                }
            }
            frozenOriginalBodies.Clear();
        }

        private void OnDestroy()
        {
            RestoreVehicle();
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
