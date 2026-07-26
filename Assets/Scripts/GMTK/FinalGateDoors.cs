using System.Collections;
using System.Collections.Generic;
using Gmtk2026.GameBalance;
using GMTK.Kit;
using UnityEngine;

namespace GMTK
{
    /// <summary>
    /// Final-gate presentation (checklist 3.4). When the duel starts this builds a two-leaf
    /// gate across the finish checkpoint, reports crossings to <see cref="FinalGate"/>, and
    /// slams the leaves shut behind the winner so the racers locked outside are visibly and
    /// physically stopped by it. The win rule and all of its timing stay in
    /// <see cref="FinalGate"/>; the leaves animate off the same settings fields, so the
    /// animation and the rule can never disagree.
    /// Self-attaches to the GMTK game-mode host — nothing has to be placed in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FinalGateDoors : MonoBehaviour
    {
        // GDD palette: the gate reads white/gold against the red danger signals.
        private static readonly Color LeafColor = new(0.93f, 0.84f, 0.45f);
        private static readonly Color HousingColor = new(0.82f, 0.80f, 0.76f);

        [Header("Gate look")]
        [Tooltip("Thickness of a closing leaf along the driving direction.")]
        [SerializeField, Min(0.1f)] private float leafThickness = 0.6f;
        [Tooltip("Vertical clearance the gate is raised above the checkpoint pivot.")]
        [SerializeField] private float verticalOffset = 0f;
        [Tooltip("Depth of the crossing trigger along the driving direction.")]
        [SerializeField, Min(0.5f)] private float triggerDepth = 2f;

        [Header("Audio")]
        [Tooltip("Placeholder cue: a dedicated gate slam SFX is still missing (checklist 3.14).")]
        [SerializeField] private string slamCueId = "SFX_VEH_COLLISION_";

        private GameObject gateRoot;
        private Transform leftLeaf;
        private Transform rightLeaf;
        private Coroutine slamRoutine;
        private float halfWidth;

        private readonly Dictionary<GameObject, int> duelCarRoots = new();

        private PresentationSettings Presentation => GameBalance.Current.presentation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            GameObject host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<FinalGateDoors>() == null)
                host.AddComponent<FinalGateDoors>();
        }

        private void OnEnable()
        {
            FinalGate.GateOpened += OnGateOpened;
            FinalGate.GateSlamming += OnGateSlamming;
            FinalGate.GateReset += RemoveGate;
        }

        private void OnDisable()
        {
            FinalGate.GateOpened -= OnGateOpened;
            FinalGate.GateSlamming -= OnGateSlamming;
            FinalGate.GateReset -= RemoveGate;
        }

        private void OnDestroy() => RemoveGate();

        // ---- build ----

        private void OnGateOpened()
        {
            RemoveGate();

            if (!TryGetFinishPose(out Vector3 position, out Quaternion rotation))
            {
                Debug.LogWarning("[FinalGateDoors] No finish checkpoint found; the final gate " +
                                 "cannot be placed and the duel has no visible gate.", this);
                return;
            }

            CacheDuelCars();
            BuildGate(position, rotation);
            SetClosedAmount(0f);
        }

        /// <summary>
        /// Finish line = highest-numbered kit checkpoint. Its facing is taken from the previous
        /// checkpoint so the gate never depends on how the checkpoint prefab was rotated.
        /// </summary>
        private static bool TryGetFinishPose(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            var checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
            if (checkpoints == null || checkpoints.Length == 0) return false;

            Checkpoint last = null, previous = null;
            int lastNumber = int.MinValue, previousNumber = int.MinValue;
            foreach (var checkpoint in checkpoints)
            {
                if (checkpoint == null) continue;
                int number = checkpoint.GetNumber();
                if (number > lastNumber)
                {
                    previous = last; previousNumber = lastNumber;
                    last = checkpoint; lastNumber = number;
                }
                else if (number > previousNumber)
                {
                    previous = checkpoint; previousNumber = number;
                }
            }
            if (last == null) return false;

            position = last.transform.position;
            Vector3 forward = previous != null
                ? position - previous.transform.position
                : last.transform.forward;
            forward.y = 0f;
            rotation = forward.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(forward.normalized, Vector3.up)
                : last.transform.rotation;
            return true;
        }

        /// <summary>
        /// Root → race index for every car. Eliminated cars are filtered by
        /// <see cref="FinalGate.ReportGateCrossing"/>, which owns the duel roster.
        /// </summary>
        private void CacheDuelCars()
        {
            duelCarRoots.Clear();
            foreach (int index in Race.AllCarIndices())
            {
                GameObject car = Race.CarByIndex(index);
                if (car != null) duelCarRoots[car] = index;
            }
        }

        private void BuildGate(Vector3 position, Quaternion rotation)
        {
            var p = Presentation;
            halfWidth = p.gateWidthMeters * 0.5f;
            float height = p.gateHeightMeters;

            gateRoot = new GameObject("GMTK_FinalGate");
            gateRoot.transform.SetPositionAndRotation(position + Vector3.up * verticalOffset, rotation);

            // Housings sit outside the drivable width and swallow the retracted leaves, so an
            // open gate leaves the whole road clear.
            float housingCentre = halfWidth * 1.5f;
            MakeBox("HousingLeft", new Vector3(-housingCentre, height * 0.575f, 0f),
                new Vector3(halfWidth, height * 1.15f, leafThickness * 2.2f), HousingColor, false);
            MakeBox("HousingRight", new Vector3(housingCentre, height * 0.575f, 0f),
                new Vector3(halfWidth, height * 1.15f, leafThickness * 2.2f), HousingColor, false);

            // Leaves keep their colliders at all times: once they slam shut the loser is
            // physically stopped by the same geometry the player sees.
            leftLeaf = MakeBox("LeafLeft", Vector3.zero,
                new Vector3(halfWidth, height, leafThickness), LeafColor, true).transform;
            rightLeaf = MakeBox("LeafRight", Vector3.zero,
                new Vector3(halfWidth, height, leafThickness), LeafColor, true).transform;

            var trigger = new GameObject("CrossingTrigger", typeof(BoxCollider), typeof(FinalGateCrossingTrigger));
            trigger.transform.SetParent(gateRoot.transform, false);
            var box = trigger.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(p.gateWidthMeters, height, triggerDepth);
            trigger.GetComponent<FinalGateCrossingTrigger>().Bind(this);
        }

        private GameObject MakeBox(string boxName, Vector3 localPosition, Vector3 size, Color color, bool collide)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = boxName;
            box.transform.SetParent(gateRoot.transform, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;

            var collider = box.GetComponent<Collider>();
            if (collider != null)
            {
                if (collide) collider.enabled = true;
                else Destroy(collider);
            }

            var renderer = box.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var material = new Material(shader);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                renderer.material = material;
            }
            return box;
        }

        // ---- animation ----

        /// <summary>0 = fully retracted into the housings, 1 = leaves meeting at the centre.</summary>
        private void SetClosedAmount(float closed)
        {
            if (leftLeaf == null || rightLeaf == null) return;
            float height = Presentation.gateHeightMeters;
            float openCentre = halfWidth * 1.5f;
            float shutCentre = halfWidth * 0.5f;
            float centre = Mathf.Lerp(openCentre, shutCentre, closed);
            leftLeaf.localPosition = new Vector3(-centre, height * 0.5f, 0f);
            rightLeaf.localPosition = new Vector3(centre, height * 0.5f, 0f);
        }

        private void OnGateSlamming(int winnerIndex)
        {
            if (gateRoot == null) return;
            if (slamRoutine != null) StopCoroutine(slamRoutine);
            slamRoutine = StartCoroutine(SlamShut());
        }

        private IEnumerator SlamShut()
        {
            var p = Presentation;
            if (p.gateCloseDelaySeconds > 0f) yield return new WaitForSeconds(p.gateCloseDelaySeconds);

            if (!string.IsNullOrEmpty(slamCueId) && GameAudioManager.Instance != null)
                GameAudioManager.Instance.PlayCue(slamCueId);

            float duration = p.gateCloseDurationSeconds;
            if (duration <= 0f)
            {
                SetClosedAmount(1f);
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    SetClosedAmount(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
                    yield return null;
                }
                SetClosedAmount(1f);
            }
            slamRoutine = null;
        }

        // ---- crossing ----

        /// <summary>Called by the gate trigger; resolves the collider to a race index.</summary>
        internal void ReportTriggerEnter(Collider other)
        {
            var gate = FinalGate.Instance;
            if (gate == null || !gate.IsOpen || other == null) return;

            GameObject root = other.transform.root.gameObject;
            if (!duelCarRoots.TryGetValue(root, out int raceIndex)) return;
            gate.ReportGateCrossing(raceIndex);
        }

        private void RemoveGate()
        {
            if (slamRoutine != null) { StopCoroutine(slamRoutine); slamRoutine = null; }
            if (gateRoot != null) Destroy(gateRoot);
            gateRoot = null;
            leftLeaf = null;
            rightLeaf = null;
            duelCarRoots.Clear();
        }
    }

    /// <summary>Forwards the gate trigger's collisions to <see cref="FinalGateDoors"/>.</summary>
    [DisallowMultipleComponent]
    public sealed class FinalGateCrossingTrigger : MonoBehaviour
    {
        private FinalGateDoors owner;

        internal void Bind(FinalGateDoors doors) => owner = doors;

        private void OnTriggerEnter(Collider other)
        {
            if (owner != null) owner.ReportTriggerEnter(other);
        }
    }
}
