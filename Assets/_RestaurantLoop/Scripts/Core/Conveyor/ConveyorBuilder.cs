using System.Collections.Generic;
using UnityEngine;
using Dreamteck.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RestaurantLoop.Core
{
    [ExecuteAlways]
    public class ConveyorBuilder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SplineComputer splineComputer;
        [SerializeField] private SplineMesh splineMesh;

        [Header("Dimensions")]
        [SerializeField] private float width = 8f;
        [SerializeField] private float height = 12f;
        [SerializeField] private float cornerRadius = 2f;

        [Header("Gap & Closed Loop Settings")]
        [SerializeField] private bool isClosedLoop = false; // False to keep corner open
        [SerializeField] private float bottomEdgeGap = 1.5f; // Trimming offset along the bottom edge (X)
        [SerializeField] private float leftEdgeGap = 1.5f;   // Trimming offset along the left edge (Z)

        [Header("Spline Point Visuals & Offsets")]
        [SerializeField] private float pointSize = 1.5f;
        [SerializeField] private float yOffset = 0f;
        [SerializeField] private float zOffset = 0f;

        [Header("Sampling Settings")]
        [Range(2, 32)] [SerializeField] private int arcSegments = 12;
        [SerializeField] private float lineStep = 1f;

        public SplineComputer Spline => splineComputer;
        public float Width => width;
        public float Height => height;
        public float YOffset => yOffset;
        public float ZOffset => zOffset;

        public Vector3 CenterPosition => transform.position + new Vector3(0f, yOffset, zOffset);

        private void OnValidate()
        {
            if (splineComputer == null) splineComputer = GetComponentInChildren<SplineComputer>();
            if (splineMesh == null) splineMesh = GetComponentInChildren<SplineMesh>();

            RebuildConveyor();
        }

        [ContextMenu("Rebuild Conveyor")]
        public void RebuildConveyor()
        {
            if (splineComputer == null) return;

            splineComputer.space = SplineComputer.Space.Local;

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            float r = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(halfW, halfH));

            Vector3 center = new Vector3(0f, yOffset, zOffset);

            Vector3 brCenter = new Vector3(center.x + halfW - r, center.y, center.z - halfH + r);
            Vector3 trCenter = new Vector3(center.x + halfW - r, center.y, center.z + halfH - r);
            Vector3 tlCenter = new Vector3(center.x - halfW + r, center.y, center.z + halfH - r);
            Vector3 blCenter = new Vector3(center.x - halfW + r, center.y, center.z - halfH + r);

            List<Vector3> points = new List<Vector3>();

            // Calculate bottom start offset independently using bottomEdgeGap
            float startOffsetX = isClosedLoop ? 0f : Mathf.Min(bottomEdgeGap, halfW - r);
            Vector3 start = new Vector3(center.x - halfW + r + startOffsetX, center.y, center.z - halfH);
            points.Add(start);

            // 1) Bottom edge & bottom-right arc
            AddLineSampled(points, start, new Vector3(center.x + halfW - r, center.y, center.z - halfH), lineStep);
            AddArc(points, brCenter, r, -90f, 0f, arcSegments);

            // 2) Right edge & top-right arc
            AddLineSampled(points, new Vector3(center.x + halfW, center.y, center.z - halfH + r), new Vector3(center.x + halfW, center.y, center.z + halfH - r), lineStep);
            AddArc(points, trCenter, r, 0f, 90f, arcSegments);

            // 3) Top edge & top-left arc
            AddLineSampled(points, new Vector3(center.x + halfW - r, center.y, center.z + halfH), new Vector3(center.x - halfW + r, center.y, center.z + halfH), lineStep);
            AddArc(points, tlCenter, r, 90f, 180f, arcSegments);

            // 4) Left edge (stopping short independently using leftEdgeGap)
            float endOffsetY = isClosedLoop ? 0f : Mathf.Min(leftEdgeGap, halfH - r);
            Vector3 leftEdgeEnd = new Vector3(center.x - halfW, center.y, center.z - halfH + r + endOffsetY);
            AddLineSampled(points, new Vector3(center.x - halfW, center.y, center.z + halfH - r), leftEdgeEnd, lineStep);

            // Include bottom-left arc only if closed loop
            if (isClosedLoop)
            {
                AddArc(points, blCenter, r, 180f, 270f, arcSegments);
            }

            SplinePoint[] splinePoints = new SplinePoint[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                splinePoints[i] = new SplinePoint
                {
                    position = points[i],
                    normal = Vector3.up,
                    size = pointSize,
                    color = Color.white
                };
            }

            splineComputer.SetPoints(splinePoints);
            splineComputer.type = Dreamteck.Splines.Spline.Type.Linear;

            if (isClosedLoop)
            {
                splineComputer.Close();
            }
            else
            {
                splineComputer.Break();
            }

            splineComputer.RebuildImmediate();

            if (splineMesh != null)
            {
                splineMesh.RebuildImmediate();
            }
        }

        private static void AddLineSampled(List<Vector3> pts, Vector3 from, Vector3 to, float step)
        {
            float dist = Vector3.Distance(from, to);
            int segments = Mathf.Max(1, Mathf.CeilToInt(dist / Mathf.Max(0.0001f, step)));

            for (int i = 1; i <= segments; i++)
            {
                pts.Add(Vector3.Lerp(from, to, i / (float)segments));
            }
        }

        private static void AddArc(List<Vector3> pts, Vector3 center, float radius, float startDeg, float endDeg, int segments)
        {
            segments = Mathf.Max(1, segments);
            float startRad = startDeg * Mathf.Deg2Rad;
            float endRad = endDeg * Mathf.Deg2Rad;

            for (int i = 1; i <= segments; i++)
            {
                float a = Mathf.Lerp(startRad, endRad, i / (float)segments);
                pts.Add(new Vector3(center.x + Mathf.Cos(a) * radius, center.y, center.z + Mathf.Sin(a) * radius));
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ConveyorBuilder))]
    public class RoundedRectConveyorBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ConveyorBuilder builder = (ConveyorBuilder)target;

            GUILayout.Space(10);
            if (GUILayout.Button("Rebuild Conveyor", GUILayout.Height(30)))
            {
                builder.RebuildConveyor();
            }
        }
    }
#endif
}