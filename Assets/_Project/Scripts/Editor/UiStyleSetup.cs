using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Espectros.EditorTools
{
    // Menú Espectros > Aplicar estilo de texto: Expediente (propuesta F).
    // Crea las fuentes de TextMeshPro (Special Elite para títulos, Courier Prime Bold para el texto),
    // sus materiales con contorno y sombra para leerse sobre la habitación real, y las aplica al HUD,
    // al tutorial (con ficha de papel detrás) y al menú de muñeca de la escena abierta.
    public static class UiStyleSetup
    {
        const string Fonts = "Assets/_Project/Fonts";

        static readonly Color Cream = new Color(0.98f, 0.95f, 0.87f);
        static readonly Color Ink = new Color(0.17f, 0.14f, 0.09f);
        static readonly Color StampRed = new Color(0.70f, 0.22f, 0.18f);
        static readonly Color Paper = new Color(0.94f, 0.89f, 0.78f);
        static readonly Color PaperDark = new Color(0.86f, 0.79f, 0.64f);

        // Caracteres que se meten en el atlas desde el principio (el resto se añade solo al usarse)
        const string Charset = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
                               "¡¿ÁÉÍÓÚÜÑáéíóúüñ·ºª€";

        [MenuItem("Espectros/Aplicar estilo de texto: Expediente")]
        public static void ApplyExpediente()
        {
            var title = EnsureFontAsset("SpecialElite-Regular.ttf", "SpecialElite SDF");
            var body = EnsureFontAsset("CourierPrime-Bold.ttf", "CourierPrime-Bold SDF");
            if (title == null || body == null)
            {
                EditorUtility.DisplayDialog("Espectros", "No encuentro las fuentes en Assets/_Project/Fonts.", "Vale");
                return;
            }

            // Materiales: contorno oscuro y sombra para el texto sobre el passthrough; sin contorno para la tinta sobre papel
            var hudMat = EnsureMaterial(body, "CourierPrime HUD", m => Outline(m, 0.22f, new Color(0.08f, 0.06f, 0.04f, 1f), true));
            var titleMat = EnsureMaterial(title, "SpecialElite Title", m => Outline(m, 0.18f, new Color(0.08f, 0.06f, 0.04f, 1f), true));
            var inkMat = EnsureMaterial(body, "CourierPrime Ink", m => Outline(m, 0f, Color.clear, false));
            var paperMat = EnsurePaperMaterial();
            AssetDatabase.SaveAssets();

            int changed = 0;

            // HUD (textos 3D delante de la cámara)
            foreach (var t in Object.FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include))
            {
                switch (t.gameObject.name)
                {
                    case "StatusText":
                    case "GestureDebugText":
                        Apply(t, body, hudMat, Cream);
                        t.fontStyle = FontStyles.Normal;
                        changed++;
                        break;
                    case "MessageText":
                        Apply(t, title, titleMat, Cream);
                        t.lineSpacing = -10f;
                        changed++;
                        break;
                    case "TutorialText":
                        Apply(t, body, inkMat, Ink);
                        t.fontSize = 0.36f;
                        t.transform.localRotation = Quaternion.Euler(0f, 0f, -1.2f);
                        AddCard(t, paperMat);
                        changed++;
                        break;
                }
            }

            // Menú de muñeca (UI)
            var menu = Object.FindAnyObjectByType<WristMenu>(FindObjectsInactive.Include);
            if (menu != null)
            {
                var panel = menu.transform.Find("Panel");
                if (panel != null)
                {
                    foreach (var img in panel.GetComponentsInChildren<Image>(true))
                    {
                        Undo.RecordObject(img, "Estilo Expediente");
                        img.color = img.gameObject.name == "Background" ? Paper : PaperDark;
                    }
                    foreach (var b in panel.GetComponentsInChildren<Button>(true))
                    {
                        var colors = b.colors;
                        colors.highlightedColor = new Color(1f, 0.97f, 0.88f);
                        colors.selectedColor = new Color(1f, 0.97f, 0.88f);
                        colors.pressedColor = new Color(1f, 0.85f, 0.75f);
                        b.colors = colors;
                    }
                    foreach (var t in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        Undo.RecordObject(t, "Estilo Expediente");
                        bool isTitle = t.gameObject.name == "Title";
                        t.font = isTitle ? title : body;
                        t.fontSharedMaterial = isTitle ? title.material : body.material;
                        t.color = isTitle ? StampRed : Ink;
                        if (isTitle) { t.text = "Espectros"; t.characterSpacing = 0f; }
                        else t.alignment = TextAlignmentOptions.Center;
                        if (t.transform.parent != null && t.transform.parent.name == "Btn_Restart") t.text = "Reiniciar ronda";
                        changed++;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Espectros] Estilo Expediente aplicado a {changed} textos. Guarda la escena (Ctrl+S).");
        }

        static void Apply(TMP_Text t, TMP_FontAsset font, Material mat, Color color)
        {
            Undo.RecordObject(t, "Estilo Expediente");
            t.font = font;
            t.fontSharedMaterial = mat;
            t.color = color;
            EditorUtility.SetDirty(t);
        }

        static void AddCard(TextMeshPro t, Material paper)
        {
            var card = t.GetComponent<TextCard>();
            if (card == null) card = Undo.AddComponent<TextCard>(t.gameObject);
            var existing = t.transform.Find("Card");
            GameObject quad;
            if (existing != null) quad = existing.gameObject;
            else
            {
                quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Card";
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(t.transform, false);
                Undo.RegisterCreatedObjectUndo(quad, "Ficha de papel");
            }
            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = paper;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var so = new SerializedObject(card);
            so.FindProperty("card").objectReferenceValue = r;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Fuentes y materiales ----------

        static TMP_FontAsset EnsureFontAsset(string ttf, string name)
        {
            string path = $"{Fonts}/{name}.asset";
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa != null) return fa;

            var font = AssetDatabase.LoadAssetAtPath<Font>($"{Fonts}/{ttf}");
            if (font == null) { Debug.LogError($"[Espectros] No encuentro {Fonts}/{ttf}"); return null; }

            // Atlas dinámico: los caracteres que falten se añaden solos al usarse (también en el casco)
            fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            fa.name = name;
            AssetDatabase.CreateAsset(fa, path);
            fa.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            foreach (var tex in fa.atlasTextures)
            {
                tex.name = name + " Atlas";
                AssetDatabase.AddObjectToAsset(tex, fa);
            }
            fa.TryAddCharacters(Charset);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        static Material EnsureMaterial(TMP_FontAsset font, string name, System.Action<Material> init)
        {
            string path = $"{Fonts}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(font.material) { name = name };
            init(m);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void Outline(Material m, float width, Color color, bool shadow)
        {
            if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", width);
            if (m.HasProperty("_OutlineColor")) m.SetColor("_OutlineColor", color);
            if (width > 0f) m.EnableKeyword("OUTLINE_ON"); else m.DisableKeyword("OUTLINE_ON");
            if (shadow && m.HasProperty("_UnderlayColor"))
            {
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.75f));
                m.SetFloat("_UnderlayOffsetX", 0.4f);
                m.SetFloat("_UnderlayOffsetY", -0.4f);
                m.SetFloat("_UnderlaySoftness", 0.5f);
            }
            else m.DisableKeyword("UNDERLAY_ON");
        }

        static Material EnsurePaperMaterial()
        {
            string path = "Assets/_Project/Materials/M_Paper.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(Shader.Find("Custom/UnlitColor"));
            m.SetColor("_Color", Paper);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
