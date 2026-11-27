using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace Espectros
{
    public enum HandSide { Left = 0, Right = 1 }

    // Da las poses de las articulaciones en espacio de mundo y la velocidad de la palma
    // (media de los últimos frames). El resto de sistemas leen las manos desde aquí.
    public class HandTracker : MonoBehaviour
    {
        [SerializeField] XROrigin xrOrigin;
        [Tooltip("Frames usados para promediar la velocidad de la palma")]
        [SerializeField, Range(2, 15)] int velocityFrames = 5;

        public static HandTracker Instance { get; private set; }

        static readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();

        XRHandSubsystem subsystem;
        Transform originTransform;
        HandState left, right;

        class HandState
        {
            public bool tracked;
            public Pose palm;
            public Vector3 velocity;
            public readonly Vector3[] positions;
            public readonly float[] times;
            public int next, count;

            public HandState(int frames)
            {
                positions = new Vector3[frames];
                times = new float[frames];
            }
        }

        void Awake()
        {
            Instance = this;
            if (xrOrigin == null) xrOrigin = FindAnyObjectByType<XROrigin>();
            originTransform = xrOrigin != null ? xrOrigin.transform : null;
            left = new HandState(velocityFrames);
            right = new HandState(velocityFrames);
        }

        void Update()
        {
            if (subsystem == null || !subsystem.running)
            {
                subsystem = null;
                SubsystemManager.GetSubsystems(subsystems);
                foreach (var s in subsystems)
                    if (s.running) { subsystem = s; break; }
                if (subsystem == null) { left.tracked = right.tracked = false; return; }
            }

            UpdateHand(subsystem.leftHand, left);
            UpdateHand(subsystem.rightHand, right);
        }

        void UpdateHand(XRHand hand, HandState state)
        {
            state.tracked = hand.isTracked && TryGetWorldPose(hand, XRHandJointID.Palm, out state.palm);
            if (!state.tracked) { state.count = 0; state.velocity = Vector3.zero; return; }

            // Buffer circular de posiciones para calcular la velocidad media
            int n = state.positions.Length;
            state.positions[state.next] = state.palm.position;
            state.times[state.next] = Time.time;
            int newest = state.next;
            state.next = (state.next + 1) % n;
            state.count = Mathf.Min(state.count + 1, n);

            if (state.count >= 2)
            {
                int oldest = (newest - state.count + 1 + n) % n;
                float dt = state.times[newest] - state.times[oldest];
                state.velocity = dt > 0f ? (state.positions[newest] - state.positions[oldest]) / dt : Vector3.zero;
            }
        }

        bool TryGetWorldPose(XRHand hand, XRHandJointID id, out Pose pose)
        {
            if (!hand.GetJoint(id).TryGetPose(out pose)) return false;
            if (originTransform != null) pose = pose.GetTransformedBy(originTransform);
            return true;
        }

        HandState Get(HandSide side) => side == HandSide.Left ? left : right;

        public bool IsTracked(HandSide side) => Get(side).tracked;
        public Pose PalmPose(HandSide side) => Get(side).palm;
        public Vector3 PalmVelocity(HandSide side) => Get(side).velocity;

        // En OpenXR el eje +Y de la palma sale por el dorso: la normal de la palma es -Y.
        public Vector3 PalmNormal(HandSide side) => -(Get(side).palm.rotation * Vector3.up);

        public bool TryGetJointPose(HandSide side, XRHandJointID id, out Pose pose)
        {
            pose = default;
            if (subsystem == null) return false;
            var hand = side == HandSide.Left ? subsystem.leftHand : subsystem.rightHand;
            return hand.isTracked && TryGetWorldPose(hand, id, out pose);
        }
    }
}
