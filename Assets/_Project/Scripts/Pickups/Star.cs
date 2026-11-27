using UnityEngine;

namespace Espectros
{
    // Estrella escondida en un punto oculto de la habitación (detrás o debajo de un mueble).
    // Se recoge acercando la mano (o la cabeza, si no hay manos: editor). Gira y flota.
    public class Star : MonoBehaviour
    {
        [SerializeField] Transform visual;

        public HidingSpot Spot { get; private set; }
        StarSpawner spawner;
        Vector3 basePos;
        float phase;

        public void Place(StarSpawner owner, HidingSpot spot)
        {
            spawner = owner;
            Spot = spot;
            basePos = spot.position;
            transform.position = basePos;
            phase = Random.value * 10f;
            gameObject.SetActive(true);
        }

        void Update()
        {
            float t = Time.time + phase;
            transform.position = basePos + Vector3.up * (Mathf.Sin(t * 2f) * 0.02f);
            if (visual != null) visual.localRotation = Quaternion.Euler(0f, t * 90f, 0f);

            if (IsTouched()) spawner.Collect(this);
        }

        bool IsTouched()
        {
            var s = GameSettings.Instance;
            var tracker = HandTracker.Instance;
            bool anyHand = false;
            if (tracker != null)
            {
                foreach (var side in new[] { HandSide.Left, HandSide.Right })
                {
                    if (!tracker.IsTracked(side)) continue;
                    anyHand = true;
                    if ((tracker.PalmPose(side).position - transform.position).sqrMagnitude < s.starPickupDistance * s.starPickupDistance)
                        return true;
                    if (tracker.TryGetJointPose(side, UnityEngine.XR.Hands.XRHandJointID.IndexTip, out var tip)
                        && (tip.position - transform.position).sqrMagnitude < s.starPickupDistance * s.starPickupDistance)
                        return true;
                }
            }
            // Sin manos (editor o mandos): basta con acercar la cabeza
            if (!anyHand)
                return (PlayerHead.Position - transform.position).sqrMagnitude < s.starPickupDistanceNoHands * s.starPickupDistanceNoHands;
            return false;
        }
    }
}
