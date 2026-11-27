using UnityEngine;

namespace Espectros
{
    // Modo depuración: la mesh de la escena se ve con colores (M_MeshDebug); en modo normal solo
    // escribe profundidad (M_Occluder) para que los muebles reales tapen a los fantasmas.
    // Revisa cada pocos segundos porque el ARMeshManager puede crear trozos nuevos.
    public class SceneMeshDebugView : MonoBehaviour
    {
        [SerializeField] Material occluderMaterial;
        [SerializeField] Material debugMaterial;
        [SerializeField] float recheckInterval = 2f;

        GameSettings settings;
        float nextCheck;

        void Start()
        {
            settings = GameSettings.Instance;
            settings.Changed += Apply;
            Apply();
        }

        void OnDestroy()
        {
            if (settings != null) settings.Changed -= Apply;
        }

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + recheckInterval;
            Apply();
        }

        void Apply()
        {
            var mat = settings.debugMode ? debugMaterial : occluderMaterial;
            if (mat == null) return;
            int layer = Layers.SceneMesh;
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
                if (r.gameObject.layer == layer && r.sharedMaterial != mat) r.sharedMaterial = mat;
        }
    }
}
