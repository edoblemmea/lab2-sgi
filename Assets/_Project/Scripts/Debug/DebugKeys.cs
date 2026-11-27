using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Espectros
{
    // Atajos de teclado para probar en el editor (en el casco no hay teclado y no hacen nada):
    //   P  charco donde mira la cámara (para probar SurfaceLiquid en bordes y esquinas)
    //   K  elimina el fantasma más cercano (se derrite en un charco)
    //   N  hace aparecer un fantasma
    //   F  linterna (en Flashlight)   B pelota   V pintura   C sonar   1/2/3/M  menú de muñeca (en WristMenu)
    //   I  hace aparecer un fantasma invisible
    public class DebugKeys : MonoBehaviour
    {
        [SerializeField] Color puddleColor = new Color(0.45f, 1f, 0.55f, 0.85f);
        [Tooltip("Tipo que aparece con la tecla I (fantasma invisible)")]
        [SerializeField] GhostType invisibleType;

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.pKey.wasPressedThisFrame && PlayerHead.Transform != null && SurfaceLiquid.Instance != null)
            {
                var head = PlayerHead.Transform;
                if (Physics.Raycast(head.position, head.forward, out var hit, 10f, Layers.SceneMeshMask, QueryTriggerInteraction.Ignore))
                    SurfaceLiquid.Instance.SpawnFromHit(hit, puddleColor, GameSettings.Instance.puddleRadius);
            }

            if (kb.kKey.wasPressedThisFrame)
            {
                GhostController nearest = null;
                float best = float.MaxValue;
                foreach (var g in GhostController.All)
                {
                    if (g == null || !g.IsAlive) continue;
                    float d = (g.Center - PlayerHead.Position).sqrMagnitude;
                    if (d < best) { best = d; nearest = g; }
                }
                if (nearest != null) nearest.Die();
            }

            if (kb.nKey.wasPressedThisFrame && GhostSpawner.Instance != null)
                GhostSpawner.Instance.Spawn(null);

            if (kb.iKey.wasPressedThisFrame && GhostSpawner.Instance != null && invisibleType != null)
                GhostSpawner.Instance.Spawn(invisibleType);
#endif
        }
    }
}
