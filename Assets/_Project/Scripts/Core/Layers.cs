using UnityEngine;

namespace Espectros
{
    // Capas de física del proyecto y máscaras de uso habitual.
    public static class Layers
    {
        static int sceneMesh = -2, furniture = -2, ghost = -2, projectile = -2, pickup = -2;

        static int Get(ref int cache, string name)
        {
            if (cache == -2)
            {
                cache = LayerMask.NameToLayer(name);
                if (cache < 0) Debug.LogError($"[Espectros] Falta la capa '{name}' en Tags and Layers");
            }
            return cache;
        }

        public static int SceneMesh => Get(ref sceneMesh, "SceneMesh");
        public static int Furniture => Get(ref furniture, "Furniture");
        public static int Ghost => Get(ref ghost, "Ghost");
        public static int Projectile => Get(ref projectile, "Projectile");
        public static int Pickup => Get(ref pickup, "Pickup");

        static int Mask(int layer) => layer >= 0 ? 1 << layer : 0;

        public static int SceneMeshMask => Mask(SceneMesh);
        public static int GhostMask => Mask(Ghost);
        // Lo que tapa la vista y la luz: la mesh y las cajas de los muebles (las sillas no tienen caja)
        public static int OccluderMask => Mask(SceneMesh) | Mask(Furniture);
    }

    // Acceso a la cabeza de la jugadora (la cámara principal).
    public static class PlayerHead
    {
        static Camera cam;

        public static Camera Camera
        {
            get
            {
                if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
                return cam;
            }
        }

        public static Transform Transform => Camera != null ? Camera.transform : null;
        public static Vector3 Position => Transform != null ? Transform.position : new Vector3(0f, 1.5f, 0f);
        public static Vector3 Forward => Transform != null ? Transform.forward : Vector3.forward;
    }
}
