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
            EditorGUILayout.LabelField("Level Grid Layout Editor", EditorStyles.boldLabel);

            if (GUILayout.Button("Rebuild Grid Config Matrix"))
            {
                Undo.RecordObject(spawner, "Rebuild Grid Configs");
                spawner.GenerateGridConfigs(manager.Rows, manager.Columns);
                EditorUtility.SetDirty(spawner);
            }

            if (spawner.SpawnConfigs == null || spawner.SpawnConfigs.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(5);

            for (int r = manager.Rows - 1; r >= 0; r--)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"Row {r} {(r == 0 ? "(Active Edge Layer)" : "")}", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();

                for (int c = 0; c < manager.Columns; c++)
                {
                    CustomerSpawnConfig config = GetConfig(spawner, r, c);
                    if (config == null)
                    {
                        continue;
                    }

                    EditorGUILayout.BeginVertical("helpbox", GUILayout.Width(80));
                    config.isEnabled = EditorGUILayout.Toggle($"Col {c}", config.isEnabled);

                    if (config.isEnabled)
                    {
                        config.itemData = (ItemDataSO)EditorGUILayout.ObjectField(config.itemData, typeof(ItemDataSO), false, GUILayout.Width(70));
                    }

                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(spawner);
            }
        }

        private CustomerSpawnConfig GetConfig(CrowdTestSpawner spawner, int row, int col)
        {
            return spawner.SpawnConfigs.Find(x => x.row == row && x.column == col);
        }
    }
}