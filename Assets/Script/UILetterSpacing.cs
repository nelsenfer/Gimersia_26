using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mengatur spasi antar huruf (Letter / Character Spacing) pada komponen Legacy UI Text Unity
/// secara proporsional terhadap ukuran huruf yang sedang ter-render, tetap rapi mengikuti
/// Alignment (Left, Center, Right), multi-baris, dan Rich Text (<color> dll.).
/// </summary>
[AddComponentMenu("UI/Effects/UI Letter Spacing")]
[ExecuteAlways]
[RequireComponent(typeof(Text))]
public class UILetterSpacing : BaseMeshEffect
{
    [Tooltip("Spasi antar huruf (proporsional). Nilai 1.5 - 3.0 memberikan jarak huruf yang rapi & mudah dibaca untuk font Dimbo.")]
    [Range(-5f, 15f)]
    [SerializeField] private float letterSpacing = 2.0f;

    private Text targetText;

    public float LetterSpacing
    {
        get => letterSpacing;
        set
        {
            if (Mathf.Approximately(letterSpacing, value)) return;
            letterSpacing = value;
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        if (graphic != null)
        {
            graphic.SetVerticesDirty();
        }
    }
#endif

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh == null || vh.currentVertCount == 0 || Mathf.Approximately(letterSpacing, 0f))
            return;

        if (targetText == null)
            targetText = GetComponent<Text>();

        if (targetText == null)
            return;

        List<UIVertex> verts = new List<UIVertex>(vh.currentVertCount);
        vh.GetUIVertexStream(verts);

        int quadCount = verts.Count / 6;
        if (quadCount <= 1)
            return;

        // Hitung faktor alignment horizontal (0 = Left, 0.5 = Center, 1 = Right)
        float alignmentFactor = 0.5f;
        switch (targetText.alignment)
        {
            case TextAnchor.UpperLeft:
            case TextAnchor.MiddleLeft:
            case TextAnchor.LowerLeft:
                alignmentFactor = 0f;
                break;
            case TextAnchor.UpperCenter:
            case TextAnchor.MiddleCenter:
            case TextAnchor.LowerCenter:
                alignmentFactor = 0.5f;
                break;
            case TextAnchor.UpperRight:
            case TextAnchor.MiddleRight:
            case TextAnchor.LowerRight:
                alignmentFactor = 1f;
                break;
        }

        // 1. Kelompokkan quad yang terlihat (bukan quad kosong/spasi dengan luas 0) ke dalam baris masing-masing
        // berdasarkan posisi vertikal tengah quad (midY), sehingga bekerja akurat walau ada Rich Text (<color>) atau word-wrap!
        List<int> currentLineQuads = new List<int>(32);
        float currentLineMidY = float.NaN;
        float currentLineHeight = 16f;

        for (int q = 0; q < quadCount; q++)
        {
            int vIdx = q * 6;
            UIVertex v0 = verts[vIdx];
            UIVertex v2 = verts[vIdx + 2];

            float width = Mathf.Abs(v2.position.x - v0.position.x);
            float height = Mathf.Abs(v2.position.y - v0.position.y);

            // Quad tak terlihat (misal tag rich text atau newline/spasi kosong) dilewati tapi jika X kembali ke kiri bisa menandakan baris baru
            if (width < 0.001f && height < 0.001f)
                continue;

            float midY = (v0.position.y + v2.position.y) * 0.5f;
            float lineThreshold = Mathf.Max(height, currentLineHeight) * 0.65f;

            if (float.IsNaN(currentLineMidY) || Mathf.Abs(midY - currentLineMidY) <= lineThreshold)
            {
                // Masih di baris yang sama (cek juga jika X tiba-tiba lompat jauh ke kiri = baris baru dengan tinggi tipis)
                if (currentLineQuads.Count > 0)
                {
                    int prevVIdx = currentLineQuads[currentLineQuads.Count - 1] * 6;
                    float prevX = verts[prevVIdx].position.x;
                    if (v0.position.x < prevX - Mathf.Max(width, 4f))
                    {
                        ApplySpacingToLine(verts, currentLineQuads, currentLineHeight, alignmentFactor);
                        currentLineQuads.Clear();
                        currentLineMidY = midY;
                        currentLineHeight = Mathf.Max(8f, height);
                    }
                }

                if (currentLineQuads.Count == 0)
                {
                    currentLineMidY = midY;
                    currentLineHeight = Mathf.Max(8f, height);
                }
                else
                {
                    currentLineHeight = Mathf.Max(currentLineHeight, height);
                }

                currentLineQuads.Add(q);
            }
            else
            {
                // Pindah baris baru
                ApplySpacingToLine(verts, currentLineQuads, currentLineHeight, alignmentFactor);
                currentLineQuads.Clear();
                currentLineMidY = midY;
                currentLineHeight = Mathf.Max(8f, height);
                currentLineQuads.Add(q);
            }
        }

        if (currentLineQuads.Count > 0)
        {
            ApplySpacingToLine(verts, currentLineQuads, currentLineHeight, alignmentFactor);
            currentLineQuads.Clear();
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }

    private void ApplySpacingToLine(List<UIVertex> verts, List<int> lineQuads, float lineHeight, float alignmentFactor)
    {
        int count = lineQuads.Count;
        if (count <= 1) return;

        // Skala offset proporsional terhadap tinggi huruf asli yang ter-render di Canvas
        // sehingga baik teks kecil di HUD (28px) maupun teks besar di Canvas HP (160px) memiliki proporsi renggang yang SAMA dan RAPI!
        float stepOffset = letterSpacing * (lineHeight * 0.045f);

        // Deteksi bila ada celah spasi antar kata (jarak X antar huruf berurutan cukup lebar) agar spasi kata ikut bergeser proporsional
        float[] cumulativeUnits = new float[count];
        cumulativeUnits[0] = 0f;

        for (int i = 1; i < count; i++)
        {
            int prevIdx = lineQuads[i - 1] * 6;
            int currIdx = lineQuads[i] * 6;

            float prevRight = Mathf.Max(verts[prevIdx].position.x, verts[prevIdx + 2].position.x);
            float currLeft = Mathf.Min(verts[currIdx].position.x, verts[currIdx + 2].position.x);
            float gap = currLeft - prevRight;

            // Jika ada spasi kata di antaranya, tambahkan 1.6 unit, jika huruf bersebelahan 1.0 unit
            float unitAdvance = (gap > lineHeight * 0.18f) ? 1.6f : 1.0f;
            cumulativeUnits[i] = cumulativeUnits[i - 1] + unitAdvance;
        }

        float totalLineExtraWidth = cumulativeUnits[count - 1] * stepOffset;
        float alignmentShift = -totalLineExtraWidth * alignmentFactor;

        for (int i = 0; i < count; i++)
        {
            float dx = (cumulativeUnits[i] * stepOffset) + alignmentShift;
            int baseVertIdx = lineQuads[i] * 6;

            for (int v = 0; v < 6; v++)
            {
                UIVertex vert = verts[baseVertIdx + v];
                vert.position.x += dx;
                verts[baseVertIdx + v] = vert;
            }
        }
    }
}
