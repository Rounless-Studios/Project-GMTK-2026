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
    /// trailer, player car and camera already authored in that scene.
    /// </summary>
    public sealed class PrologueCrashCutscene : MonoBehaviour
    {
        public const float DefaultDurationSeconds = 7.5f;

        private const string DriverAssetPath =
            "Assets/Models/Characters/NPC_Driver/NPC_driver_phone.fbx";
        private const string DriverObjectName = "NPC_driver_Phone";
        private const string DriverClipName = "Drive_Phone_Idle";

        [Header("Scene References")]
        [SerializeField] private Transform playerVehicle;
        [SerializeField] private Transform truck;
        [SerializeField] private Transform trailer;
        [SerializeField] private Camera cutsceneCamera;
        [SerializeField] private PlayableDirector timelineDirector;
        [SerializeField] private Transform driverRig;
        [SerializeField] private AudioClip impactClip;
        [SerializeField] private AudioClip introMusicClip;
        [SerializeField] private AudioClip cityMusicClip;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float durationSeconds = DefaultDurationSeconds;
        [SerializeField, Min(0f)] private float approachStartSeconds = 0.15f;
        [SerializeField, Min(0f)] private float truckApproachStartSeconds = 3.15f;
        [SerializeField, Min(0f)] private float impactSeconds = 4.4f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.8f;
        [SerializeField, Min(0f)] private float fadeOutStartSeconds = 6.35f;

        [Header("Pre-Impact Slow Motion")]
        [Tooltip("충돌 직전에 슬로 모션을 사용할지 여부입니다.")]
        [SerializeField] private bool enablePreImpactSlowMotion = true;
        [Tooltip("충돌 몇 초 전부터 슬로 모션을 시작할지 정합니다. 연출 시간 기준입니다.")]
        [SerializeField, Min(0f)] private float slowMotionLeadSeconds = 0.45f;
        [Tooltip("슬로 모션 배속입니다. 0.25는 정상 속도의 25%입니다.")]
        [SerializeField, Range(0.05f, 1f)] private float slowMotionTimeScale = 0.25f;

        [Header("Vehicle Approach")]
        [Tooltip("플레이어 차량이 충돌 지점까지 이동하는 거리입니다.")]
        [SerializeField, InspectorName("Player Car Approach Distance"), Min(0f)]
        private float sportCarApproachDistance = 44f;
        [Tooltip("트럭이 화면 밖 시작점에서 충돌 지점까지 이동하는 거리입니다.")]
        [SerializeField, Min(0.1f)] private float truckApproachDistance = 15f;
        [SerializeField, Min(0f)] private float truckPostImpactTravel = 0.9f;
        [SerializeField, Min(0.1f)] private float minimumContactGap = 2.5f;

        [Header("Player Car Impact Physics")]
        [SerializeField, InspectorName("Player Car Forward Velocity")]
        private float sportCarForwardVelocity = 10f;
        [SerializeField, InspectorName("Player Car Side Impact Velocity")]
        private float sportCarSideImpactVelocity = 17f;
        [SerializeField, InspectorName("Player Car Launch Velocity")]
        private float sportCarLaunchVelocity = 9f;
        [SerializeField, InspectorName("Player Car Rebound Velocity")]
        private float sportCarReboundVelocity = 4f;
        [Tooltip("충돌 직후 차량의 초당 회전량입니다.")]
        [SerializeField, InspectorName("Player Car Spin Radians"), Min(0f)]
        private float sportCarSpinRadians = 6.5f;
        [SerializeField] private float spinForwardWeight = 0.55f;
        [SerializeField] private float spinSideWeight = -0.35f;
        [SerializeField] private float spinUpWeight = 0.25f;
        [SerializeField] private float impactPointSideOffset = -0.8f;
        [SerializeField] private float impactPointHeightOffset = 0.45f;
        [SerializeField, InspectorName("Player Car Mass"), Min(1f)]
        private float sportCarMass = 1200f;
        [SerializeField, InspectorName("Player Car Linear Damping"), Min(0f)]
        private float sportCarLinearDamping = 0.12f;
        [SerializeField, InspectorName("Player Car Angular Damping"), Min(0f)]
        private float sportCarAngularDamping = 0.08f;

        [Header("Crash Deformation")]
        [SerializeField, Min(0.01f)] private float impactDeformationSeconds = 0.22f;
        [SerializeField, Min(0f)] private float deformationSideInset = 0.22f;
        [SerializeField, Min(0f)] private float deformationDownInset = 0.04f;
        [SerializeField] private Vector3 deformationRotation = new(-7f, 10f, 15f);
        [Tooltip("각 축에서 줄어드는 비율입니다. 0.2는 해당 축을 20% 압축합니다.")]
        [SerializeField] private Vector3 deformationScaleLoss = new(0.18f, 0.10f, 0.24f);
        [SerializeField, Range(0f, 2f)] private float bodyDeformationStrength = 0.35f;
        [SerializeField, Range(0f, 2f)] private float hoodDeformationStrength = 0.85f;
        [SerializeField, Range(0f, 2f)] private float doorDeformationStrength = 1f;
        [SerializeField, Range(0f, 2f)] private float windowDeformationStrength = 0.75f;

        [Header("Audio / VFX Timing")]
        [SerializeField, Min(0f)] private float cityMusicLeadSeconds = 0.4f;
        [SerializeField, Min(0f)] private float impactSparkHeight = 0.8f;

        public float ConfiguredDurationSeconds => durationSeconds;
        public float ConfiguredRecordingDurationSeconds =>
            CinematicToRealSeconds(durationSeconds);

        private Transform sportCar;
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
        private AudioSource introMusicSource;
        private AudioSource cityMusicSource;
        private GameObject driverPhonePrefab;
        private Animator driverAnimator;
        private Rigidbody sportCarBody;
        private readonly List<DeformationPart> deformationParts = new();
        private CanvasGroup fade;
        private bool impactPlayed;
        private DirectorUpdateMode originalTimelineUpdateMode;
        private bool timelineUpdateModeOverridden;

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

            if (truck == null) truck = FindNamedTransform("Truck");
            if (trailer == null) trailer = FindNamedTransform("Trailer");
            sportCar = playerVehicle != null
                ? playerVehicle
                : FindNamedTransform("Prologue Player Car") ?? FindNamedTransform("SportCar");
            if (driverRig == null) driverRig = FindNamedTransform(DriverObjectName);
            if (cutsceneCamera == null)
                cutsceneCamera = Camera.main != null
                    ? Camera.main
                    : FindFirstObjectByType<Camera>();
#if UNITY_EDITOR
            if (impactClip == null)
                impactClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Sound/SFX_VEH_COLLISION_-09.wav");
            if (introMusicClip == null)
                introMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Sound/Prologue/Buy Something!.mp3");
            if (cityMusicClip == null)
                cityMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Sound/Prologue/ThisUsedToBeACity.ogg");
            driverPhonePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                DriverAssetPath);
#endif

            if (truck == null || sportCar == null || cutsceneCamera == null)
            {
                Debug.LogError(
                    "[Prologue Cutscene] example_scene needs Truck, a Player Vehicle and Main Camera.",
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
            sportGroundSurfaceY = TryGetCombinedRenderBounds(
                sportCar,
                out Bounds sportBounds,
                driverRig)
                ? sportBounds.min.y
                : sportStart.y;

            // The player car drives normally toward a front-facing camera. The
            // truck waits outside the side of frame and enters shortly before impact.
            sportTravelDirection = HorizontalDirection(
                sportStart,
                sportStart + sportCar.forward,
                Vector3.back);
            truckTravelDirection = Vector3.Cross(Vector3.up, sportTravelDirection).normalized;
            collisionPoint = sportStart + sportTravelDirection * sportCarApproachDistance;
            collisionPoint.y = sportStart.y;

            truck.rotation = Quaternion.LookRotation(truckTravelDirection, Vector3.up);
            if (trailer != null)
                trailer.rotation = truck.rotation * authoredTrailerLocalRotation;

            float contactGap = Mathf.Clamp(
                ProjectedRenderRadius(truck, truckTravelDirection) +
                ProjectedRenderRadius(sportCar, truckTravelDirection, driverRig),
                minimumContactGap,
                truckApproachDistance * 0.8f);

            sportImpact = collisionPoint;
            sportImpact.y = sportStart.y;
            truckImpact = collisionPoint - truckTravelDirection * contactGap;
            truckImpact.y = truckGroundY;
            truckStart = truckImpact - truckTravelDirection * truckApproachDistance;
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
            float realElapsed = 0f;
            float recordingDuration = ConfiguredRecordingDurationSeconds;
            while (realElapsed < recordingDuration)
            {
                float elapsed = Mathf.Min(
                    durationSeconds,
                    RealToCinematicSeconds(realElapsed));

                if (!impactPlayed && elapsed >= impactSeconds)
                    PlayImpact();

                AnimateVehicles(elapsed);
                AnimateCrashDeformation(elapsed);
                AnimateFade(elapsed);
                EvaluateTimeline(elapsed);

                yield return null;
                realElapsed += Time.unscaledDeltaTime;
            }

            introMusicSource?.Stop();
            cityMusicSource?.Stop();
            StopTimeline();
        }

        private void OnDisable()
        {
            StopTimeline();
        }

        private void OnDestroy()
        {
            StopTimeline();
        }

        private float RealToCinematicSeconds(float realSeconds)
        {
            realSeconds = Mathf.Max(0f, realSeconds);
            if (!enablePreImpactSlowMotion || slowMotionLeadSeconds <= 0f)
                return realSeconds;

            float slowMotionStart = Mathf.Max(0f, impactSeconds - slowMotionLeadSeconds);
            if (realSeconds <= slowMotionStart)
                return realSeconds;

            float playbackRate = Mathf.Max(0.05f, slowMotionTimeScale);
            float slowedCinematicDuration = impactSeconds - slowMotionStart;
            float realImpactTime =
                slowMotionStart + slowedCinematicDuration / playbackRate;
            if (realSeconds <= realImpactTime)
            {
                return slowMotionStart +
                    (realSeconds - slowMotionStart) * playbackRate;
            }

            return impactSeconds + (realSeconds - realImpactTime);
        }

        private void AnimateVehicles(float elapsed)
        {
            float approach = Mathf.InverseLerp(approachStartSeconds, impactSeconds, elapsed);
            float truckApproach = Mathf.InverseLerp(
                truckApproachStartSeconds,
                impactSeconds,
                elapsed);
            truck.position = Vector3.Lerp(truckStart, truckImpact, truckApproach);
            FollowTruckWithTrailer();

            if (!impactPlayed)
            {
                sportCar.position = Vector3.Lerp(sportStart, sportImpact, approach);
                sportCar.rotation = sportStartRotation;
                return;
            }

            float aftermath = Mathf.InverseLerp(impactSeconds, durationSeconds, elapsed);
            float reaction = 1f - Mathf.Pow(1f - aftermath, 3f);
            truck.position = Vector3.Lerp(
                truckImpact,
                truckImpact + truckTravelDirection * truckPostImpactTravel,
                reaction);
            FollowTruckWithTrailer();
        }

        private void BuildInteriorShot(Bounds carBounds)
        {
            if (driverRig != null)
            {
                ResolveDriverAnimator();
                return;
            }

            float carHeight = Mathf.Max(1f, carBounds.size.y);
            float halfWidth = Mathf.Max(
                0.8f,
                ProjectedRenderRadius(sportCar, truckTravelDirection, driverRig));
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
            driverRig.name = DriverObjectName;
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

            ResolveDriverAnimator();
        }

        private void ResolveDriverAnimator()
        {
            driverAnimator = driverRig != null
                ? driverRig.GetComponentInChildren<Animator>(true)
                : null;
#if UNITY_EDITOR
            if (driverAnimator == null && driverRig != null)
            {
                driverAnimator = driverRig.gameObject.AddComponent<Animator>();
                foreach (UnityEngine.Object asset in
                         UnityEditor.AssetDatabase.LoadAllAssetsAtPath(DriverAssetPath))
                {
                    if (asset is not Avatar avatar) continue;
                    driverAnimator.avatar = avatar;
                    break;
                }

                driverAnimator.applyRootMotion = false;
                driverAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
#endif
            if (driverAnimator == null)
            {
                Debug.LogError(
                    "[Prologue Cutscene] NPC_driver_Phone Animator could not be loaded.",
                    this);
            }
        }

        private void BindAndPlayTimeline()
        {
            if (timelineDirector == null)
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

            originalTimelineUpdateMode = timelineDirector.timeUpdateMode;
            timelineDirector.timeUpdateMode = DirectorUpdateMode.Manual;
            timelineUpdateModeOverridden = true;
            timelineDirector.time = 0d;
            timelineDirector.Play();
            timelineDirector.Evaluate();
        }

        private void EvaluateTimeline(float elapsed)
        {
            if (timelineDirector == null ||
                timelineDirector.playableAsset == null ||
                !timelineUpdateModeOverridden)
            {
                return;
            }

            timelineDirector.time = Mathf.Min(
                elapsed,
                (float)timelineDirector.duration);
            timelineDirector.Evaluate();
        }

        private void StopTimeline()
        {
            if (timelineDirector == null)
                return;

            timelineDirector.Stop();
            if (!timelineUpdateModeOverridden)
                return;

            timelineDirector.timeUpdateMode = originalTimelineUpdateMode;
            timelineUpdateModeOverridden = false;
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

            sportCarBody.mass = sportCarMass;
            sportCarBody.useGravity = true;
            sportCarBody.isKinematic = true;
            sportCarBody.linearDamping = sportCarLinearDamping;
            sportCarBody.angularDamping = sportCarAngularDamping;
            sportCarBody.interpolation = RigidbodyInterpolation.Interpolate;
            sportCarBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void PrepareCrashDeformation()
        {
            CacheDeformationPart(
                FindDescendantExact(sportCar, "Body"),
                bodyDeformationStrength);
            CacheDeformationPart(
                FindDescendantExact(sportCar, "Front_Hood"),
                hoodDeformationStrength);

            Transform leftDoor = FindDescendantExact(sportCar, "Left_Door");
            Transform rightDoor = FindDescendantExact(sportCar, "Right_Door");
            Transform impactDoor = SelectImpactSide(leftDoor, rightDoor);
            if (impactDoor != null)
            {
                CacheDeformationPart(impactDoor, doorDeformationStrength);
                string windowName = impactDoor.name.StartsWith("Left")
                    ? "Left_Door_Window"
                    : "Right_Door_Window";
                CacheDeformationPart(
                    FindDescendantExact(sportCar, windowName),
                    windowDeformationStrength);
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
                    impactSeconds,
                    impactSeconds + impactDeformationSeconds,
                    elapsed));

            foreach (DeformationPart part in deformationParts)
            {
                if (part.Transform == null) continue;
                float amount = progress * part.Strength;
                Vector3 worldInset =
                    truckTravelDirection * (deformationSideInset * amount) -
                    Vector3.up * (deformationDownInset * amount);
                Vector3 localInset = part.Transform.parent != null
                    ? part.Transform.parent.InverseTransformVector(worldInset)
                    : worldInset;

                part.Transform.localPosition = part.LocalPosition + localInset;
                part.Transform.localRotation =
                    part.LocalRotation *
                    Quaternion.Euler(deformationRotation * amount);
                part.Transform.localScale = Vector3.Scale(
                    part.LocalScale,
                    new Vector3(
                        1f - deformationScaleLoss.x * amount,
                        1f - deformationScaleLoss.y * amount,
                        1f - deformationScaleLoss.z * amount));
            }
        }

        private void BuildRoadsideMotionReferences()
        {
            float halfWidth = Mathf.Max(
                0.8f,
                ProjectedRenderRadius(sportCar, truckTravelDirection, driverRig));
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

            if (elapsed < fadeInSeconds)
            {
                float progress = Mathf.Clamp01(elapsed / fadeInSeconds);
                fade.alpha = 1f - Mathf.SmoothStep(0f, 1f, progress);
                return;
            }

            if (elapsed >= fadeOutStartSeconds)
            {
                float progress = Mathf.InverseLerp(
                    fadeOutStartSeconds,
                    durationSeconds,
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
                    sportTravelDirection * sportCarForwardVelocity;
                Vector3 impactVelocity =
                    truckTravelDirection * sportCarSideImpactVelocity +
                    Vector3.up * sportCarLaunchVelocity -
                    sportTravelDirection * sportCarReboundVelocity;
                sportCarBody.AddForceAtPosition(
                    impactVelocity,
                    collisionPoint +
                    truckTravelDirection * impactPointSideOffset +
                    Vector3.up * impactPointHeightOffset,
                    ForceMode.VelocityChange);
                Vector3 spinAxis =
                    (sportTravelDirection * spinForwardWeight +
                     truckTravelDirection * spinSideWeight +
                     Vector3.up * spinUpWeight).normalized;
                sportCarBody.angularVelocity = spinAxis * sportCarSpinRadians;
            }

            GameObject spark = Resources.Load<GameObject>("VFX/Spark");
            if (spark != null)
            {
                GameObject instance = Instantiate(
                    spark,
                    collisionPoint + Vector3.up * impactSparkHeight,
                    Quaternion.identity);
                Destroy(instance, durationSeconds - impactSeconds);
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

            Debug.Log("[Prologue Cutscene] Truck and player car impact.");
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
            double switchTime = dspStart + CinematicToRealSeconds(
                impactSeconds - cityMusicLeadSeconds);
            double impactTime = dspStart + CinematicToRealSeconds(impactSeconds);
            introMusicSource.PlayScheduled(dspStart);
            introMusicSource.SetScheduledEndTime(switchTime);
            cityMusicSource.PlayScheduled(switchTime);
            cityMusicSource.SetScheduledEndTime(impactTime);

            Debug.Log(
                $"[Prologue Cutscene] Music scheduled: Buy Something! -> " +
                $"ThisUsedToBeACity at {impactSeconds - cityMusicLeadSeconds:0.0}s, " +
                $"music stops at impact ({impactSeconds:0.0}s).");
        }

        private float CinematicToRealSeconds(float cinematicSeconds)
        {
            cinematicSeconds = Mathf.Max(0f, cinematicSeconds);
            if (!enablePreImpactSlowMotion || slowMotionLeadSeconds <= 0f)
                return cinematicSeconds;

            float slowMotionStart = Mathf.Max(0f, impactSeconds - slowMotionLeadSeconds);
            if (cinematicSeconds <= slowMotionStart)
                return cinematicSeconds;

            float slowedDuration =
                Mathf.Min(cinematicSeconds, impactSeconds) - slowMotionStart;
            float realSeconds =
                slowMotionStart + slowedDuration / Mathf.Max(0.05f, slowMotionTimeScale);
            if (cinematicSeconds > impactSeconds)
                realSeconds += cinematicSeconds - impactSeconds;
            return realSeconds;
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
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
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

        private float ProjectedRenderRadius(
            Transform root,
            Vector3 axis,
            Transform excludedRoot = null)
        {
            if (!TryGetCombinedRenderBounds(root, out Bounds combined, excludedRoot))
                return minimumContactGap * 0.5f;

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
                if (!candidate.enabled ||
                    candidate is ParticleSystemRenderer ||
                    candidate is TrailRenderer ||
                    candidate is LineRenderer)
                {
                    continue;
                }

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
