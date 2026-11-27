using System;
using TMPro;
using UnityEngine;

namespace Espectros
{
    public enum ThrowKind { Fist, Pinch }

    // Recibe los gestos estáticos (HandShapeDetector) y los convierte en comandos del juego.
    // El lanzamiento y la palmada se calculan aquí con la velocidad de la palma de HandTracker.
    // El parámetro int de los métodos públicos es la mano: 0 = izquierda, 1 = derecha
    // (los UnityEvent del inspector no admiten enums).
    // TODO: mover los umbrales a GameSettings cuando exista.
    public class HandGestureRouter : MonoBehaviour
    {
        [SerializeField] HandTracker tracker;

        [Header("Lanzamiento")]
        [Tooltip("Velocidad mínima de la palma (m/s) al abrir la mano para contar como lanzamiento")]
        [SerializeField] float throwMinSpeed = 1.2f;
        [Tooltip("Segundos tras soltar el puño o el pellizco en los que se acepta el lanzamiento")]
        [SerializeField] float throwWindow = 0.25f;

        [Header("Palmada")]
        [SerializeField] float clapDistance = 0.09f;
        [Tooltip("Velocidad mínima (m/s) a la que se acercan las palmas")]
        [SerializeField] float clapMinClosingSpeed = 0.8f;
        [SerializeField] float clapCooldown = 1f;

        [Header("Depuración (opcional)")]
        [SerializeField] TMP_Text debugText;

        public event Action<bool> FlashlightGesture;                        // palma derecha hacia delante
        public event Action<HandSide, ThrowKind, Vector3, Vector3> Thrown;  // mano, tipo, origen, velocidad
        public event Action Clapped;

        public bool FlashlightActive { get; private set; }

        readonly bool[] holding = new bool[2];
        readonly ThrowKind[] holdKind = new ThrowKind[2];
        readonly float[] releaseDeadline = { -1f, -1f };
        float lastClapTime = -10f;

        void Awake()
        {
            if (tracker == null) tracker = FindFirstObjectByType<HandTracker>();
        }

        // ---- Métodos para los eventos de HandShapeDetector ----

        public void PalmForwardPerformed() { FlashlightActive = true; FlashlightGesture?.Invoke(true); Show("Linterna ON"); }
        public void PalmForwardEnded() { FlashlightActive = false; FlashlightGesture?.Invoke(false); Show("Linterna OFF"); }

        public void FistPerformed(int hand) => StartHold(hand, ThrowKind.Fist);
        public void FistEnded(int hand) => Release(hand, ThrowKind.Fist);
        public void PinchPerformed(int hand) => StartHold(hand, ThrowKind.Pinch);
        public void PinchEnded(int hand) => Release(hand, ThrowKind.Pinch);

        void StartHold(int hand, ThrowKind kind)
        {
            if (hand < 0 || hand > 1) return;
            holding[hand] = true;
            holdKind[hand] = kind;
            releaseDeadline[hand] = -1f;
            Show(kind + " " + (HandSide)hand);
        }

        void Release(int hand, ThrowKind kind)
        {
            if (hand < 0 || hand > 1 || !holding[hand] || holdKind[hand] != kind) return;
            holding[hand] = false;
            releaseDeadline[hand] = Time.time + throwWindow;
        }

        void Update()
        {
            if (tracker == null) return;

            // Lanzamiento: la mano se abre (fin del puño o del pellizco) mientras se mueve rápido
            for (int h = 0; h < 2; h++)
            {
                if (releaseDeadline[h] < 0f) continue;
                var side = (HandSide)h;
                if (Time.time > releaseDeadline[h] || !tracker.IsTracked(side)) { releaseDeadline[h] = -1f; continue; }

                Vector3 v = tracker.PalmVelocity(side);
                if (v.magnitude >= throwMinSpeed)
                {
                    releaseDeadline[h] = -1f;
                    Thrown?.Invoke(side, holdKind[h], tracker.PalmPose(side).position, v);
                    Show($"Lanzamiento {holdKind[h]} {side} {v.magnitude:0.0} m/s");
                }
            }

            DetectClap();
        }

        void DetectClap()
        {
            if (!tracker.IsTracked(HandSide.Left) || !tracker.IsTracked(HandSide.Right)) return;
            if (Time.time - lastClapTime < clapCooldown) return;

            Vector3 delta = tracker.PalmPose(HandSide.Right).position - tracker.PalmPose(HandSide.Left).position;
            if (delta.magnitude > clapDistance) return;

            // Velocidad a la que se acercan las palmas (positiva si se juntan)
            Vector3 rel = tracker.PalmVelocity(HandSide.Right) - tracker.PalmVelocity(HandSide.Left);
            float closing = -Vector3.Dot(rel, delta.normalized);
            if (closing < clapMinClosingSpeed) return;

            lastClapTime = Time.time;
            Clapped?.Invoke();
            Show("Palmada");
        }

        void Show(string msg)
        {
            Debug.Log("[Espectros] Gesto: " + msg);
            if (debugText != null) debugText.text = msg;
        }
    }
}
