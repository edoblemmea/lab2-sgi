using UnityEngine;

namespace Espectros
{
    // Gota de pintura en vuelo. Se mueve con gravedad y comprueba con rayos (sin Rigidbody, más fiable a
    // velocidades altas). Al chocar con la mesh o un mueble deja una mancha con SurfaceLiquid y revela a los
    // fantasmas invisibles que toque o que estén cerca del impacto.
    public class PaintBlob : MonoBehaviour
    {
        [SerializeField] Renderer visual;

        PaintThrower owner;
        Vector3 velocity;
        Color color;
        float despawnAt;
        bool active;
        MaterialPropertyBlock mpb;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public void Launch(PaintThrower thrower, Vector3 position, Vector3 vel, Color c)
        {
            owner = thrower;
            transform.position = position;
            velocity = vel;
            color = c;
            despawnAt = Time.time + 4f;
            active = true;
            gameObject.SetActive(true);
            if (visual != null)
            {
                if (mpb == null) mpb = new MaterialPropertyBlock();
                mpb.SetColor(ColorId, c);
                visual.SetPropertyBlock(mpb);
            }
        }

        void Update()
        {
            if (!active) return;
            if (Time.time > despawnAt) { Finish(); return; }

            float dt = Time.deltaTime;
            velocity += Physics.gravity * dt;
            Vector3 step = velocity * dt;
            float dist = step.magnitude;
            Vector3 pos = transform.position;
            var s = GameSettings.Instance;

            // ¿Ha tocado a un fantasma? (sus colliders son triggers en la capa Ghost)
            if (dist > 0f && Physics.SphereCast(pos, 0.05f, step / dist, out var gh, dist, Layers.GhostMask, QueryTriggerInteraction.Collide))
            {
                var g = gh.collider.GetComponentInParent<GhostController>();
                if (g != null && g.IsAlive)
                {
                    g.Reveal(s.paintRevealTime);
                    Debug.Log($"[Espectros] Pintura: fantasma {g.Type.displayName} revelado");
                }
            }

            // ¿Ha tocado la habitación?
            if (dist > 0f && Physics.Raycast(pos, step / dist, out var hit, dist + 0.02f, Layers.OccluderMask, QueryTriggerInteraction.Ignore))
            {
                Splat(hit);
                return;
            }
            transform.position = pos + step;
        }

        void Splat(RaycastHit hit)
        {
            var s = GameSettings.Instance;
            var liquid = SurfaceLiquid.Instance;
            if (liquid != null)
            {
                // Si ha dado en la mesh, el triángulo viene en el hit; si ha dado en la caja de un mueble,
                // SurfaceLiquid busca la mesh con un rayo corto contra la normal
                if (hit.collider is MeshCollider && hit.collider.gameObject.layer == Layers.SceneMesh)
                    liquid.SpawnFromHit(hit, color, s.paintRadius);
                else
                    liquid.Spawn(hit.point, hit.normal, color, s.paintRadius);
            }

            // Revela a los fantasmas invisibles cerca del impacto
            foreach (var g in GhostController.All)
            {
                if (g == null || !g.IsAlive) continue;
                if ((g.Center - hit.point).sqrMagnitude <= s.paintRevealRadius * s.paintRevealRadius)
                    g.Reveal(s.paintRevealTime);
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(SfxLibrary.GhostDie, hit.point, 0.4f, 1.6f);
            Finish();
        }

        void Finish()
        {
            active = false;
            gameObject.SetActive(false);
            if (owner != null) owner.Return(this);
        }
    }
}
