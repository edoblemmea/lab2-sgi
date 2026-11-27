using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Espectros
{
    // Convierte los bounding boxes de AR en muebles del juego: un hijo con FurnitureInfo y,
    // salvo en las sillas, un BoxCollider en la capa Furniture (la silla real está llena de huecos,
    // así que para ella solo cuenta la mesh).
    public class BoundingBoxFurniture : MonoBehaviour
    {
        [SerializeField] ARBoundingBoxManager boxManager;

        readonly Dictionary<TrackableId, FurnitureInfo> furniture = new Dictionary<TrackableId, FurnitureInfo>();

        void Awake()
        {
            if (boxManager == null) boxManager = GetComponent<ARBoundingBoxManager>();
            if (boxManager == null) boxManager = FindAnyObjectByType<ARBoundingBoxManager>();
        }

        void OnEnable()
        {
            if (boxManager == null) return;
            boxManager.trackablesChanged.AddListener(OnChanged);
            foreach (var box in boxManager.trackables) AddOrUpdate(box);
        }

        void OnDisable()
        {
            if (boxManager != null) boxManager.trackablesChanged.RemoveListener(OnChanged);
        }

        void OnChanged(ARTrackablesChangedEventArgs<ARBoundingBox> args)
        {
            foreach (var box in args.added) AddOrUpdate(box);
            foreach (var box in args.updated) AddOrUpdate(box);
            foreach (var pair in args.removed)
            {
                if (furniture.TryGetValue(pair.Key, out var info) && info != null) Destroy(info.gameObject);
                furniture.Remove(pair.Key);
            }
        }

        void AddOrUpdate(ARBoundingBox box)
        {
            if (!furniture.TryGetValue(box.trackableId, out var info) || info == null)
            {
                var kind = ToKind(box.classifications);
                var go = new GameObject($"Furniture_{kind}");
                go.transform.SetParent(box.transform, false);
                info = go.AddComponent<FurnitureInfo>();
                info.kind = kind;

                if (kind != FurnitureKind.Chair)
                {
                    go.layer = Layers.Furniture;
                    go.AddComponent<BoxCollider>();
                }
                furniture[box.trackableId] = info;
                Debug.Log($"[Espectros] Mueble: {kind} ({box.classifications}) tamaño {box.size}");
            }

            var col = info.GetComponent<BoxCollider>();
            if (col != null) col.size = box.size;
            info.SetSize(box.size);
        }

        static FurnitureKind ToKind(BoundingBoxClassifications c)
        {
            if ((c & BoundingBoxClassifications.Table) != 0) return FurnitureKind.Table;
            if ((c & BoundingBoxClassifications.Chair) != 0) return FurnitureKind.Chair;
            if ((c & BoundingBoxClassifications.Couch) != 0) return FurnitureKind.Couch;
            if ((c & BoundingBoxClassifications.Screen) != 0) return FurnitureKind.Screen;
            if ((c & BoundingBoxClassifications.Storage) != 0) return FurnitureKind.Storage;
            if ((c & BoundingBoxClassifications.Bed) != 0) return FurnitureKind.Bed;
            return FurnitureKind.Other;
        }
    }
}
