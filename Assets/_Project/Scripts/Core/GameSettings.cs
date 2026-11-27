using System;
using UnityEngine;

namespace Espectros
{
    public enum Difficulty { Easy, Normal, Hard }

    // Todos los valores ajustables del juego en un único sitio (editable en el inspector).
    // El menú de muñeca solo tiene que cambiar estos valores y llamar a NotifyChanged().
    [DefaultExecutionOrder(-100)]
    public class GameSettings : MonoBehaviour
    {
        static GameSettings instance;

        // Si no hay ninguno en la escena se crea uno con los valores por defecto
        public static GameSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<GameSettings>();
                    if (instance == null && Application.isPlaying)
                        instance = new GameObject("GameSettings (auto)").AddComponent<GameSettings>();
                }
                return instance;
            }
        }

        public event Action Changed;

        [Header("General")]
        public Difficulty difficulty = Difficulty.Normal;
        [Tooltip("Muestra la mesh, los escondites y el estado de cada fantasma")]
        public bool debugMode = false;

        [Header("Dificultad (multiplicadores: fácil, normal, difícil)")]
        public Vector3 ghostSpeedByDifficulty = new Vector3(0.75f, 1f, 1.3f);
        public Vector3 hideTimeByDifficulty = new Vector3(1.4f, 1f, 0.7f);
        public Vector3 roundTimeByDifficulty = new Vector3(1.3f, 1f, 0.8f);

        [Header("Fantasmas")]
        [Tooltip("Distancia a la cabeza a la que el fantasma asusta (m)")]
        public float scareDistance = 0.45f;
        [Tooltip("Tiempo escondido antes de asomarse (s, mínimo y máximo)")]
        public Vector2 hiddenTime = new Vector2(2f, 5f);
        [Tooltip("Tiempo que pasa asomado (s)")]
        public float peekDuration = 1.5f;
        [Tooltip("Tiempo que lo tienes que mirar para que huya (s)")]
        public float seenToFlee = 0.4f;
        [Tooltip("Multiplicador de velocidad al acercarse")]
        public float approachSpeedFactor = 0.6f;
        [Tooltip("Multiplicador de velocidad al huir")]
        public float fleeSpeedFactor = 2.5f;
        [Tooltip("Multiplicador de velocidad en modo furia")]
        public float enragedSpeedFactor = 1.5f;
        [Tooltip("Distancia mínima a la cabeza del escondite al que huye (m)")]
        public float fleeMinDistance = 1.5f;

        [Header("Escondites")]
        public float hidingUpdateInterval = 0.5f;
        [Tooltip("Separación entre el mueble y el escondite (m)")]
        public float spotOffset = 0.25f;
        [Tooltip("Distancia mínima entre escondites (m)")]
        public float spotMinSeparation = 0.3f;
        [Tooltip("Profundidad dentro de la pared de los escondites de pared (m)")]
        public float wallSpotDepth = 0.15f;
        [Tooltip("Rayos horizontales para buscar paredes")]
        public int wallRayCount = 24;

        [Header("Linterna")]
        public float flashlightRange = 4f;
        [Tooltip("Ángulo total del cono (grados)")]
        public float flashlightAngle = 30f;
        [Tooltip("Daño por segundo de luz (un fantasma blanco tiene 1,5 de vida)")]
        public float flashlightDamagePerSecond = 1f;
        [Tooltip("Batería máxima (segundos de luz)")]
        public float flashlightBattery = 40f;
        public float flashlightIntensity = 3f;
        public Color flashlightColor = new Color(1f, 0.95f, 0.8f);

        [Header("Charcos (SurfaceLiquid)")]
        public float puddleRadius = 0.35f;
        [Tooltip("Tiempo que tarda en extenderse (s)")]
        public float puddleSpreadTime = 1.5f;
        public int maxPuddles = 20;
        [Tooltip("Coste extra al subir: ×(1 + factor·pendiente)")]
        public float liquidUpCost = 3f;
        [Tooltip("Coste al bajar (menos de 1 para que escurra)")]
        public float liquidDownCost = 0.7f;
        [Tooltip("Separación del charco respecto a la superficie (m)")]
        public float liquidSurfaceOffset = 0.003f;

        [Header("Jugadora")]
        public int maxHearts = 3;
        public float invulnerabilityTime = 2f;
        public float redFlashTime = 0.6f;

        [Header("Rondas")]
        public float roundIntroTime = 3f;
        public float roundWonTime = 4f;

        [Header("Menú de muñeca")]
        [Tooltip("Producto escalar mínimo entre la normal de la palma y la dirección a la cabeza")]
        public float wristMenuOpenDot = 0.7f;
        public float wristMenuHoldTime = 0.3f;
        [Tooltip("Velocidad máxima de la mano para abrirlo (m/s)")]
        public float wristMenuMaxHandSpeed = 0.35f;

        // ---- Valores derivados de la dificultad ----
        float ByDifficulty(Vector3 v) => difficulty == Difficulty.Easy ? v.x : difficulty == Difficulty.Hard ? v.z : v.y;
        public float GhostSpeedMultiplier => ByDifficulty(ghostSpeedByDifficulty);
        public float HideTimeMultiplier => ByDifficulty(hideTimeByDifficulty);
        public float RoundTimeMultiplier => ByDifficulty(roundTimeByDifficulty);

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
        }

        public void SetDifficulty(Difficulty d) { difficulty = d; NotifyChanged(); }
        public void CycleDifficulty() => SetDifficulty((Difficulty)(((int)difficulty + 1) % 3));
        public void SetDebugMode(bool on) { debugMode = on; NotifyChanged(); }
        public void ToggleDebugMode() => SetDebugMode(!debugMode);

        public void NotifyChanged()
        {
            Debug.Log($"[Espectros] Ajustes: dificultad {difficulty}, depuración {(debugMode ? "ON" : "OFF")}");
            Changed?.Invoke();
        }

        void OnValidate()
        {
            if (Application.isPlaying) Changed?.Invoke();
        }
    }
}
