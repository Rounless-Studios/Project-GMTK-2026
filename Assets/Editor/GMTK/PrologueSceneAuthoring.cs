using System.IO;
using System.Linq;
using GMTK;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

/// <summary>
/// Authors the prologue player vehicle and driver as real scene objects so the
/// actor, cameras, animation and Timeline can be adjusted directly in the scene.
/// </summary>
[InitializeOnLoad]
public static class PrologueSceneAuthoring
{
    private const string ExampleSceneName = "example_scene";
    private const string PlayerVehicleAssetPath =
        "Assets/Realistic Car Controller Pro/Prefabs/Prototype/" +
        "Model_Skyline by BUMSTRUM(3DMaesen) (Prototype).prefab";
    private const string SportCarAssetPath =
        "Assets/Sport Car - 3D model/Prefabs/SportCar.prefab";
    private const string DriverAssetPath =
        "Assets/Models/Characters/NPC_Driver/NPC_driver_phone.fbx";
    private const string ImpactClipPath =
        "Assets/Sound/SFX_VEH_COLLISION_-09.wav";
    private const string IntroMusicClipPath =
        "Assets/Sound/Prologue/Buy Something!.mp3";
    private const string CityMusicClipPath =
        "Assets/Sound/Prologue/ThisUsedToBeACity.ogg";
    private const string RequestAssetPath =
        "Assets/Editor/GMTK/PrologueSceneSetup.request";
    private const string PlayerVehicleObjectName = "Prologue Player Car";
    private const string LegacyVehicleObjectName = "SportCar";
    private const string DriverObjectName = "NPC_driver_Phone";
    private const string InteriorCameraRigName = "Prologue Interior Camera Rig";
    private const string CrashExteriorCameraName = "CM Prologue Crash Exterior";
    private const string AnimationTrackName = "NPC Driver Phone";
    private const string AnimationClipName = "Drive_Phone_Idle";

    static PrologueSceneAuthoring()
    {
        EditorApplication.delayCall += TryRunRequestedSetup;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("GMTK/Prologue/Setup Player Car And Driver In Scene")]
    public static void SetupPlayerCarAndDriverInScene()
    {
        if (!TrySetupActiveScene())
            return;

        DeleteRequestAsset();
    }

    [MenuItem("GMTK/Prologue/Restore SportCar In Scene")]
    public static void RestoreSportCarInScene()
    {
        TryRestoreSportCarInActiveScene();
    }

    private static bool TryRestoreSportCarInActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(
                "[Prologue Authoring] Exit Play Mode before restoring SportCar.");
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != ExampleSceneName)
        {
            Debug.LogError(
                "[Prologue Authoring] Open Assets/Single_detailed_truck/example_scene.unity first.");
            return false;
        }

        Transform playerVehicle = FindInScene(scene, PlayerVehicleObjectName);
        Transform sportCar = FindInScene(scene, LegacyVehicleObjectName);
        Transform driver = FindInScene(scene, DriverObjectName);
        Transform interiorCameraRig = FindInScene(scene, InteriorCameraRigName);
        PlayableDirector director = FindComponentInScene<PlayableDirector>(scene);
        GameObject sportCarAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(SportCarAssetPath);

        if (sportCarAsset == null || driver == null ||
            interiorCameraRig == null || director == null)
        {
            Debug.LogError(
                "[Prologue Authoring] SportCar, driver, interior camera rig, or " +
                "Prologue Timeline is missing.");
            return false;
        }

        bool createdSportCar = sportCar == null;
        if (createdSportCar)
        {
            GameObject instance =
                PrefabUtility.InstantiatePrefab(sportCarAsset, scene) as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    "[Prologue Authoring] SportCar prefab instantiation failed.");
                return false;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Restore prologue SportCar");
            instance.name = LegacyVehicleObjectName;
            sportCar = instance.transform;

            Transform props = FindInScene(scene, "Props");
            if (props != null)
                Undo.SetTransformParent(sportCar, props, "Parent SportCar to scene Props");

            Undo.RecordObject(sportCar, "Align restored prologue SportCar");
            if (playerVehicle != null)
            {
                sportCar.SetPositionAndRotation(
                    playerVehicle.position,
                    playerVehicle.rotation);
            }
            else
            {
                sportCar.localPosition = new Vector3(-15.71f, 0.752f, -0.56f);
                sportCar.localRotation = Quaternion.Euler(0f, 181.67f, 0f);
            }
            sportCar.localScale = Vector3.one * 0.3f;
        }

        Undo.SetTransformParent(
            interiorCameraRig,
            sportCar,
            "Parent interior camera rig to SportCar");
        Undo.RecordObject(interiorCameraRig, "Restore SportCar interior camera rig");
        interiorCameraRig.localPosition = Vector3.zero;
        interiorCameraRig.localRotation = Quaternion.identity;
        interiorCameraRig.localScale = Vector3.one;
        PrefabUtility.RecordPrefabInstancePropertyModifications(interiorCameraRig);

        Undo.SetTransformParent(driver, sportCar, "Parent prologue driver to SportCar");
        PlaceDriverInCabin(driver, sportCar);

        DisableVehicleGameplay(sportCar, driver, interiorCameraRig);
        MakeVehicleKinematic(sportCar);
        BindCrashExteriorCamera(scene, sportCar);
        BindCutsceneReferences(scene, sportCar, driver, director);

        if (playerVehicle != null && playerVehicle != sportCar)
            Undo.DestroyObjectImmediate(playerVehicle.gameObject);

        PrefabUtility.RecordPrefabInstancePropertyModifications(sportCar);
        PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = sportCar.gameObject;
        EditorGUIUtility.PingObject(sportCar.gameObject);
        Debug.Log(
            "[Prologue Authoring] SportCar was restored. Driver, cameras, " +
            "Timeline and cutscene references were rebound.");
        return true;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        EditorApplication.delayCall += TryRunRequestedSetup;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += TryRunRequestedSetup;
    }

    private static void TryRunRequestedSetup()
    {
        if (!RequestExists())
            return;

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += TryRunRequestedSetup;
            return;
        }

        if (SceneManager.GetActiveScene().name != ExampleSceneName)
            return;

        if (TrySetupActiveScene())
            DeleteRequestAsset();
    }

    private static bool TrySetupActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning(
                "[Prologue Authoring] Exit Play Mode before changing the prologue vehicle.");
            return false;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != ExampleSceneName)
        {
            Debug.LogError(
                "[Prologue Authoring] Open Assets/Single_detailed_truck/example_scene.unity first.");
            return false;
        }

        Transform legacyVehicle = FindInScene(scene, LegacyVehicleObjectName);
        Transform playerVehicle = FindInScene(scene, PlayerVehicleObjectName);
        Transform driver = FindInScene(scene, DriverObjectName);
        Transform interiorCameraRig = FindInScene(scene, InteriorCameraRigName);
        PlayableDirector director = FindComponentInScene<PlayableDirector>(scene);
        GameObject playerVehicleAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(PlayerVehicleAssetPath);
        GameObject driverAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DriverAssetPath);
        UnityEngine.Object[] driverAssets = AssetDatabase.LoadAllAssetsAtPath(DriverAssetPath);
        AnimationClip driverClip = driverAssets
            .OfType<AnimationClip>()
            .FirstOrDefault(clip => clip.name == AnimationClipName);
        Avatar driverAvatar = driverAssets.OfType<Avatar>().FirstOrDefault();

        if (playerVehicleAsset == null || director == null ||
            driverAsset == null || driverClip == null)
        {
            Debug.LogError(
                "[Prologue Authoring] Player vehicle, Prologue Timeline, or " +
                "NPC_driver_phone animation asset is missing.");
            return false;
        }

        bool createdPlayerVehicle = playerVehicle == null;
        if (createdPlayerVehicle)
        {
            GameObject instance =
                PrefabUtility.InstantiatePrefab(playerVehicleAsset, scene) as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    "[Prologue Authoring] Player vehicle prefab instantiation failed.");
                return false;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place prologue player vehicle");
            instance.name = PlayerVehicleObjectName;
            playerVehicle = instance.transform;
            if (legacyVehicle != null)
            {
                Undo.RecordObject(playerVehicle, "Align prologue player vehicle");
                playerVehicle.SetPositionAndRotation(
                    legacyVehicle.position,
                    legacyVehicle.rotation);
            }
        }

        bool movedInteriorCamera =
            interiorCameraRig != null && interiorCameraRig.parent != playerVehicle;
        if (movedInteriorCamera)
        {
            Undo.SetTransformParent(
                interiorCameraRig,
                playerVehicle,
                "Parent interior camera rig to player vehicle");
            PrefabUtility.RecordPrefabInstancePropertyModifications(interiorCameraRig);
        }

        bool createdDriver = driver == null;
        if (createdDriver)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(driverAsset, scene) as GameObject;
            if (instance == null)
            {
                Debug.LogError(
                    "[Prologue Authoring] NPC_driver_phone prefab instantiation failed.");
                return false;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Place prologue driver");
            instance.name = DriverObjectName;
            driver = instance.transform;
        }

        bool movedDriver = driver.parent != playerVehicle;
        if (movedDriver)
        {
            Undo.SetTransformParent(
                driver,
                playerVehicle,
                "Parent prologue driver to player vehicle");
        }

        if (createdDriver || movedDriver || createdPlayerVehicle)
            PlaceDriverInCabin(driver, playerVehicle);

        DisableVehicleGameplay(playerVehicle, driver, interiorCameraRig);
        MakeVehicleKinematic(playerVehicle);

        if (legacyVehicle != null && legacyVehicle != playerVehicle)
            Undo.DestroyObjectImmediate(legacyVehicle.gameObject);

        Animator animator = driver.GetComponentInChildren<Animator>(true);
        if (animator == null)
            animator = Undo.AddComponent<Animator>(driver.gameObject);

        Undo.RecordObject(animator, "Configure prologue driver Animator");
        animator.avatar = driverAvatar;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        EditorUtility.SetDirty(animator);

        TimelineAsset timeline = director.playableAsset as TimelineAsset;
        AnimationTrack animationTrack = timeline == null
            ? null
            : timeline.GetOutputTracks()
                .OfType<AnimationTrack>()
                .FirstOrDefault(track => track.name == AnimationTrackName);
        if (animationTrack == null)
        {
            Debug.LogError(
                $"[Prologue Authoring] Timeline track '{AnimationTrackName}' was not found.");
            return false;
        }

        Undo.RecordObject(director, "Bind prologue driver Timeline track");
        director.SetGenericBinding(animationTrack, animator);
        foreach (TimelineClip timelineClip in animationTrack.GetClips())
        {
            if (timelineClip.asset is not AnimationPlayableAsset playable)
                continue;

            Undo.RecordObject(playable, "Assign prologue driver animation");
            playable.clip = driverClip;
            EditorUtility.SetDirty(playable);
        }

        BindCrashExteriorCamera(scene, playerVehicle);
        BindCutsceneReferences(scene, playerVehicle, driver, director);

        EditorUtility.SetDirty(director);
        EditorUtility.SetDirty(timeline);
        PrefabUtility.RecordPrefabInstancePropertyModifications(playerVehicle);
        PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = playerVehicle.gameObject;
        EditorGUIUtility.PingObject(playerVehicle.gameObject);
        Debug.Log(
            "[Prologue Authoring] The gameplay Skyline is now the prologue player car. " +
            "Driver, cameras, Timeline and cutscene references were rebound.");
        return true;
    }

    private static void BindCrashExteriorCamera(Scene scene, Transform playerVehicle)
    {
        Transform cameraTransform = FindInScene(scene, CrashExteriorCameraName);
        CinemachineCamera crashCamera =
            cameraTransform != null ? cameraTransform.GetComponent<CinemachineCamera>() : null;
        if (crashCamera == null)
        {
            Debug.LogWarning(
                $"[Prologue Authoring] '{CrashExteriorCameraName}' was not found.");
            return;
        }

        Undo.RecordObject(crashCamera, "Target player vehicle with crash camera");
        CameraTarget target = crashCamera.Target;
        target.TrackingTarget = playerVehicle;
        target.LookAtTarget = playerVehicle;
        target.CustomLookAtTarget = true;
        crashCamera.Target = target;
        EditorUtility.SetDirty(crashCamera);
    }

    private static void BindCutsceneReferences(
        Scene scene,
        Transform playerVehicle,
        Transform driver,
        PlayableDirector director)
    {
        PrologueCrashCutscene cutscene =
            FindComponentInScene<PrologueCrashCutscene>(scene);
        if (cutscene == null)
        {
            Debug.LogWarning(
                "[Prologue Authoring] PrologueCrashCutscene scene component was not found.");
            return;
        }

        Transform truck = FindInScene(scene, "Truck");
        Transform trailer = FindInScene(scene, "Trailer");
        Transform mainCameraTransform = FindInScene(scene, "Main Camera");
        Camera mainCamera = mainCameraTransform != null
            ? mainCameraTransform.GetComponent<Camera>()
            : FindComponentInScene<Camera>(scene);

        Undo.RecordObject(cutscene, "Bind prologue cutscene scene references");
        var serialized = new SerializedObject(cutscene);
        serialized.Update();
        SetObjectReference(serialized, "playerVehicle", playerVehicle);
        SetObjectReference(serialized, "truck", truck);
        SetObjectReference(serialized, "trailer", trailer);
        SetObjectReference(serialized, "cutsceneCamera", mainCamera);
        SetObjectReference(serialized, "timelineDirector", director);
        SetObjectReference(serialized, "driverRig", driver);
        SetObjectReference(
            serialized,
            "impactClip",
            AssetDatabase.LoadAssetAtPath<AudioClip>(ImpactClipPath));
        SetObjectReference(
            serialized,
            "introMusicClip",
            AssetDatabase.LoadAssetAtPath<AudioClip>(IntroMusicClipPath));
        SetObjectReference(
            serialized,
            "cityMusicClip",
            AssetDatabase.LoadAssetAtPath<AudioClip>(CityMusicClipPath));
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(cutscene);
    }

    private static void SetObjectReference(
        SerializedObject serialized,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError(
                $"[Prologue Authoring] Serialized property '{propertyName}' was not found.");
            return;
        }

        property.objectReferenceValue = value;
    }

    private static void DisableVehicleGameplay(
        Transform playerVehicle,
        Transform driver,
        Transform interiorCameraRig)
    {
        foreach (MonoBehaviour behaviour in
                 playerVehicle.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null ||
                IsInside(behaviour.transform, driver) ||
                IsInside(behaviour.transform, interiorCameraRig))
            {
                continue;
            }

            Undo.RecordObject(behaviour, "Disable prologue vehicle gameplay");
            behaviour.enabled = false;
            EditorUtility.SetDirty(behaviour);
            PrefabUtility.RecordPrefabInstancePropertyModifications(behaviour);
        }
    }

    private static void MakeVehicleKinematic(Transform playerVehicle)
    {
        foreach (Rigidbody body in playerVehicle.GetComponentsInChildren<Rigidbody>(true))
        {
            Undo.RecordObject(body, "Make prologue vehicle kinematic");
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            EditorUtility.SetDirty(body);
            PrefabUtility.RecordPrefabInstancePropertyModifications(body);
        }
    }

    private static bool IsInside(Transform candidate, Transform root)
    {
        return root != null && (candidate == root || candidate.IsChildOf(root));
    }

    private static void PlaceDriverInCabin(Transform driver, Transform playerVehicle)
    {
        if (!TryGetRenderBounds(playerVehicle, out Bounds carBounds, driver) ||
            !TryGetRenderBounds(driver, out Bounds driverBounds))
        {
            driver.localPosition = new Vector3(0.62f, 0.68f, 0.08f);
            driver.localRotation = Quaternion.identity;
            return;
        }

        Vector3 forward = playerVehicle.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;

        float halfWidth = BoundsProjectionRadius(carBounds, side);
        float halfLength = BoundsProjectionRadius(carBounds, forward);
        float carHeight = Mathf.Max(1f, carBounds.size.y);
        Vector3 cabinCenter =
            carBounds.center +
            forward * (halfLength * 0.08f) +
            Vector3.up * (carHeight * 0.03f);
        Vector3 driverCenter = cabinCenter - side * (halfWidth * 0.30f);

        driver.position = driverCenter;
        driver.rotation = Quaternion.LookRotation(forward, Vector3.up);
        float desiredHeight = carHeight * 0.78f;
        if (driverBounds.size.y > 0.001f)
            driver.localScale *= desiredHeight / driverBounds.size.y;

        if (TryGetRenderBounds(driver, out driverBounds))
            driver.position += driverCenter - driverBounds.center;

        PrefabUtility.RecordPrefabInstancePropertyModifications(driver);
    }

    private static bool TryGetRenderBounds(
        Transform root,
        out Bounds combined,
        Transform excludedRoot = null)
    {
        combined = default;
        bool found = false;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (excludedRoot != null && renderer.transform.IsChildOf(excludedRoot))
                continue;
            if (!renderer.enabled ||
                renderer is ParticleSystemRenderer ||
                renderer is TrailRenderer ||
                renderer is LineRenderer)
            {
                continue;
            }

            if (!found)
            {
                combined = renderer.bounds;
                found = true;
            }
            else
            {
                combined.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private static float BoundsProjectionRadius(Bounds bounds, Vector3 axis)
    {
        Vector3 extents = bounds.extents;
        return Mathf.Abs(axis.x) * extents.x +
               Mathf.Abs(axis.y) * extents.y +
               Mathf.Abs(axis.z) * extents.z;
    }

    private static Transform FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == objectName)
                    return candidate;
            }
        }

        return null;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }

    private static bool RequestExists()
    {
        return File.Exists(Path.GetFullPath(RequestAssetPath));
    }

    private static void DeleteRequestAsset()
    {
        if (RequestExists())
            AssetDatabase.DeleteAsset(RequestAssetPath);
    }
}
