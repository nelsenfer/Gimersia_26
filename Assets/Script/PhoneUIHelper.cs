using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Utility helper untuk membangun elemen UI placeholder secara konsisten
/// baik di Edit Mode maupun Runtime tanpa membutuhkan asset eksternal.
/// </summary>
public static class PhoneUIHelper
{
    private static Font cachedFont;

    public static Font GetDefaultFont()
    {
        if (cachedFont != null) return cachedFont;

        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (cachedFont == null)
        {
            cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        return cachedFont;
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
