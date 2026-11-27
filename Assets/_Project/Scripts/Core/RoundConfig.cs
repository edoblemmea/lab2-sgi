using System;
using System.Collections.Generic;
using UnityEngine;

namespace Espectros
{
    // Configuración de una ronda: fantasmas por tipo, tiempo y multiplicadores.
    [CreateAssetMenu(fileName = "Round", menuName = "Espectros/Round Config")]
    public class RoundConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public GhostType type;
            public int count = 1;
            [Tooltip("Estos fantasmas solo aparecen cuando ya se ha eliminado a todos los demás de la ronda")]
            public bool spawnAfterOthers = false;
        }

        public List<Entry> ghosts = new List<Entry>();
        [Tooltip("Duración de la ronda antes del modo furia (s)")]
        public float duration = 90f;
        [Tooltip("Segundos entre la aparición de un fantasma y el siguiente")]
        public float spawnInterval = 4f;
        [Tooltip("Fantasmas a la vez como máximo")]
        public int maxAlive = 3;
        public float ghostSpeedMultiplier = 1f;
        [Tooltip("Estrellas que aparecen en la ronda (fase 3)")]
        public int stars = 0;

        public int TotalGhosts
        {
            get { int n = 0; foreach (var e in ghosts) if (e.type != null) n += Mathf.Max(0, e.count); return n; }
        }
    }
}
