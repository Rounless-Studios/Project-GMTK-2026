using System;
using System.Collections.Generic;
using SpinMotion;
using UnityEngine;
using UnityEngine.Rendering;

namespace GMTK
{
    /// <summary>
    /// Records the winning player's transform at a fixed cadence and replays the fastest stored run
    /// as a non-physical cyan ghost. The ghost is never registered with RCCP or the ranking system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GhostReplay : MonoBehaviour
    {
        [Serializable]
        private struct Frame
        {
            public float time;
            public Vector3 position;
            public Quaternion rotation;
        }

        [Serializable]
        private sealed class Run
        {
            public float duration;
            public List<Frame> frames = new();
        }

        private const string RunKey = "GMTK.GhostReplay.BestRun";
        private const float SampleInterval = 0.1f;
        private Run recording;
        private Run best;
        private Transform player;
        private Transform ghost;
        private float startedAt;
        private float nextSampleAt;
        private int playbackIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            GameObject host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<GhostReplay>() == null)
                host.AddComponent<GhostReplay>();
        }

        private void Start()
        {
            string json = PlayerPrefs.GetString(RunKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try { best = JsonUtility.FromJson<Run>(json); }
                catch (ArgumentException) { best = null; }
            }

            GameEvents events = Race.Events;
            if (events == null) return;
            events.RaceStartedEvent.AddListener(Begin);
            events.RestartRaceEvent.AddListener(Stop);
            events.RaceFinishedEvent.AddListener(Finish);
        }

        private void OnDestroy()
        {
            GameEvents events = Race.Events;
            if (events != null)
            {
                events.RaceStartedEvent.RemoveListener(Begin);
                events.RestartRaceEvent.RemoveListener(Stop);
                events.RaceFinishedEvent.RemoveListener(Finish);
            }
        }

        private void Begin()
        {
            Stop();
            GameObject playerObject = Race.CarByIndex(0);
            player = playerObject != null ? playerObject.transform : null;
            if (player == null) return;

            recording = new Run();
            startedAt = Time.time;
            nextSampleAt = 0f;
            playbackIndex = 0;
            if (best != null && best.frames != null && best.frames.Count >= 2)
                CreateGhost(best.frames[0]);
        }

        private void Update()
        {
            if (recording == null || player == null || !Race.IsRaceInProgress) return;
            float elapsed = Time.time - startedAt;
            if (elapsed >= nextSampleAt)
            {
                recording.frames.Add(new Frame
                {
                    time = elapsed,
                    position = player.position,
                    rotation = player.rotation
                });
                nextSampleAt += SampleInterval;
            }

            if (ghost != null) PlayGhost(elapsed);
        }

        private void Finish(RaceFinishType result)
        {
            if (recording == null) return;
            recording.duration = Time.time - startedAt;
            if (result == RaceFinishType.Win
                && recording.frames.Count >= 2
                && (best == null || best.duration <= 0f || recording.duration < best.duration))
            {
                best = recording;
                PlayerPrefs.SetString(RunKey, JsonUtility.ToJson(best));
                PlayerPrefs.Save();
            }
            recording = null;
        }

        private void Stop()
        {
            recording = null;
            player = null;
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
        }

        private void PlayGhost(float elapsed)
        {
            while (playbackIndex + 1 < best.frames.Count
                   && best.frames[playbackIndex + 1].time < elapsed)
                playbackIndex++;

            if (playbackIndex + 1 >= best.frames.Count)
            {
                ghost.gameObject.SetActive(false);
                return;
            }

            Frame a = best.frames[playbackIndex];
            Frame b = best.frames[playbackIndex + 1];
            float blend = Mathf.InverseLerp(a.time, b.time, elapsed);
            ghost.SetPositionAndRotation(
                Vector3.Lerp(a.position, b.position, blend),
                Quaternion.Slerp(a.rotation, b.rotation, blend));
        }

        private void CreateGhost(Frame first)
        {
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "GMTK_BestRunGhost";
            Destroy(visual.GetComponent<Collider>());
            visual.transform.localScale = new Vector3(1.8f, 0.65f, 4.2f);
            visual.transform.SetPositionAndRotation(first.position + Vector3.up * 0.5f, first.rotation);
            Renderer renderer = visual.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
            Material material = new(shader);
            material.color = new Color(0.1f, 0.9f, 1f, 0.42f);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
            renderer.material = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ghost = visual.transform;
        }
    }
}
