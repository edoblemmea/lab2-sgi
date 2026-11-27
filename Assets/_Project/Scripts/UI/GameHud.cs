using TMPro;
using UnityEngine;

namespace Espectros
{
    // Texto delante de la cámara con la ronda, el tiempo, las vidas, los fantasmas, la batería, las pelotas,
    // las estrellas, los puntos y el sonar, y un mensaje grande para los cambios de estado
    // (ronda nueva, furia, carambola, estrella...).
    public class GameHud : MonoBehaviour
    {
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text messageText;
        [SerializeField] Flashlight flashlight;

        GameManager gm;
        GhostSpawner spawner;
        PlayerHealth health;
        float messageUntil;

        void Start()
        {
            gm = GameManager.Instance;
            spawner = GhostSpawner.Instance;
            health = PlayerHealth.Instance;
            if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();
            if (gm != null) gm.StateChanged += OnStateChanged;
            if (ScoreManager.Instance != null) ScoreManager.Instance.Announcement += ShowShort;
            if (StarSpawner.Instance != null) StarSpawner.Instance.Rewarded += ShowShort;
            if (messageText != null) messageText.text = "";
        }

        void OnDestroy()
        {
            if (gm != null) gm.StateChanged -= OnStateChanged;
            if (ScoreManager.Instance != null) ScoreManager.Instance.Announcement -= ShowShort;
            if (StarSpawner.Instance != null) StarSpawner.Instance.Rewarded -= ShowShort;
        }

        void OnStateChanged(GameState s)
        {
            // Estilo «Expediente»: cada ronda es un caso, con un sello rojo debajo
            string msg = s switch
            {
                GameState.RoundIntro => $"Caso {gm.RoundIndex + 1}" + Stamp("CONFIDENCIAL"),
                GameState.Enraged => "¡Furia!" + Stamp("PELIGRO"),
                GameState.RoundWon => "Caso cerrado" + Stamp("RESUELTO"),
                GameState.GameOver => "Caso abierto" + Stamp("SIN RESOLVER") + $"\n<size=40%>{Score} puntos</size>",
                GameState.Victory => "Expediente completo" + Stamp("TODOS RESUELTOS") + $"\n<size=40%>{Score} puntos</size>",
                _ => null,
            };
            if (msg == null) return;
            Show(msg, s == GameState.GameOver || s == GameState.Victory ? float.MaxValue : 2.5f);
        }

        int Score => ScoreManager.Instance != null ? ScoreManager.Instance.Score : 0;

        static string Stamp(string text) => $"\n<size=38%><color=#d0473b>{text}</color></size>";

        void ShowShort(string msg) => Show(msg, 2f);

        void Show(string msg, float seconds)
        {
            if (messageText == null) return;
            messageText.text = msg;
            messageUntil = seconds == float.MaxValue ? float.MaxValue : Time.time + seconds;
        }

        void Update()
        {
            if (messageText != null && Time.time > messageUntil) messageText.text = "";
            if (statusText == null || gm == null) return;

            if (gm.State == GameState.Tutorial)
            {
                statusText.text = "Instrucciones";
                return;
            }

            int t = Mathf.CeilToInt(Mathf.Max(0f, gm.TimeLeft));
            string time = gm.IsEnraged ? "<color=#ff6a5a>FURIA</color>" : $"T-{t / 60:00}:{t % 60:00}";
            string hearts = health != null ? $"<color=#ff8a7e>Vidas {Mathf.Max(0, health.Hearts)}</color>" : "";
            int ghosts = spawner != null ? spawner.RemainingCount : 0;
            string battery = flashlight != null ? $"{flashlight.BatteryFraction * 100f:0}%" : "-";
            int balls = BallThrower.Instance != null ? BallThrower.Instance.Balls : 0;
            var sonar = Sonar.Instance;
            string sonarText = sonar == null ? "-" : sonar.IsReady ? "<color=#9fe7d6>listo</color>" : $"{sonar.CooldownLeft:0}s";

            statusText.text = $"Caso {gm.RoundIndex + 1}/{gm.RoundCount}   {time}   {hearts}   Pts {Score:0000}\n" +
                              $"Fantasmas {ghosts}   Linterna {battery}   Pelotas {balls}   Estrellas {StarWallet.Stars}   Sonar {sonarText}";
        }
    }
}
