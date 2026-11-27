using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Espectros
{
    // Solo para probar en el editor (escena TestRoom): mover la cámara como si fuera la cabeza.
    // WASD para andar, Q/E para bajar/subir, botón derecho del ratón para mirar, Shift para ir más rápido.
    public class EditorPlayerController : MonoBehaviour
    {
        [SerializeField] float speed = 1.5f;
        [SerializeField] float lookSensitivity = 0.15f;

        float yaw, pitch;

        void Start()
        {
            var e = transform.eulerAngles;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null) return;

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                yaw += d.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - d.y * lookSensitivity, -80f, 80f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += Vector3.forward;
            if (kb.sKey.isPressed) move += Vector3.back;
            if (kb.aKey.isPressed) move += Vector3.left;
            if (kb.dKey.isPressed) move += Vector3.right;
            Vector3 flat = Quaternion.Euler(0f, yaw, 0f) * move;
            if (kb.eKey.isPressed) flat += Vector3.up;
            if (kb.qKey.isPressed) flat += Vector3.down;
            float s = speed * (kb.leftShiftKey.isPressed ? 2.5f : 1f);
            transform.position += flat * (s * Time.deltaTime);
#endif
        }
    }
}
