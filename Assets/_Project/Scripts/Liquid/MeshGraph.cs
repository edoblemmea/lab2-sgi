using System.Collections.Generic;
using UnityEngine;

namespace Espectros
{
    // Grafo de un trozo de mesh de la escena en espacio de mundo, con los vértices SOLDADOS
    // (posición redondeada a 1 mm como clave Vector3Int): así los triángulos vecinos comparten vértices
    // aunque la mesh original los tenga duplicados.
    public class MeshGraph
    {
        public readonly Vector3[] positions;          // vértices soldados (mundo)
        public readonly int[] triangles;              // índice soldado de cada esquina (3 por triángulo, mismo orden que la mesh)
        public readonly List<int>[] vertexTriangles;  // triángulos de cada vértice soldado
        public readonly Mesh source;
        public readonly int sourceVertexCount;
        public readonly Matrix4x4 localToWorld;

        public int TriangleCount => triangles.Length / 3;

        public MeshGraph(Mesh mesh, Matrix4x4 localToWorld)
        {
            source = mesh;
            sourceVertexCount = mesh.vertexCount;
            this.localToWorld = localToWorld;

            var verts = mesh.vertices;
            var tris = mesh.triangles; // todos los submeshes seguidos, igual que RaycastHit.triangleIndex

            var weld = new Dictionary<Vector3Int, int>(verts.Length);
            var remap = new int[verts.Length];
            var pos = new List<Vector3>(verts.Length);
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 w = localToWorld.MultiplyPoint3x4(verts[i]);
                var key = Quantize(w);
                if (!weld.TryGetValue(key, out int idx))
                {
                    idx = pos.Count;
                    pos.Add(w);
                    weld.Add(key, idx);
                }
                remap[i] = idx;
            }
            positions = pos.ToArray();

            triangles = new int[tris.Length];
            vertexTriangles = new List<int>[positions.Length];
            for (int i = 0; i < positions.Length; i++) vertexTriangles[i] = new List<int>(6);
            for (int t = 0; t < tris.Length / 3; t++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int v = remap[tris[t * 3 + k]];
                    triangles[t * 3 + k] = v;
                    vertexTriangles[v].Add(t);
                }
            }
        }

        public static Vector3Int Quantize(Vector3 p) =>
            new Vector3Int(Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.y * 1000f), Mathf.RoundToInt(p.z * 1000f));

        public void GetTriangle(int t, out Vector3 a, out Vector3 b, out Vector3 c)
        {
            a = positions[triangles[t * 3]];
            b = positions[triangles[t * 3 + 1]];
            c = positions[triangles[t * 3 + 2]];
        }

        // Punto del triángulo más cercano a p (Ericson, Real-Time Collision Detection)
        public static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }
    }
}
