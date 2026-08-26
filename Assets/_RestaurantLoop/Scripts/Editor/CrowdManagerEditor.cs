using UnityEditor;
using UnityEngine;
using RestaurantLoop.Core;

namespace RestaurantLoop.EditorExtensions
{
    [CustomEditor(typeof(CrowdManager))]
    public class CrowdManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();

            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }

            GUILayout.Space(6f);
            if (GUILayout.Button("Refresh Dining Board"))
            {
                CrowdManager crowdManager = (CrowdManager)target;
                crowdManager.InitializeGridSplineMapping();
                EditorUtility.SetDirty(crowdManager);
            }
        }
    }
}