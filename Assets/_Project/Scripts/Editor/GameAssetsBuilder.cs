using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Espectros.EditorTools
{
    // Crea (si no existen) los assets de la fase 2: materiales, tipo de fantasma, prefab del fantasma y rondas.
    // Si un asset ya existe NO se sobrescribe, para conservar los ajustes hechos a mano.
    public static class GameAssetsBuilder
    {
        public const string Root = "Assets/_Project";
        const string Materials = Root + "/Materials";
        const string Ghosts = Root + "/Ghosts";
        const string Prefabs = Root + "/Prefabs";
        const string Rounds = Root + "/Rounds";

        public class Assets
        {
            public Material debugSpot, liquid, screenFlash, occluder, meshDebug, roomWall, roomFurniture;
            public GhostType whiteGhost;
            public GhostController ghostPrefab;
            public List<RoundConfig> rounds = new List<RoundConfig>();
        }

        public static Assets EnsureAll()
        {
            foreach (var d in new[] { Materials, Ghosts, Prefabs, Rounds }) EnsureFolder(d);

            var a = new Assets
            {
                debugSpot = EnsureMaterial("M_DebugSpot", "Custom/UnlitColor", m => m.SetColor("_Color", Color.green)),
                liquid = EnsureMaterial("M_Liquid", "Custom/SurfaceLiquid", null),
                screenFlash = EnsureMaterial("M_ScreenFlash", "Custom/ScreenTint", m => m.SetColor("_Color", new Color(1f, 0f, 0f, 0.5f))),
                occluder = EnsureMaterial("M_Occluder", "Custom/DepthOnly", null),
                roomWall = EnsureMaterial("M_TestRoom_Wall", "Universal Render Pipeline/Lit", m => m.SetColor("_BaseColor", new Color(0.72f, 0.72f, 0.7f))),
                roomFurniture = EnsureMaterial("M_TestRoom_Furniture", "Universal Render Pipeline/Lit", m => m.SetColor("_BaseColor", new Color(0.55f, 0.38f, 0.25f))),
            };
            a.meshDebug = FindAsset<Material>("M_MeshDebug t:Material");

            a.whiteGhost = EnsureAsset<GhostType>($"{Ghosts}/GT_White.asset", t =>
            {
                t.displayName = "Blanco";
                t.color = new Color(0.85f, 0.95f, 1f, 1f);
                t.speed = 0.6f;
                t.health = 1.5f;
                t.weakness = GhostWeakness.Flashlight;
                t.size = 0.45f;
            });

            a.ghostPrefab = EnsureGhostPrefab();

            // 5 rondas solo con fantasmas blancos (los demás tipos llegan en las fases 3 y extras)
            int[] counts = { 2, 3, 4, 5, 6 };
            int[] maxAlive = { 2, 2, 3, 3, 4 };
            float[] durations = { 90f, 90f, 100f, 110f, 120f };
            float[] speeds = { 1f, 1f, 1.1f, 1.2f, 1.3f };
            for (int i = 0; i < counts.Length; i++)
            {
                int k = i;
                a.rounds.Add(EnsureAsset<RoundConfig>($"{Rounds}/Round_{i + 1}.asset", r =>
                {
                    r.ghosts.Add(new RoundConfig.Entry { type = a.whiteGhost, count = counts[k] });
                    r.maxAlive = maxAlive[k];
                    r.duration = durations[k];
                    r.ghostSpeedMultiplier = speeds[k];
                    r.spawnInterval = 4f;
                }));
            }

            AssetDatabase.SaveAssets();

            int okRounds = a.rounds.FindAll(r => r != null && AssetDatabase.Contains(r)).Count;
            string msg = $"[Espectros] Assets: tipo de fantasma {Ok(a.whiteGhost)}, prefab del fantasma {Ok(a.ghostPrefab)}, " +
                         $"rondas {okRounds}/{a.rounds.Count}, materiales {Ok(a.liquid)}/{Ok(a.debugSpot)}/{Ok(a.screenFlash)}/{Ok(a.occluder)}";
            if (okRounds < a.rounds.Count || !AssetDatabase.Contains(a.whiteGhost) || a.ghostPrefab == null) Debug.LogError(msg);
            else Debug.Log(msg);
            return a;
        }

        static string Ok(Object o) => o != null && AssetDatabase.Contains(o) ? "OK" : "FALTA";

        // Crea la carpeta (y las de encima) a través de Unity, para que el AssetDatabase la conozca
        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        // ---------- Prefab del fantasma ----------

        static GhostController EnsureGhostPrefab()
        {
            string path = $"{Prefabs}/Ghost.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GhostController>(path);
            if (existing != null) return existing;

            int ghostLayer = LayerMask.NameToLayer("Ghost");
            var root = new GameObject("Ghost");
            var sc = root.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 0.18f;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 0.5f;
            audio.maxDistance = 8f;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            // Modelo del asset GhostCharacter_Free (sin su script de demo, que usa el Input antiguo)
            GameObject model;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GhostCharacter_Free/Prefabs/Ghost.prefab");
            if (src != null)
            {
                model = (GameObject)PrefabUtility.InstantiatePrefab(src);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
                foreach (var cc in model.GetComponentsInChildren<CharacterController>(true)) Object.DestroyImmediate(cc);
                foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            }
            else
            {
                Debug.LogWarning("[Espectros] No encuentro GhostCharacter_Free: el fantasma será una esfera");
                model = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(model.GetComponent<Collider>());
            }
            model.name = "Model";
            model.transform.SetParent(visual.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // Escalar a 45 cm de alto y centrar en el pivote
            var b = RendererBounds(model);
            if (b.size.y > 0.0001f) model.transform.localScale *= 0.45f / b.size.y;
            b = RendererBounds(model);
            model.transform.position -= b.center - root.transform.position;

            var animator = model.GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;

            SetLayerRecursively(root, ghostLayer);

            var gc = root.AddComponent<GhostController>();
            var so = new SerializedObject(gc);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<GhostController>();
        }

        // ---------- Utilidades ----------

        static Material EnsureMaterial(string name, string shaderName, System.Action<Material> init)
        {
            string path = $"{Materials}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[Espectros] No encuentro el shader {shaderName}");
                return null;
            }
            m = new Material(shader);
            init?.Invoke(m);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static T EnsureAsset<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            init(a);
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        public static T FindAsset<T>(string filter) where T : Object
        {
            var guids = AssetDatabase.FindAssets(filter, new[] { "Assets" });
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        public static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursively(t.gameObject, layer);
        }
    }
}
