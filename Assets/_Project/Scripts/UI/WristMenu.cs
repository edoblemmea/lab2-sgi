using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Espectros
{
    // Menú de muñeca mínimo (fase 2): al girar la mano izquierda y mirarse la cara interna de la muñeca
    // aparece un panel anclado a la muñeca con 3 botones: reiniciar ronda, dificultad y modo depuración.
    // Los botones se pulsan tocándolos con la punta del índice derecho.
    // En el editor: teclas 1 (reiniciar), 2 (dificultad), 3 (depuración) y M (abrir/cerrar).
    public class WristMenu : MonoBehaviour
    {
        [SerializeField] HandTracker tracker;
        [Tooltip("Raíz del panel (Canvas en espacio de mundo)")]
        [SerializeField] GameObject panel;
        [SerializeField] Button restartButton;
        [SerializeField] Button difficultyButton;
        [SerializeField] Button debugButton;
        [SerializeField] TMP_Text difficultyLabel;
        [SerializeField] TMP_Text debugLabel;
        [Tooltip("Distancia del panel a la muñeca, hacia la cara interna (m)")]
        [SerializeField] float offsetFromWrist = 0.07f;
        [Tooltip("Distancia del dedo al panel para pulsar (m)")]
        [SerializeField] float pressDepth = 0.015f;

        GameSettings settings;
        float gestureTime, closeTime, lastPress;
        bool open, forcedOpen;
        Button[] buttons;

        void Start()
        {
            settings = GameSettings.Instance;
            if (tracker == null) tracker = FindAnyObjectByType<HandTracker>();
            if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
            if (difficultyButton != null) difficultyButton.onClick.AddListener(OnDifficulty);
            if (debugButton != null) debugButton.onClick.AddListener(OnDebug);
            buttons = new[] { restartButton, difficultyButton, debugButton };
            settings.Changed += RefreshLabels;
            RefreshLabels();
            SetOpen(false);
        }

        void OnDestroy()
        {
            if (settings != null) settings.Changed -= RefreshLabels;
        }

        void OnRestart() { if (GameManager.Instance != null) GameManager.Instance.RestartRound(); }
        void OnDifficulty() => settings.CycleDifficulty();
        void OnDebug() => settings.ToggleDebugMode();

        void RefreshLabels()
        {
            string[] names = { "Fácil", "Normal", "Difícil" };
            if (difficultyLabel != null) difficultyLabel.text = "Dificultad: " + names[(int)settings.difficulty];
            if (debugLabel != null) debugLabel.text = "Depuración: " + (settings.debugMode ? "ON" : "OFF");
        }

        void Update()
        {
            HandleKeys();
            if (panel == null) return;

            bool gesture = DetectGesture(out Pose wrist, out Vector3 palmNormal);
            if (gesture)
            {
                gestureTime += Time.deltaTime;
                closeTime = 0f;
                if (!open && gestureTime >= settings.wristMenuHoldTime) SetOpen(true);
            }
            else
            {
                gestureTime = 0f;
                closeTime += Time.deltaTime;
                if (open && !forcedOpen && closeTime >= 0.3f) SetOpen(false);
            }

            if (!open) return;

            // Colocar el panel encima de la cara interna de la muñeca, mirando a la cabeza, con suavizado
            Vector3 head = PlayerHead.Position;
            Vector3 target;
            if (tracker != null && tracker.IsTracked(HandSide.Left))
                target = wrist.position + palmNormal * offsetFromWrist;
            else
                target = head + PlayerHead.Forward * 0.45f - Vector3.up * 0.2f; // editor o mano perdida
            Quaternion rot = Quaternion.LookRotation(target - head, Vector3.up);
            float k = 1f - Mathf.Exp(-15f * Time.deltaTime);
            panel.transform.position = Vector3.Lerp(panel.transform.position, target, k);
            panel.transform.rotation = Quaternion.Slerp(panel.transform.rotation, rot, k);

            HandlePoke();
        }

        // Palma izquierda hacia la cara (producto escalar > umbral) y mano casi quieta
        bool DetectGesture(out Pose wrist, out Vector3 palmNormal)
        {
            wrist = default;
            palmNormal = Vector3.up;
            if (tracker == null || !tracker.IsTracked(HandSide.Left)) return false;
            if (!tracker.TryGetJointPose(HandSide.Left, XRHandJointID.Wrist, out wrist)) return false;
            palmNormal = tracker.PalmNormal(HandSide.Left);
            Vector3 toHead = (PlayerHead.Position - wrist.position).normalized;
            bool facing = Vector3.Dot(palmNormal, toHead) > (open ? settings.wristMenuOpenDot - 0.15f : settings.wristMenuOpenDot);
            bool still = tracker.PalmVelocity(HandSide.Left).magnitude < settings.wristMenuMaxHandSpeed || open;
            return facing && still;
        }

        // Pulsar con la punta del índice derecho
        void HandlePoke()
        {
            if (tracker == null || Time.time - lastPress < 0.5f) return;
            if (!tracker.TryGetJointPose(HandSide.Right, XRHandJointID.IndexTip, out Pose tip)) return;

            foreach (var b in buttons)
            {
                if (b == null) continue;
                var rt = (RectTransform)b.transform;
                Vector3 local = rt.InverseTransformPoint(tip.position);
                float depth = Mathf.Abs(Vector3.Dot(tip.position - rt.position, rt.forward));
                if (rt.rect.Contains(new Vector2(local.x, local.y)) && depth < pressDepth)
                {
                    lastPress = Time.time;
                    b.onClick.Invoke();
                    b.Select();
                    break;
                }
            }
        }

        void HandleKeys()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.digit1Key.wasPressedThisFrame) OnRestart();
            if (kb.digit2Key.wasPressedThisFrame) OnDifficulty();
            if (kb.digit3Key.wasPressedThisFrame) OnDebug();
            if (kb.mKey.wasPressedThisFrame) { forcedOpen = !open; SetOpen(!open); }
#endif
        }

        void SetOpen(bool value)
        {
            open = value;
            if (!value) forcedOpen = false;
            if (panel != null) panel.SetActive(value);
        }
    }
}
