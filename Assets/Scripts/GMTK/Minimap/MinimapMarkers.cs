using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GMTK.Minimap
{
    /// <summary>
    /// Draws the car blips of the minimap in one CanvasRenderer: an arrow for the player and a
    /// diamond per rival. One graphic keeps the whole field at one draw call, and the buffer is
    /// reused every frame so the marker layer allocates nothing while racing.
    /// </summary>
    public sealed class MinimapMarkers : MaskableGraphic
    {
        private readonly List<Marker> markers = new();

        /// <summary>Drops last frame's blips. Follow it with the Add calls, then <see cref="Flush"/>.</summary>
        public void Clear() => markers.Clear();

        /// <summary>A rival: a diamond reads as a dot at blip size without needing a circle mesh.</summary>
        public void AddDiamond(Vector2 position, float size, Color tint)
        {
            markers.Add(new Marker
            {
                position = position,
                headingDegrees = 45f,
                size = size,
                tint = tint,
                arrow = false
            });
        }

        /// <summary>The player: a triangle, so the panel also shows which way the car points.</summary>
        public void AddArrow(Vector2 position, float headingDegrees, float size, Color tint)
        {
            markers.Add(new Marker
            {
                position = position,
                headingDegrees = headingDegrees,
                size = size,
                tint = tint,
                arrow = true
            });
        }

        /// <summary>Hands the collected blips to the canvas.</summary>
        public void Flush() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            for (int i = 0; i < markers.Count; i++)
            {
                Marker marker = markers[i];
                Color32 tint = marker.tint;
                if (marker.arrow)
                {
                    float half = marker.size * 0.5f;
                    AddTriangle(vertexHelper,
                        marker.position + Rotate(new Vector2(0f, half * 1.15f), marker.headingDegrees),
                        marker.position + Rotate(new Vector2(-half * 0.8f, -half), marker.headingDegrees),
                        marker.position + Rotate(new Vector2(half * 0.8f, -half), marker.headingDegrees),
                        tint);
                }
                else
                {
                    float half = marker.size * 0.5f;
                    int start = vertexHelper.currentVertCount;
                    vertexHelper.AddVert(marker.position + new Vector2(0f, half), tint, Vector2.zero);
                    vertexHelper.AddVert(marker.position + new Vector2(half, 0f), tint, Vector2.zero);
                    vertexHelper.AddVert(marker.position + new Vector2(0f, -half), tint, Vector2.zero);
                    vertexHelper.AddVert(marker.position + new Vector2(-half, 0f), tint, Vector2.zero);
                    vertexHelper.AddTriangle(start, start + 1, start + 2);
                    vertexHelper.AddTriangle(start + 2, start + 3, start);
                }
            }
        }

        private static void AddTriangle(VertexHelper vertexHelper, Vector2 a, Vector2 b, Vector2 c,
            Color32 tint)
        {
            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(a, tint, Vector2.zero);
            vertexHelper.AddVert(b, tint, Vector2.zero);
            vertexHelper.AddVert(c, tint, Vector2.zero);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
        }

        /// <summary>Clockwise, so a heading of 90 degrees points a marker to the right of the panel.</summary>
        private static Vector2 Rotate(Vector2 offset, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(offset.x * cos + offset.y * sin, offset.y * cos - offset.x * sin);
        }

        private struct Marker
        {
            public Vector2 position;
            public float headingDegrees;
            public float size;
            public Color tint;
            public bool arrow;
        }
    }
}
