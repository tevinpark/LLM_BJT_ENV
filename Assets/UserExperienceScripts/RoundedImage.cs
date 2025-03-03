using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Sprites;

[AddComponentMenu("UI/Rounded Image")]
public class RoundedImage : Image
{
    // Rounded corner settings
    [SerializeField] private float m_TopLeftRadius = 20f;
    [SerializeField] private float m_TopRightRadius = 20f;
    [SerializeField] private float m_BottomRightRadius = 20f;
    [SerializeField] private float m_BottomLeftRadius = 20f;
    [SerializeField] private int m_CornerSegments = 5; // Segments per corner

    // Outline (border) settings
    [SerializeField] private float m_OutlineWidth = 0f;
    [SerializeField] private Color m_OutlineColor = Color.black;

    // Public properties (refresh the mesh when changed)
    public float TopLeftRadius     { get { return m_TopLeftRadius; }     set { m_TopLeftRadius = value;     SetVerticesDirty(); } }
    public float TopRightRadius    { get { return m_TopRightRadius; }    set { m_TopRightRadius = value;    SetVerticesDirty(); } }
    public float BottomRightRadius { get { return m_BottomRightRadius; } set { m_BottomRightRadius = value; SetVerticesDirty(); } }
    public float BottomLeftRadius  { get { return m_BottomLeftRadius; }  set { m_BottomLeftRadius = value;  SetVerticesDirty(); } }
    public int   CornerSegments    { get { return m_CornerSegments; }    set { m_CornerSegments = Mathf.Max(1, value); SetVerticesDirty(); } }
    public float OutlineWidth      { get { return m_OutlineWidth; }      set { m_OutlineWidth = value;      SetVerticesDirty(); } }
    public Color OutlineColor      { get { return m_OutlineColor; }      set { m_OutlineColor = value;      SetVerticesDirty(); } }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float width = rect.width;
        float height = rect.height;

        // Get sprite UV info if a sprite is assigned, otherwise use default [0,1] UVs.
        Sprite sprite = overrideSprite;
        Vector4 spriteUV;
        if (sprite != null)
            spriteUV = DataUtility.GetOuterUV(sprite);
        else
            spriteUV = new Vector4(0f, 0f, 1f, 1f);

        // Helper local function to compute a vertex’s UV based on its position.
        Vector2 GetUV(Vector2 pos)
        {
            float u = Mathf.Lerp(spriteUV.x, spriteUV.z, (pos.x - rect.x) / rect.width);
            float v = Mathf.Lerp(spriteUV.y, spriteUV.w, (pos.y - rect.y) / rect.height);
            return new Vector2(u, v);
        }

        // Clamp each corner radius so it does not exceed half of the rect’s dimensions.
        float tl = Mathf.Min(m_TopLeftRadius, Mathf.Min(width, height) / 2f);
        float tr = Mathf.Min(m_TopRightRadius, Mathf.Min(width, height) / 2f);
        float br = Mathf.Min(m_BottomRightRadius, Mathf.Min(width, height) / 2f);
        float bl = Mathf.Min(m_BottomLeftRadius, Mathf.Min(width, height) / 2f);

        int seg = m_CornerSegments;
        List<Vector2> innerPositions = new List<Vector2>();

        // Build inner boundary vertices in clockwise order starting at bottom-left.
        // Bottom Left Corner
        if (bl > 0f)
        {
            Vector2 center = new Vector2(rect.xMin + bl, rect.yMin + bl);
            for (int i = 0; i <= seg; i++)
            {
                float angle = Mathf.PI + (Mathf.PI / 2f) * (i / (float)seg);
                innerPositions.Add(center + new Vector2(Mathf.Cos(angle) * bl, Mathf.Sin(angle) * bl));
            }
        }
        else
        {
            innerPositions.Add(new Vector2(rect.xMin, rect.yMin));
        }

        // Bottom Right Corner
        if (br > 0f)
        {
            Vector2 center = new Vector2(rect.xMax - br, rect.yMin + br);
            for (int i = 0; i <= seg; i++)
            {
                float angle = (3f * Mathf.PI / 2f) + (Mathf.PI / 2f) * (i / (float)seg);
                innerPositions.Add(center + new Vector2(Mathf.Cos(angle) * br, Mathf.Sin(angle) * br));
            }
        }
        else
        {
            innerPositions.Add(new Vector2(rect.xMax, rect.yMin));
        }

        // Top Right Corner
        if (tr > 0f)
        {
            Vector2 center = new Vector2(rect.xMax - tr, rect.yMax - tr);
            for (int i = 0; i <= seg; i++)
            {
                float angle = 0f + (Mathf.PI / 2f) * (i / (float)seg);
                innerPositions.Add(center + new Vector2(Mathf.Cos(angle) * tr, Mathf.Sin(angle) * tr));
            }
        }
        else
        {
            innerPositions.Add(new Vector2(rect.xMax, rect.yMax));
        }

        // Top Left Corner
        if (tl > 0f)
        {
            Vector2 center = new Vector2(rect.xMin + tl, rect.yMax - tl);
            for (int i = 0; i <= seg; i++)
            {
                float angle = (Mathf.PI / 2f) + (Mathf.PI / 2f) * (i / (float)seg);
                innerPositions.Add(center + new Vector2(Mathf.Cos(angle) * tl, Mathf.Sin(angle) * tl));
            }
        }
        else
        {
            innerPositions.Add(new Vector2(rect.xMin, rect.yMax));
        }

        int n = innerPositions.Count;

        // Outline (optional)
        if (m_OutlineWidth > 0f)
        {
            List<Vector2> outerPositions = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = innerPositions[(i - 1 + n) % n];
                Vector2 current = innerPositions[i];
                Vector2 next = innerPositions[(i + 1) % n];

                // Compute edge normals for adjacent edges.
                Vector2 edge1 = (current - prev).normalized;
                Vector2 edge2 = (next - current).normalized;
                Vector2 normal1 = new Vector2(-edge1.y, edge1.x);
                Vector2 normal2 = new Vector2(-edge2.y, edge2.x);
                Vector2 offsetDir = (normal1 + normal2).normalized;
                if (offsetDir == Vector2.zero)
                    offsetDir = (current - rect.center).normalized;
                Vector2 outer = current + offsetDir * m_OutlineWidth;
                outerPositions.Add(outer);
            }

            // Add outline vertices (outer then inner) with UVs.
            int outlineStartIndex = vh.currentVertCount;
            for (int i = 0; i < n; i++)
            {
                UIVertex v = UIVertex.simpleVert;
                v.color = m_OutlineColor;
                v.position = outerPositions[i];
                v.uv0 = GetUV(v.position);
                vh.AddVert(v);
            }
            for (int i = 0; i < n; i++)
            {
                UIVertex v = UIVertex.simpleVert;
                v.color = m_OutlineColor;
                v.position = innerPositions[i];
                v.uv0 = GetUV(v.position);
                vh.AddVert(v);
            }

            // Create triangles for the outline ring.
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                int outer_i = outlineStartIndex + i;
                int outer_next = outlineStartIndex + next;
                int inner_i = outlineStartIndex + n + i;
                int inner_next = outlineStartIndex + n + next;

                vh.AddTriangle(inner_i, outer_i, outer_next);
                vh.AddTriangle(inner_i, outer_next, inner_next);
            }
        }

        // === Fill: Draw the inner (rounded) shape using a fan triangulation ===
        int fillStartIndex = vh.currentVertCount;
        UIVertex centerVertex = UIVertex.simpleVert;
        centerVertex.color = color;
        centerVertex.position = rect.center;
        centerVertex.uv0 = GetUV(centerVertex.position);
        vh.AddVert(centerVertex);
        for (int i = 0; i < n; i++)
        {
            UIVertex v = UIVertex.simpleVert;
            v.color = color;
            v.position = innerPositions[i];
            v.uv0 = GetUV(v.position);
            vh.AddVert(v);
        }
        for (int i = 0; i < n; i++)
        {
            int next = (i + 1) % n;
            vh.AddTriangle(fillStartIndex, fillStartIndex + 1 + i, fillStartIndex + 1 + next);
        }
    }
}
