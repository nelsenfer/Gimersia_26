using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Utility helper untuk membangun elemen UI placeholder secara konsisten
/// baik di Edit Mode maupun Runtime, serta menerapkan font utama game (Dimbo Regular).
/// </summary>
public static class PhoneUIHelper
{
    public const float DefaultLetterSpacing = 2.0f;

    private static Font cachedFont;
    private static TMP_FontAsset cachedTmpFont;

    public static void SetCustomDefaultFont(Font customFont, TMP_FontAsset customTmpFont = null)
    {
        if (customFont != null) cachedFont = customFont;
        if (customTmpFont != null) cachedTmpFont = customTmpFont;
    }

    public static Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;

#if UNITY_EDITOR
        cachedFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Asset fix/font/Dimbo Regular.ttf");
        if (cachedFont == null)
        {
            cachedFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Asset fix/font/Dimbo Italic.ttf");
        }
#endif

        if (cachedFont == null)
        {
            Font[] loadedFonts = Resources.FindObjectsOfTypeAll<Font>();
            for (int i = 0; i < loadedFonts.Length; i++)
            {
                if (loadedFonts[i] != null &&
                    loadedFonts[i].name.IndexOf("Dimbo", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cachedFont = loadedFonts[i];
                    break;
                }
            }
        }

        if (cachedFont == null)
        {
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        if (cachedFont == null)
        {
            cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        return cachedFont;
    }

    public static TMP_FontAsset GetDefaultTMPFont()
    {
        if (cachedTmpFont != null) return cachedTmpFont;

#if UNITY_EDITOR
        cachedTmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Asset fix/font/Dimbo Regular SDF.asset");
        if (cachedTmpFont == null)
        {
            cachedTmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Asset fix/font/Dimbo Italic SDF.asset");
        }
#endif

        if (cachedTmpFont == null)
        {
            TMP_FontAsset[] loadedTmpFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < loadedTmpFonts.Length; i++)
            {
                if (loadedTmpFonts[i] != null &&
                    loadedTmpFonts[i].name.IndexOf("Dimbo", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cachedTmpFont = loadedTmpFonts[i];
                    break;
                }
            }
        }

        return cachedTmpFont;
    }

    /// <summary>
    /// Menerapkan font Dimbo Regular pada seluruh komponen Text (Legacy UI) dan TMP_Text (TextMeshPro)
    /// yang ada di Scene aktif maupun di dalam Transform tertentu.
    /// </summary>
    public static int ApplyProvidedFontToAllSceneTexts(Font overrideFont = null, TMP_FontAsset overrideTmpFont = null)
    {
        if (overrideFont != null || overrideTmpFont != null)
        {
            SetCustomDefaultFont(overrideFont, overrideTmpFont);
        }

        Font targetFont = overrideFont != null ? overrideFont : GetDefaultFont();
        TMP_FontAsset targetTmpFont = overrideTmpFont != null ? overrideTmpFont : GetDefaultTMPFont();
        int updatedCount = 0;

        if (targetFont != null)
        {
            Text[] allLegacyTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allLegacyTexts.Length; i++)
            {
                Text t = allLegacyTexts[i];
                if (t == null) continue;

                bool changed = false;
                if (t.font != targetFont)
                {
                    t.font = targetFont;
                    changed = true;
                }

                UILetterSpacing spacingEffect = t.GetComponent<UILetterSpacing>();
                if (spacingEffect == null)
                {
                    spacingEffect = t.gameObject.AddComponent<UILetterSpacing>();
                    spacingEffect.LetterSpacing = DefaultLetterSpacing;
                    changed = true;
                }

                if (changed)
                {
                    updatedCount++;
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        EditorUtility.SetDirty(t);
                        EditorUtility.SetDirty(t.gameObject);
                    }
#endif
                }
            }
        }

        if (targetTmpFont != null)
        {
            TMP_Text[] allTmpTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allTmpTexts.Length; i++)
            {
                TMP_Text tmp = allTmpTexts[i];
                if (tmp == null) continue;

                bool changed = false;
                if (tmp.font != targetTmpFont)
                {
                    tmp.font = targetTmpFont;
                    changed = true;
                }

                if (Mathf.Abs(tmp.characterSpacing) < 0.01f)
                {
                    tmp.characterSpacing = DefaultLetterSpacing * 2.5f;
                    changed = true;
                }

                if (changed)
                {
                    updatedCount++;
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        EditorUtility.SetDirty(tmp);
                    }
#endif
                }
            }
        }

        return updatedCount;
    }

    public static void ApplyAnchorBox(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rt == null) return;

        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Hanya nol-kan sumbu Z, JANGAN menimpa X dan Y hasil kalkulasi anchorMin/anchorMax!
        Vector3 pos = rt.localPosition;
        pos.z = 0f;
        rt.localPosition = pos;
    }

    public static RectTransform CreatePanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        ApplyAnchorBox(rt, anchorMin, anchorMax);

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = true;

        return rt;
    }

    public static Text CreateText(
        string name,
        Transform parent,
        string content,
        Color textColor,
        Vector2 anchorMin,
        Vector2 anchorMax,
        TextAnchor alignment = TextAnchor.MiddleCenter,
        int maxFontSize = 120,
        FontStyle fontStyle = FontStyle.Bold)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        ApplyAnchorBox(rt, anchorMin, anchorMax);

        Text txt = go.GetComponent<Text>();
        txt.font = GetDefaultFont();
        txt.text = content;
        txt.color = textColor;
        txt.alignment = alignment;
        txt.fontStyle = fontStyle;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.resizeTextForBestFit = true;
        txt.resizeTextMinSize = 18;
        txt.resizeTextMaxSize = maxFontSize;
        txt.raycastTarget = false;

        UILetterSpacing spacing = go.AddComponent<UILetterSpacing>();
        spacing.LetterSpacing = DefaultLetterSpacing;

        return txt;
    }

    public static Button CreateButton(
        string name,
        Transform parent,
        string labelText,
        Color bgColor,
        Color textColor,
        Vector2 anchorMin,
        Vector2 anchorMax,
        out Text labelComponent,
        int maxFontSize = 100)
    {
        RectTransform rt = CreatePanel(name, parent, bgColor, anchorMin, anchorMax);
        Button btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = rt.GetComponent<Image>();

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        btn.colors = cb;

        labelComponent = CreateText(
            "Label",
            rt,
            labelText,
            textColor,
            new Vector2(0.08f, 0.08f),
            new Vector2(0.92f, 0.92f),
            TextAnchor.MiddleCenter,
            maxFontSize,
            FontStyle.Bold
        );

        return btn;
    }
}
