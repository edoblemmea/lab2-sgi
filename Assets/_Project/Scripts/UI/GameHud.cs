using TMPro;
using UnityEngine;

namespace Espectros
{
    // Texto delante de la cámara con la ronda, el tiempo, los corazones, los fantasmas y la batería,
    // y un mensaje grande para los cambios de estado (ronda nueva, furia, ronda superada...).
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
            if (messageText != null) messageText.text = "";
        }

        void OnDestroy()
        {
            if (gm != null) gm.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(GameState s)
        {
            string msg = s switch
            {
                GameState.RoundIntro => $"Ronda {gm.RoundIndex + 1}",
                GameState.Enraged => "¡FURIA!",
                GameState.RoundWon => "¡Ronda superada!",
                GameState.GameOver => "GAME OVER",
                GameState.Victory => "¡Victoria!",
                _ => null,
            };
            if (msg == null || messageText == null) return;
            messageText.text = msg;
            messageUntil = s == GameState.GameOver || s == GameState.Victory ? float.MaxValue : Time.time + 2.5f;
        }

        void Update()
        {
            if (messageText != null && Time.time > messageUntil) messageText.text = "";
            if (statusText == null || gm == null) return;

            int t = Mathf.CeilToInt(Mathf.Max(0f, gm.TimeLeft));
            string time = gm.IsEnraged ? "FURIA" : $"{t / 60}:{t % 60:00}";
            string hearts = health != null ? $"Vidas: {Mathf.Max(0, health.Hearts)}" : "";
            int ghosts = spawner != null ? spawner.RemainingCount : 0;
            string battery = flashlight != null ? $"{flashlight.BatteryFraction * 100f:0}%" : "-";
            statusText.text = $"Ronda {gm.RoundIndex + 1}/{gm.RoundCount}   {time}   <color=#ff5060>{hearts}</color>\n" +
                              $"Fantasmas: {ghosts}   Linterna: {battery}";
        }
    }
}
