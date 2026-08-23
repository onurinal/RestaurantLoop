using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RestaurantLoop.Core;

namespace RestaurantLoop.EditorTools
{
    /// <summary>
    /// Custom Editor Window for batch generating solvable LevelDataSO assets using 5-step quantized stack bounds.
    /// </summary>
    public class LevelGeneratorWindow : EditorWindow
    {
        [SerializeField] private List<ItemDataSO> availableItems = new List<ItemDataSO>();
        [SerializeField] private int totalLevelsToGenerate = 30;
        [SerializeField] private string outputFolder = "Assets/_RestaurantLoop/Levels";

        [Header("Layout Constraints")]
        [SerializeField] private int defaultColumnCount = 3;
        [SerializeField] private int minRackSlots = 4;
        [SerializeField] private int maxRackSlots = 6;

        [Header("Stack Size Constraints (Multiples of 5)")]
        [SerializeField] private int minStackSize = 10;
        [SerializeField] private int maxStackSize = 40;

        private SerializedObject serializedObject;
        private SerializedProperty availableItemsProp;
        private Vector2 scrollPosition;

        [MenuItem("Tools/Restaurant Loop/Level Generator")]
        public static void ShowWindow()
        {
            LevelGeneratorWindow window = GetWindow<LevelGeneratorWindow>("Level Generator");
            window.minSize = new Vector2(350, 560);
        }

        private void OnEnable()
        {
            serializedObject = new SerializedObject(this);
            availableItemsProp = serializedObject.FindProperty("availableItems");
        }

        private void OnGUI()
        {
            if (serializedObject == null)
            {
                serializedObject = new SerializedObject(this);
                availableItemsProp = serializedObject.FindProperty("availableItems");
            }

            serializedObject.Update();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Label("Level Generator Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            totalLevelsToGenerate = EditorGUILayout.IntField("Total Levels", totalLevelsToGenerate);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

            EditorGUILayout.Space();
            GUILayout.Label("Layout Constraints", EditorStyles.boldLabel);
            defaultColumnCount = EditorGUILayout.IntSlider("Queue Column Count", defaultColumnCount, 1, 6);
            minRackSlots = EditorGUILayout.IntField("Min Rack Slots", minRackSlots);
            maxRackSlots = EditorGUILayout.IntField("Max Rack Slots", maxRackSlots);

            EditorGUILayout.Space();
            GUILayout.Label("Stack Size Limits (Step: 5)", EditorStyles.boldLabel);
            minStackSize = EditorGUILayout.IntField("Min Stack Size", minStackSize);
            maxStackSize = EditorGUILayout.IntField("Max Stack Size", maxStackSize);

            // Force GUI input validation to 5-step bounds
            minStackSize = Mathf.Max(5, Mathf.RoundToInt((float)minStackSize / 5f) * 5);
            maxStackSize = Mathf.Max(minStackSize, Mathf.RoundToInt((float)maxStackSize / 5f) * 5);

            if (minRackSlots < 1) minRackSlots = 1;
            if (maxRackSlots < minRackSlots) maxRackSlots = minRackSlots;

            EditorGUILayout.Space();
            GUILayout.Label("Available Colors / Items", EditorStyles.boldLabel);

            if (availableItemsProp != null)
            {
                EditorGUILayout.PropertyField(availableItemsProp, new GUIContent("Item Data SOs"), true);
            }

            EditorGUILayout.Space(20);

            if (GUILayout.Button("Generate All Levels", GUILayout.Height(40)))
            {
                GenerateBatchLevels();
            }

            EditorGUILayout.EndScrollView();
            serializedObject.ApplyModifiedProperties();
        }

        private void GenerateBatchLevels()
        {
            if (availableItems == null || availableItems.Count < 2)
            {
                EditorUtility.DisplayDialog("Error", "Please assign at least 2 ItemDataSO assets!", "OK");
                return;
            }

            EnsureFolderExists(outputFolder);

            for (int i = 1; i <= totalLevelsToGenerate; i++)
            {
                LevelDataSO level = CreateInstance<LevelDataSO>();

                int colorCount = Mathf.Clamp(2 + (i / 8), 2, availableItems.Count);

                // Base demand progressive step curve (multiples of 5)
                int totalDemand = 20 + (i * 10);
                int currentLevelMaxStack = Mathf.Clamp(minStackSize + ((i / 3) * 5), minStackSize, maxStackSize);

                level.activeEdgeSlotCount = Mathf.Clamp(5 + (i / 10), 5, 8);
                level.rackSlotCount = Mathf.Clamp(minRackSlots + (i / 10), minRackSlots, maxRackSlots);
                level.columnCount = defaultColumnCount;

                level.minStackSize = minStackSize;
                level.maxStackSize = currentLevelMaxStack;

                // Guarantee color demand is strictly quantized to a multiple of 5
                int rawDemandPerColor = totalDemand / colorCount;
                int demandPerColor = Mathf.Max(minStackSize, Mathf.RoundToInt((float)rawDemandPerColor / 5f) * 5);

                level.stationConfigs = new List<StationLevelConfig>();

                for (int c = 0; c < colorCount; c++)
                {
                    if (availableItems[c] != null)
                    {
                        level.stationConfigs.Add(new StationLevelConfig
                        {
                            itemData = availableItems[c],
                            totalCustomerCount = demandPerColor
                        });
                    }
                }

                level.queueStackConfigs = LevelMathUtility.PartitionDemandToStacks(
                    level.stationConfigs,
                    minStackSize,
                    currentLevelMaxStack,
                    level.columnCount,
                    out int calculatedRows);

                level.calculatedRowCount = calculatedRows;

                string assetPath = $"{outputFolder}/Level_{i:D2}.asset";
                AssetDatabase.CreateAsset(level, assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"{totalLevelsToGenerate} levels generated in {outputFolder}", "OK");
        }

        private void EnsureFolderExists(string folderPath)
        {
            string[] folders = folderPath.Split('/');
            string currentPath = folders[0];

            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }

                currentPath = nextPath;
            }
        }
    }
}