using System;
using UnityEngine;

namespace Espectros
{
    // Corazones de la jugadora. Scare() quita uno, da 2 s de invulnerabilidad y pone la pantalla roja.
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        [Tooltip("Quad delante de la cámara con el material de pantalla roja (se crea solo si se asigna el material)")]
        [SerializeField] Renderer screenFlash;
        [SerializeField] Material screenFlashMaterial;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip hurtSound;

        public int Hearts { get; private set; }
        public int MaxHearts => GameSettings.Instance.maxHearts;
        public bool IsDead => Hearts <= 0;
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        public event Action Changed;
        public event Action Died;

        float invulnerableUntil, flashUntil;
        MaterialPropertyBlock mpb;
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            Instance = this;
            mpb = new MaterialPropertyBlock();
        }

        void Start()
        {
            ResetHearts();
            if (screenFlash == null && screenFlashMaterial != null) CreateFlashQuad();
            if (screenFlash != null) screenFlash.enabled = false;
        }

        public void ResetHearts()
        {
            Hearts = MaxHearts;
            invulnerableUntil = 0f;
            Changed?.Invoke();
        }

        public void AddHeart()
        {
            Hearts = Mathf.Min(MaxHearts, Hearts + 1);
            Changed?.Invoke();
        }

        public void Scare()
        {
            if (IsDead || IsInvulnerable) return;
            Hearts--;
            invulnerableUntil = Time.time + GameSettings.Instance.invulnerabilityTime;
            flashUntil = Time.time + GameSettings.Instance.redFlashTime;
            if (audioSource != null && hurtSound != null) audioSource.PlayOneShot(hurtSound);
            Debug.Log($"[Espectros] ¡Susto! Corazones: {Hearts}");
            Changed?.Invoke();
            if (IsDead) Died?.Invoke();
        }

        void Update()
        {
            if (screenFlash == null) return;
            float left = flashUntil - Time.time;
            bool on = left > 0f;
            screenFlash.enabled = on;
            if (on)
            {
                float a = Mathf.Clamp01(left / GameSettings.Instance.redFlashTime) * 0.55f;
                mpb.SetColor(ColorId, new Color(1f, 0f, 0f, a));
                screenFlash.SetPropertyBlock(mpb);
            }
        }

        void CreateFlashQuad()
        {
            var cam = PlayerHead.Transform;
            if (cam == null) return;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "ScreenFlash";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(cam, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 0.2f);
            quad.transform.localScale = new Vector3(1f, 1f, 1f);
            screenFlash = quad.GetComponent<Renderer>();
            screenFlash.sharedMaterial = screenFlashMaterial;
            screenFlash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    // Contrato con el sistema de estrellas (fase 3): de momento solo cuenta.
    public static class StarWallet
    {
        public static int Stars { get; private set; }
        public static event Action Changed;

        public static void Add()
        {
            Stars++;
            Changed?.Invoke();
        }

        public static void Reset()
        {
            Stars = 0;
            Changed?.Invoke();
        }
    }
}
