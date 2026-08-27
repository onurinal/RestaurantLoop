using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RestaurantLoop.Core;

namespace RestaurantLoop.EditorTools
{
    public class LevelGeneratorWindow : EditorWindow
    {
        [SerializeField] private List<ItemDataSO> availableItems = new List<ItemDataSO>();
        [SerializeField] private int totalLevelsToGenerate = 30;
        [SerializeField] private string outputFolder = "Assets/_RestaurantLoop/Levels";

        [Header("Difficulty & Demand Curve (25 to 120)")]
        [Tooltip("Total customer count for Level 1 (Shallow crowd).")]
        [SerializeField] private int minTotalCustomers = 25;
        [Tooltip("Total customer count for Level 30 (Deep crowd).")]
        [SerializeField] private int maxTotalCustomers = 120;

        [Header("Active Edge Setup (Strict Range: 3 to 20)")]
        [Range(3, 20)] [SerializeField] private int minActiveEdgeSlots = 6;
        [Range(3, 20)] [SerializeField] private int maxActiveEdgeSlots = 10;

        [Header("Layout Constraints")]
        [SerializeField] private int defaultColumnCount = 4;
        [SerializeField] private int minRackSlots = 5;
        [SerializeField] private int maxRackSlots = 7;

        [Header("Stack Size Constraints (Step: 5)")]
        [SerializeField] private int minStackSize = 10;
        [SerializeField] private int maxStackSize = 40;

        private SerializedObject serializedObject;
        private SerializedProperty availableItemsProp;
        private Vector2 scrollPosition;

        [MenuItem("Tools/Restaurant Loop/Level Generator")]
        public static void ShowWindow()
        {
            LevelGeneratorWindow window = GetWindow<LevelGeneratorWindow>("Level Generator");
            window.minSize = new Vector2(350, 600);
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
            GUILayout.Label("Difficulty Curve (Shallow to Deep Crowd)", EditorStyles.boldLabel);
            minTotalCustomers = EditorGUILayout.IntField("Min Customers (Lvl 1)", minTotalCustomers);
            maxTotalCustomers = EditorGUILayout.IntField("Max Customers (Lvl 30)", maxTotalCustomers);

            minTotalCustomers = Mathf.Max(15, Mathf.RoundToInt((float)minTotalCustomers / 5f) * 5);
            maxTotalCustomers = Mathf.Max(minTotalCustomers, Mathf.RoundToInt((float)maxTotalCustomers / 5f) * 5);

            EditorGUILayout.Space();
            GUILayout.Label("Active Edge Slots Scaling (Min: 3, Max: 20)", EditorStyles.boldLabel);
            minActiveEdgeSlots = EditorGUILayout.IntSlider("Min Edge Slots", minActiveEdgeSlots, 3, 20);
            maxActiveEdgeSlots = EditorGUILayout.IntSlider("Max Edge Slots", maxActiveEdgeSlots, minActiveEdgeSlots, 20);

            minActiveEdgeSlots = Mathf.Clamp(minActiveEdgeSlots, 3, 20);
            maxActiveEdgeSlots = Mathf.Clamp(maxActiveEdgeSlots, minActiveEdgeSlots, 20);

            EditorGUILayout.Space();
            GUILayout.Label("Layout & Rack Constraints", EditorStyles.boldLabel);
            defaultColumnCount = EditorGUILayout.IntSlider("Queue Column Count", defaultColumnCount, 1, 6);
            minRackSlots = EditorGUILayout.IntField("Min Rack Slots", minRackSlots);
            maxRackSlots = EditorGUILayout.IntField("Max Rack Slots", maxRackSlots);

            EditorGUILayout.Space();
            GUILayout.Label("Stack Size Limits (Step: 5)", EditorStyles.boldLabel);
            minStackSize = EditorGUILayout.IntField("Min Stack Size", minStackSize);
            maxStackSize = EditorGUILayout.IntField("Max Stack Size", maxStackSize);

            minStackSize = Mathf.Max(5, Mathf.RoundToInt((float)minStackSize / 5f) * 5);
            maxStackSize = Mathf.Max(minStackSize, Mathf.RoundToInt((float)maxStackSize / 5f) * 5);

            if (minRackSlots < 1) minRackSlots = 1;
            if (maxRackSlots < minRackSlots) maxRackSlots = minRackSlots;

            EditorGUILayout.Space();
            GUILayout.Label("Available Colors / Items (3 to 6 types)", EditorStyles.boldLabel);

            if (availableItemsProp != null)
            {
                EditorGUILayout.PropertyField(availableItemsProp, new GUIContent("Item Data SOs"), true);
            }

            EditorGUILayout.Space(20);

            if (GUILayout.Button("Generate & Validate All Levels", GUILayout.Height(40)))
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
            int invalidLevelCount = 0;

            for (int i = 1; i <= totalLevelsToGenerate; i++)
            {
                LevelDataSO level = CreateInstance<LevelDataSO>();

                int colorCount = Mathf.Clamp(3 + (i / 7), 3, Mathf.Min(6, availableItems.Count));

                float progress = (float)(i - 1) / Mathf.Max(1, totalLevelsToGenerate - 1);
                int targetTotalDemand = Mathf.RoundToInt(Mathf.Lerp(minTotalCustomers, maxTotalCustomers, progress));
                targetTotalDemand = Mathf.Max(minStackSize * colorCount, Mathf.RoundToInt((float)targetTotalDemand / 5f) * 5);

                int currentLevelMaxStack = Mathf.Clamp(minStackSize + ((i / 3) * 5), minStackSize, maxStackSize);

                int activeEdgeSlots = Mathf.RoundToInt(Mathf.Lerp(minActiveEdgeSlots, maxActiveEdgeSlots, progress));
                level.activeEdgeSlotCount = Mathf.Clamp(activeEdgeSlots, 3, 20);

                level.rackSlotCount = Mathf.Clamp(minRackSlots + (i / 10), minRackSlots, maxRackSlots);
                level.columnCount = defaultColumnCount;
                level.minStackSize = minStackSize;
                level.maxStackSize = currentLevelMaxStack;

                int baseDemandPerColor = Mathf.RoundToInt((float)(targetTotalDemand / colorCount) / 5f) * 5;
                baseDemandPerColor = Mathf.Max(minStackSize, baseDemandPerColor);

                level.customerDemands = new List<CustomerDemandConfig>();

                for (int c = 0; c < colorCount; c++)
                {
                    if (availableItems[c] != null)
                    {
                        level.customerDemands.Add(new CustomerDemandConfig
                        {
                            itemData = availableItems[c],
                            totalCustomerCount = baseDemandPerColor
                        });
                    }
                }

                // Retry loop to guarantee a 100% solvable stack layout shuffle
                bool isValid = false;
                int maxRetries = 100;

                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    level.queueStackConfigs = LevelMathUtility.PartitionDemandToStacks(
                        level.customerDemands,
                        minStackSize,
                        currentLevelMaxStack,
                        level.columnCount,
                        out int calculatedRows);

                    level.calculatedRowCount = calculatedRows;

                    isValid = LevelValidator.ValidateLevel(level);
                    if (isValid) break;
                }

                if (!isValid) invalidLevelCount++;

                string assetPath = $"{outputFolder}/Level_{i:D2}.asset";
                AssetDatabase.CreateAsset(level, assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string statusMessage = invalidLevelCount == 0
                ? $"{totalLevelsToGenerate} levels generated and 100% verified solvable!"
                : $"{totalLevelsToGenerate} levels generated. Warning: {invalidLevelCount} levels failed greedy validation!";

            EditorUtility.DisplayDialog("Generation Complete", statusMessage, "OK");
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