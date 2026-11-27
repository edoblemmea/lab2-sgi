using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Espectros
{
    // Pintura: se lanza con el gesto de pellizco y soltar (HandGestureRouter.Thrown con ThrowKind.Pinch).
    // La gota vuela con gravedad; al chocar con la habitación deja una mancha que se adapta a la mesh
    // (SurfaceLiquid) y revela a los fantasmas invisibles cercanos. En el editor: tecla V.
    public class PaintThrower : MonoBehaviour
    {
        public static PaintThrower Instance { get; private set; }

        [SerializeField] PaintBlob blobPrefab;
        [SerializeField] HandGestureRouter router;
        [SerializeField] Color[] palette =
        {
            new Color(1f, 0.2f, 0.7f, 0.9f),   // magenta
            new Color(0.2f, 0.9f, 1f, 0.9f),   // cian
            new Color(1f, 0.9f, 0.1f, 0.9f),   // amarillo
            new Color(0.4f, 1f, 0.3f, 0.9f),   // verde
        };

        public event System.Action Thrown;

        readonly Queue<PaintBlob> pool = new Queue<PaintBlob>();
        GameSettings settings;
        float lastThrow = -10f;
        int colorIndex;

        void Awake() => Instance = this;

        void Start()
        {
            settings = GameSettings.Instance;
            if (router == null) router = FindAnyObjectByType<HandGestureRouter>();
            if (router != null) router.Thrown += OnThrown;
        }

        void OnDestroy()
        {
            if (router != null) router.Thrown -= OnThrown;
        }

        void OnThrown(HandSide hand, ThrowKind kind, Vector3 origin, Vector3 velocity)
        {
            if (kind != ThrowKind.Pinch) return;
            float speed = Mathf.Clamp(velocity.magnitude * settings.paintThrowMultiplier, settings.paintMinSpeed, settings.paintMaxSpeed);
            Throw(origin, velocity.normalized * speed);
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame && PlayerHead.Transform != null)
            {
                var head = PlayerHead.Transform;
                Vector3 dir = (head.forward + Vector3.up * 0.15f).normalized;
                Throw(head.position + head.forward * 0.3f - head.up * 0.1f, dir * 5f);
            }
#endif
        }

        public bool Throw(Vector3 origin, Vector3 velocity)
        {
            if (blobPrefab == null) { Debug.LogError("[Espectros] PaintThrower: falta el prefab de la pintura"); return false; }
            if (Time.time - lastThrow < settings.paintCooldown) return false;
            lastThrow = Time.time;

            Color c = palette.Length > 0 ? palette[colorIndex++ % palette.Length] : Color.magenta;
            var blob = pool.Count > 0 ? pool.Dequeue() : Instantiate(blobPrefab);
            blob.Launch(this, origin, velocity, c);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(SfxLibrary.Throw, origin, 0.5f, 1.4f);
            Thrown?.Invoke();
            return true;
        }

        public void Return(PaintBlob b) => pool.Enqueue(b);
    }
}
