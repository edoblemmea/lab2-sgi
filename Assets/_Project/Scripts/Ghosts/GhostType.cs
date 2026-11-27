using UnityEngine;

namespace Espectros
{
    public enum GhostWeakness { Flashlight, Ball, Water, PaintThenFlashlight }

    // Tipo de fantasma: color, velocidad, vida, herramienta a la que es vulnerable y sonidos.
    [CreateAssetMenu(fileName = "GhostType", menuName = "Espectros/Ghost Type")]
    public class GhostType : ScriptableObject
    {
        public string displayName = "Blanco";
        public Color color = new Color(0.9f, 0.95f, 1f, 1f);
        [Tooltip("Velocidad base (m/s)")]
        public float speed = 0.6f;
        [Tooltip("Vida (con la linterna: segundos de luz a 1 de daño por segundo)")]
        public float health = 1.5f;
        public GhostWeakness weakness = GhostWeakness.Flashlight;
        [Tooltip("Altura del fantasma (m)")]
        public float size = 0.45f;
        [Tooltip("Aviso que se muestra la primera vez que aparece este tipo")]
        [TextArea] public string hint = "";
        [Tooltip("Prefab del fantasma (si está vacío se usa el del spawner)")]
        public GhostController prefab;

        [Header("Sonidos (opcionales)")]
        public AudioClip appearSound;
        public AudioClip scareSound;
        public AudioClip hurtSound;
        public AudioClip dieSound;
    }
}
