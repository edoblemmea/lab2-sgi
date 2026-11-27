using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Espectros.EditorTools
{
    // Menú Espectros > Mesh:
    //  - "Modo oclusión": la mesh solo escribe profundidad (invisible, tapa lo virtual) + cubo rojo de prueba.
    //  - "Modo depuración": vuelve a mostrar la mesh con colores (M_MeshDebug).
    public static class OcclusionSetup
    {
        const string MaterialsFolder = "Assets/_Project/Materials";
        const string OccluderPath = MaterialsFolder + "/M_Occluder.mat";
        const string TestRedPath = MaterialsFolder + "/M_TestRed.mat";
        const string TestCubeName = "OcclusionTestCube";

        [MenuItem("Espectros/Mesh: modo oclusión (con cubo de prueba)")]
        public static void OcclusionMode()
        {
            Directory.CreateDirectory(MaterialsFolder);

            var occluder = AssetDatabase.LoadAssetAtPath<Material>(OccluderPath);
            if (occluder == null)
            {
                var shader = Shader.Find("Custom/DepthOnly");
                if (shader == null) { Error("No encuentro el shader Custom/DepthOnly."); return; }
                occluder = new Material(shader);
                AssetDatabase.CreateAsset(occluder, OccluderPath);
            }

            if (!SetMeshMaterial(occluder)) return;
            CreateTestCube();
            AssetDatabase.SaveAssets();
            Debug.Log("[Espectros] Mesh en modo oclusión y cubo de prueba creado. Guarda la escena (Ctrl+S) y haz Build And Run.");
        }

        [MenuItem("Espectros/Mesh: modo depuración (colores)")]
        public static void DebugMode()
        {
            var debugMat = FindAsset<Material>("M_MeshDebug t:Material");
            if (debugMat == null) { Error("No encuentro el material M_MeshDebug."); return; }
            if (SetMeshMaterial(debugMat))
                Debug.Log("[Espectros] Mesh en modo depuración (colores).");
        }

        [MenuItem("Espectros/Mesh: quitar cubo de prueba")]
        public static void RemoveTestCube()
        {
            var cube = GameObject.Find(TestCubeName);
            if (cube != null)
            {
                Undo.DestroyObjectImmediate(cube);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        // Cambia el material del prefab de la mesh de la escena (SceneMeshChunk)
        static bool SetMeshMaterial(Material mat)
        {
            string guid = FindGuid("SceneMeshChunk t:Prefab");
            if (guid == null) { Error("No encuentro el prefab SceneMeshChunk."); return false; }
            string path = AssetDatabase.GUIDToAssetPath(guid);

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var mr = root.GetComponent<MeshRenderer>();
                if (mr == null) { Error("SceneMeshChunk no tiene MeshRenderer."); return false; }
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                root.transform.localPosition = Vector3.zero; // la mesh ya viene en coordenadas del mundo
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return true;
        }

        static void CreateTestCube()
        {
            if (GameObject.Find(TestCubeName) != null) return;

            var red = AssetDatabase.LoadAssetAtPath<Material>(TestRedPath);
            if (red == null)
            {
                red = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                red.SetColor("_BaseColor", Color.red);
                AssetDatabase.CreateAsset(red, TestRedPath);
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = TestCubeName;
            cube.transform.position = new Vector3(0f, 0.5f, 1.5f);
            cube.transform.localScale = Vector3.one * 0.2f;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.GetComponent<MeshRenderer>().sharedMaterial = red;
            Undo.RegisterCreatedObjectUndo(cube, "Cubo de prueba de oclusión");
            EditorSceneManager.MarkSceneDirty(cube.scene);
            Selection.activeGameObject = cube;
        }

        static string FindGuid(string filter)
        {
            var guids = AssetDatabase.FindAssets(filter, new[] { "Assets" });
            return guids.Length > 0 ? guids[0] : null;
        }

        static T FindAsset<T>(string filter) where T : Object
        {
            string guid = FindGuid(filter);
            return guid != null ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }

        static void Error(string msg)
        {
            Debug.LogError("[Espectros] " + msg);
            EditorUtility.DisplayDialog("Espectros", msg, "Vale");
        }
    }
}
