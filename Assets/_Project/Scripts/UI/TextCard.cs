using TMPro;
using UnityEngine;

namespace Espectros
{
    // Ficha de papel detrás de un texto 3D (TextMeshPro): un quad que se ajusta al tamaño del texto
    // y se oculta cuando el texto está vacío. Da un fondo sólido para leer sobre cualquier pared real.
    [RequireComponent(typeof(TMP_Text))]
    public class TextCard : MonoBehaviour
    {
        [SerializeField] Renderer card;
        [Tooltip("Margen alrededor del texto (en las unidades del texto)")]
        [SerializeField] Vector2 padding = new Vector2(0.04f, 0.03f);
        [Tooltip("Distancia del papel por detrás del texto")]
        [SerializeField] float depth = 0.003f;

        TMP_Text text;

        void Awake() => text = GetComponent<TMP_Text>();

        void LateUpdate()
        {
            if (card == null) return;
            bool show = !string.IsNullOrEmpty(text.text) && text.textInfo != null && text.textInfo.characterCount > 0;
            card.enabled = show;
            if (!show) return;

            Bounds b = text.textBounds;
            var t = card.transform;
            t.localPosition = new Vector3(b.center.x, b.center.y, depth);
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(b.size.x + padding.x * 2f, b.size.y + padding.y * 2f, 1f);
        }
    }
}
