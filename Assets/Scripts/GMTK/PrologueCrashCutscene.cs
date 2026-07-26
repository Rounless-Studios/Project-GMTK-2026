using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GMTK
{
    /// <summary>
    /// Deterministic recording performance for example_scene. It uses the truck,
    /// trailer, sport car and camera already authored in that scene.
    /// </summary>
    public sealed class PrologueCrashCutscene : MonoBehaviour
    {
        public const float DurationSeconds = 7.5f;

        private const string DriverAssetPath =
            "Assets/Models/Characters/NPC_Driver/NPC_driver_phone.fbx";
        private const string DriverClipName = "Drive_Phone_Idle";
        private const float ApproachStartSeconds = 0.15f;
        private const float TruckApproachStartSeconds = 3.15f;
        private const float ImpactSeconds = 4.4f;
        private const float CityMusicLeadSeconds = 0.4f;
        private const float MinimumContactGap = 2.5f;
        private const float SportCarApproachDistance = 44f;
        private const float TruckApproachDistance = 15f;
        private const float TruckPostImpactTravel = 0.9f;
        private const float FadeInSeconds = 0.8f;
        private const float FadeOutStartSeconds = 6.35f;
        private const float ImpactDeformationSeconds = 0.22f;
        private const float SportCarForwardVelocity = 10f;
        private const float SportCarSideImpactVelocity = 17f;
        private const float SportCarLaunchVelocity = 9f;
        private const float SportCarReboundVelocity = 4f;
        private const float SportCarSpinRadians = 6.5f;

        private Transform truck;
        private Transform trailer;
        private Transform sportCar;
        private Camera cutsceneCamera;
        private PlayableDirector timelineDirector;
        private Vector3 truckStart;
        private Vector3 trailerStart;
        private Vector3 sportStart;
        private Vector3 truckImpact;
        private Vector3 sportImpact;
        private Quaternion sportStartRotation;
        private float sportGroundSurfaceY;
        private Vector3 collisionPoint;
        private Vector3 sportTravelDirection;
        private Vector3 truckTravelDirection;
        private AudioClip impactClip;
        private AudioClip introMusicClip;
        private AudioClip cityMusicClip;
        private AudioSource introMusicSource;
        private AudioSource cityMusicSource;
        private Transform driverRig;
        private GameObject driverPhonePrefab;
        private Animator driverAnimator;
        private Rigidbody sportCarBody;
        private readonly List<DeformationPart> deformationParts = new();
        private CanvasGroup fade;
        private bool impactPlayed;

        private sealed class DeformationPart
        {
            public Transform Transform;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
            public float Strength;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (SceneManager.GetActiveScene().name != "example_scene") return;
            if (FindFirstObjectByType<PrologueCrashCutscene>() != null) return;
            new GameObject("PrologueCrashCutscene").AddComponent<PrologueCrashCutscene>();
        }

        private IEnumerator Start()
        {
            // Let prefab instances and the Recorder finish their scene-load setup.
            yield return null;

            truck = FindNamedTransform("Truck");
            trailer = FindNamedTransform("Trailer");
            sportCar = FindNamedTransform("SportCar");
            cutsceneCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
#if UNITY_EDITOR
            impactClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sound/SFX_VEH_COLLISION_-09.wav");
            introMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sound/Prologue/Buy Something!.mp3");
            cityMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sound/Prologue/ThisUsedToBeACity.ogg");
            driverPhonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                DriverAssetPath);
#endif

            if (truck == null || sportCar == null || cutsceneCamera == null)
            {
                Debug.LogError(
                    "[Prologue Cutscene] example_scene needs objects named Truck, SportCar and Main Camera.",
                    this);
                enabled = false;
                yield break;
            }

            Vector3 authoredTrailerLocalPosition = Vector3.zero;
            Quaternion authoredTrailerLocalRotation = Quaternion.identity;
            if (trailer != null)
            {
                authoredTrailerLocalPosition =
                    truck.InverseTransformPoint(trailer.position);
                authoredTrailerLocalRotation =
                    Quaternion.Inverse(truck.rotation) * trailer.rotation;
            }

            MakeKinematic(truck);
            MakeKinematic(sportCar);
            if (trailer != null) MakeKinematic(trailer);

            float truckGroundY = truck.position.y;
            sportStart = sportCar.position;
            sportStartRotation = sportCar.rotation;
            sportGroundSurfaceY = TryGetCombinedRenderBounds(sportCar, out Bounds sportBounds)
                ? sportBounds.min.y
                : sportStart.y;

            // The sports car drives normally toward a front-facing camera. The
            // truck waits outside the side of frame and enters shortly before impact.
            sportTravelDirection = HorizontalDirection(
                sportStart,
                sportStart + sportCar.forward,
                Vector3.back);
            truckTravelDirection = Vector3.Cross(Vector3.up, sportTravelDirection).normalized;
            collisionPoint = sportStart + sportTravelDirection * SportCarApproachDistance;
            collisionPoint.y = sportStart.y;

            truck.rotation = Quaternion.LookRotation(truckTravelDirection, Vector3.up);
            if (trailer != null)
                trailer.rotation = truck.rotation * authoredTrailerLocalRotation;

            float contactGap = Mathf.Clamp(
                ProjectedRenderRadius(truck, truckTravelDirection) +
                ProjectedRenderRadius(sportCar, truckTravelDirection),
                MinimumContactGap,
                TruckApproachDistance * 0.8f);

            sportImpact = collisionPoint;
            sportImpact.y = sportStart.y;
            truckImpact = collisionPoint - truckTravelDirection * contactGap;
            truckImpact.y = truckGroundY;
            truckStart = truckImpact - truckTravelDirection * TruckApproachDistance;
            truckStart.y = truckGroundY;
            truck.position = truckStart;

            if (trailer != null)
            {
                trailer.SetParent(truck, true);
                trailer.localPosition = authoredTrailerLocalPosition;
                trailer.localRotation = authoredTrailerLocalRotation;
                trailerStart = trailer.position;
            }

            BuildInteriorShot(sportBounds);
            PrepareSportCarPhysics(sportBounds);
            PrepareCrashDeformation();
            BuildRoadsideMotionReferences();
            fade = BuildFadeOverlay();
            ScheduleMusic();
            BindAndPlayTimeline();
            float startedAt = Time.time;
            while (Time.time - startedAt < DurationSeconds)
            {
                float elapsed = Time.time - startedAt;

                if (!impactPlayed && elapsed >= ImpactSeconds)
                    PlayImpact();

                AnimateVehicles(elapsed);
                AnimateCrashDeformation(elapsed);
                AnimateFade(elapsed);

                yield return null;
            }

            introMusicSource?.Stop();
            cityMusicSource?.Stop();
            timelineDirector?.Stop();
        }

        private void AnimateVehicles(float elapsed)
        {
            float approach = Mathf.InverseLerp(ApproachStartSeconds, ImpactSeconds, elapsed);
            float truckApproach = Mathf.InverseLerp(
                TruckApproachStartSeconds,
                ImpactSeconds,
                elapsed);
            truck.position = Vector3.Lerp(truckStart, truckImpact, truckApproach);
            FollowTruckWithTrailer();

            if (!impactPlayed)
            {
                sportCar.position = Vector3.Lerp(sportStart, sportImpact, approach);
                sportCar.rotation = sportStartRotation;
                return;
            }

            float aftermath = Mathf.InverseLerp(ImpactSeconds, DurationSeconds, elapsed);
            float reaction = 1f - Mathf.Pow(1f - aftermath, 3f);
            truck.position = Vector3.Lerp(
                truckImpact,
                truckImpact + truckTravelDirection * TruckPostImpactTravel,
                reaction);
            FollowTruckWithTrailer();
        }

        private void BuildInteriorShot(Bounds carBounds)
        {
            float carHeight = Mathf.Max(1f, carBounds.size.y);
            float halfWidth = Mathf.Max(
                0.8f,
                ProjectedRenderRadius(sportCar, truckTravelDirection));
            float halfLength = Mathf.Max(
                1.5f,
                ProjectedRenderRadius(sportCar, sportTravelDirection));

            Vector3 cabinCenter =
                carBounds.center +
                sportTravelDirection * (halfLength * 0.08f) +
                Vector3.up * (carHeight * 0.03f);
            Vector3 driverCenter =
                cabinCenter -
                truckTravelDirection * (halfWidth * 0.30f);
            BuildNpcDriver(driverCenter, carHeight);
        }

        private void BuildNpcDriver(Vector3 driverCenter, float carHeight)
        {
            if (driverPhonePrefab == null)
            {
                Debug.LogError(
                    $"[Prologue Cutscene] Required driver model is missing: {DriverAssetPath}",
                    this);
                return;
            }

            driverRig = Instantiate(driverPhonePrefab).transform;
            driverRig.name = "NPC_driver_Phone";
            driverRig.SetParent(sportCar, true);
            driverRig.position = driverCenter;
            driverRig.rotation = Quaternion.LookRotation(sportTravelDirection, Vector3.up);

            if (TryGetCombinedRenderBounds(driverRig, out Bounds driverBounds))
            {
                float desiredHeight = carHeight * 0.78f;
                if (driverBounds.size.y > 0.001f)
                    driverRig.localScale *= desiredHeight / driverBounds.size.y;

                if (TryGetCombinedRenderBounds(driverRig, out driverBounds))
                    driverRig.position += driverCenter - driverBounds.center;
            }

            driverAnimator = driverRig.GetComponentInChildren<Animator>(true);
            if (driverAnimator == null)
            {
                Debug.LogError(
                    "[Prologue Cutscene] NPC_driver_Phone Animator could not be loaded.",
                    this);
            }
        }

        private void BindAndPlayTimeline()
        {
            timelineDirector = FindFirstObjectByType<PlayableDirector>();
            if (timelineDirector == null || timelineDirector.playableAsset == null)
            {
                Debug.LogError(
                    "[Prologue Cutscene] The scene needs a Prologue Timeline PlayableDirector.",
                    this);
                return;
            }

            bool driverTrackBound = false;
            foreach (PlayableBinding output in timelineDirector.playableAsset.outputs)
            {
                if (output.streamName != "NPC Driver Phone") continue;
                if (driverAnimator != null)
                    timelineDirector.SetGenericBinding(output.sourceObject, driverAnimator);
                driverTrackBound = driverAnimator != null;
                break;
            }

            if (!driverTrackBound)
            {
                Debug.LogError(
                    $"[Prologue Cutscene] Timeline track 'NPC Driver Phone' ({DriverClipName}) binding failed.",
                    this);
            }

            timelineDirector.time = 0d;
            timelineDirector.Play();
        }

        private void PrepareSportCarPhysics(Bounds carBounds)
        {
            foreach (Collider existingCollider in sportCar.GetComponentsInChildren<Collider>(true))
                existingCollider.enabled = false;

            BoxCollider crashCollider = sportCar.gameObject.AddComponent<BoxCollider>();
            crashCollider.center = sportCar.InverseTransformPoint(carBounds.center);
            Vector3 scale = sportCar.lossyScale;
            crashCollider.size = new Vector3(
                carBounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                carBounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
                carBounds.size.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)));

            sportCarBody = sportCar.GetComponent<Rigidbody>();
            if (sportCarBody == null)
                sportCarBody = sportCar.gameObject.AddComponent<Rigidbody>();

            sportCarBody.mass = 1200f;
            sportCarBody.useGravity = true;
            sportCarBody.isKinematic = true;
            sportCarBody.linearDamping = 0.12f;
            sportCarBody.angularDamping = 0.08f;
            sportCarBody.interpolation = RigidbodyInterpolation.Interpolate;
            sportCarBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void PrepareCrashDeformation()
        {
            CacheDeformationPart(FindDescendantExact(sportCar, "Body"), 0.35f);
            CacheDeformationPart(FindDescendantExact(sportCar, "Front_Hood"), 0.85f);

            Transform leftDoor = FindDescendantExact(sportCar, "Left_Door");
            Transform rightDoor = FindDescendantExact(sportCar, "Right_Door");
            Transform impactDoor = SelectImpactSide(leftDoor, rightDoor);
            if (impactDoor != null)
            {
                CacheDeformationPart(impactDoor, 1f);
                string windowName = impactDoor.name.StartsWith("Left")
                    ? "Left_Door_Window"
                    : "Right_Door_Window";
                CacheDeformationPart(FindDescendantExact(sportCar, windowName), 0.75f);
            }
        }

        private Transform SelectImpactSide(Transform first, Transform second)
        {
            if (first == null) return second;
            if (second == null) return first;
            float firstSide = Vector3.Dot(
                first.position - sportCar.position,
                truckTravelDirection);
            float secondSide = Vector3.Dot(
                second.position - sportCar.position,
                truckTravelDirection);
            return firstSide <= secondSide ? first : second;
        }

        private void CacheDeformationPart(Transform part, float strength)
        {
            if (part == null) return;
            deformationParts.Add(new DeformationPart
            {
                Transform = part,
                LocalPosition = part.localPosition,
                LocalRotation = part.localRotation,
                LocalScale = part.localScale,
                Strength = strength
            });
        }

        private void AnimateCrashDeformation(float elapsed)
        {
            if (!impactPlayed || deformationParts.Count == 0) return;

            float progress = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    ImpactSeconds,
                    ImpactSeconds + ImpactDeformationSeconds,
                    elapsed));

            foreach (DeformationPart part in deformationParts)
            {
                if (part.Transform == null) continue;
                float amount = progress * part.Strength;
                Vector3 worldInset =
                    truckTravelDirection * (0.22f * amount) -
                    Vector3.up * (0.04f * amount);
                Vector3 localInset = part.Transform.parent != null
                    ? part.Transform.parent.InverseTransformVector(worldInset)
                    : worldInset;

                part.Transform.localPosition = part.LocalPosition + localInset;
                part.Transform.localRotation =
                    part.LocalRotation *
                    Quaternion.Euler(-7f * amount, 10f * amount, 15f * amount);
                part.Transform.localScale = Vector3.Scale(
                    part.LocalScale,
                    new Vector3(
                        1f - 0.18f * amount,
                        1f - 0.10f * amount,
                        1f - 0.24f * amount));
            }
        }

        private void BuildRoadsideMotionReferences()
        {
            float halfWidth = Mathf.Max(
                0.8f,
                ProjectedRenderRadius(sportCar, truckTravelDirection));
            Transform markers = new GameObject("PrologueRoadsideMarkers").transform;
            markers.SetParent(transform, false);

            Material postMaterial = CreateSetDressingMaterial(
                "Prologue Roadside Post",
                new Color(0.18f, 0.2f, 0.22f));
            Material reflectorMaterial = CreateSetDressingMaterial(
                "Prologue Roadside Reflector",
                new Color(1f, 0.38f, 0.05f));

            for (int i = 0; i < 14; i++)
            {
                Vector3 basePosition =
                    sportStart +
                    sportTravelDirection * (2f + i * 3f) -
                    truckTravelDirection * (halfWidth + 1.35f);
                basePosition.y = sportGroundSurfaceY;

                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"RoadsidePost_{i:00}",
                    basePosition + Vector3.up * 0.55f,
                    new Vector3(0.14f, 1.1f, 0.14f),
                    postMaterial,
                    markers);
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"RoadsideReflector_{i:00}",
                    basePosition +
                    Vector3.up * 0.82f +
                    truckTravelDirection * 0.075f,
                    new Vector3(0.25f, 0.14f, 0.06f),
                    reflectorMaterial,
                    markers);
            }
        }

        private CanvasGroup BuildFadeOverlay()
        {
            var root = new GameObject(
                "PrologueFade",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            root.transform.SetParent(transform, false);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            var black = new GameObject(
                "Black",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            black.transform.SetParent(root.transform, false);
            RectTransform rect = black.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = black.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        private void AnimateFade(float elapsed)
        {
            if (fade == null) return;

            if (elapsed < FadeInSeconds)
            {
                float progress = Mathf.Clamp01(elapsed / FadeInSeconds);
                fade.alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);
                return;
            }

            if (elapsed >= FadeOutStartSeconds)
            {
                float progress = Mathf.InverseLerp(
                    FadeOutStartSeconds,
                    DurationSeconds,
                    elapsed);
                fade.alpha = Mathf.SmoothStep(0f, 1f, progress);
                return;
            }

            fade.alpha = 0f;
        }

        private void PlayImpact()
        {
            impactPlayed = true;

            if (sportCarBody != null)
            {
                sportCarBody.position = sportImpact;
                sportCarBody.rotation = sportStartRotation;
                sportCarBody.isKinematic = false;
                sportCarBody.linearVelocity =
                    sportTravelDirection * SportCarForwardVelocity;
                Vector3 impactVelocity =
                    truckTravelDirection * SportCarSideImpactVelocity +
                    Vector3.up * SportCarLaunchVelocity -
                    sportTravelDirection * SportCarReboundVelocity;
                sportCarBody.AddForceAtPosition(
                    impactVelocity,
                    collisionPoint - truckTravelDirection * 0.8f + Vector3.up * 0.45f,
                    ForceMode.VelocityChange);
                Vector3 spinAxis =
                    (sportTravelDirection * 0.55f -
                     truckTravelDirection * 0.35f +
                     Vector3.up * 0.25f).normalized;
                sportCarBody.angularVelocity = spinAxis * SportCarSpinRadians;
            }

            GameObject spark = Resources.Load<GameObject>("VFX/Spark");
            if (spark != null)
            {
                GameObject instance = Instantiate(
                    spark,
                    collisionPoint + Vector3.up * 0.8f,
                    Quaternion.identity);
                Destroy(instance, DurationSeconds - ImpactSeconds);
            }

            if (impactClip != null)
            {
                var audioObject = new GameObject("PrologueCollisionAudio");
                audioObject.transform.position = collisionPoint;
                var source = audioObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.clip = impactClip;
                source.volume = 1f;
                source.Play();
                Destroy(audioObject, impactClip.length + 0.1f);
            }
            else
            {
                Debug.LogWarning(
                    "[Prologue Cutscene] SFX_VEH_COLLISION_-09.wav could not be loaded.",
                    this);
            }

            Debug.Log("[Prologue Cutscene] Truck and SportCar impact.");
        }

        private void ScheduleMusic()
        {
            if (introMusicClip == null || cityMusicClip == null)
            {
                Debug.LogWarning(
                    "[Prologue Cutscene] Buy Something! or ThisUsedToBeACity could not be loaded.",
                    this);
                return;
            }

            introMusicSource = CreateMusicSource("Buy Something!", introMusicClip);
            cityMusicSource = CreateMusicSource("ThisUsedToBeACity", cityMusicClip);

            double dspStart = AudioSettings.dspTime + 0.01d;
            double switchTime = dspStart + ImpactSeconds - CityMusicLeadSeconds;
            double impactTime = dspStart + ImpactSeconds;
            introMusicSource.PlayScheduled(dspStart);
            introMusicSource.SetScheduledEndTime(switchTime);
            cityMusicSource.PlayScheduled(switchTime);
            cityMusicSource.SetScheduledEndTime(impactTime);

            Debug.Log(
                $"[Prologue Cutscene] Music scheduled: Buy Something! -> " +
                $"ThisUsedToBeACity at {ImpactSeconds - CityMusicLeadSeconds:0.0}s, " +
                $"music stops at impact ({ImpactSeconds:0.0}s).");
        }

        private AudioSource CreateMusicSource(string sourceName, AudioClip clip)
        {
            var audioObject = new GameObject(sourceName);
            audioObject.transform.SetParent(transform, false);
            var source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0.65f;
            source.clip = clip;
            return source;
        }

        private void FollowTruckWithTrailer()
        {
            if (trailer != null && trailer.parent != truck)
                trailer.position = trailerStart + (truck.position - truckStart);
        }

        private static Transform FindDescendantExact(Transform root, string objectName)
        {
            if (root == null) return null;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == objectName)
                    return candidate;
            }

            return null;
        }

        private static Transform FindNamedTransform(string objectName)
        {
            foreach (Transform candidate in FindObjectsByType<Transform>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (candidate.name == objectName)
                    return candidate;
            }

            return null;
        }

        private static void MakeKinematic(Transform root)
        {
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        private static Vector3 HorizontalDirection(Vector3 from, Vector3 to, Vector3 fallback)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = fallback;
                direction.y = 0f;
            }

            return direction.normalized;
        }

        private static Material CreateSetDressingMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = materialName,
                color = color
            };
            return material;
        }

        private static Transform CreatePrimitive(
            PrimitiveType type,
            string objectName,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = objectName;
            primitive.transform.position = position;
            primitive.transform.localScale = scale;
            primitive.transform.SetParent(parent, true);
            if (primitive.TryGetComponent(out Collider primitiveCollider))
                Destroy(primitiveCollider);
            if (primitive.TryGetComponent(out Renderer primitiveRenderer))
                primitiveRenderer.sharedMaterial = material;
            return primitive.transform;
        }

        private static float ProjectedRenderRadius(Transform root, Vector3 axis)
        {
            if (!TryGetCombinedRenderBounds(root, out Bounds combined))
                return MinimumContactGap * 0.5f;

            return BoundsProjectionRadius(combined, axis);
        }

        private static float BoundsProjectionRadius(Bounds bounds, Vector3 axis)
        {
            Vector3 extents = bounds.extents;
            return Mathf.Abs(axis.x) * extents.x +
                   Mathf.Abs(axis.y) * extents.y +
                   Mathf.Abs(axis.z) * extents.z;
        }

        private static bool TryGetCombinedRenderBounds(
            Transform root,
            out Bounds combined,
            Transform excludedRoot = null)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            combined = default;
            bool found = false;
            foreach (Renderer candidate in renderers)
            {
                if (excludedRoot != null && candidate.transform.IsChildOf(excludedRoot))
                    continue;

                if (!found)
                {
                    combined = candidate.bounds;
                    found = true;
                }
                else
                {
                    combined.Encapsulate(candidate.bounds);
                }
            }

            return found;
        }

    }
}
