using System.Collections;
using UnityEngine;
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
        private const float ApproachStartSeconds = 0.15f;
        private const float TruckApproachStartSeconds = 3.25f;
        private const float ImpactSeconds = 4f;
        private const float CityMusicLeadSeconds = 0.4f;
        private const float EndSeconds = 5f;
        private const float MinimumContactGap = 2.5f;
        private const float SportCarApproachDistance = 44f;
        private const float TruckApproachDistance = 15f;
        private const float TruckPostImpactTravel = 0.9f;
        private const float SportCarReboundDistance = 5.5f;
        private const float SportCarSideSlideDistance = 16f;
        private const float SportCarLaunchVelocity = 8f;
        private const float AirTumbleDegrees = 430f;
        private const float Gravity = 9.81f;
        private const float GroundTumbleDuration = 1.15f;
        private const float GroundRollDistance = 5.5f;
        private const float GroundBounceHeight = 0.45f;
        private const float GroundRollDegrees = 190f;
        private const float FadeInSeconds = 0.5f;
        private const float FadeOutStartSeconds = ImpactSeconds;
        private static readonly Vector3 AuthoredInteriorCameraPosition =
            new Vector3(-15.4888725f, 1.25f, -7.13000011f);
        private static readonly Quaternion AuthoredInteriorCameraRotation =
            Quaternion.Euler(10.9999962f, 15.4400387f, 0f);

        private Transform truck;
        private Transform trailer;
        private Transform sportCar;
        private Camera cutsceneCamera;
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
        private Transform phone;
        private Vector3 interiorCameraLocalPosition;
        private Quaternion interiorCameraLocalRotation;
        private CanvasGroup fade;
        private bool impactPlayed;

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
            BuildRoadsideMotionReferences();
            fade = BuildFadeOverlay();
            ScheduleMusic();
            float startedAt = Time.time;
            while (Time.time - startedAt < EndSeconds)
            {
                float elapsed = Time.time - startedAt;
                AnimateVehicles(elapsed);
                AnimateCamera(elapsed);
                AnimateFade(elapsed);

                if (!impactPlayed && elapsed >= ImpactSeconds)
                    PlayImpact();

                yield return null;
            }

            introMusicSource?.Stop();
            cityMusicSource?.Stop();
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
            sportCar.position = Vector3.Lerp(sportStart, sportImpact, approach);

            if (elapsed <= ImpactSeconds) return;

            float aftermath = Mathf.InverseLerp(ImpactSeconds, EndSeconds, elapsed);
            float reaction = 1f - Mathf.Pow(1f - aftermath, 3f);
            float timeSinceImpact = elapsed - ImpactSeconds;
            float flightDuration = 2f * SportCarLaunchVelocity / Gravity;
            float flightProgress = Mathf.Clamp01(timeSinceImpact / flightDuration);
            float launchReaction = 1f - Mathf.Pow(1f - flightProgress, 2f);
            truck.position = Vector3.Lerp(
                truckImpact,
                truckImpact + truckTravelDirection * TruckPostImpactTravel,
                reaction);
            FollowTruckWithTrailer();

            Vector3 sportEnd =
                sportImpact -
                sportTravelDirection * SportCarReboundDistance +
                truckTravelDirection * SportCarSideSlideDistance;
            Vector3 airTumbleAxis =
                (truckTravelDirection +
                 Vector3.up * 0.22f -
                 sportTravelDirection * 0.16f).normalized;
            Quaternion landingRotation =
                Quaternion.AngleAxis(AirTumbleDegrees, airTumbleAxis) *
                sportStartRotation;

            if (timeSinceImpact <= flightDuration)
            {
                float ballisticHeight =
                    SportCarLaunchVelocity * timeSinceImpact -
                    0.5f * Gravity * timeSinceImpact * timeSinceImpact;
                Vector3 launchedPosition = Vector3.Lerp(sportImpact, sportEnd, launchReaction);
                launchedPosition.y = sportImpact.y + Mathf.Max(0f, ballisticHeight);
                sportCar.position = launchedPosition;
                sportCar.rotation =
                    Quaternion.AngleAxis(
                        flightProgress * AirTumbleDegrees,
                        airTumbleAxis) *
                    sportStartRotation;
                KeepAboveGround(sportCar, sportGroundSurfaceY);
                return;
            }

            float groundElapsed = timeSinceImpact - flightDuration;
            float groundProgress = Mathf.Clamp01(groundElapsed / GroundTumbleDuration);
            float groundMotion = 1f - Mathf.Pow(1f - groundProgress, 3f);
            Vector3 groundEnd =
                sportEnd +
                truckTravelDirection * GroundRollDistance +
                sportTravelDirection * 1.5f;
            Vector3 rollingPosition = Vector3.Lerp(sportEnd, groundEnd, groundMotion);
            float bounce =
                Mathf.Abs(Mathf.Sin(groundElapsed * 10f)) *
                GroundBounceHeight *
                (1f - groundProgress);
            rollingPosition.y = sportImpact.y + bounce;
            sportCar.position = rollingPosition;

            Vector3 rollAxis = Vector3.Cross(Vector3.up, truckTravelDirection).normalized;
            Quaternion groundRoll =
                Quaternion.AngleAxis(groundMotion * GroundRollDegrees, rollAxis) *
                Quaternion.AngleAxis(
                    Mathf.Sin(groundElapsed * 9f) * 10f * (1f - groundProgress),
                    truckTravelDirection);
            sportCar.rotation = groundRoll * landingRotation;
            KeepAboveGround(sportCar, sportGroundSurfaceY);
        }

        private void AnimateCamera(float elapsed)
        {
            // Preserve the user-authored interior framing. The camera follows the
            // sports car but never pans toward the approaching truck. At impact,
            // freeze the camera in world space while the picture fades to black.
            if (elapsed < ImpactSeconds)
            {
                cutsceneCamera.transform.position =
                    sportCar.TransformPoint(interiorCameraLocalPosition);
                cutsceneCamera.transform.rotation =
                    sportCar.rotation * interiorCameraLocalRotation;
            }
            else
            {
                Matrix4x4 impactPose = Matrix4x4.TRS(
                    sportImpact,
                    sportStartRotation,
                    sportCar.lossyScale);
                cutsceneCamera.transform.position =
                    impactPose.MultiplyPoint3x4(interiorCameraLocalPosition);
                cutsceneCamera.transform.rotation =
                    sportStartRotation * interiorCameraLocalRotation;
            }

            cutsceneCamera.fieldOfView = 60f;
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
            Vector3 phonePosition =
                driverCenter +
                sportTravelDirection * 0.30f +
                Vector3.up * (carHeight * 0.08f);
            Vector3 cameraPosition = AuthoredInteriorCameraPosition;
            Quaternion cameraRotation = AuthoredInteriorCameraRotation;

            driverRig = new GameObject("PrologueDriver").transform;
            driverRig.SetParent(sportCar, true);
            driverRig.position = driverCenter;
            driverRig.rotation = sportCar.rotation;

            Material clothes = CreateSetDressingMaterial(
                "Prologue Driver Clothes",
                new Color(0.08f, 0.11f, 0.16f));
            Material skin = CreateSetDressingMaterial(
                "Prologue Driver Skin",
                new Color(0.72f, 0.47f, 0.32f));

            float bodyScale = carHeight * 0.26f;
            Vector3 hips = driverCenter - Vector3.up * (bodyScale * 0.45f);
            Vector3 shoulders = driverCenter + Vector3.up * (bodyScale * 0.52f);
            CreateCapsuleBetween(
                "Torso",
                hips,
                shoulders,
                bodyScale * 0.34f,
                clothes,
                driverRig);
            CreatePrimitive(
                PrimitiveType.Sphere,
                "Head",
                driverCenter + Vector3.up * (bodyScale * 1.18f),
                Vector3.one * (bodyScale * 0.64f),
                skin,
                driverRig);

            Vector3 leftShoulder =
                shoulders - truckTravelDirection * (bodyScale * 0.34f);
            Vector3 rightShoulder =
                shoulders + truckTravelDirection * (bodyScale * 0.34f);
            Vector3 leftHand =
                phonePosition - truckTravelDirection * (bodyScale * 0.20f);
            Vector3 rightHand =
                phonePosition + truckTravelDirection * (bodyScale * 0.20f);
            CreateCapsuleBetween(
                "LeftArm",
                leftShoulder,
                leftHand,
                bodyScale * 0.12f,
                skin,
                driverRig);
            CreateCapsuleBetween(
                "RightArm",
                rightShoulder,
                rightHand,
                bodyScale * 0.12f,
                skin,
                driverRig);
            CreatePrimitive(
                PrimitiveType.Sphere,
                "LeftHand",
                leftHand,
                Vector3.one * (bodyScale * 0.25f),
                skin,
                driverRig);
            CreatePrimitive(
                PrimitiveType.Sphere,
                "RightHand",
                rightHand,
                Vector3.one * (bodyScale * 0.25f),
                skin,
                driverRig);

            GameObject phonePrefab = Resources.Load<GameObject>("FreePhone1k");
            if (phonePrefab != null)
            {
                phone = Instantiate(phonePrefab, phonePosition, Quaternion.identity).transform;
                phone.name = "DriverPhone";
                phone.SetParent(driverRig, true);
                if (TryGetCombinedRenderBounds(phone, out Bounds phoneBounds))
                {
                    float currentSize = Mathf.Max(
                        phoneBounds.size.x,
                        Mathf.Max(phoneBounds.size.y, phoneBounds.size.z));
                    if (currentSize > 0.001f)
                        phone.localScale *= (bodyScale * 0.72f) / currentSize;
                }

                phone.position = phonePosition;
                phone.rotation = Quaternion.LookRotation(
                    cameraPosition - phonePosition,
                    Vector3.up);
                if (TryGetCombinedRenderBounds(phone, out Bounds placedPhoneBounds))
                    phone.position += phonePosition - placedPhoneBounds.center;
            }
            else
            {
                Debug.LogWarning(
                    "[Prologue Cutscene] Resources/FreePhone1k could not be loaded.",
                    this);
            }

            interiorCameraLocalPosition = sportCar.InverseTransformPoint(cameraPosition);
            interiorCameraLocalRotation =
                Quaternion.Inverse(sportCar.rotation) * cameraRotation;
            cutsceneCamera.nearClipPlane = 0.03f;
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
                    EndSeconds,
                    elapsed);
                fade.alpha = Mathf.SmoothStep(0f, 1f, progress);
                return;
            }

            fade.alpha = 0f;
        }

        private void PlayImpact()
        {
            impactPlayed = true;
            GameObject spark = Resources.Load<GameObject>("VFX/Spark");
            if (spark != null)
            {
                GameObject instance = Instantiate(
                    spark,
                    collisionPoint + Vector3.up * 0.8f,
                    Quaternion.identity);
                Destroy(instance, EndSeconds - ImpactSeconds);
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

        private static Transform CreateCapsuleBetween(
            string objectName,
            Vector3 from,
            Vector3 to,
            float radius,
            Material material,
            Transform parent)
        {
            Vector3 segment = to - from;
            float length = Mathf.Max(radius * 2f, segment.magnitude);
            Transform capsule = CreatePrimitive(
                PrimitiveType.Capsule,
                objectName,
                (from + to) * 0.5f,
                new Vector3(radius * 2f, length * 0.5f, radius * 2f),
                material,
                parent);
            if (segment.sqrMagnitude > 0.0001f)
                capsule.rotation = Quaternion.FromToRotation(Vector3.up, segment.normalized);
            return capsule;
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

        private void KeepAboveGround(Transform root, float groundSurfaceY)
        {
            if (!TryGetCombinedRenderBounds(root, out Bounds bounds, driverRig)) return;

            float penetration = groundSurfaceY - bounds.min.y;
            if (penetration > 0f)
                root.position += Vector3.up * penetration;
        }
    }
}
