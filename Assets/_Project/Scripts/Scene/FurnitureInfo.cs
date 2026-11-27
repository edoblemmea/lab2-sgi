using System;
using System.Collections.Generic;
using UnityEngine;

namespace Espectros
{
    public enum FurnitureKind { Table, Chair, Couch, Screen, Storage, Bed, Other }

    // Un mueble de la habitación: caja centrada en el transform con el tamaño indicado.
    // En el casco lo crea BoundingBoxFurniture a partir de los bounding boxes; en TestRoom va puesto a mano.
    public class FurnitureInfo : MonoBehaviour
    {
        public FurnitureKind kind = FurnitureKind.Other;
        [Tooltip("Tamaño de la caja en espacio local (m)")]
        public Vector3 size = Vector3.one;

        static readonly List<FurnitureInfo> all = new List<FurnitureInfo>();
        public static IReadOnlyList<FurnitureInfo> All => all;
        public static event Action Changed;

        public Vector3 Center => transform.position;
        public Vector3 HalfExtents => size * 0.5f;
        public float Bottom => Center.y - HalfExtents.y;
        public float Top => Center.y + HalfExtents.y;

        void OnEnable() { all.Add(this); Changed?.Invoke(); }
        void OnDisable() { all.Remove(this); Changed?.Invoke(); }

        public void SetSize(Vector3 newSize)
        {
            if ((newSize - size).sqrMagnitude < 1e-6f) return;
            size = newSize;
            Changed?.Invoke();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
