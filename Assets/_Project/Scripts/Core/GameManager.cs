using System;
using System.Collections.Generic;
using UnityEngine;

namespace Espectros
{
    public enum GameState { WaitingScene, Tutorial, RoundIntro, Playing, Enraged, RoundWon, GameOver, Victory }

    // Rondas, temporizador, corazones y modo furia.
    // WaitingScene -> (Tutorial) -> RoundIntro -> Playing -> (Enraged) -> RoundWon -> ... -> Victory / GameOver
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] List<RoundConfig> rounds = new List<RoundConfig>();
        [SerializeField] GhostSpawner spawner;
        [SerializeField] PlayerHealth health;

        public GameState State { get; private set; } = GameState.WaitingScene;
        public int RoundIndex { get; private set; }
        public int RoundCount => rounds.Count;
        public float TimeLeft { get; private set; }
        public bool IsEnraged => State == GameState.Enraged;
        public bool IsPlaying => State == GameState.Playing || State == GameState.Enraged;
        public RoundConfig CurrentRound => rounds.Count > 0 ? rounds[Mathf.Clamp(RoundIndex, 0, rounds.Count - 1)] : null;

        public event Action<GameState> StateChanged;

        GameSettings settings;
        float stateTimer;

        void Awake() => Instance = this;

        void Start()
        {
            settings = GameSettings.Instance;
            if (spawner == null) spawner = FindAnyObjectByType<GhostSpawner>();
            if (health == null) health = FindAnyObjectByType<PlayerHealth>();
            if (health != null) health.Died += OnPlayerDied;

            int missing = rounds.RemoveAll(r => r == null);
            if (missing > 0)
                Debug.LogError($"[Espectros] GameManager: {missing} rondas vacías en la lista. Vuelve a ejecutar el menú Espectros.");

            // Sin rondas configuradas: una ronda por defecto con el tipo del spawner
            if (rounds.Count == 0 && spawner != null && spawner.DefaultType != null)
            {
                var r = ScriptableObject.CreateInstance<RoundConfig>();
                r.ghosts.Add(new RoundConfig.Entry { type = spawner.DefaultType, count = 3 });
                rounds.Add(r);
            }

            if (SceneReady.IsReady) OnSceneReady(); else SceneReady.OnSceneReady += OnSceneReady;
        }

        void OnDestroy()
        {
            SceneReady.OnSceneReady -= OnSceneReady;
            if (health != null) health.Died -= OnPlayerDied;
        }

        void OnSceneReady() => StartGame();

        // ---------- Control (lo usa también el menú de muñeca) ----------

        public void StartGame()
        {
            StarWallet.Reset();
            if (health != null) health.ResetHearts();
            if (ScoreManager.Instance != null) ScoreManager.Instance.ResetScore();
            if (BallThrower.Instance != null) BallThrower.Instance.ResetBalls();

            // La primera vez, tutorial de los gestos antes de la ronda 1
            var tutorial = TutorialManager.Instance;
            if (tutorial != null && settings.showTutorial && !tutorial.Done)
            {
                spawner.Clear();
                SetState(GameState.Tutorial);
                tutorial.Begin(() => StartRound(0));
                return;
            }
            StartRound(0);
        }

        public void RestartRound()
        {
            if (health != null && health.IsDead) health.ResetHearts();
            StartRound(RoundIndex);
        }

        public void StartRound(int index)
        {
            if (rounds.Count == 0 || spawner == null)
            {
                Debug.LogError("[Espectros] GameManager: faltan rondas o spawner");
                return;
            }
            RoundIndex = Mathf.Clamp(index, 0, rounds.Count - 1);
            spawner.Clear();
            if (SurfaceLiquid.Instance != null) SurfaceLiquid.Instance.ClearAll();
            SetState(GameState.RoundIntro);
        }

        // ---------- Bucle ----------

        void Update()
        {
            stateTimer += Time.deltaTime;
            switch (State)
            {
                case GameState.RoundIntro:
                    if (CurrentRound == null || spawner == null) break;
                    if (stateTimer >= settings.roundIntroTime)
                    {
                        TimeLeft = CurrentRound.duration * settings.RoundTimeMultiplier;
                        spawner.StartRound(CurrentRound);
                        SetState(GameState.Playing);
                    }
                    break;

                case GameState.Playing:
                    TimeLeft -= Time.deltaTime;
                    if (CheckRoundWon()) break;
                    if (TimeLeft <= 0f)
                    {
                        TimeLeft = 0f;
                        SetState(GameState.Enraged); // +50 % de velocidad y sin esconderse
                    }
                    break;

                case GameState.Enraged:
                    CheckRoundWon();
                    break;

                case GameState.RoundWon:
                    if (stateTimer >= settings.roundWonTime)
                    {
                        if (RoundIndex + 1 < rounds.Count) StartRound(RoundIndex + 1);
                        else SetState(GameState.Victory);
                    }
                    break;
            }
        }

        bool CheckRoundWon()
        {
            if (spawner.RemainingCount > 0) return false;
            SetState(GameState.RoundWon);
            return true;
        }

        void OnPlayerDied()
        {
            spawner.Clear();
            SetState(GameState.GameOver);
        }

        void SetState(GameState s)
        {
            State = s;
            stateTimer = 0f;
            Debug.Log($"[Espectros] Juego: {s} (ronda {RoundIndex + 1}/{rounds.Count})");
            StateChanged?.Invoke(s);
        }
    }
}
