using System.Collections.Generic;
using UnityEngine;
using SpinMotion;

namespace GMTK
{
    /// <summary>
    /// Fires random comedic hazards onto the track during a race: construction zones,
    /// livestock crossings, giant beach balls, meteor crates, mini earthquakes and boost
    /// pads. Placement uses the AI waypoint positions so hazards land on the track.
    /// All tunable values are exposed in the inspector.
    /// </summary>
    public class RandomEventManager : MonoBehaviour
    {
        [Header("Timing")]
        [Tooltip("Delay from race start to the first event.")]
        public float firstEventDelay = 8f;
        [Tooltip("Random seconds between events (x = min, y = max).")]
        public Vector2 intervalRange = new Vector2(9f, 16f);
        public bool enableEvents = true;

        [Header("Construction Zone")]
        public int constructionBarrierCount = 5;
        public float constructionSpacing = 2.2f;
        public Vector3 constructionBarrierSize = new Vector3(1f, 1.2f, 1f);
        public Color constructionBarrierColor = new Color(1f, 0.5f, 0f);
        public Vector3 constructionSignSize = new Vector3(2.4f, 1.4f, 0.2f);
        public Color constructionSignColor = new Color(1f, 0.8f, 0.1f);
        public float constructionLifetime = 9f;

        [Header("Livestock Crossing")]
        public Vector3 cowSize = new Vector3(1.4f, 1.6f, 2.6f);
        public Color cowColor = new Color(0.95f, 0.95f, 0.95f);
        public float cowMass = 40f;
        public float cowSpeed = 5f;
        public float cowStartSideOffset = 8f;
        public float cowLifetime = 10f;

        [Header("Giant Beach Ball")]
        public float ballScale = 5f;
        public float ballMass = 3f;
        public float ballBounciness = 0.85f;
        public float ballDropHeight = 12f;
        public Color ballColor = new Color(1f, 0.2f, 0.4f);
        public float ballLifetime = 14f;

        [Header("Meteor Crates")]
        public int meteorMinCount = 4;
        public int meteorMaxCount = 8;
        public Vector2 meteorDropHeightRange = new Vector2(14f, 22f);
        public float meteorScatterRadius = 4f;
        public Vector2 crateSizeRange = new Vector2(1.2f, 2f);
        public float crateMass = 15f;
        public Color crateColor = new Color(0.5f, 0.3f, 0.15f);
        public float meteorLifetime = 10f;

        [Header("Earthquake")]
        public Vector2 quakeUpForceRange = new Vector2(4f, 7f);
        public float quakeRandomForce = 2f;

        [Header("Boost Pad")]
        public Vector3 boostPadSize = new Vector3(6f, 0.15f, 3f);
        public Color boostPadColor = new Color(0.1f, 0.9f, 1f);
        public float boostForce = 25f;
        public float boostPadLifetime = 12f;

        private float nextEventTime;
        private bool armed;
        private readonly List<GameObject> spawned = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            var host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<RandomEventManager>() == null)
                host.AddComponent<RandomEventManager>();
        }

        private void Start()
        {
            var events = Race.Events;
            if (events == null) return;
            events.RaceStartedEvent.AddListener(OnRaceStarted);
            events.RestartRaceEvent.AddListener(ClearAll);
            events.RaceFinishedEvent.AddListener(_ => armed = false);
        }

        private void OnRaceStarted()
        {
            ClearAll();
            armed = enableEvents;
            nextEventTime = Time.time + firstEventDelay;
        }

        private void ClearAll()
        {
            armed = false;
            foreach (var go in spawned) if (go != null) Destroy(go);
            spawned.Clear();
        }

        private void Update()
        {
            if (!armed || !Race.IsRaceInProgress) return;
            if (Time.time < nextEventTime) return;

            nextEventTime = Time.time + Random.Range(intervalRange.x, intervalRange.y);
            TriggerRandomEvent();
        }

        /// <summary>Fire one random hazard immediately (also callable from tools/tests).</summary>
        public void TriggerRandomEvent()
        {
            switch (Random.Range(0, 6))
            {
                case 0: ConstructionZone(); break;
                case 1: LivestockCrossing(); break;
                case 2: GiantBeachBall(); break;
                case 3: MeteorCrates(); break;
                case 4: Earthquake(); break;
                default: BoostPad(); break;
            }
        }

        // ---- events ---------------------------------------------------------

        private void ConstructionZone()
        {
            if (!TryRandomWaypoint(out var wp)) return;
            Vector3 right = wp.right;
            int count = constructionBarrierCount;
            int gap = Random.Range(0, count); // one gap to squeeze through
            for (int i = 0; i < count; i++)
            {
                if (i == gap) continue;
                float lateral = (i - (count - 1) * 0.5f) * constructionSpacing;
                Vector3 pos = wp.position + right * lateral + Vector3.up * (constructionBarrierSize.y * 0.5f);
                var barrier = MakeBox(pos, constructionBarrierSize, constructionBarrierColor, isStatic: true);
                barrier.transform.rotation = wp.rotation;
                Register(barrier, constructionLifetime);
            }
            var sign = MakeBox(wp.position + Vector3.up * 1.6f, constructionSignSize, constructionSignColor, isStatic: true);
            sign.transform.rotation = wp.rotation;
            Register(sign, constructionLifetime);
        }

        private void LivestockCrossing()
        {
            if (!TryRandomWaypoint(out var wp)) return;
            Vector3 right = wp.right;
            Vector3 start = wp.position + right * -cowStartSideOffset + Vector3.up * 1f;
            var cow = MakeBox(start, cowSize, cowColor, isStatic: false);
            cow.GetComponent<Rigidbody>().mass = cowMass;
            cow.AddComponent<ConstantMover>().velocity = right * cowSpeed;
            Register(cow, cowLifetime);
        }

        private void GiantBeachBall()
        {
            if (!TryRandomWaypoint(out var wp)) return;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "GMTK_BeachBall";
            ball.transform.position = wp.position + Vector3.up * ballDropHeight;
            ball.transform.localScale = Vector3.one * ballScale;
            Paint(ball, ballColor);
            ball.AddComponent<Rigidbody>().mass = ballMass;
            ball.GetComponent<Collider>().material =
                new PhysicsMaterial { bounciness = ballBounciness, frictionCombine = PhysicsMaterialCombine.Minimum };
            Register(ball, ballLifetime);
        }

        private void MeteorCrates()
        {
            var waypoints = GetWaypoints();
            if (waypoints.Count == 0) return;
            int drops = Random.Range(meteorMinCount, meteorMaxCount);
            for (int i = 0; i < drops; i++)
            {
                var wp = waypoints[Random.Range(0, waypoints.Count)];
                Vector3 pos = wp.position + Random.insideUnitSphere * meteorScatterRadius
                              + Vector3.up * Random.Range(meteorDropHeightRange.x, meteorDropHeightRange.y);
                pos.y = Mathf.Abs(pos.y);
                var crate = MakeBox(pos, Vector3.one * Random.Range(crateSizeRange.x, crateSizeRange.y), crateColor, isStatic: false);
                crate.GetComponent<Rigidbody>().mass = crateMass;
                Register(crate, meteorLifetime);
            }
        }

        private void Earthquake()
        {
            foreach (int idx in Race.AllCarIndices())
            {
                var car = Race.CarByIndex(idx);
                if (car == null) continue;
                foreach (var rb in car.GetComponentsInChildren<Rigidbody>())
                {
                    if (rb == null) continue;
                    rb.AddForce(Vector3.up * Random.Range(quakeUpForceRange.x, quakeUpForceRange.y)
                                + Random.insideUnitSphere * quakeRandomForce, ForceMode.VelocityChange);
                }
            }
        }

        private void BoostPad()
        {
            if (!TryRandomWaypoint(out var wp)) return;
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "GMTK_BoostPad";
            pad.transform.position = wp.position + Vector3.up * 0.1f;
            pad.transform.rotation = wp.rotation;
            pad.transform.localScale = boostPadSize;
            Paint(pad, boostPadColor);
            pad.GetComponent<Collider>().isTrigger = true;
            pad.AddComponent<BoostPadTrigger>().boostForce = boostForce;
            Register(pad, boostPadLifetime);
        }

        // ---- helpers --------------------------------------------------------

        private bool TryRandomWaypoint(out Transform wp)
        {
            var waypoints = GetWaypoints();
            if (waypoints.Count == 0) { wp = null; return false; }
            wp = waypoints[Random.Range(0, waypoints.Count)];
            return wp != null;
        }

        private List<Transform> GetWaypoints()
        {
            var list = new List<Transform>();
            AIWaypointSet set = null;
            var tracker = Object.FindAnyObjectByType<AIWaypointTracker>();
            if (tracker != null) set = tracker.aiWaypointSet;
            if (set == null)
            {
                var wps = Object.FindAnyObjectByType<AIWaypoints>();
                if (wps != null) set = wps.aiWaypointSet;
            }
            if (set != null)
                foreach (var w in set.Items)
                    if (w != null && w.aiWaypointTransform != null) list.Add(w.aiWaypointTransform);
            return list;
        }

        private GameObject MakeBox(Vector3 pos, Vector3 size, Color color, bool isStatic)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.position = pos;
            box.transform.localScale = size;
            Paint(box, color);
            if (!isStatic) box.AddComponent<Rigidbody>();
            return box;
        }

        private static void Paint(GameObject go, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            rend.material = mat;
        }

        private void Register(GameObject go, float lifetime)
        {
            spawned.Add(go);
            go.AddComponent<TimedDestroy>().lifetime = lifetime;
        }
    }

    /// <summary>Destroys the GameObject after a delay.</summary>
    public class TimedDestroy : MonoBehaviour
    {
        public float lifetime = 10f;
        private void Start() { Destroy(gameObject, lifetime); }
    }

    /// <summary>Moves a rigidbody at a constant horizontal velocity (the crossing "cow").</summary>
    public class ConstantMover : MonoBehaviour
    {
        public Vector3 velocity;
        private Rigidbody rb;
        private void Awake() { rb = GetComponent<Rigidbody>(); }
        private void FixedUpdate()
        {
            if (rb != null) rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
        }
    }

    /// <summary>Boost pad: shoves any car that drives over it forward.</summary>
    public class BoostPadTrigger : MonoBehaviour
    {
        public float boostForce = 25f;
        private void OnTriggerEnter(Collider other)
        {
            var controller = other.transform.root.GetComponent<CarController>();
            if (controller == null) return;
            var rb = controller.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(controller.transform.forward * boostForce, ForceMode.VelocityChange);
        }
    }
}
