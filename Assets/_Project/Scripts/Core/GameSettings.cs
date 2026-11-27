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
        public Vector2 hiddenTime = new Vector2(1.5f, 3.5f);
        [Tooltip("Tiempo que pasa asomado (s)")]
        public float peekDuration = 4f;
        [Tooltip("Tiempo que lo tienes que mirar para que huya (s)")]
        public float seenToFlee = 2f;
        [Tooltip("Multiplicador de velocidad al acercarse")]
        public float approachSpeedFactor = 0.6f;
        [Tooltip("Multiplicador de velocidad al huir")]
        public float fleeSpeedFactor = 1.2f;
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
        public float flashlightAngle = 50f;
        [Tooltip("Daño por segundo de luz (un fantasma blanco tiene 1,5 de vida)")]
        public float flashlightDamagePerSecond = 4f;
        [Tooltip("Tiempo que el fantasma sigue paralizado (y sin poder huir) después de dejar de iluminarlo (s)")]
        public float lightStunTime = 1f;
        [Tooltip("La linterna sigue encendida este tiempo después de perder el gesto (por si el reconocimiento parpadea) (s)")]
        public float flashlightOffDelay = 0.3f;
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

        [Header("Pelota")]
        public int ballsAtStart = 5;
        public int maxBalls = 10;
        [Tooltip("Multiplicador de la velocidad de la mano al lanzar")]
        public float ballThrowMultiplier = 1.5f;
        public float ballMinSpeed = 2.5f;
        public float ballMaxSpeed = 9f;
        [Tooltip("Rebote del Physic Material (0-1)")]
        public float ballBounciness = 0.8f;
        [Tooltip("Segundos hasta que la pelota desaparece")]
        public float ballLifetime = 8f;
        [Tooltip("Cada cuántos segundos se recarga una pelota (hasta tener Balls At Start; con estrellas se puede pasar)")]
        public float ballRechargeTime = 4f;

        [Header("Pintura (revela al fantasma invisible)")]
        public float paintThrowMultiplier = 1.4f;
        public float paintMinSpeed = 3f;
        public float paintMaxSpeed = 8f;
        [Tooltip("Tiempo mínimo entre dos lanzamientos (s)")]
        public float paintCooldown = 0.4f;
        [Tooltip("Radio de la mancha (m)")]
        public float paintRadius = 0.18f;
        [Tooltip("Tiempo que el fantasma invisible queda visible y vulnerable a la linterna (s)")]
        public float paintRevealTime = 6f;
        [Tooltip("Distancia del impacto a la que la pintura revela a un fantasma (m)")]
        public float paintRevealRadius = 0.5f;

        [Header("Puntos")]
        public int killScore = 100;
        [Tooltip("Puntos extra por cada rebote antes de dar al fantasma (carambola)")]
        public int bounceBonus = 50;

        [Header("Estrellas")]
        [Tooltip("Estrellas a la vez en la habitación")]
        public int starsAtOnce = 2;
        [Tooltip("Segundos entre una estrella recogida y la siguiente")]
        public float starRespawnTime = 8f;
        [Tooltip("Distancia de la mano para recogerla (m)")]
        public float starPickupDistance = 0.12f;
        [Tooltip("Distancia de la cabeza para recogerla si no hay manos (editor) (m)")]
        public float starPickupDistanceNoHands = 0.7f;
        [Tooltip("Segundos de batería que recarga")]
        public float starBatteryRecharge = 15f;
        [Tooltip("Pelotas que da")]
        public int starBalls = 2;
        [Tooltip("Cada cuántas estrellas se gana un corazón")]
        public int starsPerHeart = 5;

        [Header("Sonar")]
        public float sonarCooldown = 8f;
        [Tooltip("Tiempo que se ven las siluetas a través de los muebles (s)")]
        public float sonarSilhouetteTime = 1f;
        [Tooltip("Velocidad de la onda sobre la mesh (m/s)")]
        public float sonarWaveSpeed = 4f;
        public float sonarRange = 6f;

        [Header("Sonido")]
        [Range(0f, 1f)] public float ghostAmbientVolume = 0.35f;
        [Range(0f, 1f)] public float effectsVolume = 0.8f;

        [Header("Tutorial")]
        public bool showTutorial = true;
        [Tooltip("Si no se hace el gesto en este tiempo, se pasa al siguiente paso (s)")]
        public float tutorialStepTimeout = 20f;

        // ---- Valores derivados de la dificultad ----
        float ByDifficulty(Vector3 v) => difficulty == Difficulty.Easy ? v.x : difficulty == Difficulty.Hard ? v.z : v.y;
        public float GhostSpeedMultiplier => ByDifficulty(ghostSpeedByDifficulty);
        public float HideTimeMultiplier => ByDifficulty(hideTimeByDifficulty);
        public float RoundTimeMultiplier => ByDifficulty(roundTimeByDifficulty);

        // Versión de los valores guardados en la escena. Si se cambian los valores por defecto en el código,
        // se sube CurrentVersion y se añade el cambio en Migrate(): así las escenas ya creadas se actualizan solas.
        const int CurrentVersion = 3;
        // Empieza en 0: las escenas guardadas antes de existir este campo lo cargan como 0 y se migran.
        [SerializeField, HideInInspector] int settingsVersion = 0;

        // Al añadir el componente (o hacer Reset) ya tiene los valores actuales
        void Reset() => settingsVersion = CurrentVersion;

        void Migrate()
        {
            if (settingsVersion >= CurrentVersion) return;
            if (settingsVersion < 2)
            {
                // Linterna más eficaz y fantasmas que tardan más en escaparse
                flashlightAngle = 40f;
                flashlightDamagePerSecond = 2f;
                lightStunTime = 0.6f;
                seenToFlee = 0.8f;
                peekDuration = 2.5f;
                fleeSpeedFactor = 1.8f;
            }
            if (settingsVersion < 3)
            {
                // Más fácil todavía: muere en menos de medio segundo de luz y tarda mucho más en asustarse
                flashlightAngle = 50f;
                flashlightDamagePerSecond = 4f;
                lightStunTime = 1f;
                flashlightOffDelay = 0.3f;
                seenToFlee = 2f;
                peekDuration = 4f;
                fleeSpeedFactor = 1.2f;
                hiddenTime = new Vector2(1.5f, 3.5f);
            }
            settingsVersion = CurrentVersion;
            Debug.Log("[Espectros] GameSettings actualizado a la versión " + CurrentVersion);
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            Migrate();
            Debug.Log($"[Espectros] Ajustes v{settingsVersion}: linterna {flashlightAngle}° daño {flashlightDamagePerSecond}/s " +
                      $"parálisis {lightStunTime}s, huye tras {seenToFlee}s mirándolo, asomado {peekDuration}s, huida x{fleeSpeedFactor}");
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
            Migrate();
            if (Application.isPlaying) Changed?.Invoke();
        }
    }
}
