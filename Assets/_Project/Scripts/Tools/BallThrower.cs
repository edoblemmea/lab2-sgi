using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Espectros
{
    // Lanza pelotas con el gesto de puño y abrir la mano (HandGestureRouter.Thrown con ThrowKind.Fist).
    // En el editor: tecla B lanza desde la cámara hacia donde mira.
    // Usa un pool de pelotas y lleva la cuenta de las que quedan.
    public class BallThrower : MonoBehaviour
    {
        public static BallThrower Instance { get; private set; }

        [SerializeField] Ball ballPrefab;
        [SerializeField] HandGestureRouter router;
        [SerializeField] PhysicsMaterial ballMaterial;

        public int Balls { get; private set; }
        public event Action Thrown;
        public event Action Changed;

        readonly Queue<Ball> pool = new Queue<Ball>();
        GameSettings settings;

        void Awake()
        {
            Instance = this;
            // Matriz de colisiones: las pelotas no chocan entre sí ni con las estrellas,
            // y los fantasmas no chocan con la habitación (la atraviesan)
            Physics.IgnoreLayerCollision(Layers.Projectile, Layers.Projectile, true);
            Physics.IgnoreLayerCollision(Layers.Projectile, Layers.Pickup, true);
            Physics.IgnoreLayerCollision(Layers.Ghost, Layers.SceneMesh, true);
            Physics.IgnoreLayerCollision(Layers.Ghost, Layers.Furniture, true);
        }

        void Start()
        {
            settings = GameSettings.Instance;
            if (router == null) router = FindAnyObjectByType<HandGestureRouter>();
            if (router != null) router.Thrown += OnThrown;
            ResetBalls();
        }

        void OnDestroy()
        {
            if (router != null) router.Thrown -= OnThrown;
        }

        public void ResetBalls()
        {
            Balls = GameSettings.Instance.ballsAtStart; // puede llamarse antes de Start (desde GameManager)
            Changed?.Invoke();
        }

        public void AddBalls(int n)
        {
            Balls = Mathf.Min(GameSettings.Instance.maxBalls, Balls + n);
            Changed?.Invoke();
        }

        void OnThrown(HandSide hand, ThrowKind kind, Vector3 origin, Vector3 velocity)
        {
            if (kind != ThrowKind.Fist) return; // el pellizco es para la pintura (extra)
            float speed = Mathf.Clamp(velocity.magnitude * settings.ballThrowMultiplier, settings.ballMinSpeed, settings.ballMaxSpeed);
            Throw(origin, velocity.normalized * speed);
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame && PlayerHead.Transform != null)
            {
                var head = PlayerHead.Transform;
                Vector3 dir = (head.forward + Vector3.up * 0.1f).normalized;
                Throw(head.position + head.forward * 0.3f - head.up * 0.1f, dir * 6f);
            }
#endif
        }

        public bool Throw(Vector3 origin, Vector3 velocity)
        {
            if (ballPrefab == null) { Debug.LogError("[Espectros] BallThrower: falta el prefab de la pelota"); return false; }
            if (Balls <= 0) return false;
            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying
                && GameManager.Instance.State != GameState.Tutorial) return false;

            Balls--;
            var ball = pool.Count > 0 ? pool.Dequeue() : CreateBall();
            ball.Launch(this, origin, velocity, settings.ballLifetime);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(SfxLibrary.Throw, origin, 0.6f);
            Debug.Log($"[Espectros] Pelota lanzada a {velocity.magnitude:0.0} m/s (quedan {Balls})");
            Thrown?.Invoke();
            Changed?.Invoke();
            return true;
        }

        Ball CreateBall()
        {
            var b = Instantiate(ballPrefab);
            if (ballMaterial != null)
            {
                ballMaterial.bounciness = settings.ballBounciness;
                b.GetComponent<SphereCollider>().sharedMaterial = ballMaterial;
            }
            return b;
        }

        public void Return(Ball b) => pool.Enqueue(b);
    }
}
