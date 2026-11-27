using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Espectros
{
    // Charcos y manchas que se adaptan a la mesh de la escena (ectoplasma, agua, pintura).
    //
    // 1. Por cada trozo de mesh (MeshCollider en SceneMesh) se construye un MeshGraph con los vértices soldados.
    // 2. Al impactar: Physics.Raycast contra SceneMesh y hit.triangleIndex como punto de partida.
    // 3. Dijkstra sobre los triángulos conectados acumulando la distancia SOBRE LA SUPERFICIE (desplegándola
    //    arista a arista). Funciona aunque la mesh tenga triángulos de medio metro, como la del Quest.
    // 4. Se copian los triángulos alcanzados desplazados unos mm según la normal, con uv.x = distancia / radio
    //    y la fuente desplegada de cada triángulo en uv1.
    // 5. El shader calcula la distancia en cada píxel, con más coste al subir (×(1 + 3·pendiente)) y menos al
    //    bajar (×0,7) para que el líquido escurra, y la compara con _Spread (más ruido). LiquidPuddle anima
    //    _Spread de 0 a 1.
    public class SurfaceLiquid : MonoBehaviour
    {
        public static SurfaceLiquid Instance { get; private set; }

        [SerializeField] Material liquidMaterial;

        readonly Dictionary<MeshCollider, MeshGraph> graphs = new Dictionary<MeshCollider, MeshGraph>();
        readonly List<LiquidPuddle> active = new List<LiquidPuddle>();
        readonly Queue<LiquidPuddle> pool = new Queue<LiquidPuddle>();
        GameSettings settings;

        void Awake() => Instance = this;

        void Start()
        {
            settings = GameSettings.Instance;
            if (SceneReady.IsReady) StartCoroutine(Prewarm()); else SceneReady.OnSceneReady += OnSceneReady;
        }

        void OnDestroy() => SceneReady.OnSceneReady -= OnSceneReady;
        void OnSceneReady() => StartCoroutine(Prewarm());

        // Construye los grafos de toda la mesh de la escena, uno por frame, para que el primer charco no tarde
        IEnumerator Prewarm()
        {
            yield return null;
            var colliders = FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude);
            int n = 0;
            foreach (var mc in colliders)
            {
                if (mc == null || mc.gameObject.layer != Layers.SceneMesh) continue;
                if (GetGraph(mc) != null) n++;
                yield return null;
            }
            Debug.Log($"[Espectros] SurfaceLiquid: {n} trozos de mesh preparados");
        }

        MeshGraph GetGraph(MeshCollider mc)
        {
            var mesh = mc.sharedMesh;
            if (mesh == null) return null;
            if (!mesh.isReadable)
            {
                Debug.LogError($"[Espectros] La mesh {mesh.name} no es legible (Read/Write desactivado)");
                return null;
            }
            // Se reconstruye si el ARMeshManager ha actualizado la mesh o si el objeto se ha movido
            if (graphs.TryGetValue(mc, out var g) && g.source == mesh && g.sourceVertexCount == mesh.vertexCount
                && g.localToWorld == mc.transform.localToWorldMatrix)
                return g;
            g = new MeshGraph(mesh, mc.transform.localToWorldMatrix);
            graphs[mc] = g;
            return g;
        }

        // ---------- API ----------

        // Crea un charco en el punto de la mesh más cercano a 'point' en la dirección contraria a 'normal'
        public LiquidPuddle Spawn(Vector3 point, Vector3 normal, Color color, float radius)
        {
            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            normal.Normalize();
            int mask = Layers.SceneMeshMask;
            // Las colisiones de partículas no dan el triángulo: rayo corto contra la normal para obtenerlo
            if (!Physics.Raycast(point + normal * 0.05f, -normal, out var hit, 0.2f, mask, QueryTriggerInteraction.Ignore)
                && !Physics.Raycast(point + Vector3.up * 0.1f, Vector3.down, out hit, 0.6f, mask, QueryTriggerInteraction.Ignore))
                return null;
            return SpawnFromHit(hit, color, radius);
        }

        public LiquidPuddle SpawnFromHit(RaycastHit hit, Color color, float radius)
        {
            if (!(hit.collider is MeshCollider mc) || hit.triangleIndex < 0) return null;
            var graph = GetGraph(mc);
            if (graph == null || hit.triangleIndex >= graph.TriangleCount) return null;

            var puddle = GetPuddle();
            float t0 = Time.realtimeSinceStartup;
            bool ok = BuildPuddleMesh(graph, hit.triangleIndex, hit.point, radius, puddle.Mesh);
            float ms = (Time.realtimeSinceStartup - t0) * 1000f;
            if (!ok)
            {
                ReturnPuddle(puddle);
                return null;
            }
            puddle.Play(color, settings.puddleSpreadTime, radius, settings.liquidUpCost, settings.liquidDownCost);
            Debug.Log($"[Espectros] Charco: {source.Count} triángulos de la mesh en {ms:0.0} ms");
            return puddle;
        }

        public void ClearAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) ReturnPuddle(active[i]);
        }

        // ---------- Pool ----------

        LiquidPuddle GetPuddle()
        {
            LiquidPuddle p;
            if (pool.Count > 0) p = pool.Dequeue();
            else if (active.Count >= Mathf.Max(1, settings.maxPuddles)) { p = active[0]; active.RemoveAt(0); }
            else
            {
                var go = new GameObject("Puddle");
                // En la raíz de la escena: los vértices del charco están en espacio de mundo
                p = go.AddComponent<LiquidPuddle>();
                p.Setup(liquidMaterial);
            }
            p.gameObject.SetActive(true);
            active.Add(p);
            return p;
        }

        void ReturnPuddle(LiquidPuddle p)
        {
            active.Remove(p);
            p.gameObject.SetActive(false);
            pool.Enqueue(p);
        }

        // ---------- Construcción del charco ----------
        //
        // Dijkstra sobre los TRIÁNGULOS desplegando la superficie: cada triángulo guarda una "fuente virtual",
        // que es el punto de impacto desdoblado sobre su plano a través de las aristas recorridas. Así la
        // distancia sobre la superficie es exacta en las zonas planas y al doblar una esquina o el canto de una
        // mesa, aunque la mesh tenga triángulos de medio metro (como la del Quest). El shader calcula en cada
        // píxel la distancia a esa fuente, con más coste al subir y menos al bajar, y la compara con _Spread.

        readonly Dictionary<int, Vector3> source = new Dictionary<int, Vector3>();
        readonly Dictionary<int, float> key = new Dictionary<int, float>();
        readonly MinHeap heap = new MinHeap();
        readonly List<Vector3> outVerts = new List<Vector3>();
        readonly List<Vector2> outUV = new List<Vector2>();
        readonly List<Vector3> outSource = new List<Vector3>();
        readonly List<int> outTris = new List<int>();
        readonly List<Vector3> poly = new List<Vector3>(16);
        readonly List<Vector3> polyTmp = new List<Vector3>(16);

        bool BuildPuddleMesh(MeshGraph g, int hitTri, Vector3 center, float R, Mesh mesh)
        {
            // Alcance máximo: bajando, el coste es menor, así que puede llegar más lejos que R
            float limit = R / Mathf.Clamp(settings.liquidDownCost, 0.2f, 1f) * 1.05f;

            source.Clear(); key.Clear(); heap.Clear();
            source[hitTri] = center;
            key[hitTri] = 0f;
            heap.Push(0f, hitTri);
            int guard = 0;

            while (heap.Count > 0 && guard++ < 5000)
            {
                heap.Pop(out float k, out int t);
                if (k > key[t]) continue; // entrada antigua
                Vector3 S = source[t];

                for (int e = 0; e < 3; e++)
                {
                    int ia = g.triangles[t * 3 + e];
                    int ib = g.triangles[t * 3 + (e + 1) % 3];
                    int ic = g.triangles[t * 3 + (e + 2) % 3];
                    foreach (int t2 in g.vertexTriangles[ia])
                    {
                        if (t2 == t) continue;
                        int ic2 = ThirdVertex(g, t2, ia, ib);
                        if (ic2 < 0) continue; // no comparte la arista a-b

                        Vector3 pa = g.positions[ia], pb = g.positions[ib];
                        Vector3 S2 = Unfold(S, pa, pb, g.positions[ic], g.positions[ic2]);
                        g.GetTriangle(t2, out var a2, out var b2, out var c2);
                        float k2 = (MeshGraph.ClosestPointOnTriangle(S2, a2, b2, c2) - S2).magnitude;
                        if (k2 > limit) continue;
                        if (key.TryGetValue(t2, out float old) && old <= k2 + 1e-4f) continue;
                        key[t2] = k2;
                        source[t2] = S2;
                        heap.Push(k2, t2);
                    }
                }
            }

            // Mesh de salida: los triángulos alcanzados, recortados a una caja alrededor del impacto
            // (para no dibujar triángulos enormes casi transparentes) y separados unos mm de la superficie
            outVerts.Clear(); outUV.Clear(); outSource.Clear(); outTris.Clear();
            float offset = settings.liquidSurfaceOffset;
            foreach (var pair in source)
            {
                int t = pair.Key;
                Vector3 S = pair.Value;
                g.GetTriangle(t, out var a, out var b, out var c);
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) continue;
                n.Normalize();

                poly.Clear(); poly.Add(a); poly.Add(b); poly.Add(c);
                ClipToBox(center, limit);
                if (poly.Count < 3) continue;

                int first = outVerts.Count;
                foreach (var p in poly)
                {
                    outVerts.Add(p + n * offset);
                    outSource.Add(S);
                    outUV.Add(new Vector2(Mathf.Min(2f, (p - S).magnitude / R), 0f)); // uv.x = distancia / radio
                }
                for (int i = 1; i < poly.Count - 1; i++)
                {
                    outTris.Add(first); outTris.Add(first + i); outTris.Add(first + i + 1);
                }
            }
            if (outTris.Count == 0) return false;

            mesh.Clear();
            mesh.indexFormat = outVerts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(outVerts);
            mesh.SetUVs(0, outUV);
            mesh.SetUVs(1, outSource); // fuente desplegada de cada triángulo (para la distancia por píxel)
            mesh.SetTriangles(outTris, 0);
            mesh.RecalculateBounds();
            return true;
        }

        static int ThirdVertex(MeshGraph g, int t, int ia, int ib)
        {
            int v0 = g.triangles[t * 3], v1 = g.triangles[t * 3 + 1], v2 = g.triangles[t * 3 + 2];
            bool hasA = v0 == ia || v1 == ia || v2 == ia;
            bool hasB = v0 == ib || v1 == ib || v2 == ib;
            if (!hasA || !hasB) return -1;
            if (v0 != ia && v0 != ib) return v0;
            if (v1 != ia && v1 != ib) return v1;
            return v2;
        }

        // Desdobla la fuente S (en el plano del triángulo a-b-c) sobre el plano del triángulo vecino a-b-c2,
        // girándola alrededor de la arista a-b: queda al otro lado de la arista respecto a c2.
        static Vector3 Unfold(Vector3 S, Vector3 a, Vector3 b, Vector3 c, Vector3 c2)
        {
            Vector3 e = (b - a).normalized;
            Vector3 u = Vector3.ProjectOnPlane(c - a, e).normalized;   // hacia dentro del triángulo actual
            Vector3 u2 = Vector3.ProjectOnPlane(c2 - a, e).normalized; // hacia dentro del vecino
            Vector3 r = S - a;
            return a + e * Vector3.Dot(r, e) - u2 * Vector3.Dot(r, u);
        }

        // Sutherland-Hodgman contra los 6 planos de una caja alineada con los ejes
        void ClipToBox(Vector3 c, float h)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                ClipPlane(axis, c[axis] + h, true);
                ClipPlane(axis, c[axis] - h, false);
                if (poly.Count < 3) return;
            }
        }

        void ClipPlane(int axis, float value, bool keepBelow)
        {
            polyTmp.Clear();
            for (int i = 0; i < poly.Count; i++)
            {
                Vector3 p = poly[i], q = poly[(i + 1) % poly.Count];
                float dp = keepBelow ? value - p[axis] : p[axis] - value;
                float dq = keepBelow ? value - q[axis] : q[axis] - value;
                if (dp >= 0f) polyTmp.Add(p);
                if ((dp >= 0f) != (dq >= 0f)) polyTmp.Add(Vector3.Lerp(p, q, dp / (dp - dq)));
            }
            poly.Clear();
            poly.AddRange(polyTmp);
        }

        // Montículo binario mínimo para el Dijkstra
        class MinHeap
        {
            readonly List<float> keys = new List<float>();
            readonly List<int> values = new List<int>();
            public int Count => keys.Count;
            public void Clear() { keys.Clear(); values.Clear(); }

            public void Push(float k, int v)
            {
                keys.Add(k); values.Add(v);
                int i = keys.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) / 2;
                    if (keys[p] <= keys[i]) break;
                    Swap(i, p);
                    i = p;
                }
            }

            public void Pop(out float k, out int v)
            {
                k = keys[0]; v = values[0];
                int last = keys.Count - 1;
                keys[0] = keys[last]; values[0] = values[last];
                keys.RemoveAt(last); values.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < keys.Count && keys[l] < keys[m]) m = l;
                    if (r < keys.Count && keys[r] < keys[m]) m = r;
                    if (m == i) break;
                    Swap(i, m);
                    i = m;
                }
            }

            void Swap(int a, int b)
            {
                (keys[a], keys[b]) = (keys[b], keys[a]);
                (values[a], values[b]) = (values[b], values[a]);
            }
        }
    }
}
