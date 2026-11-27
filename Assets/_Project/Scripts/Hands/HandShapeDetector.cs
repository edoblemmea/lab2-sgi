using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

namespace Espectros
{
    // Detecta una Hand Shape o Hand Pose de XR Hands y lanza eventos al empezar y al terminar.
    // Versión simplificada del StaticHandGesture del sample de Gestures (sin iconos de UI).
    public class HandShapeDetector : MonoBehaviour
    {
        [Tooltip("Componente XRHandTrackingEvents de la mano que se vigila")]
        [SerializeField] XRHandTrackingEvents handTrackingEvents;
        [Tooltip("Asset XRHandShape o XRHandPose")]
        [SerializeField] ScriptableObject handShapeOrPose;
        [Tooltip("Referencia para las condiciones de orientación de una Hand Pose (normalmente la Main Camera)")]
        [SerializeField] Transform targetTransform;
        [SerializeField] float minimumHoldTime = 0.1f;
        [SerializeField] float detectionInterval = 0.05f;

        public UnityEvent gesturePerformed = new UnityEvent();
        public UnityEvent gestureEnded = new UnityEvent();

        public bool IsPerformed => performed;

        XRHandShape shape;
        XRHandPose pose;
        bool wasDetected, performed;
        float lastCheck, holdStart;

        void OnEnable()
        {
            shape = handShapeOrPose as XRHandShape;
            pose = handShapeOrPose as XRHandPose;
            if (pose != null && pose.relativeOrientation != null)
                pose.relativeOrientation.targetTransform = targetTransform;

            if (handTrackingEvents != null)
                handTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);
            else
                Debug.LogError($"[Espectros] {name}: falta asignar Hand Tracking Events");
        }

        void OnDisable()
        {
            if (handTrackingEvents != null)
                handTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
            if (performed) { performed = false; gestureEnded?.Invoke(); }
            wasDetected = false;
        }

        void OnJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            if (Time.time < lastCheck + detectionInterval) return;
            lastCheck = Time.time;

            bool detected = handTrackingEvents.handIsTracked &&
                            ((shape != null && shape.CheckConditions(args)) ||
                             (pose != null && pose.CheckConditions(args)));

            if (detected && !wasDetected) holdStart = Time.time;

            if (!detected && performed)
            {
                performed = false;
                gestureEnded?.Invoke();
            }

            if (detected && !performed && Time.time - holdStart >= minimumHoldTime)
            {
                performed = true;
                gesturePerformed?.Invoke();
            }

            wasDetected = detected;
        }
    }
}
