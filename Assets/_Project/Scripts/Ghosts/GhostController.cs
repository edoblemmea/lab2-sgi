using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Espectros
{
    public enum GhostState { Entering, SeekingHide, Hidden, Peeking, Approaching, Scaring, Fleeing, Dying }

    // Fantasma: máquina de estados con enum + switch.
    // Vuela en línea recta y atraviesa paredes y muebles (sin NavMesh). El pivote es el centro del fantasma.
    [RequireComponent(typeof(SphereCollider))]
    public class GhostController : MonoBehaviour
    {
        static readonly List<GhostController> all = new List<GhostController>();
        public static IReadOnlyList<GhostController> All => all;

        [SerializeField] Animator animator;
        [Tooltip("Hijo con el modelo: se balancea arriba y abajo")]
        [SerializeField] Transform visual;
        [SerializeField] AudioSource audioSource;
        [Tooltip("Texto de depuración (si está vacío se crea solo)")]
        [SerializeField] TMP_Text debugLabel;
        [Tooltip("Capa de color del tipo de fantasma (Custom/GhostOverlay)")]
        [SerializeField] Material tintMaterial;

        public GhostType Type { get; private set; }
        public GhostState State { get; private set; } = GhostState.Entering;
        public float Health { get; private set; }
        public Vector3 Center => transform.position;
        public bool IsAlive => State != GhostState.Dying;
        public bool IsRevealed => Time.time < revealedUntil;

        public event Action<GhostController> Died;

        GameSettings settings;
        HidingSpotFinder finder;
        SphereCollider col;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        HidingSpot spot;
        Vector3 exitPoint;
        float speedMultiplier = 1f;
        float stateTime, hiddenDuration, seenTimer, stunTimer, flashTimer, revealedUntil = -1f;
        bool litThisFrame;
        Material[][] baseMaterials;
        Material silhouetteMaterial;
        float silhouetteUntil = -1f;
        bool silhouetteShown;
        AudioSource ambient;
        float lastLitTime = -10f;

        // Paralizado por la luz: no se mueve ni puede huir mientras lo iluminan y un rato después
        bool StunnedByLight => Time.time - lastLitTime < settings.lightStunTime;
        Vector3 visualBasePos;
        float bobPhase;

        // ---------- Inicio ----------

        void Awake()
        {
            col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            renderers = GetComponentsInChildren<Renderer>();
            baseMaterials = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++) baseMaterials[i] = renderers[i].sharedMaterials;
            mpb = new MaterialPropertyBlock();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (visual != null) visualBasePos = visual.localPosition;
            bobPhase = UnityEngine.Random.value * 10f;
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public void Init(GhostType type, Vector3 entryPoint, Vector3 exit, float speedMult)
        {
            settings = GameSettings.Instance;
            finder = HidingSpotFinder.Instance;
            Type = type;
            Health = type.health;
            speedMultiplier = speedMult;
            transform.position = entryPoint;
            exitPoint = exit;
            col.radius = type.size * 0.4f;
            if (debugLabel == null) CreateDebugLabel();
            RefreshMaterials();
            ApplyColor(0f);
            StartAmbient();
            Play(type.appearSound, SfxLibrary.GhostAppear);
            SetState(GhostState.Entering);
        }

        // ---------- Bucle ----------

        void Update()
        {
            if (Type == null || State == GhostState.Dying) return;

            stateTime += Time.deltaTime;
            if (stunTimer > 0f) stunTimer -= Time.deltaTime;
            bool enraged = GameManager.Instance != null && GameManager.Instance.IsEnraged;

            switch (State)
            {
                case GhostState.Entering:
                    if (MoveTo(exitPoint, Speed * 0.7f)) SetState(GhostState.SeekingHide);
                    break;

                case GhostState.SeekingHide:
                    if (enraged || finder == null) { SetState(GhostState.Approaching); break; }
                    if (spot == null) spot = finder.Reserve(this, transform.position);
                    if (MoveTo(spot.position, Speed)) SetState(GhostState.Hidden);
                    break;

                case GhostState.Hidden:
                    if (enraged) { SetState(GhostState.Approaching); break; }
                    // Si la jugadora se ha movido y ahora lo ve, se va a otro sitio
                    if (CountSeen(IsSeenByPlayer()) >= settings.seenToFlee) { SetState(GhostState.Fleeing); break; }
                    if (stateTime >= hiddenDuration) SetState(GhostState.Peeking);
                    break;

                case GhostState.Peeking:
                    MoveTo(spot != null ? spot.peekPosition : transform.position, Speed * 0.5f);
                    if (CountSeen(IsSeenByPlayer()) >= settings.seenToFlee)
                    {
                        PlayAnim("surprised");
                        SetState(GhostState.Fleeing);
                    }
                    else if (stateTime >= settings.peekDuration + 1f)
                    {
                        SetState(GhostState.Approaching); // nadie lo ha visto: a por ella
                    }
                    break;

                case GhostState.Approaching:
                    bool seen = !enraged && IsSeenByPlayer();
                    // Cuando lo miras se queda quieto; si lo miras un rato, huye
                    if (CountSeen(seen) >= settings.seenToFlee * 3f) { SetState(GhostState.Fleeing); break; }
                    if (!seen)
                    {
                        Vector3 target = PlayerHead.Position - Vector3.up * 0.15f;
                        MoveTo(target, Speed * (enraged ? 1f : settings.approachSpeedFactor));
                        if ((PlayerHead.Position - transform.position).magnitude < settings.scareDistance)
                            SetState(GhostState.Scaring);
                    }
                    break;

                case GhostState.Scaring:
                    if (stateTime >= 0.8f) SetState(GhostState.Fleeing);
                    break;

                case GhostState.Fleeing:
                    if (spot == null) SetState(GhostState.SeekingHide);
                    else if (MoveTo(spot.position, Speed * settings.fleeSpeedFactor)) SetState(GhostState.Hidden);
                    break;
            }

            UpdateVisuals();
            litThisFrame = false;
        }

        float Speed
        {
            get
            {
                float s = Type.speed * speedMultiplier * settings.GhostSpeedMultiplier;
                if (GameManager.Instance != null && GameManager.Instance.IsEnraged) s *= settings.enragedSpeedFactor;
                return s;
            }
        }

        // Vuela en línea recta hacia el destino. Devuelve true al llegar.
        bool MoveTo(Vector3 target, float speed)
        {
            if (stunTimer > 0f || StunnedByLight) return false; // la luz lo deja paralizado
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
            return (transform.position - target).sqrMagnitude < 0.0025f;
        }

        float CountSeen(bool seen)
        {
            if (StunnedByLight) return 0f; // iluminado no puede escapar
            seenTimer = seen ? seenTimer + Time.deltaTime : Mathf.Max(0f, seenTimer - Time.deltaTime);
            return seenTimer;
        }

        void SetState(GhostState s)
        {
            State = s;
            stateTime = 0f;
            seenTimer = 0f;

            switch (s)
            {
                case GhostState.Entering:
                case GhostState.SeekingHide:
                case GhostState.Approaching:
                    PlayAnim("move");
                    break;

                case GhostState.Hidden:
                    hiddenDuration = UnityEngine.Random.Range(settings.hiddenTime.x, settings.hiddenTime.y) * settings.HideTimeMultiplier;
                    PlayAnim("idle");
                    break;

                case GhostState.Peeking:
                    PlayAnim("idle");
                    break;

                case GhostState.Scaring:
                    PlayAnim("attack");
                    Play(Type.scareSound, SfxLibrary.GhostScare);
                    if (PlayerHealth.Instance != null) PlayerHealth.Instance.Scare();
                    break;

                case GhostState.Fleeing:
                    PlayAnim("move");
                    if (finder != null)
                    {
                        finder.Release(spot, this);
                        float minDist = settings.fleeMinDistance;
                        spot = finder.Reserve(this, transform.position,
                            sp => (sp.position - PlayerHead.Position).magnitude >= minDist);
                    }
                    else spot = null;
                    break;
            }
        }

        // ---------- Visibilidad ----------

        // Está en el campo de visión y no lo tapa la mesh ni un mueble (comprueba el centro y la parte de arriba)
        public bool IsSeenByPlayer()
        {
            var cam = PlayerHead.Camera;
            if (cam == null) return false;
            if (Type != null && Type.weakness == GhostWeakness.PaintThenFlashlight && !IsRevealed) return false;

            Vector3 vp = cam.WorldToViewportPoint(Center);
            if (vp.z <= 0f || vp.x < -0.05f || vp.x > 1.05f || vp.y < -0.05f || vp.y > 1.05f) return false;

            Vector3 head = cam.transform.position;
            int mask = Layers.OccluderMask;
            float up = Type != null ? Type.size * 0.35f : 0.15f;
            return !Physics.Linecast(head, Center, mask, QueryTriggerInteraction.Ignore)
                || !Physics.Linecast(head, Center + Vector3.up * up, mask, QueryTriggerInteraction.Ignore);
        }

        // ---------- Contrato con las herramientas ----------

        public void ApplyLight(float amount)
        {
            if (!IsAlive) return;
            litThisFrame = true;
            bool vulnerable = Type.weakness == GhostWeakness.Flashlight
                              || (Type.weakness == GhostWeakness.PaintThenFlashlight && IsRevealed);
            if (!vulnerable)
            {
                // No le hace daño, pero lo espanta
                if (State == GhostState.Approaching || State == GhostState.Peeking) SetState(GhostState.Fleeing);
                return;
            }

            if (!StunnedByLight) PlayAnim("surprised");
            lastLitTime = Time.time;
            flashTimer = 0.1f;
            Damage(amount);
        }

        public void HitByBall(int bounces)
        {
            if (!IsAlive) return;
            if (Type.weakness == GhostWeakness.Ball)
            {
                Debug.Log($"[Espectros] Pelota: fantasma {Type.displayName} eliminado con {bounces} rebotes");
                Damage(Health);
            }
            else stunTimer = 1f;
        }

        public void HitByWater()
        {
            if (!IsAlive) return;
            if (Type.weakness == GhostWeakness.Water) Damage(Health);
            else stunTimer = 0.5f;
        }

        public void Reveal(float seconds) => revealedUntil = Mathf.Max(revealedUntil, Time.time + seconds);

        void Damage(float amount)
        {
            Health -= amount;
            if (Health <= 0f) Die();
        }

        // ---------- Muerte: se encoge, cae como una gota y deja un charco que se adapta a la mesh ----------

        public void Die()
        {
            if (!IsAlive) return;
            State = GhostState.Dying;
            if (finder != null) finder.Release(spot, this);
            col.enabled = false;
            Play(Type.dieSound, SfxLibrary.GhostDie);
            if (ambient != null) ambient.Stop();
            PlayAnim("dissolve");
            Debug.Log($"[Espectros] Fantasma {Type.displayName} eliminado");
            StartCoroutine(DieRoutine());
        }

        IEnumerator DieRoutine()
        {
            Vector3 startScale = transform.localScale;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.2f, t / 0.4f);
                ApplyColor(1f - t / 0.4f);
                yield return null;
            }

            // Cae como una gota hasta tocar la mesh
            float v = 0f, fallen = 0f;
            int mask = Layers.SceneMeshMask;
            RaycastHit hit = default;
            bool landed = false;
            while (fallen < 4f)
            {
                v += 9.81f * Time.deltaTime;
                float step = v * Time.deltaTime;
                if (Physics.Raycast(transform.position, Vector3.down, out hit, step + 0.02f, mask, QueryTriggerInteraction.Ignore))
                {
                    landed = true;
                    break;
                }
                transform.position += Vector3.down * step;
                fallen += step;
                yield return null;
            }

            if (landed && SurfaceLiquid.Instance != null)
                SurfaceLiquid.Instance.Spawn(hit.point, hit.normal, Type.color, settings.puddleRadius);

            Died?.Invoke(this);
            Destroy(gameObject);
        }

        // ---------- Aspecto ----------

        void UpdateVisuals()
        {
            // Siempre mira a la jugadora
            Vector3 toHead = PlayerHead.Position - transform.position;
            toHead.y = 0f;
            if (toHead.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toHead), 5f * Time.deltaTime);

            if (visual != null)
                visual.localPosition = visualBasePos + Vector3.up * Mathf.Sin((Time.time + bobPhase) * 2f) * 0.03f;

            // El invisible solo se ve si está revelado (o en modo depuración)
            bool silhouette = Time.time < silhouetteUntil;
            if (silhouette != silhouetteShown) RefreshMaterials();
            bool visible = Type.weakness != GhostWeakness.PaintThenFlashlight || IsRevealed || settings.debugMode || silhouette;
            foreach (var r in renderers) if (r != null) r.enabled = visible;

            if (flashTimer > 0f) flashTimer -= Time.deltaTime;
            ApplyColor(flashTimer > 0f || litThisFrame ? 0.6f : 0f);

            if (debugLabel != null)
            {
                debugLabel.gameObject.SetActive(settings.debugMode);
                if (settings.debugMode) debugLabel.text = $"{State}\n{Health:0.0}";
            }
        }

        void ApplyColor(float whiteFlash)
        {
            if (Type == null) return;
            Color c = Color.Lerp(Type.color, Color.white, whiteFlash);
            mpb.SetColor(ColorId, c);
            mpb.SetColor(BaseColorId, c);
            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(mpb);
        }

        void PlayAnim(string state)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            int hash = Animator.StringToHash(state);
            if (animator.HasState(0, hash)) animator.CrossFade(hash, 0.1f, 0);
        }

        void Play(AudioClip clip, AudioClip fallback = null)
        {
            if (clip == null) clip = fallback;
            if (clip == null) return;
            float vol = settings != null ? settings.effectsVolume : 1f;
            if (audioSource != null) audioSource.PlayOneShot(clip, vol);
            else if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(clip, transform.position);
        }

        // Lamento en bucle con sonido espacial: se oye de dónde viene aunque esté escondido detrás de un mueble
        void StartAmbient()
        {
            if (ambient == null)
            {
                ambient = AudioManager.Make3D(gameObject.AddComponent<AudioSource>());
                ambient.loop = true;
                ambient.maxDistance = 6f;
            }
            ambient.clip = SfxLibrary.GhostMoan;
            ambient.volume = settings.ghostAmbientVolume;
            // Cada tipo con un tono distinto (el travieso, más agudo)
            ambient.pitch = (Type.weakness == GhostWeakness.Ball ? 1.5f : 1f) * UnityEngine.Random.Range(0.9f, 1.1f);
            ambient.time = UnityEngine.Random.Range(0f, ambient.clip.length * 0.9f);
            ambient.Play();
        }

        // ---------- Silueta del sonar (se ve a través de los muebles) ----------

        public void ShowSilhouette(float seconds, Material mat)
        {
            if (mat == null) return;
            silhouetteMaterial = mat;
            silhouetteUntil = Time.time + seconds;
            RefreshMaterials();
        }

        // Materiales del modelo + capa de color del tipo + silueta (si el sonar está activo)
        void RefreshMaterials()
        {
            silhouetteShown = Time.time < silhouetteUntil && silhouetteMaterial != null;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                var mats = new List<Material>(baseMaterials[i]);
                if (tintMaterial != null) mats.Add(tintMaterial);
                if (silhouetteShown) mats.Add(silhouetteMaterial);
                r.sharedMaterials = mats.ToArray();
            }
        }

        void CreateDebugLabel()
        {
            var go = new GameObject("DebugLabel");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * (Type.size * 0.7f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.fontSize = 0.6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(0.6f, 0.2f);
            tmp.color = Color.yellow;
            debugLabel = tmp;
            go.AddComponent<FaceCamera>();
            go.SetActive(false);
        }
    }
}
