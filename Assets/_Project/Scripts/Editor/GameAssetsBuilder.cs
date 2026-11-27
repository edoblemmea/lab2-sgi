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
            public Material ghostTint, ghostSilhouette, sonarWave, ball, star, paint, lightBeam, ghostPaint;
            public PhysicsMaterial ballPhysics;
            public GhostType whiteGhost, mischiefGhost, invisibleGhost;
            public GhostController ghostPrefab;
            public Ball ballPrefab;
            public Star starPrefab;
            public PaintBlob paintPrefab;
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
            a.ghostTint = EnsureMaterial("M_GhostTint", "Custom/GhostOverlay", m =>
            {
                m.SetFloat("_Intensity", 0.6f); m.SetFloat("_Base", 0.15f); m.SetFloat("_ZTest", 4f); // LessEqual
            });
            a.ghostSilhouette = EnsureMaterial("M_GhostSilhouette", "Custom/GhostOverlay", m =>
            {
                m.SetFloat("_Intensity", 1.3f); m.SetFloat("_Base", 0.45f); m.SetFloat("_ZTest", 8f); // Always: a través de los muebles
                m.renderQueue = 3100;
            });
            a.sonarWave = EnsureMaterial("M_SonarWave", "Custom/SonarWave", null);
            a.ball = EnsureMaterial("M_Ball", "Universal Render Pipeline/Lit", m =>
            {
                m.SetColor("_BaseColor", new Color(1f, 0.45f, 0.15f)); m.SetFloat("_Smoothness", 0.7f);
            });
            a.star = EnsureMaterial("M_Star", "Custom/UnlitColor", m => m.SetColor("_Color", new Color(1f, 0.85f, 0.2f)));
            a.ballPhysics = EnsurePhysicsMaterial();
            a.paint = EnsureMaterial("M_Paint", "Custom/UnlitColor", m => m.SetColor("_Color", Color.magenta));
            a.lightBeam = EnsureMaterial("M_LightBeam", "Custom/LightBeam", null);
            a.ghostPaint = EnsureMaterial("M_GhostPaint", "Custom/GhostPaint", null);

            a.whiteGhost = EnsureAsset<GhostType>($"{Ghosts}/GT_White.asset", t =>
            {
                t.displayName = "Blanco";
                t.color = new Color(0.85f, 0.95f, 1f, 1f);
                t.speed = 0.6f;
                t.health = 1.5f;
                t.weakness = GhostWeakness.Flashlight;
                t.size = 0.45f;
                t.hint = "Fantasma blanco: ilumínalo con la linterna (palma derecha hacia delante)";
            });
            a.mischiefGhost = EnsureAsset<GhostType>($"{Ghosts}/GT_Mischief.asset", t =>
            {
                t.displayName = "Travieso";
                t.color = new Color(1f, 0.6f, 0.15f, 1f);
                t.speed = 0.85f;
                t.health = 1f;
                t.weakness = GhostWeakness.Ball;
                t.size = 0.38f;
                t.hint = "¡Fantasma travieso! La luz solo lo espanta: dale con la pelota (puño y lanzar). " +
                         "Si rebota antes en la habitación, ¡carambola!";
            });

            if (string.IsNullOrEmpty(a.whiteGhost.hint))
            {
                a.whiteGhost.hint = "Fantasma blanco: ilumínalo con la linterna (palma derecha hacia delante)";
                EditorUtility.SetDirty(a.whiteGhost);
            }

            a.invisibleGhost = EnsureAsset<GhostType>($"{Ghosts}/GT_Invisible.asset", t =>
            {
                t.displayName = "Invisible";
                t.color = new Color(0.75f, 0.4f, 1f, 1f);
                t.speed = 0.55f;
                t.health = 1.5f;
                t.weakness = GhostWeakness.PaintThenFlashlight;
                t.size = 0.45f;
                t.hint = "¡Fantasma invisible! Escucha de dónde viene su lamento, lánzale pintura (pellizco y soltar) " +
                         "y después ilumínalo con la linterna";
            });

            a.ghostPrefab = EnsureGhostPrefab();
            UpdateGhostPrefab(a.ghostPrefab, a.ghostTint, a.ghostPaint);
            a.ballPrefab = EnsureBallPrefab(a.ball, a.ballPhysics);
            a.starPrefab = EnsureStarPrefab(a.star);
            a.paintPrefab = EnsurePaintPrefab(a.paint);

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

            // Fase 3: fantasmas traviesos a partir de la ronda 2 y estrellas en todas (también en rondas ya creadas)
            int[] mischief = { 0, 1, 2, 3, 3 };
            int[] invisible = { 0, 0, 0, 2, 2 }; // extra: fantasma invisible en las rondas 4 y 5
            for (int i = 0; i < a.rounds.Count; i++)
            {
                var r = a.rounds[i];
                if (r == null) continue;
                bool changed = false;
                if (mischief[i] > 0 && !r.ghosts.Exists(e => e.type == a.mischiefGhost))
                {
                    r.ghosts.Add(new RoundConfig.Entry { type = a.mischiefGhost, count = mischief[i] });
                    changed = true;
                }
                if (invisible[i] > 0 && !r.ghosts.Exists(e => e.type == a.invisibleGhost))
                {
                    r.ghosts.Add(new RoundConfig.Entry { type = a.invisibleGhost, count = invisible[i] });
                    changed = true;
                }
                if (r.stars == 0) { r.stars = 3; changed = true; }

                // Rondas 1-3 más tranquilas (solo si siguen con el valor antiguo de 4 s)
                float[] calmIntervals = { 8f, 7f, 6f };
                if (i < calmIntervals.Length && Mathf.Approximately(r.spawnInterval, 4f))
                {
                    r.spawnInterval = calmIntervals[i];
                    changed = true;
                }
                // Ronda 4: los invisibles salen cuando ya no queda ningún otro
                if (i == 3)
                {
                    var inv = r.ghosts.Find(e => e.type == a.invisibleGhost);
                    if (inv != null && !inv.spawnAfterOthers) { inv.spawnAfterOthers = true; changed = true; }
                }
                if (changed) EditorUtility.SetDirty(r);
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

        // ---------- Fase 3: pelota, estrella y capa de color del fantasma ----------

        static PhysicsMaterial EnsurePhysicsMaterial()
        {
            string path = $"{Materials}/PM_Ball.asset";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm != null) return pm;
            pm = new PhysicsMaterial("PM_Ball")
            {
                bounciness = 0.8f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                dynamicFriction = 0.3f,
                staticFriction = 0.3f,
                frictionCombine = PhysicsMaterialCombine.Average,
            };
            AssetDatabase.CreateAsset(pm, path);
            return pm;
        }

        // Añade la capa de color del tipo al prefab del fantasma (si no la tiene)
        static void UpdateGhostPrefab(GhostController prefab, Material tint, Material paint)
        {
            if (prefab == null) return;
            var so = new SerializedObject(prefab);
            bool changed = false;
            foreach (var (field, mat) in new[] { ("tintMaterial", tint), ("paintMaterial", paint) })
            {
                var p = so.FindProperty(field);
                if (p == null || mat == null || p.objectReferenceValue == mat) continue;
                p.objectReferenceValue = mat;
                changed = true;
            }
            if (!changed) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
        }

        static Ball EnsureBallPrefab(Material mat, PhysicsMaterial pm)
        {
            string path = $"{Prefabs}/Ball.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<Ball>(path);
            if (existing != null) return existing;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ball";
            go.layer = LayerMask.NameToLayer("Projectile");
            go.transform.localScale = Vector3.one * 0.08f; // 8 cm de diámetro
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            var col = go.GetComponent<SphereCollider>();
            col.sharedMaterial = pm;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.2f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<Ball>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Ball>();
        }

        static PaintBlob EnsurePaintPrefab(Material mat)
        {
            string path = $"{Prefabs}/PaintBlob.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<PaintBlob>(path);
            if (existing != null) return existing;

            var root = new GameObject("PaintBlob") { layer = LayerMask.NameToLayer("Projectile") };
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Visual";
            Object.DestroyImmediate(sphere.GetComponent<Collider>()); // se mueve con rayos, sin física
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localScale = Vector3.one * 0.05f;
            var mr = sphere.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var blob = root.AddComponent<PaintBlob>();
            var so = new SerializedObject(blob);
            so.FindProperty("visual").objectReferenceValue = mr;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<PaintBlob>();
        }

        static Star EnsureStarPrefab(Material mat)
        {
            string path = $"{Prefabs}/Star.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<Star>(path);
            if (existing != null) return existing;

            string meshPath = $"{Prefabs}/StarMesh.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = BuildStarMesh();
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            var root = new GameObject("Star") { layer = LayerMask.NameToLayer("Pickup") };
            var visual = new GameObject("Visual") { layer = root.layer };
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.1f; // 10 cm
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = visual.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var star = root.AddComponent<Star>();
            var so = new SerializedObject(star);
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<Star>();
        }

        // Estrella de 5 puntas con grosor (1 unidad de alto)
        static Mesh BuildStarMesh()
        {
            var v = new System.Collections.Generic.List<Vector3>();
            var t = new System.Collections.Generic.List<int>();
            const int points = 5;
            float outer = 0.5f, inner = 0.22f, depth = 0.12f;
            var ring = new Vector3[points * 2];
            for (int i = 0; i < points * 2; i++)
            {
                float ang = Mathf.PI * 0.5f + i * Mathf.PI / points;
                float r = i % 2 == 0 ? outer : inner;
                ring[i] = new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
            }
            // Caras delantera y trasera (abanico desde el centro, con el centro adelantado para darle volumen)
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -depth : depth;
                int c = v.Count;
                v.Add(new Vector3(0f, 0f, z));
                for (int i = 0; i < ring.Length; i++) v.Add(ring[i]);
                for (int i = 0; i < ring.Length; i++)
                {
                    int a = c + 1 + i, b = c + 1 + (i + 1) % ring.Length;
                    if (side == 0) { t.Add(c); t.Add(b); t.Add(a); }
                    else { t.Add(c); t.Add(a); t.Add(b); }
                }
            }
            var m = new Mesh { name = "StarMesh" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

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
            if (File.Exists(path))
            {
                // Existe pero no se carga como T (por ejemplo, con el script perdido): se rehace
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                Debug.LogWarning($"[Espectros] {path} no se puede cargar como {typeof(T).Name} " +
                                 $"(se ha cargado como {(main != null ? main.GetType().Name : "nada")}). Se vuelve a crear.");
                AssetDatabase.DeleteAsset(path);
            }
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
