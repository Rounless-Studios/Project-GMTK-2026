using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GMTK.Minimap
{
    /// <summary>
    /// Draws a polyline as a thick ribbon inside one CanvasRenderer: the minimap's road surface.
    /// The track has to become UI geometry because a LineRenderer cannot live in a screen-space
    /// canvas, and a render texture from a second top-down camera would pay for a full extra render
    /// of the level to fill a small panel.
    /// </summary>
    public sealed class MinimapRibbon : MaskableGraphic
    {
        private readonly List<Vector2> points = new();
        private bool closedLoop;
        private float thicknessPixels = 4f;

        /// <summary>
        /// Replaces the drawn line. Coordinates are pixels measured from the panel centre, which is
        /// what this graphic's own rect uses.
        /// </summary>
        public void SetPolyline(List<Vector2> panelPoints, bool closed, float thickness)
        {
            points.Clear();
            if (panelPoints != null) points.AddRange(panelPoints);
            closedLoop = closed;
            thicknessPixels = Mathf.Max(0.5f, thickness);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (points.Count < 2) return;

            Color32 tint = color;
            float half = thicknessPixels * 0.5f;
            int segments = closedLoop ? points.Count : points.Count - 1;

            for (int i = 0; i < segments; i++)
            {
                Vector2 from = points[i];
                Vector2 to = points[(i + 1) % points.Count];
                Vector2 along = to - from;
                float length = along.magnitude;
                if (length < 0.0001f) continue;

                Vector2 side = new Vector2(-along.y, along.x) * (half / length);
                AddQuad(vertexHelper, from - side, from + side, to + side, to - side, tint);
            }

            // square patches over the joins: two quads meeting at an angle leave a wedge-shaped gap
            // on the outside of every corner, and this track is nothing but corners
            var patch = new Vector2(half, half);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 centre = points[i];
                AddQuad(vertexHelper,
                    centre + new Vector2(-patch.x, -patch.y),
                    centre + new Vector2(-patch.x, patch.y),
                    centre + new Vector2(patch.x, patch.y),
                    centre + new Vector2(patch.x, -patch.y),
                    tint);
            }
        }

        private static void AddQuad(VertexHelper vertexHelper, Vector2 a, Vector2 b, Vector2 c,
            Vector2 d, Color32 tint)
        {
            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(a, tint, Vector2.zero);
            vertexHelper.AddVert(b, tint, Vector2.zero);
            vertexHelper.AddVert(c, tint, Vector2.zero);
            vertexHelper.AddVert(d, tint, Vector2.zero);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start + 2, start + 3, start);
        }
    }
}
