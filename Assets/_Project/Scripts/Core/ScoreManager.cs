using System;
using UnityEngine;

namespace Espectros
{
    // Puntos de la partida: por cada fantasma eliminado y bonus por carambola (rebotes de la pelota).
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        public int Score { get; private set; }
        public int BestCombo { get; private set; }

        public event Action Changed;
        // Mensaje corto para el HUD ("¡Carambola x2!")
        public event Action<string> Announcement;

        void Awake() => Instance = this;

        void Start()
        {
            if (GhostSpawner.Instance != null) GhostSpawner.Instance.GhostDied += OnGhostDied;
        }

        void OnDestroy()
        {
            if (GhostSpawner.Instance != null) GhostSpawner.Instance.GhostDied -= OnGhostDied;
        }

        public void ResetScore()
        {
            Score = 0;
            BestCombo = 0;
            Changed?.Invoke();
        }

        void OnGhostDied(GhostController g) => Add(GameSettings.Instance.killScore);

        // Llamado por la pelota al eliminar a un fantasma tras rebotar
        public void AddCarambola(int bounces)
        {
            if (bounces <= 0) return;
            Add(bounces * GameSettings.Instance.bounceBonus);
            BestCombo = Mathf.Max(BestCombo, bounces);
            Announcement?.Invoke(bounces == 1 ? "¡Carambola!" : $"¡Carambola x{bounces}!");
            Debug.Log($"[Espectros] Carambola de {bounces} rebotes");
        }

        public void Add(int points)
        {
            Score += points;
            Changed?.Invoke();
        }
    }
}
