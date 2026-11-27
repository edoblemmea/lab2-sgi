using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Espectros
{
    // Hace aparecer a los fantasmas de cada ronda. Entran por la tele (bounding box de tipo pantalla)
    // o salen de las paredes; si no hay ninguna de las dos cosas, aparecen alrededor de la jugadora.
    public class GhostSpawner : MonoBehaviour
    {
        public static GhostSpawner Instance { get; private set; }

        [SerializeField] GhostController defaultPrefab;
        [SerializeField] GhostType defaultType;
        [Tooltip("Probabilidad de entrar por la tele si hay una")]
        [SerializeField, Range(0f, 1f)] float screenEntryChance = 0.5f;

        readonly List<GhostController> alive = new List<GhostController>();
        readonly List<GhostType> queue = new List<GhostType>();
        Coroutine routine;

        public GhostType DefaultType => defaultType;
        public int AliveCount => alive.Count;
        public int PendingCount => queue.Count;
        public int RemainingCount => alive.Count + queue.Count;
        public event Action<GhostController> GhostDied;
        public event Action<GhostController> GhostSpawned;

        void Awake() => Instance = this;

        void Start()
        {
            if (defaultPrefab == null || defaultType == null)
                Debug.LogError($"[Espectros] GhostSpawner: falta {(defaultPrefab == null ? "el prefab" : "")} " +
                               $"{(defaultType == null ? "el tipo de fantasma" : "")}. Vuelve a ejecutar el menú Espectros.");
        }

        public void StartRound(RoundConfig cfg)
        {
            Clear();
            foreach (var e in cfg.ghosts)
                if (e.type != null)
                    for (int i = 0; i < e.count; i++) queue.Add(e.type);
            // Mezclar el orden de los tipos
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (queue[i], queue[j]) = (queue[j], queue[i]);
            }
            routine = StartCoroutine(SpawnRoutine(cfg));
        }

        IEnumerator SpawnRoutine(RoundConfig cfg)
        {
            while (queue.Count > 0)
            {
                while (alive.Count >= Mathf.Max(1, cfg.maxAlive)) yield return null;
                var type = queue[0];
                queue.RemoveAt(0);
                Spawn(type, cfg.ghostSpeedMultiplier);
                yield return new WaitForSeconds(cfg.spawnInterval);
            }
            routine = null;
        }

        public GhostController Spawn(GhostType type, float speedMultiplier = 1f)
        {
            if (type == null) type = defaultType;
            var prefab = type != null && type.prefab != null ? type.prefab : defaultPrefab;
            if (prefab == null || type == null)
            {
                Debug.LogError("[Espectros] GhostSpawner: falta el prefab o el tipo de fantasma");
                return null;
            }

            ChooseEntry(out Vector3 entry, out Vector3 exit);
            var ghost = Instantiate(prefab, entry, Quaternion.identity);
            ghost.Init(type, entry, exit, speedMultiplier);
            ghost.Died += OnGhostDied;
            alive.Add(ghost);
            GhostSpawned?.Invoke(ghost);
            return ghost;
        }

        void ChooseEntry(out Vector3 entry, out Vector3 exit)
        {
            Vector3 head = PlayerHead.Position;

            // 1) Por la tele
            var screens = new List<FurnitureInfo>();
            foreach (var f in FurnitureInfo.All) if (f != null && f.kind == FurnitureKind.Screen) screens.Add(f);
            if (screens.Count > 0 && UnityEngine.Random.value < screenEntryChance)
            {
                var s = screens[UnityEngine.Random.Range(0, screens.Count)];
                entry = s.Center;
                Vector3 toRoom = Vector3.ProjectOnPlane(head - entry, Vector3.up).normalized;
                exit = entry + toRoom * 0.6f;
                return;
            }

            // 2) Desde dentro de una pared
            var wall = HidingSpotFinder.Instance != null ? HidingSpotFinder.Instance.RandomWallSpot() : null;
            if (wall != null)
            {
                entry = wall.position;
                exit = wall.peekPosition;
                return;
            }

            // 3) Alrededor de la jugadora
            float a = UnityEngine.Random.value * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            entry = head + dir * 2.5f + Vector3.down * 0.3f;
            exit = head + dir * 1.8f + Vector3.down * 0.3f;
        }

        void OnGhostDied(GhostController g)
        {
            alive.Remove(g);
            GhostDied?.Invoke(g);
        }

        public void Clear()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            queue.Clear();
            foreach (var g in alive) if (g != null) Destroy(g.gameObject);
            alive.Clear();
        }
    }
}
