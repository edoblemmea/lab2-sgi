using UnityEngine;

namespace Espectros
{
    // Pelota que rebota en la habitación real (mesh de la escena y cajas de los muebles).
    // Cuenta los rebotes: si da a un fantasma después de rebotar, es una carambola (bonus).
    // Rigidbody con colisión Continuous Dynamic y Physic Material con rebote 0,8.
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class Ball : MonoBehaviour
    {
        public int Bounces { get; private set; }
        public bool Active { get; private set; }

        Rigidbody rb;
        BallThrower owner;
        float despawnAt, slowSince;
        int sceneMeshLayer, furnitureLayer;
        float lastBounceSound;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            sceneMeshLayer = Layers.SceneMesh;
            furnitureLayer = Layers.Furniture;
        }

        public void Launch(BallThrower thrower, Vector3 position, Vector3 velocity, float lifetime)
        {
            owner = thrower;
            Bounces = 0;
            Active = true;
            transform.position = position;
            gameObject.SetActive(true);
            rb.linearVelocity = velocity;
            rb.angularVelocity = Vector3.zero;
            despawnAt = Time.time + lifetime;
            slowSince = -1f;
        }

        void Update()
        {
            if (!Active) return;
            if (Time.time >= despawnAt) { Despawn(); return; }
            // Si se queda parada en el suelo más de 1,5 s, desaparece
            if (rb.linearVelocity.sqrMagnitude < 0.04f)
            {
                if (slowSince < 0f) slowSince = Time.time;
                else if (Time.time - slowSince > 1.5f) Despawn();
            }
            else slowSince = -1f;
        }

        void OnCollisionEnter(Collision c)
        {
            int layer = c.gameObject.layer;
            if (layer != sceneMeshLayer && layer != furnitureLayer) return;
            Bounces++;
            if (Time.time - lastBounceSound > 0.05f && AudioManager.Instance != null)
            {
                lastBounceSound = Time.time;
                float v = Mathf.Clamp01(c.relativeVelocity.magnitude / 6f);
                AudioManager.Instance.PlayAt(SfxLibrary.Bounce, c.GetContact(0).point, 0.3f + 0.7f * v, 0.9f + 0.2f * Random.value);
            }
        }

        // Los fantasmas tienen un collider trigger en la capa Ghost
        void OnTriggerEnter(Collider other)
        {
            if (!Active) return;
            var ghost = other.GetComponentInParent<GhostController>();
            if (ghost == null || !ghost.IsAlive) return;

            bool kills = ghost.Type != null && ghost.Type.weakness == GhostWeakness.Ball;
            ghost.HitByBall(Bounces);
            if (kills && ScoreManager.Instance != null) ScoreManager.Instance.AddCarambola(Bounces);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(SfxLibrary.GhostHit, transform.position);
            Despawn();
        }

        public void Despawn()
        {
            if (!Active) return;
            Active = false;
            gameObject.SetActive(false);
            if (owner != null) owner.Return(this);
        }
    }
}
