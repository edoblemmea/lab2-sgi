using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Espectros
{
    // Linterna: se enciende con la palma derecha abierta hacia delante (HandGestureRouter).
    // La luz SOLO afecta a los fantasmas: la Light tiene la culling mask en la capa Ghost (indicación del
    // profesor: no iluminar el espacio real) y el daño se calcula con un cono bloqueado por la mesh y los muebles.
    // En el editor (sin manos) se enciende manteniendo pulsada la tecla F y apunta con la cámara.
    public class Flashlight : MonoBehaviour
    {
        [SerializeField] HandGestureRouter router;
        [SerializeField] HandTracker tracker;
        [Tooltip("Luz de tipo Spot hija de este objeto")]
        [SerializeField] Light spotLight;
        [Tooltip("Modelo de la linterna (opcional)")]
        [SerializeField] GameObject model;
        [Tooltip("Material del haz visible (Custom/LightBeam). Si está vacío no se dibuja el haz")]
        [SerializeField] Material beamMaterial;
        [Tooltip("El haz empieza este poco por delante de la linterna (m)")]
        [SerializeField] float beamStartOffset = 0.04f;

        Transform beam;
        float beamLength;

        public float Battery { get; private set; }
        public float BatteryFraction => GameSettings.Instance.flashlightBattery > 0f ? Battery / GameSettings.Instance.flashlightBattery : 0f;
        public bool IsOn { get; private set; }
        public event Action<bool> Toggled;

        GameSettings settings;
        float lastWantTime = -10f;

        void Start()
        {
            settings = GameSettings.Instance;
            if (router == null) router = FindAnyObjectByType<HandGestureRouter>();
            if (tracker == null) tracker = FindAnyObjectByType<HandTracker>();
            Battery = settings.flashlightBattery;
            if (beamMaterial != null) CreateBeam();
            if (spotLight != null)
            {
                spotLight.type = LightType.Spot;
                spotLight.shadows = LightShadows.None;
                spotLight.cullingMask = Layers.GhostMask; // no ilumina la habitación real
            }
            SetOn(false);
        }

        public void Recharge(float seconds) => Battery = Mathf.Min(settings.flashlightBattery, Battery + seconds);
        public void RechargeFull() => Battery = settings.flashlightBattery;

        void Update()
        {
            bool handTracked = tracker != null && tracker.IsTracked(HandSide.Right);
            bool wantOn = router != null && router.FlashlightActive && handTracked;
#if ENABLE_INPUT_SYSTEM
            bool keyOn = Keyboard.current != null && Keyboard.current.fKey.isPressed;
            wantOn |= keyOn;
#else
            bool keyOn = false;
#endif
            // Si el reconocimiento del gesto parpadea, no apagarla al instante
            if (wantOn) lastWantTime = Time.time;
            else if (handTracked && Time.time - lastWantTime < settings.flashlightOffDelay) wantOn = true;
            if (wantOn && Battery <= 0f) wantOn = false;
            if (wantOn != IsOn) SetOn(wantOn);

            // Pose: delante de la palma, apuntando según su normal; con la tecla, desde la cámara
            if (handTracked && !keyOn)
            {
                Pose palm = tracker.PalmPose(HandSide.Right);
                Vector3 dir = tracker.PalmNormal(HandSide.Right);
                transform.SetPositionAndRotation(palm.position + dir * 0.06f,
                    Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 20f * Time.deltaTime));
            }
            else if (PlayerHead.Transform != null)
            {
                var head = PlayerHead.Transform;
                transform.SetPositionAndRotation(head.position + head.right * 0.15f - head.up * 0.15f, head.rotation);
            }

            if (!IsOn) return;
            Battery = Mathf.Max(0f, Battery - Time.deltaTime);
            if (spotLight != null)
            {
                spotLight.range = settings.flashlightRange;
                spotLight.spotAngle = settings.flashlightAngle;
                spotLight.intensity = settings.flashlightIntensity;
                spotLight.color = settings.flashlightColor;
            }
            UpdateBeam();
            LightGhosts();
        }

        void LightGhosts()
        {
            Vector3 origin = transform.position;
            Vector3 fwd = transform.forward;
            float range = settings.flashlightRange;
            float cosHalf = Mathf.Cos(settings.flashlightAngle * 0.5f * Mathf.Deg2Rad);
            float dmg = settings.flashlightDamagePerSecond * Time.deltaTime;
            int mask = Layers.OccluderMask;

            var ghosts = GhostController.All;
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var g = ghosts[i];
                if (g == null || !g.IsAlive) continue;
                Vector3 to = g.Center - origin;
                float dist = to.magnitude;
                if (dist > range || dist < 0.01f) continue;
                if (Vector3.Dot(to / dist, fwd) < cosHalf) continue;
                // La mesh y los muebles bloquean la luz
                if (Physics.Linecast(origin, g.Center, mask, QueryTriggerInteraction.Ignore)) continue;
                g.ApplyLight(dmg);
            }
        }

        // ---------- Haz visible ----------
        // Cono de 1 m de largo con el ángulo de la linterna; se escala con la distancia hasta el primer obstáculo,
        // así el haz se corta en la mesh y en los muebles (y no ilumina las superficies reales)

        void CreateBeam()
        {
            var go = new GameObject("Beam");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.forward * beamStartOffset;
            go.AddComponent<MeshFilter>().sharedMesh = BuildCone(settings.flashlightAngle);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = beamMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            beam = go.transform;
            beam.gameObject.SetActive(false);
            beamAngle = settings.flashlightAngle;
        }

        float beamAngle;

        void UpdateBeam()
        {
            if (beam == null) return;
            // Si se cambia el ángulo en los ajustes, se rehace el cono
            if (!Mathf.Approximately(beamAngle, settings.flashlightAngle))
            {
                beamAngle = settings.flashlightAngle;
                beam.GetComponent<MeshFilter>().sharedMesh = BuildCone(beamAngle);
            }
            Vector3 origin = beam.position;
            float target = settings.flashlightRange;
            if (Physics.Raycast(origin, transform.forward, out var hit, settings.flashlightRange, Layers.OccluderMask, QueryTriggerInteraction.Ignore))
                target = hit.distance;
            beamLength = Mathf.Lerp(beamLength <= 0f ? target : beamLength, target, 1f - Mathf.Exp(-25f * Time.deltaTime));
            beam.localScale = Vector3.one * Mathf.Max(0.05f, beamLength);
        }

        // Cono abierto: vértice en el origen, base a z = 1, uv.y = distancia a lo largo
        static Mesh BuildCone(float angle)
        {
            const int seg = 24, rings = 6;
            float r = Mathf.Tan(angle * 0.5f * Mathf.Deg2Rad);
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int k = 0; k <= rings; k++)
            {
                float z = k / (float)rings;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    v.Add(dir * (r * z) + Vector3.forward * z);
                    n.Add((dir - Vector3.forward * r).normalized);
                    uv.Add(new Vector2(i / (float)seg, z));
                }
            }
            for (int k = 0; k < rings; k++)
                for (int i = 0; i < seg; i++)
                {
                    int a = k * (seg + 1) + i, b = a + seg + 1;
                    t.Add(a); t.Add(b); t.Add(a + 1);
                    t.Add(a + 1); t.Add(b); t.Add(b + 1);
                }
            var m = new Mesh { name = "FlashlightBeam" };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.bounds = new Bounds(Vector3.forward * 0.5f, new Vector3(2f * r + 0.1f, 2f * r + 0.1f, 1.1f));
            return m;
        }

        void SetOn(bool on)
        {
            IsOn = on;
            if (beam != null) { beam.gameObject.SetActive(on); if (on) beamLength = 0f; }
            if (on && AudioManager.Instance != null) AudioManager.Instance.PlayAt(SfxLibrary.Click, transform.position, 0.6f);
            if (spotLight != null) spotLight.enabled = on;
            if (model != null) model.SetActive(on);
            Toggled?.Invoke(on);
        }
    }
}
