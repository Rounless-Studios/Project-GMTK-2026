using UnityEngine;

namespace GMTK.TrackAuthoring
{
    /// <summary>
    /// Editor-only visible proxy for one serialized track control point.
    /// The Editor creates these as real sphere objects so Unity can render, pick,
    /// frame, and move them without relying on Scene-view gizmo drawing.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class RaceTrackControlPointHandle : MonoBehaviour
    {
        [SerializeField, HideInInspector] private RaceTrackAuthoring owner;
        [SerializeField, HideInInspector] private int pointIndex;

        public RaceTrackAuthoring Owner => owner;
        public int PointIndex => pointIndex;

        public void Configure(RaceTrackAuthoring newOwner, int newIndex)
        {
            owner = newOwner;
            pointIndex = newIndex;
            name = $"CONTROL POINT {pointIndex:00}";
            ApplyColor();
        }

        public void SnapToAuthoringData()
        {
            if (owner == null || pointIndex < 0 || pointIndex >= owner.ControlPoints.Count)
                return;

            transform.localPosition = owner.ControlPoints[pointIndex].position;
            transform.localRotation = Quaternion.identity;
            transform.hasChanged = false;
            ApplyColor();
        }

        private void Update()
        {
            if (Application.isPlaying || owner == null || !transform.hasChanged)
                return;
            if (pointIndex < 0 || pointIndex >= owner.ControlPoints.Count)
                return;

            TrackControlPoint point = owner.ControlPoints[pointIndex];
            point.position = transform.localPosition;
            owner.SetControlPoint(pointIndex, point);
            transform.hasChanged = false;
            owner.RefreshPreviewLine();
            ApplyColor();
        }

        private void OnValidate() => ApplyColor();

        private void ApplyColor()
        {
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer == null) return;

            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Color cyan = new(0f, 1f, 1f, 1f);
            properties.SetColor("_BaseColor", cyan);
            properties.SetColor("_Color", cyan);
            properties.SetColor("_EmissionColor", cyan * 2f);
            renderer.SetPropertyBlock(properties);
        }
    }
}
