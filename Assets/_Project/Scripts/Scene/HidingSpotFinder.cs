using System;
using System.Collections.Generic;
using UnityEngine;

namespace Espectros
{
    public enum SpotKind { BehindFurniture, UnderTable, InWall, Ceiling, BehindPlayer }

    public class HidingSpot
    {
        public Vector3 position;        // donde se queda escondido el fantasma
        public Vector3 peekPosition;    // donde se asoma (fuera de la pared, al lado del mueble...)
        public SpotKind kind;
        public FurnitureInfo furniture;
        public bool hidden;             // la cabeza no lo ve (Linecast bloqueado por la mesh o un mueble)
        public object owner;            // quién lo tiene reservado
        public bool Reserved => owner != null;
    }

    // Genera puntos candidatos alrededor y debajo de los muebles, dentro de las paredes y en el techo,
    // y cada ~0,5 s marca cuáles están ocultos con Physics.Linecast desde la cabeza.
    // Lo usan los fantasmas (y más adelante las estrellas) mediante Reserve/Release.
    public class HidingSpotFinder : MonoBehaviour
    {
        public static HidingSpotFinder Instance { get; private set; }

        [Tooltip("Material para las esferas de depuración (verde = oculto, rojo = visible)")]
        [SerializeField] Material debugMaterial;

        readonly List<HidingSpot> spots = new List<HidingSpot>();
        public IReadOnlyList<HidingSpot> Spots => spots;
        public event Action SpotsRebuilt;

        GameSettings settings;
        bool ready;
        float nextVisibilityUpdate;
        float rebuildAt = -1f;
        Vector3 roomCenter;

        // Depuración
        readonly List<Renderer> debugSpheres = new List<Renderer>();
        MaterialPropertyBlock mpb;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            Instance = this;
            mpb = new MaterialPropertyBlock();
        }

        void Start()
        {
            settings = GameSettings.Instance;
            settings.Changed += UpdateDebugVisuals;
            if (SceneReady.IsReady) OnSceneReady(); else SceneReady.OnSceneReady += OnSceneReady;
        }

        void OnDestroy()
        {
            SceneReady.OnSceneReady -= OnSceneReady;
            FurnitureInfo.Changed -= ScheduleRebuild;
            if (settings != null) settings.Changed -= UpdateDebugVisuals;
        }

        void OnSceneReady()
        {
            ready = true;
            FurnitureInfo.Changed += ScheduleRebuild;
            Rebuild();
        }

        void ScheduleRebuild() => rebuildAt = Time.time + 0.5f;

        void Update()
        {
            if (!ready) return;
            if (rebuildAt > 0f && Time.time >= rebuildAt) { rebuildAt = -1f; Rebuild(); }
            if (Time.time >= nextVisibilityUpdate)
            {
                nextVisibilityUpdate = Time.time + settings.hidingUpdateInterval;
                UpdateVisibility();
            }
        }

        // ---------- Generación ----------

        public void Rebuild()
        {
            // Conservar las reservas de los puntos que sigan existiendo no merece la pena: los fantasmas
            // vuelven a pedir escondite si el suyo desaparece.
            foreach (var s in spots) s.owner = null;
            spots.Clear();

            roomCenter = PlayerHead.Position;
            float floorY = FindFloorY(roomCenter);

            foreach (var f in FurnitureInfo.All)
            {
                if (f == null || f.kind == FurnitureKind.Screen) continue; // la tele es un portal, no un escondite
                AddFurnitureSpots(f, floorY);
            }
            AddWallAndCeilingSpots(floorY);

            UpdateVisibility();
            BuildDebugVisuals();
            Debug.Log($"[Espectros] Escondites: {spots.Count} ({FurnitureInfo.All.Count} muebles)");
            SpotsRebuilt?.Invoke();
        }

        void AddFurnitureSpots(FurnitureInfo f, float floorY)
        {
            Transform t = f.transform;
            Vector3 c = f.Center;
            Vector3 half = f.HalfExtents;
            float bottom = Mathf.Max(f.Bottom, floorY);
            float height = Mathf.Max(0.2f, f.Top - bottom);
            // Detrás del mueble, a media altura (siempre por debajo del tablero)
            float behindY = bottom + Mathf.Clamp(height * 0.5f, 0.2f, 0.9f);

            Vector3 right = Vector3.ProjectOnPlane(t.right, Vector3.up).normalized;
            Vector3 fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.5f) right = Vector3.right;
            if (fwd.sqrMagnitude < 0.5f) fwd = Vector3.forward;

            var sides = new[] { (right, half.x), (-right, half.x), (fwd, half.z), (-fwd, half.z) };
            foreach (var (dir, extent) in sides)
            {
                Vector3 p = c + dir * (extent + settings.spotOffset);
                p.y = behindY;
                // Al asomarse sube por encima del mueble
                Vector3 peek = c + dir * (extent + settings.spotOffset * 0.6f);
                peek.y = f.Top + 0.15f;
                TryAdd(new HidingSpot { position = p, peekPosition = peek, kind = SpotKind.BehindFurniture, furniture = f });
            }

            if (f.kind == FurnitureKind.Table || f.kind == FurnitureKind.Bed)
            {
                Vector3 under = c;
                under.y = bottom + height * 0.35f;
                // Se asoma por el lado más cercano a la cabeza
                Vector3 toHead = Vector3.ProjectOnPlane(roomCenter - c, Vector3.up).normalized;
                Vector3 peek = c + toHead * (Mathf.Max(half.x, half.z) + 0.1f);
                peek.y = bottom + height * 0.45f;
                TryAdd(new HidingSpot { position = under, peekPosition = peek, kind = SpotKind.UnderTable, furniture = f }, f);
            }
        }

        void AddWallAndCeilingSpots(float floorY)
        {
            int mask = Layers.SceneMeshMask;
            int n = Mathf.Max(4, settings.wallRayCount);
            float[] heights = { floorY + 0.6f, floorY + 1.4f };

            foreach (float h in heights)
            {
                for (int i = 0; i < n; i++)
                {
                    float a = (i + (h > floorY + 1f ? 0.5f : 0f)) * Mathf.PI * 2f / n;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 origin = new Vector3(roomCenter.x, h, roomCenter.z);
                    if (!Physics.Raycast(origin, dir, out var hit, 8f, mask, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.distance < 0.8f || Mathf.Abs(hit.normal.y) > 0.3f) continue; // solo paredes no muy cercanas
                    TryAdd(new HidingSpot
                    {
                        position = hit.point - hit.normal * settings.wallSpotDepth,
                        peekPosition = hit.point + hit.normal * 0.25f,
                        kind = SpotKind.InWall,
                    }, null, true);
                }
            }

            // Techo: unos pocos puntos alrededor del centro
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                Vector3 origin = roomCenter + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.2f;
                if (!Physics.Raycast(origin, Vector3.up, out var hit, 4f, mask, QueryTriggerInteraction.Ignore)) continue;
                TryAdd(new HidingSpot
                {
                    position = hit.point + Vector3.up * settings.wallSpotDepth,
                    peekPosition = hit.point - Vector3.up * 0.3f,
                    kind = SpotKind.Ceiling,
                }, null, true);
            }
        }

        // Añade el punto si no está demasiado cerca de otro y (salvo los de pared) no está dentro de algo
        void TryAdd(HidingSpot s, FurnitureInfo ownFurniture = null, bool insideGeometryOk = false)
        {
            foreach (var o in spots)
                if ((o.position - s.position).sqrMagnitude < settings.spotMinSeparation * settings.spotMinSeparation) return;

            if (!insideGeometryOk)
            {
                var hits = Physics.OverlapSphere(s.position, 0.1f, Layers.OccluderMask, QueryTriggerInteraction.Ignore);
                foreach (var h in hits)
                {
                    if (ownFurniture != null && h.transform.IsChildOf(ownFurniture.transform)) continue;
                    if (ownFurniture != null && ownFurniture.transform.IsChildOf(h.transform)) continue;
                    return; // dentro de una pared o de otro mueble
                }
                // Tiene que haber suelo debajo (dentro de la habitación)
                if (!Physics.Raycast(s.position + Vector3.up * 0.05f, Vector3.down, 3f, Layers.SceneMeshMask, QueryTriggerInteraction.Ignore))
                    return;
            }
            spots.Add(s);
        }

        float FindFloorY(Vector3 from)
        {
            if (Physics.Raycast(from, Vector3.down, out var hit, 3f, Layers.SceneMeshMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return from.y - 1.5f;
        }

        // ---------- Visibilidad ----------

        void UpdateVisibility()
        {
            Vector3 head = PlayerHead.Position;
            int mask = Layers.OccluderMask;
            foreach (var s in spots)
                s.hidden = Physics.Linecast(head, s.position, mask, QueryTriggerInteraction.Ignore);
            UpdateDebugVisuals();
        }

        public static bool IsPointHidden(Vector3 p) =>
            Physics.Linecast(PlayerHead.Position, p, Layers.OccluderMask, QueryTriggerInteraction.Ignore);

        // ---------- Reservas ----------

        // Elige el mejor escondite libre: oculto, cerca de 'near' y que cumpla el filtro.
        // Si no hay ninguno oculto, devuelve un punto a la espalda de la jugadora.
        public HidingSpot Reserve(object owner, Vector3 near, Func<HidingSpot, bool> filter = null)
        {
            HidingSpot best = null;
            float bestScore = float.MaxValue;
            foreach (var s in spots)
            {
                if (s.Reserved || !s.hidden) continue;
                if (filter != null && !filter(s)) continue;
                float score = (s.position - near).magnitude + UnityEngine.Random.value * 1.5f;
                if (score < bestScore) { bestScore = score; best = s; }
            }

            if (best == null) best = MakeBehindPlayerSpot();
            best.owner = owner;
            return best;
        }

        public void Release(HidingSpot s, object owner)
        {
            if (s != null && s.owner == owner) s.owner = null;
        }

        HidingSpot MakeBehindPlayerSpot()
        {
            Vector3 head = PlayerHead.Position;
            Vector3 back = -Vector3.ProjectOnPlane(PlayerHead.Forward, Vector3.up).normalized;
            if (back.sqrMagnitude < 0.5f) back = Vector3.back;
            Vector3 p = head + back * 1.3f;
            p.y = head.y - 0.3f;
            return new HidingSpot { position = p, peekPosition = head + back * 0.9f, kind = SpotKind.BehindPlayer, hidden = true };
        }

        // Puntos de pared: los usa el spawner para hacer entrar a los fantasmas
        public HidingSpot RandomWallSpot()
        {
            var walls = spots.FindAll(s => s.kind == SpotKind.InWall);
            return walls.Count > 0 ? walls[UnityEngine.Random.Range(0, walls.Count)] : null;
        }

        // ---------- Depuración ----------

        void BuildDebugVisuals()
        {
            foreach (var r in debugSpheres) if (r != null) Destroy(r.gameObject);
            debugSpheres.Clear();
            if (debugMaterial == null) return;

            foreach (var s in spots)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(go.GetComponent<Collider>());
                go.name = "DebugSpot_" + s.kind;
                go.transform.SetParent(transform, false);
                go.transform.position = s.position;
                go.transform.localScale = Vector3.one * 0.06f;
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = debugMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                debugSpheres.Add(r);
            }
            UpdateDebugVisuals();
        }

        void UpdateDebugVisuals()
        {
            bool show = settings != null && settings.debugMode;
            for (int i = 0; i < debugSpheres.Count && i < spots.Count; i++)
            {
                var r = debugSpheres[i];
                if (r == null) continue;
                r.enabled = show;
                if (!show) continue;
                var s = spots[i];
                Color c = s.Reserved ? new Color(1f, 0.8f, 0f) : s.hidden ? Color.green : Color.red;
                mpb.SetColor(ColorId, c);
                mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(mpb);
            }
        }

        void OnDrawGizmos()
        {
            foreach (var s in spots)
            {
                Gizmos.color = s.Reserved ? Color.yellow : s.hidden ? Color.green : Color.red;
                Gizmos.DrawSphere(s.position, 0.04f);
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(s.position, s.peekPosition);
            }
        }
    }
}
