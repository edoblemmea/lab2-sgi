using System;
using System.Collections.Generic;
using UnityEngine;

namespace Espectros
{
    // Pone estrellas en escondites ocultos (detrás o debajo de los muebles) durante la ronda.
    // Al recogerla: StarWallet.Add(), recarga la linterna si le falta batería o da pelotas,
    // y cada 5 estrellas da un corazón.
    public class StarSpawner : MonoBehaviour
    {
        public static StarSpawner Instance { get; private set; }

        [SerializeField] Star starPrefab;

        readonly List<Star> active = new List<Star>();
        readonly Queue<Star> pool = new Queue<Star>();
        float nextSpawn;
        GameSettings settings;

        public event Action<string> Rewarded; // texto para el HUD

        void Awake() => Instance = this;

        void Start() => settings = GameSettings.Instance;

        void Update()
        {
            var gm = GameManager.Instance;
            bool playing = gm != null && gm.IsPlaying;
            if (!playing)
            {
                if (gm != null && gm.State != GameState.Tutorial) ClearAll();
                return;
            }
            if (starPrefab == null || HidingSpotFinder.Instance == null) return;
            if (active.Count < settings.starsAtOnce && Time.time >= nextSpawn) SpawnOne();
        }

        void SpawnOne()
        {
            nextSpawn = Time.time + settings.starRespawnTime;
            // Solo detrás o debajo de los muebles (no dentro de las paredes ni en el techo)
            var spot = HidingSpotFinder.Instance.Reserve(this, PlayerHead.Position,
                s => s.kind == SpotKind.BehindFurniture || s.kind == SpotKind.UnderTable);
            if (spot == null || spot.kind == SpotKind.BehindPlayer)
            {
                HidingSpotFinder.Instance.Release(spot, this);
                return;
            }
            var star = pool.Count > 0 ? pool.Dequeue() : Instantiate(starPrefab);
            star.Place(this, spot);
            active.Add(star);
        }

        public void Collect(Star star)
        {
            Remove(star);
            StarWallet.Add();
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(SfxLibrary.Star, star.transform.position);

            string msg;
            var fl = FindAnyObjectByType<Flashlight>();
            var thrower = BallThrower.Instance;
            if (fl != null && fl.BatteryFraction < 0.5f)
            {
                fl.Recharge(settings.starBatteryRecharge);
                msg = "★ Linterna recargada";
            }
            else if (thrower != null)
            {
                thrower.AddBalls(settings.starBalls);
                msg = $"★ +{settings.starBalls} pelotas";
            }
            else msg = "★";

            if (settings.starsPerHeart > 0 && StarWallet.Stars % settings.starsPerHeart == 0 && PlayerHealth.Instance != null)
            {
                PlayerHealth.Instance.AddHeart();
                msg += " · ¡Corazón extra!";
            }
            Debug.Log($"[Espectros] Estrella recogida ({StarWallet.Stars}): {msg}");
            Rewarded?.Invoke(msg);
        }

        void Remove(Star star)
        {
            if (HidingSpotFinder.Instance != null) HidingSpotFinder.Instance.Release(star.Spot, this);
            active.Remove(star);
            star.gameObject.SetActive(false);
            pool.Enqueue(star);
        }

        public void ClearAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) Remove(active[i]);
        }
    }
}
