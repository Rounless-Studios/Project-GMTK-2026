using System.Collections.Generic;
using UnityEngine;
using GMTK.Kit;
using Gmtk2026.GameBalance;
using Gmtk2026.Quiz;

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
        private int eventsTriggered;
        private int lastEvent = -1;
        private int activeEvents;

        private SpecialEventSettings Settings => GameBalance.Current.specialEvents;

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
            events.RaceFinishedEvent.AddListener(OnRaceFinished);
        }

        private void OnDestroy()
        {
            GameEvents events = Race.Events;
            if (events == null) return;
            events.RaceStartedEvent.RemoveListener(OnRaceStarted);
            events.RestartRaceEvent.RemoveListener(ClearAll);
            events.RaceFinishedEvent.RemoveListener(OnRaceFinished);
        }

        private void OnRaceFinished(RaceFinishType _) => armed = false;

        private void OnRaceStarted()
        {
            ClearAll();
            eventsTriggered = 0;
            lastEvent = -1;
            activeEvents = 0;
            armed = enableEvents && Settings.enabled;
            nextEventTime = Time.time + Settings.initialDelaySeconds;
        }

        private void ClearAll()
        {
            armed = false;
            foreach (var go in spawned) if (go != null) Destroy(go);
            spawned.Clear();
            activeEvents = 0;
        }

        private void Update()
        {
            if (!armed || !Race.IsRaceInProgress) return;
            if (Time.time < nextEventTime) return;
            if (eventsTriggered >= Settings.maximumEventsPerRace)
            {
                armed = false;
                return;
            }
            if (IsProtectedPhase())
            {
                nextEventTime = Time.time + 0.5f;
                return;
            }
            if (activeEvents >= Settings.maximumSimultaneousEvents) return;

            nextEventTime = Time.time + Random.Range(
                Settings.minimumIntervalSeconds,
                Settings.maximumIntervalSeconds);
            TriggerRandomEvent();
        }

        /// <summary>Fire one random hazard immediately (also callable from tools/tests).</summary>
        public void TriggerRandomEvent()
        {
            int next;
            do next = Random.Range(0, 3);
            while (Settings.preventImmediateRepeat && next == lastEvent);
            lastEvent = next;
            eventsTriggered++;
            activeEvents++;
            StartCoroutine(ReleaseEventSlotAfter(
                Mathf.Max(Settings.dumpTruckLifetimeSeconds,
                    Settings.earthquakeDurationSeconds,
                    Settings.meteorDebrisLifetimeSeconds + Settings.meteorWarningSeconds)));

            switch (next)
            {
                case 0: DumpTruck(); break;
                case 1: StartCoroutine(EarthquakeOverTime()); break;
                default: StartCoroutine(WarnThenMeteor()); break;
            }
        }

        private bool IsProtectedPhase()
        {
            if (Settings.blockDuringFinalDuel &&
                GMTKRaceState.Instance != null &&
                GMTKRaceState.Instance.CurrentPhase == RacePhase.FinalDuel)
                return true;

            EliminationManager elimination = FindFirstObjectByType<EliminationManager>();
            if (elimination != null)
            {
                if (Settings.blockDuringExecutionWarning &&
                    elimination.Level != EliminationWarningLevel.None)
                    return true;
                if (elimination.SecondsToElimination <=
                    Settings.blockBeforeEliminationSeconds)
                    return true;
            }

            if (Settings.blockDuringOvertakeChallenge &&
                OvertakeManager.Instance?.Challenge?.Status == OvertakeStatus.Active)
                return true;

            if (Settings.blockDuringQuiz)
            {
                QuizSessionController quiz =
                    FindFirstObjectByType<QuizSessionController>(FindObjectsInactive.Include);
                if (quiz != null && quiz.State != QuizSessionState.Waiting) return true;
            }

            return false;
        }

        private System.Collections.IEnumerator ReleaseEventSlotAfter(float seconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));
            activeEvents = Mathf.Max(0, activeEvents - 1);
        }

        private void DumpTruck()
        {
            if (!TryRandomWaypoint(out Transform waypoint)) return;
            Vector3 right = waypoint.right;
            GameObject truck = MakeBox(
                waypoint.position - right * 12f + Vector3.up * 1.5f,
                new Vector3(3.5f, 3f, 7f),
                new Color(0.75f, 0.16f, 0.05f),
                isStatic: false);
            truck.name = "GMTK_DumpTruck";
            truck.transform.rotation = Quaternion.LookRotation(right, Vector3.up);
            Rigidbody body = truck.GetComponent<Rigidbody>();
            body.mass = 1200f;
            truck.AddComponent<ConstantMover>().velocity =
                right * Settings.dumpTruckSpeed;
            HazardImpact impact = truck.AddComponent<HazardImpact>();
            impact.damage = Settings.dumpTruckDamage;
            impact.knockback = Settings.dumpTruckKnockback;
            Register(truck, Settings.dumpTruckLifetimeSeconds);
        }

        private System.Collections.IEnumerator EarthquakeOverTime()
        {
            float end = Time.time + Settings.earthquakeDurationSeconds;
            int direction = Random.value < 0.5f ? -1 : 1;
            while (Time.time < end)
            {
                foreach (int index in Race.AllCarIndices())
                {
                    GameObject car = Race.CarByIndex(index);
                    Rigidbody body = car != null ? car.GetComponent<Rigidbody>() : null;
                    if (body != null)
                        body.AddForce(
                            car.transform.right * direction *
                            Settings.earthquakeLateralVelocityChange * Time.deltaTime,
                            ForceMode.VelocityChange);
                }
                direction *= -1;
                yield return null;
            }
        }

        private System.Collections.IEnumerator WarnThenMeteor()
        {
            if (!TryRandomWaypoint(out Transform waypoint)) yield break;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "GMTK_MeteorWarning";
            marker.transform.position = waypoint.position + Vector3.up * 0.05f;
            marker.transform.localScale =
                new Vector3(Settings.meteorImpactRadius, 0.03f, Settings.meteorImpactRadius);
            Paint(marker, new Color(1f, 0.08f, 0.02f));
            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null) Destroy(markerCollider);
            Register(marker, Settings.meteorWarningSeconds);

            yield return new WaitForSeconds(Settings.meteorWarningSeconds);

            Collider[] hits = Physics.OverlapSphere(
                waypoint.position,
                Settings.meteorImpactRadius);
            var affected = new HashSet<Rigidbody>();
            foreach (Collider hit in hits)
            {
                Rigidbody body = hit.attachedRigidbody;
                if (body == null || !affected.Add(body)) continue;
                DurabilityController durability =
                    body.GetComponentInParent<DurabilityController>();
                durability?.ApplyDamage(Settings.meteorDamage);
                Vector3 away = body.worldCenterOfMass - waypoint.position;
                away.y = Mathf.Max(0.25f, away.y);
                body.AddForce(
                    away.normalized * Settings.meteorKnockback,
                    ForceMode.VelocityChange);
            }

            GameObject debris = MakeBox(
                waypoint.position + Vector3.up,
                Vector3.one * 2f,
                new Color(0.35f, 0.12f, 0.04f),
                isStatic: false);
            debris.name = "GMTK_MeteorDebris";
            Register(debris, Settings.meteorDebrisLifetimeSeconds);
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
            var adapter = other.GetComponentInParent<GmtkVehicleAdapter>();
            if (adapter != null)
                adapter.ApplyForwardImpulse(boostForce);
        }
    }

    /// <summary>Applies authored hazard damage once per impacted vehicle.</summary>
    public sealed class HazardImpact : MonoBehaviour
    {
        public float damage;
        public float knockback;
        private readonly HashSet<Rigidbody> hitBodies = new();

        private void OnCollisionEnter(Collision collision)
        {
            Rigidbody body = collision.rigidbody;
            if (body == null || !hitBodies.Add(body)) return;
            collision.gameObject.GetComponentInParent<DurabilityController>()?.ApplyDamage(damage);
            Vector3 away = collision.transform.position - transform.position;
            away.y = 0.2f;
            body.AddForce(away.normalized * knockback, ForceMode.VelocityChange);
        }
    }
}
