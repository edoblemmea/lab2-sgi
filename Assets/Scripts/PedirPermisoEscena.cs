using UnityEngine;
using UnityEngine.Android;


public class PedirPermisoEscena : MonoBehaviour
{
    void Start()
    {
        const string permisoEscena = "com.oculus.permission.USE_SCENE";


        if (!Permission.HasUserAuthorizedPermission(permisoEscena))
        {
            Permission.RequestUserPermission(permisoEscena);
        }
    }
}