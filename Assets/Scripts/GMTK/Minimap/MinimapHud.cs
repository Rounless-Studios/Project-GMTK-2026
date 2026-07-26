using System.Collections.Generic;
using Gmtk2026.GameBalance;
using GMTK.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace GMTK.Minimap
{
    public enum MinimapViewMode
    {
        /// <summary>The stretch of track around the player, turning with the car.</summary>
        FollowPlayer = 0,
        /// <summary>The whole lap fitted into the panel, north up.</summary>
        WholeTrack = 1
    }

    /// <summary>
    /// Top-down track minimap. It draws the baked AI waypoint path as a ribbon around the player
    /// rather than rendering the level, so there is no second camera and no render texture: the panel
    /// costs one line mesh plus one blip mesh per frame, and it works on whatever
    /// <see cref="TrackAuthoring.RaceTrackAuthoring"/> last baked.
    /// <para>
    /// Self-attaches on its own canvas below <see cref="RaceHud"/>, so no scene wiring is needed. The
    /// path is read with the same rule as <see cref="GmtkRaceProgress"/> — waypoint children that
    /// carry a MeshRenderer — so the minimap, the AI and the standings all follow one line.
    /// </para>
    /// </summary>
    public sealed class MinimapHud : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private MinimapViewMode viewMode = MinimapViewMode.FollowPlayer;
        [Tooltip("The frame the map is drawn inside. Empty is the normal case: authored in a HUD canvas " +
            "the component uses its own rect, and on a canvas of its own it builds the panel below.")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private Vector2 panelSizePixels = new(260f, 260f);
        [Tooltip("Screen corner a self-built panel hangs from: (1,0) is bottom-right.")]
        [SerializeField] private Vector2 panelAnchor = new(1f, 0f);
        [SerializeField] private Vector2 panelMarginPixels = new(-26f, 26f);

        [Header("View")]
        [Tooltip("Metres of track visible ahead of and behind the car in Follow Player mode.")]
        [SerializeField, Min(20f)] private float visibleRadiusMetres = 150f;
        [Tooltip("Turn the map so the car always drives up the panel. Off leaves it north up.")]
        [SerializeField] private bool rotateWithPlayer = true;
        [SerializeField, Min(0f)] private float headingSmoothing = 10f;
        [Tooltip("Drawn road width in metres. The waypoint path carries no width, so this is a guess " +
            "that only has to read as a road at panel scale.")]
        [SerializeField, Min(1f)] private float roadWidthMetres = 14f;
        [Tooltip("Outline detail: waypoints closer together than this are dropped.")]
        [SerializeField, Min(1f)] private float outlineSpacingMetres = 8f;
        [Tooltip("Point budget for the Whole Track outline. A 20 km lap holds thousands of waypoints, " +
            "and at panel scale almost all of them land on the same pixel.")]
        [SerializeField, Min(64)] private int wholeTrackPointBudget = 600;

        [Header("Colours")]
        [Tooltip("Self-built panel only. An authored panel keeps the colour of its own Image.")]
        [SerializeField] private Color backgroundColor = new(0.03f, 0.04f, 0.06f, 0.55f);
        [SerializeField] private Color trackColor = new(0.72f, 0.76f, 0.85f, 0.9f);
        [SerializeField] private Color playerColor = new(0.35f, 1f, 0.5f, 1f);
        [Tooltip("Every rival, the purge target included: one red so a glance reads as 'where they are'.")]
        [SerializeField] private Color rivalColor = new(0.9f, 0.15f, 0.12f, 1f);
        [SerializeField, Min(2f)] private float playerMarkerPixels = 15f;
        [SerializeField, Min(2f)] private float rivalMarkerPixels = 9f;

        private Canvas canvas;
        private Image background;
        private MinimapRibbon ribbon;
        private MinimapMarkers markers;

        private readonly List<Vector3> waypoints = new();
        private readonly List<Vector3> path = new();
        private readonly List<Vector3> window = new();
        private readonly List<Vector2> panelPoints = new();

        private TrackProgress progress;
        private TrackProgress.CarCursor playerCursor;
        private Transform[] carRoots;
        private int[] raceIndices;
        private Transform playerRoot;
        private EliminationManager elimination;

        private Vector2 trackCentre;
        private Vector2 drawnPanelSize;
        private float wholeTrackPixelsPerMetre = 1f;
        private bool wholeTrackDrawn;
        private MinimapViewMode drawnViewMode;
        private float smoothedHeading;
        private bool headingPlaced;
        private bool pathMissingReported;

        /// <summary>
        /// Segments <see cref="TrackProgress"/> scans either side of the player's last one. A car
        /// covers a couple of metres per frame, so a handful is plenty — and a wide window is what
        /// lets a hairpin snap the cursor onto the straight coming back the other way.
        /// </summary>
        private const int CursorSearchWindow = 6;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            if (FindFirstObjectByType<MinimapHud>(FindObjectsInactive.Include) != null) return;

            var root = new GameObject("GMTK_Minimap");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // one below RaceHud: its purge warnings must never end up behind the map
            canvas.sortingOrder = 79;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            // last, so the component's Awake already finds the canvas it lives on
            root.AddComponent<MinimapHud>();
        }

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            BuildUi();
            elimination = FindFirstObjectByType<EliminationManager>(FindObjectsInactive.Include);
            SetVisible(false);
        }

        private void Start()
        {
            GameEvents events = Race.Events;
            if (events != null)
            {
                events.PlayersCheckpointTrackersAssignedEvent.AddListener(OnPlayersAssigned);
                events.RaceStartedEvent.AddListener(OnRaceStarted);
            }

            // living in the RaceUI prefab, this panel is switched on partway through the race flow, by
            // which time the spawner has already announced the cars. Pick them up from the kit instead
            // of waiting for an event that has been and gone.
            RealTimeRacePositions positions = Race.Positions;
            if (positions != null && positions.CarCheckpointTrackers.Count > 0)
                OnPlayersAssigned(positions.CarCheckpointTrackers);
        }

        private void OnDestroy()
        {
            GameEvents events = Race.Events;
            if (events == null) return;

            events.PlayersCheckpointTrackersAssignedEvent.RemoveListener(OnPlayersAssigned);
            events.RaceStartedEvent.RemoveListener(OnRaceStarted);
        }

        /// <summary>
        /// Fills in whatever the prefab does not author. The RaceUI prefab owns the panel — its frame,
        /// its corner and its size — while the two line meshes are built here: they are procedural
        /// geometry with nothing for a designer to set, and authoring them would only add two empty
        /// objects to the prefab.
        /// </summary>
        private void BuildUi()
        {
            // authored inside a HUD canvas, this component sits on the panel itself; owning a canvas
            // means nothing authored the frame, so build one
            if (panel == null) panel = canvas == null ? transform as RectTransform : null;
            if (panel == null) panel = BuildStandalonePanel();

            background = panel.GetComponent<Image>();
            if (background != null) background.raycastTarget = false;
            // the map is drawn well past the panel edges, so something has to cut it off
            if (panel.GetComponent<RectMask2D>() == null) panel.gameObject.AddComponent<RectMask2D>();

            if (ribbon == null) ribbon = NewLayer<MinimapRibbon>("Track", trackColor);
            if (markers == null) markers = NewLayer<MinimapMarkers>("Cars", Color.white);
        }

        /// <summary>The fallback frame for a scene that has no authored HUD to hang the map on.</summary>
        private RectTransform BuildStandalonePanel()
        {
            // every graphic gets its CanvasRenderer added by hand: neither the GameObject constructor
            // nor AddComponent honours the [RequireComponent] Graphic inherits, and a UI graphic
            // without one throws out of RectMask2D's clipping pass on every frame it is visible
            var panelObject = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer));
            var rect = panelObject.GetComponent<RectTransform>();
            panelObject.AddComponent<Image>().color = backgroundColor;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = panelAnchor;
            rect.pivot = panelAnchor;
            rect.sizeDelta = panelSizePixels;
            rect.anchoredPosition = panelMarginPixels;
            return rect;
        }

        private T NewLayer<T>(string layerName, Color tint) where T : MaskableGraphic
        {
            var layer = new GameObject(layerName, typeof(RectTransform), typeof(CanvasRenderer));
            var rect = layer.GetComponent<RectTransform>();
            rect.SetParent(panel, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            T graphic = layer.AddComponent<T>();
            graphic.color = tint;
            graphic.raycastTarget = false;
            return graphic;
        }

        private void OnPlayersAssigned(List<CheckpointTracker> trackers)
        {
            if (trackers == null) return;

            int count = 0;
            foreach (CheckpointTracker tracker in trackers)
                if (tracker != null) count++;

            carRoots = new Transform[count];
            raceIndices = new int[count];
            playerRoot = null;

            int slot = 0;
            foreach (CheckpointTracker tracker in trackers)
            {
                if (tracker == null) continue;
                carRoots[slot] = tracker.transform.root;
                raceIndices[slot] = tracker.GetCarRacePositionIndex();
                if (raceIndices[slot] == 0) playerRoot = carRoots[slot];
                slot++;
            }

            BuildPath();
        }

        private void OnRaceStarted()
        {
            TrackProgress.Unplace(ref playerCursor);
            headingPlaced = false;
            wholeTrackDrawn = false;
        }

        /// <summary>Reads the baked waypoint path out of the scene, once per race.</summary>
        private void BuildPath()
        {
            AIWaypoints source = FindAnyObjectByType<AIWaypoints>();
            if (source == null)
            {
                if (!pathMissingReported)
                    Debug.LogWarning("MinimapHud: no AI waypoint path in the scene, so the minimap " +
                        "stays hidden.");
                pathMissingReported = true;
                return;
            }

            waypoints.Clear();
            Transform[] candidates = source.GetComponentsInChildren<Transform>(true);
            // same selection rule as GmtkRccpWaypointPath and GmtkRaceProgress: the AI, the standings
            // and the map all have to follow one line
            for (int i = 1; i < candidates.Length; i++)
                if (candidates[i].TryGetComponent(out MeshRenderer _))
                    waypoints.Add(candidates[i].position);

            RebuildOutline();
        }

        /// <summary>
        /// Thins the waypoints down to what the panel can show. Follow Player keeps the dense path,
        /// since it only ever draws a short window of it; Whole Track gets a coarser copy, because
        /// thousands of points would land on the same handful of pixels.
        /// </summary>
        private void RebuildOutline()
        {
            float spacing = outlineSpacingMetres;
            if (viewMode == MinimapViewMode.WholeTrack)
                spacing = Mathf.Max(spacing,
                    MinimapView.FlatLength(waypoints) / Mathf.Max(64, wholeTrackPointBudget));
            MinimapView.Thin(waypoints, spacing, path);
            drawnViewMode = viewMode;

            if (path.Count < 2)
            {
                if (waypoints.Count > 0)
                    Debug.LogWarning($"MinimapHud: the waypoint path has only {path.Count} usable " +
                        "points, so the minimap stays hidden.");
                return;
            }

            progress = new TrackProgress(path, CursorSearchWindow, Mathf.Max(30f, spacing * 8f));
            MinimapView.Bounds(path, out trackCentre, out Vector2 sizeMetres);
            drawnPanelSize = PanelSize();
            wholeTrackPixelsPerMetre = MinimapView.FitPixelsPerMetre(sizeMetres, drawnPanelSize,
                roadWidthMetres * 0.5f + 6f);
            TrackProgress.Unplace(ref playerCursor);
            wholeTrackDrawn = false;
        }

        private Vector2 PanelSize() => panel != null ? panel.rect.size : panelSizePixels;

        private void Update()
        {
            bool visible = ShouldShow();
            SetVisible(visible);
            if (!visible) return;

            // the view mode is a live inspector switch, and a resolution change resizes the panel:
            // both change how much track fits, so the outline has to be measured again
            if (drawnViewMode != viewMode || drawnPanelSize != PanelSize())
            {
                RebuildOutline();
                if (path.Count < 2) return;
            }

            if (viewMode == MinimapViewMode.WholeTrack) DrawWholeTrack();
            else DrawAroundPlayer();
        }

        /// <summary>
        /// Hides the map outside the race by switching the graphics off rather than deactivating the
        /// panel: inside the RaceUI prefab this component lives on the panel itself, and a component
        /// that deactivates its own object never runs the frame that would bring it back.
        /// </summary>
        private void SetVisible(bool visible)
        {
            if (canvas != null) canvas.enabled = visible;
            if (background != null) background.enabled = visible;
            if (ribbon != null) ribbon.enabled = visible;
            if (markers != null) markers.enabled = visible;
        }

        private bool ShouldShow()
        {
            if (!Application.isPlaying) return false;
            if (path.Count < 2 || playerRoot == null) return false;

            RacePhase phase = GMTKRaceState.Instance != null
                ? GMTKRaceState.Instance.CurrentPhase
                : (Race.IsRaceInProgress ? RacePhase.Racing : RacePhase.Boot);
            return phase == RacePhase.Racing || phase == RacePhase.FinalDuel;
        }

        private void DrawAroundPlayer()
        {
            Vector3 playerPosition = playerRoot.position;
            progress.Advance(ref playerCursor, playerPosition);

            var centre = new Vector2(playerPosition.x, playerPosition.z);
            float pixelsPerMetre = Mathf.Min(PanelSize().x, PanelSize().y) * 0.5f
                / Mathf.Max(1f, visibleRadiusMetres);
            float upHeading = UpHeading();

            // 1.5x the radius: the panel corners are further from the middle than its edges are, and a
            // ribbon that stopped at the radius would show a gap there
            MinimapView.Window(path, playerCursor.segment, visibleRadiusMetres * 1.5f, window);

            panelPoints.Clear();
            for (int i = 0; i < window.Count; i++)
                panelPoints.Add(MinimapView.ToPanel(window[i], centre, pixelsPerMetre, upHeading));
            ribbon.SetPolyline(panelPoints, false, roadWidthMetres * pixelsPerMetre);

            DrawCars(centre, pixelsPerMetre, upHeading);
        }

        private void DrawWholeTrack()
        {
            float pixelsPerMetre = wholeTrackPixelsPerMetre;

            if (!wholeTrackDrawn)
            {
                panelPoints.Clear();
                for (int i = 0; i < path.Count; i++)
                    panelPoints.Add(MinimapView.ToPanel(path[i], trackCentre, pixelsPerMetre, 0f));
                ribbon.SetPolyline(panelPoints, true,
                    Mathf.Max(2f, roadWidthMetres * pixelsPerMetre));
                wholeTrackDrawn = true;
            }

            DrawCars(trackCentre, pixelsPerMetre, 0f);
        }

        /// <summary>
        /// One blip per car still in the race: every rival red, the player's own arrow green. An
        /// eliminated car is dropped rather than greyed out — it is no longer a threat or a target.
        /// </summary>
        private void DrawCars(Vector2 centre, float pixelsPerMetre, float upHeading)
        {
            markers.Clear();
            if (carRoots == null)
            {
                markers.Flush();
                return;
            }

            for (int i = 0; i < carRoots.Length; i++)
            {
                if (carRoots[i] == null || raceIndices[i] == 0) continue;
                if (elimination != null && elimination.IsEliminated(raceIndices[i])) continue;

                markers.AddDiamond(
                    MinimapView.ToPanel(carRoots[i].position, centre, pixelsPerMetre, upHeading),
                    rivalMarkerPixels,
                    rivalColor);
            }

            // the player goes last so its arrow stays on top of any rival sharing the same pixel
            markers.AddArrow(
                MinimapView.ToPanel(playerRoot.position, centre, pixelsPerMetre, upHeading),
                MinimapView.Heading(playerRoot.forward) - upHeading,
                playerMarkerPixels,
                playerColor);
            markers.Flush();
        }

        /// <summary>
        /// World heading that points up the panel. Steering wobble would shake the whole map, so the
        /// rotating view follows a damped heading rather than the car's own.
        /// </summary>
        private float UpHeading()
        {
            if (!rotateWithPlayer) return 0f;

            float heading = MinimapView.Heading(playerRoot.forward);
            if (!headingPlaced || headingSmoothing <= 0f)
            {
                headingPlaced = true;
                smoothedHeading = heading;
                return smoothedHeading;
            }

            smoothedHeading = Mathf.LerpAngle(smoothedHeading, heading,
                1f - Mathf.Exp(-headingSmoothing * Time.deltaTime));
            return smoothedHeading;
        }
    }
}
