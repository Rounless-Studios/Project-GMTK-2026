using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GMTK
{
    /// <summary>Small keyed pool for repeatable, short-lived particle prefabs.</summary>
    [DisallowMultipleComponent]
    public sealed class PooledEffectService : MonoBehaviour
    {
        private readonly Dictionary<int, Queue<GameObject>> available = new();
        private readonly Dictionary<GameObject, int> prefabKeys = new();
        public static PooledEffectService Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttach()
        {
            GameObject host = GMTKGameMode.GetOrCreate();
            if (host.GetComponent<PooledEffectService>() == null)
                host.AddComponent<PooledEffectService>();
        }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public GameObject Play(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            float lifetime)
        {
            if (prefab == null) return null;
            int key = prefab.GetInstanceID();
            if (!available.TryGetValue(key, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                available.Add(key, pool);
            }

            GameObject effect = null;
            while (pool.Count > 0 && effect == null) effect = pool.Dequeue();
            if (effect == null)
            {
                effect = Instantiate(prefab, transform);
                prefabKeys[effect] = key;
            }

            effect.transform.SetPositionAndRotation(position, rotation);
            effect.transform.localScale = scale;
            effect.SetActive(true);
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Clear(true);
                particles.Play(true);
            }

            StartCoroutine(ReturnAfter(effect, Mathf.Max(0.05f, lifetime)));
            return effect;
        }

        private IEnumerator ReturnAfter(GameObject effect, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (effect == null || !prefabKeys.TryGetValue(effect, out int key)) yield break;
            effect.SetActive(false);
            effect.transform.SetParent(transform, false);
            if (!available.TryGetValue(key, out Queue<GameObject> pool))
            {
                pool = new Queue<GameObject>();
                available.Add(key, pool);
            }
            pool.Enqueue(effect);
        }
    }
}
