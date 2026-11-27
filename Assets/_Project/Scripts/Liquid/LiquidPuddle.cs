using UnityEngine;

namespace Espectros
{
    // Un charco o mancha ya construido por SurfaceLiquid. Anima _Spread de 0 a 1 para que se vea extenderse.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LiquidPuddle : MonoBehaviour
    {
        public Mesh Mesh { get; private set; }

        MeshRenderer mr;
        MaterialPropertyBlock mpb;
        float startTime, spreadTime = 1f;
        bool animating;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int SpreadId = Shader.PropertyToID("_Spread");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int UpCostId = Shader.PropertyToID("_UpCost");
        static readonly int DownCostId = Shader.PropertyToID("_DownCost");

        public void Setup(Material material)
        {
            Mesh = new Mesh { name = "PuddleMesh" };
            Mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = Mesh;
            mr = GetComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mpb = new MaterialPropertyBlock();
        }

        public void Play(Color color, float time, float radius, float upCost, float downCost)
        {
            spreadTime = Mathf.Max(0.05f, time);
            startTime = Time.time;
            animating = true;
            mpb.SetColor(ColorId, color);
            mpb.SetFloat(SeedId, Random.value * 100f);
            mpb.SetFloat(RadiusId, radius);
            mpb.SetFloat(UpCostId, upCost);
            mpb.SetFloat(DownCostId, downCost);
            mpb.SetFloat(SpreadId, 0f);
            mr.SetPropertyBlock(mpb);
        }

        void Update()
        {
            if (!animating) return;
            float t = Mathf.Clamp01((Time.time - startTime) / spreadTime);
            float eased = 1f - (1f - t) * (1f - t) * (1f - t); // rápido al principio, lento al final
            mpb.SetFloat(SpreadId, eased);
            mr.SetPropertyBlock(mpb);
            if (t >= 1f) animating = false;
        }
    }
}
