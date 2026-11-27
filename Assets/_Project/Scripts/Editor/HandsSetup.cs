using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

namespace Espectros.EditorTools
{
    // Menú Espectros > Configurar manos y gestos.
    // 1) Crea (si no existen) las formas de mano en Assets/_Project/Gestures.
    // 2) Monta en la escena abierta el objeto "Hands" con HandTracker, HandGestureRouter,
    //    los XRHandTrackingEvents de cada mano y un HandShapeDetector por gesto, con los eventos conectados.
    // Se puede volver a ejecutar: reemplaza "Hands" y conserva las formas ya creadas (y sus ajustes).
    public static class HandsSetup
    {
        const string GesturesFolder = "Assets/_Project/Gestures";

        [MenuItem("Espectros/Configurar manos y gestos")]
        public static void Run()
        {
            // ---------- 1. Formas de mano ----------
            Directory.CreateDirectory(GesturesFolder);

            var fist = GetOrCreate<XRHandShape>("HS_Fist", s => s.fingerShapeConditions = new()
            {
                // Mismos valores que "Fist Hand Shape" del sample de XR Hands
                Curl(XRHandFingerID.Index, 1f, 0.15f, 0.15f),
                Curl(XRHandFingerID.Middle, 1f, 0.15f, 0.15f),
                Curl(XRHandFingerID.Ring, 1f, 0.15f, 0.15f),
                Curl(XRHandFingerID.Little, 1f, 0.25f, 0.25f),
                Curl(XRHandFingerID.Thumb, 1f, 0.375f, 0.375f),
            });

            var open = GetOrCreate<XRHandShape>("HS_Open", s => s.fingerShapeConditions = new()
            {
                // Mismos valores que "Open Hand Shape" del sample de XR Hands
                Curl(XRHandFingerID.Index, 0f, 0.35f, 0.25f),
                Curl(XRHandFingerID.Thumb, 0f, 0.4f, 0.4f),
                Curl(XRHandFingerID.Middle, 0f, 0.35f, 0.25f),
                Curl(XRHandFingerID.Ring, 0f, 0.35f, 0.25f),
                Curl(XRHandFingerID.Little, 0f, 0.25f, 0.25f),
            });

            var pinch = GetOrCreate<XRHandShape>("HS_Pinch", s => s.fingerShapeConditions = new()
            {
                // Pulgar tocando el índice...
                Condition(XRHandFingerID.Index, XRFingerShapeType.Pinch, 1f, 0f, 0.25f),
                // ...y el corazón sin cerrar del todo, para no confundirlo con el puño
                Curl(XRHandFingerID.Middle, 0f, 0.6f, 0f),
            });

            var palmForward = GetOrCreate<XRHandPose>("HP_PalmForward", p =>
            {
                p.handShape = open;
                p.relativeOrientation = new XRHandRelativeOrientation
                {
                    userConditions = new[]
                    {
                        // La palma mira hacia donde mira la jugadora
                        new XRHandRelativeOrientation.UserCondition
                        {
                            handAxis = XRHandAxis.PalmDirection,
                            alignmentCondition = XRHandAlignmentCondition.AlignsWith,
                            referenceDirection = XRHandUserRelativeDirection.NoseDirection,
                            angleTolerance = 45f,
                        }
                    },
                    targetConditions = new XRHandRelativeOrientation.TargetCondition[0],
                };
            });
            AssetDatabase.SaveAssets();

            // ---------- 2. Objetos de la escena ----------
            var origin = Object.FindFirstObjectByType<XROrigin>();
            if (origin == null)
            {
                EditorUtility.DisplayDialog("Espectros", "No hay ningún XR Origin en la escena abierta. Abre la escena Main.", "Vale");
                return;
            }
            Camera cam = origin.Camera != null ? origin.Camera : Camera.main;

            var old = GameObject.Find("Hands");
            if (old != null)
            {
                if (!EditorUtility.DisplayDialog("Espectros", "Ya existe un objeto \"Hands\" en la escena. ¿Reemplazarlo?", "Reemplazar", "Cancelar"))
                    return;
                Undo.DestroyObjectImmediate(old);
            }

            var root = new GameObject("Hands");
            Undo.RegisterCreatedObjectUndo(root, "Configurar manos");

            var tracker = root.AddComponent<HandTracker>();
            SetRef(tracker, "xrOrigin", origin);

            var router = root.AddComponent<HandGestureRouter>();
            SetRef(router, "tracker", tracker);

            var leftEvents = CreateHandEvents(root.transform, "LeftHandEvents", Handedness.Left);
            var rightEvents = CreateHandEvents(root.transform, "RightHandEvents", Handedness.Right);

            // Linterna: palma derecha hacia delante
            var d = CreateDetector(root.transform, "PalmForward_R", rightEvents, palmForward, cam != null ? cam.transform : null);
            UnityEventTools.AddVoidPersistentListener(d.gesturePerformed, router.PalmForwardPerformed);
            UnityEventTools.AddVoidPersistentListener(d.gestureEnded, router.PalmForwardEnded);

            // Puño (pelota / agua): las dos manos
            d = CreateDetector(root.transform, "Fist_R", rightEvents, fist, null);
            UnityEventTools.AddIntPersistentListener(d.gesturePerformed, router.FistPerformed, 1);
            UnityEventTools.AddIntPersistentListener(d.gestureEnded, router.FistEnded, 1);

            d = CreateDetector(root.transform, "Fist_L", leftEvents, fist, null);
            UnityEventTools.AddIntPersistentListener(d.gesturePerformed, router.FistPerformed, 0);
            UnityEventTools.AddIntPersistentListener(d.gestureEnded, router.FistEnded, 0);

            // Pellizco (pintura): mano derecha
            d = CreateDetector(root.transform, "Pinch_R", rightEvents, pinch, null);
            UnityEventTools.AddIntPersistentListener(d.gesturePerformed, router.PinchPerformed, 1);
            UnityEventTools.AddIntPersistentListener(d.gestureEnded, router.PinchEnded, 1);

            // Texto de depuración delante de los ojos
            if (cam != null)
            {
                var text = CreateDebugText(cam.transform);
                SetRef(router, "debugText", text);
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeGameObject = root;
            Debug.Log("[Espectros] Manos y gestos configurados. Guarda la escena (Ctrl+S) y haz Build And Run.");
        }

        // ---------- Utilidades ----------

        static T GetOrCreate<T>(string name, System.Action<T> init) where T : ScriptableObject
        {
            string path = $"{GesturesFolder}/{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset; // ya existe: no se pisan los ajustes hechos a mano

            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static XRFingerShapeCondition Curl(XRHandFingerID finger, float desired, float upper, float lower) =>
            Condition(finger, XRFingerShapeType.FullCurl, desired, upper, lower);

        static XRFingerShapeCondition Condition(XRHandFingerID finger, XRFingerShapeType type, float desired, float upper, float lower) =>
            new XRFingerShapeCondition
            {
                fingerID = finger,
                targets = new[]
                {
                    new XRFingerShapeCondition.Target
                    {
                        shapeType = type,
                        desired = desired,
                        upperTolerance = upper,
                        lowerTolerance = lower,
                    }
                }
            };

        static XRHandTrackingEvents CreateHandEvents(Transform parent, string name, Handedness handedness)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var events = go.AddComponent<XRHandTrackingEvents>();
            events.handedness = handedness;
            return events;
        }

        static HandShapeDetector CreateDetector(Transform parent, string name, XRHandTrackingEvents events,
                                                ScriptableObject shapeOrPose, Transform target)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var det = go.AddComponent<HandShapeDetector>();
            var so = new SerializedObject(det);
            so.FindProperty("handTrackingEvents").objectReferenceValue = events;
            so.FindProperty("handShapeOrPose").objectReferenceValue = shapeOrPose;
            so.FindProperty("targetTransform").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
            return det;
        }

        static TMP_Text CreateDebugText(Transform cam)
        {
            var go = new GameObject("GestureDebugText");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0f, -0.15f, 0.6f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "Gestos: esperando...";
            tmp.fontSize = 0.35f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.9f, 0.2f);
            tmp.rectTransform.sizeDelta = new Vector2(0.6f, 0.15f);
            return tmp;
        }

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null) { Debug.LogError($"[Espectros] No encuentro el campo {field} en {target}"); return; }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
