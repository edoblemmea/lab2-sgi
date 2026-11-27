using UnityEngine;

namespace Espectros
{
    // Gira el objeto para que mire siempre a la cámara (textos de depuración, etiquetas...).
    public class FaceCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = PlayerHead.Transform;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.position);
        }
    }
}
