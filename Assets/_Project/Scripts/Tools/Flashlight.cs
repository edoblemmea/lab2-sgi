using System;
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

        public float Battery { get; private set; }
        public float BatteryFraction => GameSettings.Instance.flashlightBattery > 0f ? Battery / GameSettings.Instance.flashlightBattery : 0f;
        public bool IsOn { get; private set; }
        public event Action<bool> Toggled;

        GameSettings settings;

        void Start()
        {
            settings = GameSettings.Instance;
            if (router == null) router = FindAnyObjectByType<HandGestureRouter>();
            if (tracker == null) tracker = FindAnyObjectByType<HandTracker>();
            Battery = settings.flashlightBattery;
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

        void SetOn(bool on)
        {
            IsOn = on;
            if (spotLight != null) spotLight.enabled = on;
            if (model != null) model.SetActive(on);
            Toggled?.Invoke(on);
        }
    }
}
