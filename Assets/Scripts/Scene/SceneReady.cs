using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Espectros
{
    // Pide el permiso de escena, activa los managers de AR y avisa cuando hay datos.
    // Nada del juego debe arrancar antes de OnSceneReady.
    // Los managers deben estar DESACTIVADOS en el inspector: este script los enciende.
    public class SceneReady : MonoBehaviour
    {
        // Horizon OS reciente usa el permiso "horizonos"; los anteriores, el "com.oculus". Pedimos los dos.
        static readonly string[] ScenePermissions =
        {
            "com.oculus.permission.USE_SCENE",
            "horizonos.permission.USE_SCENE"
        };

        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARBoundingBoxManager boxManager;
        [SerializeField] ARMeshManager meshManager;
        [SerializeField] float timeout = 10f;
        [SerializeField] GameObject readyIndicator; // opcional: algo que se enciende al estar lista

        public static bool IsReady { get; private set; }
        public static event Action OnSceneReady;

        bool managersEnabled;

        void Awake()
        {
            IsReady = false;
            if (readyIndicator != null) readyIndicator.SetActive(false);
        }

        void Start()
        {
#if UNITY_EDITOR
            // En el editor usamos la habitación falsa (TestRoom): lista al instante.
            Fire();
#elif UNITY_ANDROID
            bool allGranted = true;
            foreach (var p in ScenePermissions)
            {
                bool granted = Permission.HasUserAuthorizedPermission(p);
                Debug.Log($"[Espectros] Permiso {p}: {(granted ? "concedido" : "no concedido")}");
                allGranted &= granted;
            }

            if (allGranted)
            {
                EnableManagers();
                return;
            }

            // Solo activamos los managers cuando están concedidos TODOS los permisos:
            // si falta uno, el casco responde ERROR_PACKAGE_UNTRUSTED en cada frame.
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += p =>
            {
                Debug.Log("[Espectros] Concedido: " + p);
                if (AllGranted()) EnableManagers();
            };
            callbacks.PermissionDenied += p =>
                Debug.LogError("[Espectros] DENEGADO: " + p + ". Actívalo en Ajustes > Privacidad y seguridad > Datos espaciales.");
            Permission.RequestUserPermissions(ScenePermissions, callbacks);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static bool AllGranted()
        {
            foreach (var p in ScenePermissions)
                if (!Permission.HasUserAuthorizedPermission(p)) return false;
            return true;
        }
#endif

        void EnableManagers()
        {
            if (managersEnabled) return; // los callbacks pueden llegar una vez por permiso
            managersEnabled = true;

            Debug.Log("[Espectros] Activando managers de escena");
            if (planeManager != null) planeManager.enabled = true;
            if (boxManager != null) boxManager.enabled = true;
            if (meshManager != null) meshManager.enabled = true;
            StartCoroutine(WaitForData());
        }

        IEnumerator WaitForData()
        {
            float t = 0f;
            while (t < timeout)
            {
                if (MeshCount() > 0 && PlaneCount() > 0) break;
                t += Time.deltaTime;
                yield return null;
            }
            // Margen para que lleguen el resto de trozos de mesh.
            yield return new WaitForSeconds(0.5f);

            Debug.Log($"[Espectros] Meshes: {MeshCount()}, planos: {PlaneCount()}, " +
                      $"cajas: {(boxManager != null ? boxManager.trackables.count : 0)}");
            if (MeshCount() == 0)
                Debug.LogWarning("[Espectros] No hay mesh: revisa Space Setup en esta habitación y la feature Meshing (Android).");
            Fire();
        }

        int MeshCount() => meshManager != null && meshManager.meshes != null ? meshManager.meshes.Count : 0;
        int PlaneCount() => planeManager != null ? planeManager.trackables.count : 0;

        void Fire()
        {
            IsReady = true;
            Debug.Log("[Espectros] Escena lista");
            if (readyIndicator != null) readyIndicator.SetActive(true);
            OnSceneReady?.Invoke();
        }
    }
}
