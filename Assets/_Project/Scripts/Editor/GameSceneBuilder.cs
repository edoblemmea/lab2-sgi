using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace Espectros.EditorTools
{
    // Menú Espectros:
    //  - "Fase 2: configurar juego en la escena abierta": añade a la escena Main (o a la que esté abierta)
    //    todos los sistemas del juego, la linterna, el HUD y el menú de muñeca, ya conectados.
    //  - "Crear escena TestRoom": habitación falsa con mesas, sillas y una tele para probar en el editor.
    // Se pueden volver a ejecutar: reutilizan los objetos que ya existan.
    public static class GameSceneBuilder
    {
        const string TestRoomPath = "Assets/Scenes/TestRoom.unity";
        const string TestRoomMeshes = GameAssetsBuilder.Root + "/TestRoom";

        [MenuItem("Espectros/Configurar juego en la escena abierta")]
        public static void SetupOpenScene()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            if (origin == null)
            {
                EditorUtility.DisplayDialog("Espectros", "No hay ningún XR Origin en la escena abierta. Abre la escena Main.", "Vale");
                return;
            }
            var assets = GameAssetsBuilder.EnsureAll();
            BuildSystems(assets, origin.Camera != null ? origin.Camera : Camera.main, false);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            VerifyReferences();
            Debug.Log("[Espectros] Juego configurado. Guarda la escena (Ctrl+S) y haz Build And Run.");
        }

        // Vuelve a asignar los assets (rondas, fantasma, materiales) a los sistemas de la escena abierta,
        // sin rehacer la habitación ni el resto de objetos
        [MenuItem("Espectros/Reparar referencias del juego en la escena abierta")]
        public static void RepairReferences()
        {
            var origin = Object.FindAnyObjectByType<XROrigin>();
            Camera cam = origin != null && origin.Camera != null ? origin.Camera : Camera.main;
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Espectros", "No encuentro la cámara principal en la escena abierta.", "Vale");
                return;
            }
            var assets = GameAssetsBuilder.EnsureAll();
            BuildSystems(assets, cam, origin == null);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            if (VerifyReferences()) Debug.Log("[Espectros] Referencias reparadas. Guarda la escena (Ctrl+S).");
        }

        // Comprueba que los sistemas tienen asignados sus assets y lo escribe en la consola
        static bool VerifyReferences()
        {
            var problems = new List<string>();
            var gm = Object.FindAnyObjectByType<GameManager>();
            if (gm == null) problems.Add("no hay GameManager");
            else
            {
                var list = new SerializedObject(gm).FindProperty("rounds");
                int empty = 0;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == null) empty++;
                if (list.arraySize == 0 || empty > 0) problems.Add($"rondas vacías: {empty}/{list.arraySize}");
            }
            var spawner = Object.FindAnyObjectByType<GhostSpawner>();
            if (spawner == null) problems.Add("no hay GhostSpawner");
            else
            {
                var so = new SerializedObject(spawner);
                if (so.FindProperty("defaultPrefab").objectReferenceValue == null) problems.Add("falta el prefab del fantasma");
                if (so.FindProperty("defaultType").objectReferenceValue == null) problems.Add("falta el tipo de fantasma");
            }
            if (problems.Count == 0)
            {
                Debug.Log("[Espectros] Referencias de la escena: OK");
                return true;
            }
            Debug.LogError("[Espectros] Referencias de la escena con problemas: " + string.Join(", ", problems));
            return false;
        }

        [MenuItem("Espectros/Crear escena TestRoom")]
        public static void CreateTestRoom()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(TestRoomPath) &&
                !EditorUtility.DisplayDialog("Espectros", "Ya existe TestRoom. ¿Volver a crearla?", "Volver a crear", "Cancelar"))
                return;

            // Primero la escena nueva: al abrirla Unity descarga de memoria los assets que no usa nadie,
            // así que los assets se cargan DESPUÉS para que las referencias no queden vacías
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var assets = GameAssetsBuilder.EnsureAll();

            // Luz y cámara (la "cabeza")
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<EditorPlayerController>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, -1.4f), Quaternion.Euler(15f, 0f, 0f));

            BuildRoom(assets);
            BuildSystems(assets, cam, true);

            GameAssetsBuilder.EnsureFolder(Path.GetDirectoryName(TestRoomPath));
            EditorSceneManager.SaveScene(scene, TestRoomPath);
            VerifyReferences();
            Debug.Log("[Espectros] TestRoom creada. Dale a Play. Controles: WASD/QE mover, botón derecho mirar, F linterna, " +
                      "B pelota, V pintura, C sonar, P charco donde miras, K eliminar fantasma, N nuevo fantasma, 1/2/3 menú, M abrir menú.");
        }

        // ---------- Sistemas del juego ----------

        static void BuildSystems(GameAssetsBuilder.Assets a, Camera cam, bool testRoom)
        {
            var sys = FindOrCreate("GameSystems", null);
            GetOrAdd<GameSettings>(sys);
            if (testRoom) GetOrAdd<SceneReady>(sys); // en el editor lanza OnSceneReady al instante

            var finder = GetOrAdd<HidingSpotFinder>(sys);
            Set(finder, "debugMaterial", a.debugSpot);

            var spawner = GetOrAdd<GhostSpawner>(sys);
            Set(spawner, "defaultPrefab", a.ghostPrefab);
            Set(spawner, "defaultType", a.whiteGhost);

            var health = GetOrAdd<PlayerHealth>(sys);
            Set(health, "screenFlashMaterial", a.screenFlash);

            var gm = GetOrAdd<GameManager>(sys);
            Set(gm, "spawner", spawner);
            Set(gm, "health", health);
            var so = new SerializedObject(gm);
            var list = so.FindProperty("rounds");
            list.ClearArray();
            for (int i = 0; i < a.rounds.Count; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = a.rounds[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            var liquid = GetOrAdd<SurfaceLiquid>(sys);
            Set(liquid, "liquidMaterial", a.liquid);

            var keys = GetOrAdd<DebugKeys>(sys);
            Set(keys, "invisibleType", a.invisibleGhost);
            GetOrAdd<AudioManager>(sys);
            GetOrAdd<ScoreManager>(sys);

            // Fase 3: pelota, estrellas y sonar
            var thrower = GetOrAdd<BallThrower>(sys);
            Set(thrower, "ballPrefab", a.ballPrefab);
            Set(thrower, "ballMaterial", a.ballPhysics);
            Set(thrower, "router", Object.FindAnyObjectByType<HandGestureRouter>());

            var paint = GetOrAdd<PaintThrower>(sys);
            Set(paint, "blobPrefab", a.paintPrefab);
            Set(paint, "router", Object.FindAnyObjectByType<HandGestureRouter>());

            var stars = GetOrAdd<StarSpawner>(sys);
            Set(stars, "starPrefab", a.starPrefab);

            var sonar = GetOrAdd<Sonar>(sys);
            Set(sonar, "router", Object.FindAnyObjectByType<HandGestureRouter>());
            Set(sonar, "waveMaterial", a.sonarWave);
            Set(sonar, "silhouetteMaterial", a.ghostSilhouette);

            var tutorial = GetOrAdd<TutorialManager>(sys);

            if (!testRoom)
            {
                var view = GetOrAdd<SceneMeshDebugView>(sys);
                Set(view, "occluderMaterial", a.occluder);
                Set(view, "debugMaterial", a.meshDebug);

                // Muebles a partir de los bounding boxes
                var boxManager = Object.FindAnyObjectByType<ARBoundingBoxManager>(FindObjectsInactive.Include);
                if (boxManager != null)
                {
                    var bbf = GetOrAdd<BoundingBoxFurniture>(boxManager.gameObject);
                    Set(bbf, "boxManager", boxManager);
                }
                else Debug.LogWarning("[Espectros] No encuentro el AR Bounding Box Manager: no habrá muebles");
            }

            BuildFlashlight(cam);
            var flashlight = Object.FindAnyObjectByType<Flashlight>();
            if (flashlight != null) Set(flashlight, "beamMaterial", a.lightBeam);
            var tutorialText = BuildHud(cam);
            Set(tutorial, "tutorialText", tutorialText);
            BuildWristMenu();
        }

        static void BuildFlashlight(Camera cam)
        {
            var hands = GameObject.Find("Hands");
            var go = FindOrCreate("Flashlight", hands != null ? hands.transform : null);
            var fl = GetOrAdd<Flashlight>(go);

            var lightGo = FindOrCreate("SpotLight", go.transform);
            var light = GetOrAdd<Light>(lightGo);
            light.type = LightType.Spot;
            light.shadows = LightShadows.None;
            light.range = 4f;
            light.spotAngle = 30f;
            light.intensity = 3f;
            light.cullingMask = 1 << LayerMask.NameToLayer("Ghost"); // solo ilumina a los fantasmas
            light.enabled = false;

            Set(fl, "spotLight", light);
            Set(fl, "router", Object.FindAnyObjectByType<HandGestureRouter>());
            Set(fl, "tracker", Object.FindAnyObjectByType<HandTracker>());

            // Modelo de la linterna (Free-FlashLight), unos 18 cm, apuntando hacia +Z
            var modelT = go.transform.Find("Model");
            if (modelT == null)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Free-FlashLight/Prefabs/FlashLight.prefab");
                if (src != null)
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(src, go.transform);
                    model.name = "Model";
                    foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                    foreach (var l in model.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
                    model.transform.localPosition = Vector3.zero;
                    var b = GameAssetsBuilder.RendererBounds(model);
                    Vector3 s = b.size;
                    float longest = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                    if (longest > 0.0001f) model.transform.localScale *= 0.18f / longest;
                    if (s.x >= s.y && s.x >= s.z) model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    else if (s.y >= s.x && s.y >= s.z) model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    model.transform.localPosition = new Vector3(0f, 0f, -0.09f);
                    modelT = model.transform;
                }
            }
            if (modelT != null) Set(fl, "model", modelT.gameObject);
        }

        static TextMeshPro BuildHud(Camera cam)
        {
            if (cam == null) return null;
            var hud = FindOrCreate("HUD", cam.transform);
            var gh = GetOrAdd<GameHud>(hud);

            var status = MakeText("StatusText", hud.transform, new Vector3(0f, 0.2f, 0.8f), 0.32f, new Vector2(1.2f, 0.2f));
            var message = MakeText("MessageText", hud.transform, new Vector3(0f, 0.05f, 0.8f), 0.9f, new Vector2(1.2f, 0.3f));
            message.color = new Color(1f, 0.85f, 0.3f);
            Set(gh, "statusText", status);
            Set(gh, "messageText", message);
            Set(gh, "flashlight", Object.FindAnyObjectByType<Flashlight>());

            var tutorial = MakeText("TutorialText", hud.transform, new Vector3(0f, -0.12f, 0.8f), 0.45f, new Vector2(1.1f, 0.4f));
            tutorial.color = new Color(0.85f, 0.95f, 1f);
            return tutorial;
        }

        static TextMeshPro MakeText(string name, Transform parent, Vector3 localPos, float size, Vector2 rect)
        {
            var go = FindOrCreate(name, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            var t = GetOrAdd<TextMeshPro>(go);
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta = rect;
            t.text = "";
            return t;
        }

        static void BuildWristMenu()
        {
            var root = FindOrCreate("WristMenu", null);
            var wm = GetOrAdd<WristMenu>(root);

            var old = root.transform.Find("Panel");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Canvas));
            panel.transform.SetParent(root.transform, false);
            panel.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var prt = (RectTransform)panel.transform;
            prt.sizeDelta = new Vector2(220f, 170f);
            prt.localScale = Vector3.one * 0.0006f; // 220 px = 13 cm

            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var bg = NewUI("Background", panel.transform, new Vector2(0f, 0f), new Vector2(220f, 170f));
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = sprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.08f, 0.1f, 0.14f, 0.9f);

            var title = NewUI("Title", panel.transform, new Vector2(0f, 62f), new Vector2(200f, 30f)).AddComponent<TextMeshProUGUI>();
            title.text = "Espectros";
            title.fontSize = 20;
            title.alignment = TextAlignmentOptions.Center;

            var restart = MakeButton(panel.transform, "Btn_Restart", new Vector2(0f, 22f), "Reiniciar ronda", sprite, out _);
            var diff = MakeButton(panel.transform, "Btn_Difficulty", new Vector2(0f, -22f), "Dificultad: Normal", sprite, out var diffLabel);
            var dbg = MakeButton(panel.transform, "Btn_Debug", new Vector2(0f, -66f), "Depuración: OFF", sprite, out var dbgLabel);

            Set(wm, "tracker", Object.FindAnyObjectByType<HandTracker>());
            Set(wm, "panel", panel);
            Set(wm, "restartButton", restart);
            Set(wm, "difficultyButton", diff);
            Set(wm, "debugButton", dbg);
            Set(wm, "difficultyLabel", diffLabel);
            Set(wm, "debugLabel", dbgLabel);
            panel.SetActive(false);
        }

        static Button MakeButton(Transform parent, string name, Vector2 pos, string text, Sprite sprite, out TextMeshProUGUI label)
        {
            var go = NewUI(name, parent, pos, new Vector2(196f, 38f));
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.2f, 0.55f, 0.6f, 1f);
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(0.6f, 0.9f, 1f);
            colors.selectedColor = new Color(0.6f, 0.9f, 1f);
            colors.pressedColor = new Color(1f, 1f, 0.6f);
            b.colors = colors;

            label = NewUI("Label", go.transform, Vector2.zero, new Vector2(190f, 36f)).AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 18;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            return b;
        }

        static GameObject NewUI(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go;
        }

        // ---------- Habitación falsa de TestRoom ----------

        class MeshBuilder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<int> t = new List<int>();

            // Caja de 6 caras. 'inward' = caras hacia dentro (paredes de la habitación vistas desde dentro)
            public void Box(Vector3 center, Vector3 size, float rotY, bool inward = false)
            {
                var m = Matrix4x4.TRS(center, Quaternion.Euler(0f, rotY, 0f), Vector3.one);
                Vector3 h = size * 0.5f;
                Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (var n in normals)
                {
                    Vector3 u = Mathf.Abs(n.x) > 0.5f ? Vector3.forward : Vector3.right;
                    Vector3 w = Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up;
                    Vector3 c = Vector3.Scale(n, h);
                    Vector3 du = Vector3.Scale(u, h), dw = Vector3.Scale(w, h);
                    Vector3 p0 = m.MultiplyPoint3x4(c - du - dw), p1 = m.MultiplyPoint3x4(c + du - dw);
                    Vector3 p2 = m.MultiplyPoint3x4(c + du + dw), p3 = m.MultiplyPoint3x4(c - du + dw);
                    Vector3 want = m.MultiplyVector(inward ? -n : n);
                    int i0 = v.Count;
                    v.Add(p0); v.Add(p1); v.Add(p2); v.Add(p3);
                    // En Unity la cara visible es aquella hacia la que apunta cross(b - a, c - a)
                    if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), want) > 0f)
                    { t.Add(i0); t.Add(i0 + 1); t.Add(i0 + 2); t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 3); }
                    else
                    { t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1); t.Add(i0); t.Add(i0 + 3); t.Add(i0 + 2); }
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(v);
                mesh.SetTriangles(t, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        static void BuildRoom(GameAssetsBuilder.Assets a)
        {
            GameAssetsBuilder.EnsureFolder(TestRoomMeshes);
            var room = new GameObject("Room");
            var furnitureRoot = new GameObject("Furniture");
            const float W = 5f, D = 4f, H = 2.6f;

            // Carcasa de la habitación (suelo, paredes, techo)
            var shell = new MeshBuilder();
            shell.Box(new Vector3(0f, H * 0.5f, 0f), new Vector3(W, H, D), 0f, true);
            AddChunk(room.transform, "SceneMesh_Room", shell.ToMesh("TestRoom_Shell"), a.roomWall);

            // Muebles: también forman parte de la mesh, como en el escaneo real
            var fm = new MeshBuilder();
            AddTable(fm, furnitureRoot.transform, new Vector3(1.0f, 0f, 0.6f), 0f);
            AddTable(fm, furnitureRoot.transform, new Vector3(-1.2f, 0f, 0.1f), 20f);
            AddChair(fm, furnitureRoot.transform, new Vector3(1.0f, 0f, -0.05f), 180f);
            AddChair(fm, furnitureRoot.transform, new Vector3(-1.05f, 0f, -0.55f), 200f);
            // Mueble bajo con la tele encima, junto a la pared del fondo
            AddBlock(fm, furnitureRoot.transform, FurnitureKind.Storage, new Vector3(0f, 0.25f, 1.75f), new Vector3(1.2f, 0.5f, 0.4f), 0f);
            AddBlock(fm, furnitureRoot.transform, FurnitureKind.Screen, new Vector3(0f, 0.85f, 1.85f), new Vector3(1.0f, 0.6f, 0.08f), 0f);
            // Estantería alta en una esquina
            AddBlock(fm, furnitureRoot.transform, FurnitureKind.Storage, new Vector3(-2.15f, 0.8f, -1.3f), new Vector3(0.6f, 1.6f, 0.4f), 90f);
            AddChunk(room.transform, "SceneMesh_Furniture", fm.ToMesh("TestRoom_Furniture"), a.roomFurniture);
        }

        static void AddTable(MeshBuilder mb, Transform parent, Vector3 floorPos, float rotY)
        {
            var size = new Vector3(1.2f, 0.76f, 0.7f);
            var rot = Quaternion.Euler(0f, rotY, 0f);
            mb.Box(floorPos + Vector3.up * 0.74f, new Vector3(size.x, 0.04f, size.z), rotY); // tablero
            float lx = size.x * 0.5f - 0.06f, lz = size.z * 0.5f - 0.06f;
            foreach (var (x, z) in new[] { (lx, lz), (-lx, lz), (lx, -lz), (-lx, -lz) })
                mb.Box(floorPos + rot * new Vector3(x, 0.36f, z), new Vector3(0.05f, 0.72f, 0.05f), rotY); // patas
            AddFurniture(parent, FurnitureKind.Table, floorPos + Vector3.up * (size.y * 0.5f), size, rotY);
        }

        static void AddChair(MeshBuilder mb, Transform parent, Vector3 floorPos, float rotY)
        {
            var rot = Quaternion.Euler(0f, rotY, 0f);
            mb.Box(floorPos + Vector3.up * 0.45f, new Vector3(0.44f, 0.04f, 0.44f), rotY);                       // asiento
            mb.Box(floorPos + rot * new Vector3(0f, 0.7f, -0.2f), new Vector3(0.44f, 0.46f, 0.04f), rotY);      // respaldo
            float l = 0.19f;
            foreach (var (x, z) in new[] { (l, l), (-l, l), (l, -l), (-l, -l) })
                mb.Box(floorPos + rot * new Vector3(x, 0.215f, z), new Vector3(0.035f, 0.43f, 0.035f), rotY);
            AddFurniture(parent, FurnitureKind.Chair, floorPos + Vector3.up * 0.465f, new Vector3(0.44f, 0.93f, 0.44f), rotY);
        }

        static void AddBlock(MeshBuilder mb, Transform parent, FurnitureKind kind, Vector3 center, Vector3 size, float rotY)
        {
            mb.Box(center, size, rotY);
            AddFurniture(parent, kind, center, size, rotY);
        }

        // Equivalente a lo que crea BoundingBoxFurniture en el casco
        static void AddFurniture(Transform parent, FurnitureKind kind, Vector3 center, Vector3 size, float rotY)
        {
            var go = new GameObject($"Furniture_{kind}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, rotY, 0f));
            var info = go.AddComponent<FurnitureInfo>();
            info.kind = kind;
            info.size = size;
            if (kind != FurnitureKind.Chair) // la silla real está llena de huecos: solo cuenta la mesh
            {
                go.layer = LayerMask.NameToLayer("Furniture");
                go.AddComponent<BoxCollider>().size = size;
            }
        }

        static void AddChunk(Transform parent, string name, Mesh mesh, Material mat)
        {
            string path = $"{TestRoomMeshes}/{mesh.name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject(name) { layer = LayerMask.NameToLayer("SceneMesh") };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // ---------- Utilidades ----------

        static GameObject FindOrCreate(string name, Transform parent)
        {
            Transform t = parent != null ? parent.Find(name) : null;
            if (t == null && parent == null)
            {
                var found = GameObject.Find(name);
                if (found != null && found.transform.parent == null) t = found.transform;
            }
            if (t != null) return t.gameObject;
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Espectros");
            return go;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : Undo.AddComponent<T>(go);
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Espectros] No encuentro el campo {field} en {target.GetType().Name}"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
