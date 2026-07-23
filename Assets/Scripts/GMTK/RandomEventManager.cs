using System.Collections.Generic;
using UnityEngine;
using SpinMotion;

namespace GMTK
{
    /// <summary>
    /// Fires random comedic hazards onto the track during a race: construction zones,
    /// livestock crossings, giant beach balls, meteor crates, mini earthquakes and boost
    /// pads. Placement uses the AI waypoint positions so hazards land on the track.
    /// Self-attaches to the game-mode host; no scene wiring required.
    /// </summary>
    public class RandomEventManager : MonoBehaviour
    {
        [Header("Event Timing")]
        public float firstEventDelay = 8f;
        public Vector2 intervalRange = new Vector2(9f, 16f);
        public bool enableEvents = true;

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

        private void TriggerRandomEvent()
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
            // a row of barrels/cones spread across the track with one gap to squeeze through
            int count = 5;
            int gap = Random.Range(0, count);
            for (int i = 0; i < count; i++)
            {
                if (i == gap) continue;
                float lateral = (i - (count - 1) * 0.5f) * 2.2f;
                Vector3 pos = wp.position + right * lateral + Vector3.up * 0.6f;
                var cone = MakeBox(pos, new Vector3(1f, 1.2f, 1f), new Color(1f, 0.5f, 0f), isStatic: true);
                cone.transform.rotation = wp.rotation;
                Register(cone, 9f);
            }
            // a little "sign"
            var sign = MakeBox(wp.position + Vector3.up * 1.6f, new Vector3(2.4f, 1.4f, 0.2f), new Color(1f, 0.8f, 0.1f), isStatic: true);
            sign.transform.rotation = wp.rotation;
            Register(sign, 9f);
        }

        private void LivestockCrossing()
        {
            if (!TryRandomWaypoint(out var wp)) return;
            Vector3 right = wp.right;
            // a "cow" trundling across the track from one side to the other
            Vector3 start = wp.position + right * -8f + Vector3.up * 1f;
            var cow = MakeBox(start, new Vector3(1.4f, 1.6f, 2.6f), new Color(0.95f, 0.95f, 0.95f), isStatic: false);
            var rb = cow.GetComponent<Rigidbody>();
            rb.mass = 40f;
            var mover = cow.AddComponent<ConstantMover>();
            mover.velocity = right * 5f;
            Register(cow, 10f);
        }

        private void GiantBeachBall()
        {
            if (!TryRandomWaypoint(out var wp)) return;
            Vector3 pos = wp.position + Vector3.up * 12f;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "GMTK_BeachBall";
            ball.transform.position = pos;
            ball.transform.localScale = Vector3.one * 5f;
            Paint(ball, new Color(1f, 0.2f, 0.4f));
            var rb = ball.AddComponent<Rigidbody>();
            rb.mass = 3f; // light so it bounces around comically
            var bounce = new PhysicsMaterial { bounciness = 0.85f, frictionCombine = PhysicsMaterialCombine.Minimum };
            ball.GetComponent<Collider>().material = bounce;
            Register(ball, 14f);
        }

        private void MeteorCrates()
        {
            var waypoints = GetWaypoints();
            if (waypoints.Count == 0) return;
            int drops = Random.Range(4, 8);
            for (int i = 0; i < drops; i++)
            {
                var wp = waypoints[Random.Range(0, waypoints.Count)];
                Vector3 pos = wp.position + Random.insideUnitSphere * 4f + Vector3.up * Random.Range(14f, 22f);
                pos.y = Mathf.Abs(pos.y);
                var crate = MakeBox(pos, Vector3.one * Random.Range(1.2f, 2f), new Color(0.5f, 0.3f, 0.15f), isStatic: false);
                crate.GetComponent<Rigidbody>().mass = 15f;
                Register(crate, 10f);
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
                    rb.AddForce(Vector3.up * Random.Range(4f, 7f) + Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
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
            pad.transform.localScale = new Vector3(6f, 0.15f, 3f);
            Paint(pad, new Color(0.1f, 0.9f, 1f));
            var col = pad.GetComponent<Collider>();
            col.isTrigger = true;
            pad.AddComponent<BoostPadTrigger>();
            Register(pad, 12f);
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
            var td = go.AddComponent<TimedDestroy>();
            td.lifetime = lifetime;
        }
    }

    /// <summary>Destroys the GameObject after a delay.</summary>
    public class TimedDestroy : MonoBehaviour
    {
        public float lifetime = 10f;
        private void Start() { Destroy(gameObject, lifetime); }
    }

    /// <summary>Moves a rigidbody at a constant velocity (used for the crossing "cow").</summary>
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
        private void OnTriggerEnter(Collider other)
        {
            var controller = other.transform.root.GetComponent<CarController>();
            if (controller == null) return;
            var rb = controller.GetComponent<Rigidbody>();
            if (rb == null) return;
            rb.AddForce(controller.transform.forward * 25f, ForceMode.VelocityChange);
        }
    }
}
