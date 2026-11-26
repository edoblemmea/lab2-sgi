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
    public class SceneReady : MonoBehaviour
    {
        const string ScenePermission = "com.oculus.permission.USE_SCENE";

        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARBoundingBoxManager boxManager;
        [SerializeField] ARMeshManager meshManager;
        [SerializeField] float timeout = 10f;
        [SerializeField] GameObject readyIndicator; // opcional: algo que se enciende al estar lista

        public static bool IsReady { get; private set; }
        public static event Action OnSceneReady;

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
            if (Permission.HasUserAuthorizedPermission(ScenePermission))
            {
                EnableManagers();
            }
            else
            {
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => EnableManagers();
                callbacks.PermissionDenied += _ =>
                    Debug.LogError("[Espectros] Permiso de escena DENEGADO. Actívalo en Ajustes > Privacidad > Datos espaciales.");
                Permission.RequestUserPermission(ScenePermission, callbacks);
            }
#endif
        }

        void EnableManagers()
        {
            Debug.Log("[Espectros] Permiso de escena concedido, activando managers");
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
                int meshes = meshManager != null ? meshManager.meshes.Count : 0;
                int planes = planeManager != null ? planeManager.trackables.count : 0;
                if (meshes > 0 && planes > 0) break;
                t += Time.deltaTime;
                yield return null;
            }
            // Margen para que lleguen el resto de trozos de mesh.
            yield return new WaitForSeconds(0.5f);

            Debug.Log($"[Espectros] Meshes: {(meshManager != null ? meshManager.meshes.Count : 0)}, " +
                      $"planos: {(planeManager != null ? planeManager.trackables.count : 0)}");
            Fire();
        }

        void Fire()
        {
            IsReady = true;
            Debug.Log("[Espectros] Escena lista");
            if (readyIndicator != null) readyIndicator.SetActive(true);
            OnSceneReady?.Invoke();
        }
    }
}