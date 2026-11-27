using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Espectros
{
    // Sonar: con una palmada (HandGestureRouter.Clapped) sale una onda desde la jugadora que recorre la mesh
    // de la escena, y las siluetas de los fantasmas se ven a través de los muebles durante 1 s
    // (material con ZTest Always). Recarga de 8 s. En el editor: tecla C.
    public class Sonar : MonoBehaviour
    {
        public static Sonar Instance { get; private set; }

        [SerializeField] HandGestureRouter router;
        [Tooltip("Material de la onda (Custom/SonarWave)")]
        [SerializeField] Material waveMaterial;
        [Tooltip("Material de las siluetas (Custom/GhostSilhouette)")]
        [SerializeField] Material silhouetteMaterial;

        public bool IsReady => Time.time >= readyAt;
        public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);
        public event Action Fired;

        GameSettings settings;
        float readyAt, waveStart = -100f;
        Vector3 waveCenter;
        Material waveInstance;
        readonly List<MeshFilter> sceneMeshes = new List<MeshFilter>();

        static readonly int CenterId = Shader.PropertyToID("_Center");
        static readonly int RadiusId = Shader.PropertyToID("_Radius");
        static readonly int FadeId = Shader.PropertyToID("_Fade");

        void Awake() => Instance = this;

        void Start()
        {
            settings = GameSettings.Instance;
            if (router == null) router = FindAnyObjectByType<HandGestureRouter>();
            if (router != null) router.Clapped += Fire;
            if (waveMaterial != null) waveInstance = new Material(waveMaterial);
        }

        void OnDestroy()
        {
            if (router != null) router.Clapped -= Fire;
        }

        public void Fire()
        {
            if (!IsReady) return;
            readyAt = Time.time + settings.sonarCooldown;
            waveStart = Time.time;
            waveCenter = PlayerHead.Position;

            // Trozos de mesh de la escena (pueden cambiar: se buscan en cada palmada)
            sceneMeshes.Clear();
            int layer = Layers.SceneMesh;
            foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
                if (mf.gameObject.layer == layer && mf.sharedMesh != null) sceneMeshes.Add(mf);

            foreach (var g in GhostController.All)
                if (g != null && g.IsAlive) g.ShowSilhouette(settings.sonarSilhouetteTime, silhouetteMaterial);

            if (AudioManager.Instance != null) AudioManager.Instance.Play2D(SfxLibrary.Sonar, 0.8f);
            Debug.Log($"[Espectros] Sonar: {GhostController.All.Count} fantasmas revelados");
            Fired?.Invoke();
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.cKey.wasPressedThisFrame) Fire();
#endif
            DrawWave();
        }

        // La onda se dibuja encima de la mesh con Graphics.RenderMesh mientras dura
        void DrawWave()
        {
            if (waveInstance == null) return;
            float t = Time.time - waveStart;
            float duration = settings.sonarRange / Mathf.Max(0.1f, settings.sonarWaveSpeed);
            if (t < 0f || t > duration) return;

            waveInstance.SetVector(CenterId, waveCenter);
            waveInstance.SetFloat(RadiusId, t * settings.sonarWaveSpeed);
            waveInstance.SetFloat(FadeId, 1f - t / duration);
            var rp = new RenderParams(waveInstance) { layer = Layers.SceneMesh };
            foreach (var mf in sceneMeshes)
                if (mf != null) Graphics.RenderMesh(rp, mf.sharedMesh, 0, mf.transform.localToWorldMatrix);
        }
    }
}
