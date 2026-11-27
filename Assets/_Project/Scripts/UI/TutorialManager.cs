using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Espectros
{
    // Tutorial de los gestos antes de la primera ronda, y avisos la primera vez que aparece cada tipo de fantasma.
    // Cada paso se completa al hacer el gesto (o con su tecla en el editor) o, si no, al pasar el tiempo máximo.
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [SerializeField] TMP_Text tutorialText;

        public bool Done { get; private set; }

        class Step
        {
            public string text;
            public Func<bool> autoComplete; // condición que se comprueba cada frame (opcional)
        }

        readonly List<Step> steps = new List<Step>();
        readonly HashSet<GhostType> hintedTypes = new HashSet<GhostType>();
        int current = -1;
        float stepStart, hintUntil;
        bool stepDone;
        Action onFinished;
        GameSettings settings;
        Flashlight flashlight;
        float flashlightOnTime;

        void Awake() => Instance = this;

        void Start()
        {
            settings = GameSettings.Instance;
            flashlight = FindAnyObjectByType<Flashlight>();
            if (tutorialText != null && current < 0) tutorialText.text = "";

            if (BallThrower.Instance != null) BallThrower.Instance.Thrown += () => CompleteIf(1);
            if (Sonar.Instance != null) Sonar.Instance.Fired += () => CompleteIf(2);
            var menu = FindAnyObjectByType<WristMenu>();
            if (menu != null) menu.Opened += () => CompleteIf(3);
            if (GhostSpawner.Instance != null) GhostSpawner.Instance.GhostSpawned += OnGhostSpawned;
        }

        public void Begin(Action finished)
        {
            onFinished = finished;
            steps.Clear();
            steps.Add(new Step
            {
                text = "LINTERNA\nAbre la mano derecha con la palma hacia delante\n<size=70%>(en el editor: mantén F)</size>",
                autoComplete = () => flashlightOnTime > 1f,
            });
            steps.Add(new Step { text = "PELOTA\nCierra el puño y lánzala abriendo la mano\n<size=70%>(en el editor: B)</size>" });
            steps.Add(new Step { text = "SONAR\nDa una palmada para ver a los fantasmas a través de los muebles\n<size=70%>(en el editor: C)</size>" });
            steps.Add(new Step { text = "MENÚ\nGira la mano izquierda y mírate la muñeca\n<size=70%>(en el editor: M)</size>" });
            current = -1;
            NextStep();
        }

        void CompleteIf(int stepIndex)
        {
            if (current == stepIndex) stepDone = true;
        }

        void NextStep()
        {
            current++;
            stepDone = false;
            stepStart = Time.time;
            flashlightOnTime = 0f;
            if (current >= steps.Count)
            {
                Done = true;
                current = -1;
                Show("<size=70%><color=#b2392f>INSTRUCCIONES COMPLETADAS</color></size>\nElimina a todos los fantasmas", 3f);
                Debug.Log("[Espectros] Tutorial terminado");
                onFinished?.Invoke();
                return;
            }
            Show($"<size=70%><color=#b2392f>INSTRUCCIÓN {current + 1}/{steps.Count}</color></size>\n{steps[current].text}", float.MaxValue);
        }

        void Update()
        {
            if (current >= 0)
            {
                if (flashlight != null && flashlight.IsOn) flashlightOnTime += Time.deltaTime;
                var step = steps[current];
                if (step.autoComplete != null && step.autoComplete()) stepDone = true;
                // Pequeña pausa al completar para que se vea el cambio
                if (stepDone || Time.time - stepStart > settings.tutorialStepTimeout) NextStep();
            }
            else if (tutorialText != null && Time.time > hintUntil && tutorialText.text != "")
            {
                tutorialText.text = "";
            }
        }

        void OnGhostSpawned(GhostController g)
        {
            if (g.Type == null || string.IsNullOrEmpty(g.Type.hint) || !hintedTypes.Add(g.Type)) return;
            if (current >= 0) return; // no tapar el tutorial
            Show(g.Type.hint, 5f);
        }

        void Show(string text, float seconds)
        {
            if (tutorialText != null) tutorialText.text = text;
            hintUntil = seconds == float.MaxValue ? float.MaxValue : Time.time + seconds;
        }

        // Para el menú: volver a ver el tutorial en la próxima partida
        public void ResetTutorial() => Done = false;
    }
}
