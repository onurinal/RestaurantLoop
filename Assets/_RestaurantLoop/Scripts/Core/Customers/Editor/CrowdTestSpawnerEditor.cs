using UnityEditor;
using UnityEngine;

namespace RestaurantLoop.Core
{
    [CustomEditor(typeof(CrowdTestSpawner))]
    public class CrowdTestSpawnerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            CrowdTestSpawner spawner = (CrowdTestSpawner)target;
            CrowdManager manager = FindFirstObjectByType<CrowdManager>();

            if (manager == null)
            {
                EditorGUILayout.HelpBox("CrowdManager not found in scene!", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Re-Spawn Stations (Runtime Test)", GUILayout.Height(30)))
            {
                spawner.InitializeStations();
            }
        }
    }
}