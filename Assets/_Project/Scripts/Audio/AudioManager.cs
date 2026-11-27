using UnityEngine;

namespace Espectros
{
    // Reproduce efectos en un punto del espacio (Spatial Blend 1) con un pool de AudioSources,
    // o en la cabeza (2D). Si no hay ninguno en la escena se crea solo.
    public class AudioManager : MonoBehaviour
    {
        static AudioManager instance;

        public static AudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<AudioManager>();
                    if (instance == null && Application.isPlaying)
                        instance = new GameObject("AudioManager (auto)").AddComponent<AudioManager>();
                }
                return instance;
            }
        }

        [SerializeField] int poolSize = 12;

        AudioSource[] pool;
        AudioSource source2D;
        int next;

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            pool = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Sfx3D_" + i);
                go.transform.SetParent(transform, false);
                pool[i] = Make3D(go.AddComponent<AudioSource>());
            }
            source2D = gameObject.AddComponent<AudioSource>();
            source2D.playOnAwake = false;
            source2D.spatialBlend = 0f;
        }

        public static AudioSource Make3D(AudioSource s)
        {
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 0.4f;
            s.maxDistance = 8f;
            s.dopplerLevel = 0f;
            return s;
        }

        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || pool == null) return;
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.transform.position = position;
            s.pitch = pitch;
            s.volume = volume * GameSettings.Instance.effectsVolume;
            s.clip = clip;
            s.Play();
        }

        public void Play2D(AudioClip clip, float volume = 1f)
        {
            if (clip == null || source2D == null) return;
            source2D.PlayOneShot(clip, volume * GameSettings.Instance.effectsVolume);
        }
    }
}
