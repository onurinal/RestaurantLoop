using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RestaurantLoop.Core;

namespace RestaurantLoop.EditorTools
{
    public sealed class LevelGeneratorWindow : EditorWindow
    {
        private const string DefaultBatchOutputFolder = "Assets/_RestaurantLoop/Data/Levels";
        private const int MaxSolvabilityAttemptsPerLevel = 5000;
        private static readonly string[] GeneratorTabs =
            { "Single Level Generator", "Batch Level Generator", "Difficulty Presets" };

        private enum DifficultyPresetTier
        {
            Easy,
            Medium,
            Hard,
            VeryHard
        }

        private enum ValidationStatus
        {
            NotValidated,
            SolvableWithoutPowerUps,
            SolvableWithPowerUps,
            InvalidOrUnsolvable
        }

        [SerializeField] private LevelDataSO targetLevel;
        [SerializeField] private int randomSeed;
        [SerializeField] private List<CustomerDemandConfig> demandConfigs = new List<CustomerDemandConfig>();

        [Header("Custom Sequence Settings")]
        [SerializeField] private bool useCustomSequence;
        [SerializeField] private List<ItemDataSO> customCustomerSequence = new List<ItemDataSO>();

        [SerializeField, Range(3, 20)] private int activeEdgeSlotCount = 6;
        [SerializeField, Range(2, 4)] private int columnCount = 3;
        [SerializeField, Range(1, 15)] private int rowCount = 3;
        [SerializeField] private int minStackSize = 10;
        [SerializeField] private int maxStackSize = 40;

        [SerializeField] private int selectedTab;
        [SerializeField] private string batchOutputFolder = DefaultBatchOutputFolder;
        [SerializeField] private int totalLevelsToGenerate = 30;
        [SerializeField] private int startingLevelNumber = 1;
        [SerializeField] private int baseRandomSeed;
        [SerializeField] private List<ItemDataSO> availableItems = new List<ItemDataSO>();
        [SerializeField] private AnimationCurve batchDifficultyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("MVP Progression Preset")]
        [Tooltip("The approved 30-level progression rises overall but eases every third level. Use the menu command to restore its defaults.")]
        [SerializeField] private bool useMvpSawtoothPreset = true;

        // Demand (Min / Max per tier)
        [SerializeField] private int batchEarlyMinDemand = 20;
        [SerializeField] private int batchEarlyMaxDemand = 40;
        [SerializeField] private int batchMidMinDemand = 45;
        [SerializeField] private int batchMidMaxDemand = 75;
        [SerializeField] private int batchLateMinDemand = 80;
        [SerializeField] private int batchLateMaxDemand = 125;

        // Active Edge Slots (Min / Max per tier)
        [SerializeField] private int batchEarlyMinActiveEdgeSlots = 4;
        [SerializeField] private int batchEarlyMaxActiveEdgeSlots = 6;
        [SerializeField] private int batchMidMinActiveEdgeSlots = 6;
        [SerializeField] private int batchMidMaxActiveEdgeSlots = 8;
        [SerializeField] private int batchLateMinActiveEdgeSlots = 8;
        [SerializeField] private int batchLateMaxActiveEdgeSlots = 12;

        // Food Variety (Min / Max per tier)
        [SerializeField] private int batchEarlyMinFoodTypes = 2;
        [SerializeField] private int batchEarlyMaxFoodTypes = 3;
        [SerializeField] private int batchMidMinFoodTypes = 3;
        [SerializeField] private int batchMidMaxFoodTypes = 4;
        [SerializeField] private int batchLateMinFoodTypes = 5;
        [SerializeField] private int batchLateMaxFoodTypes = 6;

        // Stack Sizes (Min / Max per tier)
        [SerializeField] private int batchEarlyMinStackSize = 5;
        [SerializeField] private int batchEarlyMaxStackSize = 10;
        [SerializeField] private int batchMidMinStackSize = 5;
        [SerializeField] private int batchMidMaxStackSize = 15;
        [SerializeField] private int batchLateMinStackSize = 5;
        [SerializeField] private int batchLateMaxStackSize = 20;

        // Timed Customers (levels before batchTimedCustomerStartLevel always use zero)
        [SerializeField, Min(1)] private int batchTimedCustomerStartLevel = 15;
        [SerializeField, Min(0)] private int batchMidMinTimedCustomers = 1;
        [SerializeField, Min(0)] private int batchMidMaxTimedCustomers = 2;
        [SerializeField, Min(0)] private int batchLateMinTimedCustomers = 3;
        [SerializeField, Min(0)] private int batchLateMaxTimedCustomers = 6;
        [SerializeField, Min(1)] private int batchTimedCustomerMinDuration = 12;
        [SerializeField, Min(1)] private int batchTimedCustomerMaxDuration = 20;

        // Queue Layout Constraints (Global Min / Max)
        [SerializeField] private int batchMinQueueColumns = 2;
        [SerializeField] private int batchMaxQueueColumns = 4;
        [SerializeField] private int batchMinQueueRows = 2;
        [SerializeField] private int batchMaxQueueRows = 8;

        [SerializeField] private bool overwriteExistingAssets;
        [SerializeField] private bool assignToOpenLevelManager;

        private readonly List<ItemDataSO> sequencePreview = new List<ItemDataSO>();
        private SerializedObject windowSerializedObject;
        private SerializedProperty demandConfigsProperty;
        private SerializedProperty availableItemsProperty;
        private SerializedProperty customCustomerSequenceProperty;

        private Vector2 mainScrollPosition;
        private Vector2 batchScrollPosition;
        private Vector2 presetScrollPosition;
        private Vector2 sequenceScrollPosition;
        private ValidationStatus validationStatus;
        private string validationMessage = "Generate or select a level, then validate it.";
        private MessageType batchMessageType = MessageType.Info;
        private string batchMessage = "Configure the batch ranges, then generate deterministic level assets.";
        private MessageType presetMessageType = MessageType.Info;
        private string presetMessage = "Choose a target asset and generate an official difficulty preset.";

        [MenuItem("Tools/RestaurantLoop/Level Generator")]
        public static void ShowWindow()
        {
            LevelGeneratorWindow window = GetWindow<LevelGeneratorWindow>("Level Generator");
            window.minSize = new Vector2(480f, 650f);
        }

        [MenuItem("Tools/RestaurantLoop/Rebuild MVP Levels 1-30")]
        private static void RebuildMvpLevels()
        {
            LevelGeneratorWindow window = GetWindow<LevelGeneratorWindow>("Level Generator");
            window.ApplyMvpSawtoothPreset();
            window.GenerateBatchLevels();
        }

        private void OnEnable()
        {
            InitializeSerializedProperties();
        }

        private void OnGUI()
        {
            InitializeSerializedProperties();

            selectedTab = GUILayout.Toolbar(selectedTab, GeneratorTabs, GUILayout.Height(26f));
            EditorGUILayout.Space(6f);

            if (selectedTab == 0)
            {
                DrawSingleLevelTab();
            }
            else if (selectedTab == 1)
            {
                DrawBatchLevelTab();
            }
            else
            {
                DrawDifficultyPresetsTab();
            }
        }

        private void DrawSingleLevelTab()
        {
            mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition);
            EditorGUILayout.LabelField("Deterministic Level Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            DrawTargetControls();
            DrawGenerationInputs();
            DrawActions();
            DrawValidationStatus();

            if (targetLevel != null)
            {
                DrawQueuePreview(targetLevel);
                DrawCustomerSequencePreview(targetLevel);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBatchLevelTab()
        {
            batchScrollPosition = EditorGUILayout.BeginScrollView(batchScrollPosition);
            EditorGUILayout.LabelField("Batch Level Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            DrawBatchOutputSettings();
            DrawBatchAvailableItems();
            DrawBatchProgressionSettings();

            EditorGUILayout.Space(10f);
            if (GUILayout.Button("Apply MVP Sawtooth Defaults"))
            {
                ApplyMvpSawtoothPreset();
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button($"Generate All {Mathf.Max(1, totalLevelsToGenerate)} Levels", GUILayout.Height(38f)))
            {
                GenerateBatchLevels();
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(batchMessage, batchMessageType);
            EditorGUILayout.EndScrollView();
        }

        private void DrawDifficultyPresetsTab()
        {
            presetScrollPosition = EditorGUILayout.BeginScrollView(presetScrollPosition);
            EditorGUILayout.LabelField("Official Difficulty Presets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Each preset creates an exact five-step queue, derives rows automatically, restricts timed " +
                "customers to foods visible in queue rows 1–2, and saves only after strict no-booster validation.",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            DrawTargetControls();
            randomSeed = EditorGUILayout.IntField("Preset Random Seed", randomSeed);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Available Food Types", EditorStyles.boldLabel);
            windowSerializedObject.Update();
            EditorGUILayout.PropertyField(availableItemsProperty, new GUIContent("Food Items"), true);
            windowSerializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("Find All Food Items"))
            {
                availableItems = FindAllFoodItems();
                InitializeSerializedProperties(force: true);
            }

            EditorGUILayout.Space(10f);
            DrawPresetButton("Generate Easy Level", DifficultyPresetTier.Easy,
                "25–40 demand · 2–3 foods · 2–3 columns · 5–8 edge slots · 0 timed");
            DrawPresetButton("Generate Medium Level", DifficultyPresetTier.Medium,
                "45–75 demand · 3–4 foods · 3 columns · 8–12 edge slots · 3–6 timed at 15–20s");
            DrawPresetButton("Generate Hard Level", DifficultyPresetTier.Hard,
                "80–100 demand · 4–5 foods · 3–4 columns · 10–15 edge slots · 7–12 timed at 12–18s");
            DrawPresetButton("Generate Very Hard Level", DifficultyPresetTier.VeryHard,
                "105–125 demand · 5–6 foods · 4 columns · 14–20 edge slots · 15–20 timed at 10–15s");

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(presetMessage, presetMessageType);
            DrawValidationStatus();

            if (targetLevel != null)
            {
                DrawQueuePreview(targetLevel);
                DrawCustomerSequencePreview(targetLevel);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawPresetButton(string label, DifficultyPresetTier tier, string description)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button(label, GUILayout.Height(36f))) GenerateDifficultyPreset(tier);
            }
        }

        private void DrawBatchOutputSettings()
        {
            EditorGUILayout.LabelField("Output Settings", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                batchOutputFolder = EditorGUILayout.TextField("Target Save Folder", batchOutputFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(70f))) BrowseForBatchOutputFolder();
            }

            totalLevelsToGenerate = Mathf.Max(1, EditorGUILayout.IntField("Total Levels", totalLevelsToGenerate));
            startingLevelNumber = Mathf.Max(1, EditorGUILayout.IntField("Starting Level Number", startingLevelNumber));
            baseRandomSeed = EditorGUILayout.IntField(
                new GUIContent("Base Random Seed", "Level index and retry offset are added to this seed."),
                baseRandomSeed);
            overwriteExistingAssets = EditorGUILayout.Toggle(
                new GUIContent("Update Existing Assets", "Preserves existing asset GUIDs while replacing generated level data."),
                overwriteExistingAssets);
            assignToOpenLevelManager = EditorGUILayout.Toggle(
                new GUIContent("Assign To Level Manager", "Replaces the open scene LevelManager's serialized level sequence."),
                assignToOpenLevelManager);
        }

        private void DrawBatchAvailableItems()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Available Items", EditorStyles.boldLabel);
            windowSerializedObject.Update();
            EditorGUILayout.PropertyField(availableItemsProperty, new GUIContent("Food Items"), true);
            windowSerializedObject.ApplyModifiedProperties();
        }

        private void ApplyMvpSawtoothPreset()
        {
            batchOutputFolder = DefaultBatchOutputFolder;
            totalLevelsToGenerate = 30;
            startingLevelNumber = 1;
            baseRandomSeed = 20260903;
            overwriteExistingAssets = true;
            assignToOpenLevelManager = false;
            useMvpSawtoothPreset = true;

            batchDifficultyCurve = CreateMvpSawtoothCurve();

            batchEarlyMinDemand = 25;
            batchEarlyMaxDemand = 40;
            batchMidMinDemand = 55;
            batchMidMaxDemand = 80;
            batchLateMinDemand = 90;
            batchLateMaxDemand = 125;

            batchEarlyMinActiveEdgeSlots = 4;
            batchEarlyMaxActiveEdgeSlots = 6;
            batchMidMinActiveEdgeSlots = 6;
            batchMidMaxActiveEdgeSlots = 9;
            batchLateMinActiveEdgeSlots = 8;
            batchLateMaxActiveEdgeSlots = 12;

            batchEarlyMinFoodTypes = 3;
            batchEarlyMaxFoodTypes = 3;
            batchMidMinFoodTypes = 3;
            batchMidMaxFoodTypes = 5;
            batchLateMinFoodTypes = 5;
            batchLateMaxFoodTypes = 6;

            batchEarlyMinStackSize = 5;
            batchEarlyMaxStackSize = 15;
            batchMidMinStackSize = 5;
            batchMidMaxStackSize = 20;
            batchLateMinStackSize = 5;
            batchLateMaxStackSize = 25;

            batchTimedCustomerStartLevel = 15;
            batchMidMinTimedCustomers = 1;
            batchMidMaxTimedCustomers = 2;
            batchLateMinTimedCustomers = 3;
            batchLateMaxTimedCustomers = 5;
            batchTimedCustomerMinDuration = 12;
            batchTimedCustomerMaxDuration = 20;

            batchMinQueueColumns = 3;
            batchMaxQueueColumns = 4;
            batchMinQueueRows = 1;
            batchMaxQueueRows = 8;
            availableItems = FindAllFoodItems();
            NormalizeBatchRanges();
        }

        private static AnimationCurve CreateMvpSawtoothCurve()
        {
            const int levelCount = 30;
            Keyframe[] keys = new Keyframe[levelCount];
            for (int levelIndex = 0; levelIndex < levelCount; levelIndex++)
            {
                float linearProgress = levelIndex / (float)(levelCount - 1);
                float sawtoothProgress = GetMvpSawtoothProgress(levelIndex, linearProgress);
                keys[levelIndex] = new Keyframe(linearProgress, sawtoothProgress);
            }

            return new AnimationCurve(keys);
        }

        private static List<ItemDataSO> FindAllFoodItems()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemDataSO");
            Array.Sort(guids, StringComparer.Ordinal);

            List<ItemDataSO> items = new List<ItemDataSO>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                ItemDataSO item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (item != null) items.Add(item);
            }

            return items;
        }

        private void DrawBatchProgressionSettings()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Early / Mid / Late Difficulty Progression", EditorStyles.boldLabel);
            useMvpSawtoothPreset = EditorGUILayout.Toggle(
                new GUIContent("Use MVP Sawtooth", "Rising progression with periodic relief levels. The preset is tuned for Levels 1-30."),
                useMvpSawtoothPreset);
            if (useMvpSawtoothPreset)
            {
                EditorGUILayout.HelpBox(
                    "MVP targets: 25→125 customers, 3→6 food types, relief every third level, " +
                    "0–5% target fail rate in L1–10, 10–20% in L11–20, and 20–30% in L21–30.",
                    MessageType.Info);
            }
            batchDifficultyCurve = EditorGUILayout.CurveField(
                new GUIContent("Progression Curve", "Maps normalized batch progress to difficulty. In the MVP preset this is a sawtooth curve."),
                batchDifficultyCurve);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Total Customer Demand Range", EditorStyles.miniBoldLabel);
            DrawMinMaxInt("Early Level Demand", ref batchEarlyMinDemand, ref batchEarlyMaxDemand, 5, 10000);
            DrawMinMaxInt("Mid Level Demand", ref batchMidMinDemand, ref batchMidMaxDemand, 5, 10000);
            DrawMinMaxInt("Late Level Demand", ref batchLateMinDemand, ref batchLateMaxDemand, 5, 10000);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Active Edge Customer Slots", EditorStyles.miniBoldLabel);
            DrawMinMaxInt("Early Active Slots", ref batchEarlyMinActiveEdgeSlots, ref batchEarlyMaxActiveEdgeSlots, 3, 20);
            DrawMinMaxInt("Mid Active Slots", ref batchMidMinActiveEdgeSlots, ref batchMidMaxActiveEdgeSlots, 3, 20);
            DrawMinMaxInt("Late Active Slots", ref batchLateMinActiveEdgeSlots, ref batchLateMaxActiveEdgeSlots, 3, 20);

            EditorGUILayout.HelpBox($"Rack Slots are fixed globally at {LevelDataSO.FixedRackSlotCount}.", MessageType.Info);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Food Variety Constraints", EditorStyles.miniBoldLabel);
            DrawMinMaxInt("Early Level Types", ref batchEarlyMinFoodTypes, ref batchEarlyMaxFoodTypes, 1, 50);
            DrawMinMaxInt("Mid Level Types", ref batchMidMinFoodTypes, ref batchMidMaxFoodTypes, 1, 50);
            DrawMinMaxInt("Late Level Types", ref batchLateMinFoodTypes, ref batchLateMaxFoodTypes, 1, 50);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Stack Size Constraints", EditorStyles.miniBoldLabel);
            DrawMinMaxInt("Early Stack Size", ref batchEarlyMinStackSize, ref batchEarlyMaxStackSize, 5, 500);
            DrawMinMaxInt("Mid Stack Size", ref batchMidMinStackSize, ref batchMidMaxStackSize, 5, 500);
            DrawMinMaxInt("Late Stack Size", ref batchLateMinStackSize, ref batchLateMaxStackSize, 5, 500);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Timed Customers", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Timed customers begin at the configured level. Timed foods must appear in queue row 1 or 2.", MessageType.Info);
            batchTimedCustomerStartLevel = EditorGUILayout.IntField("Timed Customer Start Level", batchTimedCustomerStartLevel);
            DrawMinMaxInt("Mid Timed Customers", ref batchMidMinTimedCustomers, ref batchMidMaxTimedCustomers, 0, 100);
            DrawMinMaxInt("Late Timed Customers", ref batchLateMinTimedCustomers, ref batchLateMaxTimedCustomers, 0, 100);
            batchTimedCustomerMinDuration = EditorGUILayout.IntField("Minimum Timer Duration", batchTimedCustomerMinDuration);
            batchTimedCustomerMaxDuration = EditorGUILayout.IntField("Maximum Timer Duration", batchTimedCustomerMaxDuration);
            batchTimedCustomerMinDuration = Mathf.Max(1, batchTimedCustomerMinDuration);
            batchTimedCustomerMaxDuration = Mathf.Max(batchTimedCustomerMinDuration, batchTimedCustomerMaxDuration);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Queue Layout Grid Constraints", EditorStyles.miniBoldLabel);
            batchMinQueueColumns = EditorGUILayout.IntSlider("Min Queue Columns", batchMinQueueColumns, 2, 4);
            batchMaxQueueColumns = EditorGUILayout.IntSlider("Max Queue Columns", batchMaxQueueColumns, batchMinQueueColumns, 4);
            batchMinQueueRows = EditorGUILayout.IntSlider("Min Queue Rows", batchMinQueueRows, 1, 15);
            batchMaxQueueRows = EditorGUILayout.IntSlider("Max Queue Rows", batchMaxQueueRows, batchMinQueueRows, 15);

            SnapBatchStackSizes();
        }

        private void DrawTargetControls()
        {
            EditorGUILayout.LabelField("Target Asset", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            LevelDataSO selectedLevel = (LevelDataSO)EditorGUILayout.ObjectField(
                "Level Data", targetLevel, typeof(LevelDataSO), false);
            if (EditorGUI.EndChangeCheck())
            {
                targetLevel = selectedLevel;
                LoadTargetValues();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create New Asset")) CreateNewLevelAsset();

                using (new EditorGUI.DisabledScope(targetLevel == null))
                {
                    if (GUILayout.Button("Load Values From Target")) LoadTargetValues();
                    if (GUILayout.Button("Select Asset")) Selection.activeObject = targetLevel;
                }
            }
        }

        private void DrawGenerationInputs()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Seeded Generation Inputs", EditorStyles.boldLabel);
            randomSeed = EditorGUILayout.IntField(new GUIContent("Random Seed", "The same inputs and seed always produce identical output."), randomSeed);

            windowSerializedObject.Update();
            EditorGUILayout.PropertyField(demandConfigsProperty, new GUIContent("Customer Demands"), true);
            windowSerializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            useCustomSequence = EditorGUILayout.Toggle(
                new GUIContent("Use Custom Sequence", "Preserves custom explicit customer sequence without randomizing."),
                useCustomSequence);

            if (useCustomSequence)
            {
                windowSerializedObject.Update();
                EditorGUILayout.PropertyField(customCustomerSequenceProperty, new GUIContent("Deterministic Customer Order"), true);
                windowSerializedObject.ApplyModifiedProperties();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Populate Sequence From Demands"))
                    {
                        customCustomerSequence = LevelMathUtility.GenerateDeterministicCustomerSequence(demandConfigs, randomSeed);
                    }
                    if (GUILayout.Button("Sync Demands From Sequence"))
                    {
                        SyncDemandsFromCustomSequence();
                    }
                }
            }

            EditorGUILayout.Space(6f);
            activeEdgeSlotCount = EditorGUILayout.IntSlider("Active Edge Slots", activeEdgeSlotCount, 3, 20);
            columnCount = EditorGUILayout.IntSlider("Queue Columns", columnCount, 2, 4);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField("Calculated Queue Rows", rowCount);
            }
            minStackSize = EditorGUILayout.IntField("Minimum Stack Size", minStackSize);
            maxStackSize = EditorGUILayout.IntField("Maximum Stack Size", maxStackSize);

            EditorGUILayout.HelpBox(
                $"Rack Slots are fixed at {LevelDataSO.FixedRackSlotCount}. Rows are derived from total demand, " +
                "the selected columns, and five-step stack bounds.",
                MessageType.Info);

            minStackSize = SnapStackSize(minStackSize);
            maxStackSize = Mathf.Max(minStackSize, SnapStackSize(maxStackSize));
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Full Level", GUILayout.Height(34f))) GenerateLevelData();

                using (new EditorGUI.DisabledScope(targetLevel == null))
                {
                    if (GUILayout.Button("Generate Stacks Only (Keep Order)", GUILayout.Height(34f))) GenerateQueueStacksOnly();
                    if (GUILayout.Button("Validate Solvability", GUILayout.Height(34f))) ValidateTargetLevel();
                }
            }
        }

        private void GenerateLevelData()
        {
            if (!ValidateGenerationInputs(out string error))
            {
                SetValidationStatus(ValidationStatus.InvalidOrUnsolvable, error);
                return;
            }

            if (targetLevel == null && !CreateNewLevelAsset()) return;

            for (int attempt = 0; attempt < MaxSolvabilityAttemptsPerLevel; attempt++)
            {
                int effectiveSeed = unchecked(randomSeed + attempt * 1000);
                if (!LevelMathUtility.TryCalculateRowsAndPartition(
                        demandConfigs,
                        minStackSize,
                        maxStackSize,
                        columnCount,
                        effectiveSeed,
                        out int generatedRows,
                        out List<QueueStackConfig> stacks,
                        out error))
                {
                    if (attempt == 0)
                    {
                        SetValidationStatus(ValidationStatus.InvalidOrUnsolvable, error);
                        return;
                    }
                    continue;
                }

                rowCount = generatedRows;

                List<ItemDataSO> sequence = useCustomSequence && customCustomerSequence.Count > 0
                    ? new List<ItemDataSO>(customCustomerSequence)
                    : LevelMathUtility.GenerateDeterministicCustomerSequence(demandConfigs, effectiveSeed);

                if (!TryBuildSingleTimedCustomers(
                        demandConfigs, stacks, columnCount, rowCount, sequence, effectiveSeed,
                        out List<TimedCustomerConfig> timedCustomers, out _)) continue;

                LevelDataSO validationAsset = CreateInstance<LevelDataSO>();
                ApplyCandidateData(validationAsset, activeEdgeSlotCount, columnCount, rowCount,
                    demandConfigs, stacks, sequence, timedCustomers);
                validationAsset.minStackSize = minStackSize;
                validationAsset.maxStackSize = maxStackSize;

                bool isSolvable = LevelValidator.ValidateLevel(validationAsset);
                DestroyImmediate(validationAsset);
                if (!isSolvable) continue;

                Undo.RecordObject(targetLevel, "Generate Deterministic Level Data");
                ApplyCandidateData(targetLevel, activeEdgeSlotCount, columnCount, rowCount,
                    demandConfigs, stacks, sequence, timedCustomers);
                targetLevel.minStackSize = minStackSize;
                targetLevel.maxStackSize = maxStackSize;
                EditorUtility.SetDirty(targetLevel);
                AssetDatabase.SaveAssets();

                SetValidationStatus(
                    ValidationStatus.SolvableWithoutPowerUps,
                    $"Generated and validated {stacks.Count} queue stacks, {sequence.Count} fixed customers, " +
                    $"and {timedCustomers.Count} timed customers (Seed: {effectiveSeed}).");
                Repaint();
                return;
            }

            SetValidationStatus(
                ValidationStatus.InvalidOrUnsolvable,
                $"The {columnCount}×{rowCount} layout could not produce a strict-solvable ordering after " +
                $"{MaxSolvabilityAttemptsPerLevel:N0} attempts.");
        }

        private void GenerateQueueStacksOnly()
        {
            if (targetLevel == null)
            {
                SetValidationStatus(ValidationStatus.InvalidOrUnsolvable, "Assign or select a LevelDataSO asset first.");
                return;
            }

            List<ItemDataSO> sequenceToUse = new List<ItemDataSO>();
            if (useCustomSequence && customCustomerSequence.Count > 0)
            {
                sequenceToUse.AddRange(customCustomerSequence);
            }
            else
            {
                targetLevel.CopyResolvedCustomerSequenceTo(sequenceToUse);
            }

            if (sequenceToUse.Count == 0)
            {
                SetValidationStatus(ValidationStatus.InvalidOrUnsolvable, "No customer sequence available to partition stacks for.");
                return;
            }

            SyncDemandsFromSequenceList(sequenceToUse);

            for (int attempt = 0; attempt < MaxSolvabilityAttemptsPerLevel; attempt++)
            {
                int effectiveSeed = unchecked(randomSeed + attempt * 1000);
                if (!LevelMathUtility.TryCalculateRowsAndPartition(
                        demandConfigs, minStackSize, maxStackSize, columnCount, effectiveSeed,
                        out int generatedRows, out List<QueueStackConfig> stacks, out string error))
                {
                    if (attempt == 0)
                    {
                        SetValidationStatus(ValidationStatus.InvalidOrUnsolvable, error);
                        return;
                    }
                    continue;
                }

                rowCount = generatedRows;

                if (!TryBuildSingleTimedCustomers(demandConfigs, stacks, columnCount, rowCount, sequenceToUse, effectiveSeed,
                        out List<TimedCustomerConfig> timedCustomers, out _)) continue;

                LevelDataSO validationAsset = CreateInstance<LevelDataSO>();
                ApplyCandidateData(validationAsset, activeEdgeSlotCount, columnCount, rowCount,
                    demandConfigs, stacks, sequenceToUse, timedCustomers);
                validationAsset.minStackSize = minStackSize;
                validationAsset.maxStackSize = maxStackSize;

                bool isSolvable = LevelValidator.ValidateLevel(validationAsset);
                DestroyImmediate(validationAsset);
                if (!isSolvable) continue;

                Undo.RecordObject(targetLevel, "Generate Queue Stacks Only");
                ApplyCandidateData(targetLevel, activeEdgeSlotCount, columnCount, rowCount,
                    demandConfigs, stacks, sequenceToUse, timedCustomers);
                targetLevel.minStackSize = minStackSize;
                targetLevel.maxStackSize = maxStackSize;
                EditorUtility.SetDirty(targetLevel);
                AssetDatabase.SaveAssets();

                SetValidationStatus(
                    ValidationStatus.SolvableWithoutPowerUps,
                    $"Successfully updated Queue Stacks for preserved customer sequence ({sequenceToUse.Count} items, Seed: {effectiveSeed}).");
                Repaint();
                return;
            }

            SetValidationStatus(
                ValidationStatus.InvalidOrUnsolvable,
                $"Could not generate a solvable queue partition for the target customer sequence after {MaxSolvabilityAttemptsPerLevel:N0} attempts.");
        }

        private void SyncDemandsFromCustomSequence()
        {
            if (customCustomerSequence == null || customCustomerSequence.Count == 0) return;
            SyncDemandsFromSequenceList(customCustomerSequence);
            InitializeSerializedProperties(force: true);
        }

        private void SyncDemandsFromSequenceList(List<ItemDataSO> sequenceList)
        {
            Dictionary<ItemDataSO, int> counts = new Dictionary<ItemDataSO, int>();
            for (int i = 0; i < sequenceList.Count; i++)
            {
                ItemDataSO item = sequenceList[i];
                if (item == null) continue;
                counts.TryGetValue(item, out int current);
                counts[item] = current + 1;
            }

            demandConfigs.Clear();
            foreach (var pair in counts)
            {
                demandConfigs.Add(new CustomerDemandConfig
                {
                    itemData = pair.Key,
                    totalCustomerCount = pair.Value
                });
            }
        }

        private void ValidateTargetLevel()
        {
            if (targetLevel == null)
            {
                SetValidationStatus(ValidationStatus.InvalidOrUnsolvable, "Assign or create a LevelDataSO first.");
                return;
            }

            if (targetLevel.OrderedCustomerSequence.Count > 0 &&
                !targetLevel.ValidateOrderedCustomerSequence(out string sequenceMessage))
            {
                SetValidationStatus(ValidationStatus.InvalidOrUnsolvable, sequenceMessage);
                return;
            }

            if (!targetLevel.ValidateTimedCustomers(out string timedMessage))
            {
                SetValidationStatus(ValidationStatus.InvalidOrUnsolvable, timedMessage);
                return;
            }

            LevelValidationReport report = LevelValidator.AnalyzeLevel(targetLevel);
            if (report.SolvableWithoutPowerUps)
            {
                SetValidationStatus(
                    ValidationStatus.SolvableWithoutPowerUps,
                    $"Solvable without power-ups. {report.ValidationMessage}");
            }
            else if (report.SolvableWithPowerUps)
            {
                SetValidationStatus(
                    ValidationStatus.SolvableWithPowerUps,
                    $"Solvable with power-ups: {report.PowerUpsUsedByGreedySimulation}.");
            }
            else
            {
                SetValidationStatus(
                    ValidationStatus.InvalidOrUnsolvable,
                    $"Unsolvable or invalid. Demand: {targetLevel.TotalCustomerDemand}, queue items: {targetLevel.TotalQueueItems}.");
            }
        }

        private void GenerateDifficultyPreset(DifficultyPresetTier tier)
        {
            PresetDifficultyProfile profile = PresetDifficultyProfile.For(tier);
            List<ItemDataSO> validItems = GetDistinctAvailableItems();
            if (validItems.Count < profile.MinFoodTypes)
            {
                SetPresetMessage(
                    $"{profile.DisplayName} requires at least {profile.MinFoodTypes} food assets; " +
                    $"only {validItems.Count} valid items are configured.",
                    MessageType.Error);
                return;
            }

            if (targetLevel == null && !CreateNewLevelAsset()) return;

            for (int attempt = 0; attempt < MaxSolvabilityAttemptsPerLevel; attempt++)
            {
                int candidateSeed = unchecked(randomSeed + ((int)tier + 1) * 1000003 + attempt * 7919);
                System.Random random = new System.Random(candidateSeed);
                int foodTypeCount = NextInclusive(
                    random,
                    profile.MinFoodTypes,
                    Mathf.Min(profile.MaxFoodTypes, validItems.Count));
                int columns = NextInclusive(random, profile.MinColumns, profile.MaxColumns);
                int activeSlots = NextInclusive(random, profile.MinActiveSlots, profile.MaxActiveSlots);
                int totalDemand = NextInclusive(
                    random,
                    profile.MinDemand / LevelMathUtility.StackSizeStep,
                    profile.MaxDemand / LevelMathUtility.StackSizeStep) * LevelMathUtility.StackSizeStep;

                int minimumRows = Mathf.Max(1, Mathf.CeilToInt(foodTypeCount / (float)columns));
                int maximumRows = Mathf.Min(15, totalDemand / (columns * profile.MinStackSize));
                if (maximumRows < minimumRows) continue;
                int desiredRows = NextInclusive(random, minimumRows, maximumRows);
                int rows = FindClosestFeasibleRowCount(
                    totalDemand,
                    columns,
                    desiredRows,
                    minimumRows,
                    maximumRows,
                    profile.MinStackSize,
                    profile.MaxStackSize);
                if (rows <= 0) continue;

                int queueSlotCount = columns * rows;
                List<CustomerDemandConfig> demands = CreateRandomDemandDistribution(
                    validItems,
                    totalDemand,
                    foodTypeCount,
                    queueSlotCount,
                    profile.MinStackSize,
                    profile.MaxStackSize,
                    random);
                if (demands == null) continue;

                if (!LevelMathUtility.TryPartitionDemandToGrid(
                        demands,
                        profile.MinStackSize,
                        profile.MaxStackSize,
                        columns,
                        rows,
                        candidateSeed,
                        out List<QueueStackConfig> stacks,
                        out _))
                    continue;

                List<ItemDataSO> sequence =
                    LevelMathUtility.GenerateDeterministicCustomerSequence(demands, candidateSeed);
                int timedCount = NextInclusive(random, profile.MinTimedCustomers, profile.MaxTimedCustomers);
                int timedDuration = timedCount > 0
                    ? NextInclusive(random, profile.MinTimerDuration, profile.MaxTimerDuration)
                    : 0;
                if (!TryBuildBatchTimedCustomers(
                        stacks,
                        columns,
                        rows,
                        sequence,
                        timedCount,
                        timedDuration,
                        random,
                        out List<TimedCustomerConfig> timedCustomers))
                    continue;

                LevelDataSO validationAsset = CreateInstance<LevelDataSO>();
                ApplyCandidateData(validationAsset, activeSlots, columns, rows, demands, stacks, sequence, timedCustomers);
                validationAsset.minStackSize = profile.MinStackSize;
                validationAsset.maxStackSize = profile.MaxStackSize;
                LevelValidationReport report = LevelValidator.AnalyzeLevel(validationAsset);
                DestroyImmediate(validationAsset);
                if (!report.SolvableWithoutPowerUps) continue;

                Undo.RecordObject(targetLevel, $"Generate {profile.DisplayName} Level");
                ApplyCandidateData(targetLevel, activeSlots, columns, rows, demands, stacks, sequence, timedCustomers);
                targetLevel.minStackSize = profile.MinStackSize;
                targetLevel.maxStackSize = profile.MaxStackSize;
                EditorUtility.SetDirty(targetLevel);
                AssetDatabase.SaveAssets();

                LoadTargetValues();
                SetValidationStatus(
                    ValidationStatus.SolvableWithoutPowerUps,
                    $"{profile.DisplayName} certified without power-ups. {report.ValidationMessage}");
                SetPresetMessage(
                    $"Updated {AssetDatabase.GetAssetPath(targetLevel)} using seed {candidateSeed}. " +
                    $"Demand {totalDemand}, foods {foodTypeCount}, queue {columns}×{rows}, edge slots {activeSlots}, " +
                    $"timed customers {timedCustomers.Count}{(timedCount > 0 ? $" at {timedDuration}s" : string.Empty)}.",
                    MessageType.Info);
                Selection.activeObject = targetLevel;
                return;
            }

            SetValidationStatus(
                ValidationStatus.InvalidOrUnsolvable,
                $"No strictly solvable {profile.DisplayName} candidate was found after " +
                $"{MaxSolvabilityAttemptsPerLevel:N0} attempts.");
            SetPresetMessage(
                "Try another seed or confirm that enough valid food assets are assigned.",
                MessageType.Error);
        }

        private List<ItemDataSO> GetDistinctAvailableItems()
        {
            if (availableItems == null || availableItems.Count == 0) availableItems = FindAllFoodItems();

            List<ItemDataSO> validItems = new List<ItemDataSO>();
            HashSet<ItemDataSO> seen = new HashSet<ItemDataSO>();
            for (int i = 0; i < availableItems.Count; i++)
            {
                ItemDataSO item = availableItems[i];
                if (item != null && seen.Add(item)) validItems.Add(item);
            }

            return validItems;
        }

        private void SetPresetMessage(string message, MessageType type)
        {
            presetMessage = message;
            presetMessageType = type;
            Repaint();
        }

        private void GenerateBatchLevels()
        {
            if (!ValidateBatchInputs(out List<ItemDataSO> validItems, out string error))
            {
                SetBatchMessage(error, MessageType.Error);
                return;
            }

            List<string> assetPaths = BuildBatchAssetPaths();
            if (!ValidateBatchAssetPaths(assetPaths, out error))
            {
                SetBatchMessage(error, MessageType.Error);
                return;
            }

            List<BatchLevelCandidate> candidates = new List<BatchLevelCandidate>(totalLevelsToGenerate);
            List<LevelDataSO> generatedAssets = new List<LevelDataSO>(totalLevelsToGenerate);
            HashSet<int> acceptedSeeds = new HashSet<int>();

            try
            {
                for (int levelIndex = 0; levelIndex < totalLevelsToGenerate; levelIndex++)
                {
                    int levelNumber = startingLevelNumber + levelIndex;
                    BatchDifficultyProfile profile = CreateDifficultyProfile(levelIndex, validItems.Count);
                    EditorUtility.DisplayProgressBar(
                        "Batch Level Generator",
                        $"Generating Level_{levelNumber:D2}...",
                        levelIndex / (float)totalLevelsToGenerate);

                    if (!TryCreateSolvableCandidate(
                            levelIndex,
                            levelNumber,
                            profile,
                            validItems,
                            acceptedSeeds,
                            out BatchLevelCandidate candidate))
                    {
                        SetBatchMessage(
                            $"Level_{levelNumber:D2} could not be made solvable after {MaxSolvabilityAttemptsPerLevel:N0} attempts.",
                            MessageType.Error);
                        return;
                    }

                    candidates.Add(candidate);
                    acceptedSeeds.Add(candidate.Seed);
                }

                EnsureAssetFolderExists(batchOutputFolder);
                for (int levelIndex = 0; levelIndex < candidates.Count; levelIndex++)
                {
                    BatchLevelCandidate candidate = candidates[levelIndex];
                    EditorUtility.DisplayProgressBar(
                        "Batch Level Generator",
                        $"Saving Level_{candidate.LevelNumber:D2}...",
                        0.8f + 0.2f * ((levelIndex + 1f) / candidates.Count));
                    generatedAssets.Add(SaveBatchCandidate(candidate, assetPaths[levelIndex]));
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                string assignmentMessage = string.Empty;
                if (assignToOpenLevelManager)
                {
                    assignmentMessage = AssignToOpenLevelManager(generatedAssets);
                }

                Selection.objects = generatedAssets.ToArray();
                SetBatchMessage(
                    $"Generated {generatedAssets.Count} validated 1:1 demand levels in {batchOutputFolder}." + assignmentMessage,
                    MessageType.Info);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetBatchMessage($"Batch generation stopped: {exception.Message}", MessageType.Error);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private bool TryCreateSolvableCandidate(
            int levelIndex,
            int levelNumber,
            BatchDifficultyProfile profile,
            List<ItemDataSO> validItems,
            HashSet<int> acceptedSeeds,
            out BatchLevelCandidate candidate)
        {
            candidate = null;

            for (int attempt = 0; attempt < MaxSolvabilityAttemptsPerLevel; attempt++)
            {
                int candidateSeed = unchecked(baseRandomSeed + levelIndex + attempt * 1000);
                if (acceptedSeeds.Contains(candidateSeed)) continue;

                System.Random random = new System.Random(candidateSeed);

                int activeSlots = NextInclusive(random, profile.MinActiveSlots, profile.MaxActiveSlots);
                int foodTypeCount = NextInclusive(random, profile.MinFoodTypes, profile.MaxFoodTypes);
                int columns = NextInclusive(random, profile.MinColumns, profile.MaxColumns);
                int desiredRows = NextInclusive(random, profile.MinRows, profile.MaxRows);

                int minDemandStep = profile.MinDemand / LevelMathUtility.StackSizeStep;
                int maxDemandStep = profile.MaxDemand / LevelMathUtility.StackSizeStep;
                if (minDemandStep > maxDemandStep) minDemandStep = maxDemandStep;
                int totalDemand = NextInclusive(random, minDemandStep, maxDemandStep) * LevelMathUtility.StackSizeStep;

                int rows = FindClosestFeasibleRowCount(
                    totalDemand, columns, desiredRows, profile.MinRows, profile.MaxRows, profile.MinStackSize, profile.MaxStackSize);

                if (rows <= 0) continue;
                int queueSlotCount = columns * rows;

                List<CustomerDemandConfig> demands = CreateRandomDemandDistribution(
                    validItems,
                    totalDemand,
                    foodTypeCount,
                    queueSlotCount,
                    profile.MinStackSize,
                    profile.MaxStackSize,
                    random);
                if (demands == null) continue;

                if (!LevelMathUtility.TryPartitionDemandToGrid(
                        demands,
                        profile.MinStackSize,
                        profile.MaxStackSize,
                        columns,
                        rows,
                        candidateSeed,
                        out List<QueueStackConfig> stacks,
                        out _)) continue;

                if (!HasExactPerItemConservation(demands, stacks)) continue;

                List<ItemDataSO> sequence = LevelMathUtility.GenerateDeterministicCustomerSequence(demands, candidateSeed);

                GetBatchTimedCustomerRange(levelNumber, out int minimumTimedCustomers, out int maximumTimedCustomers);
                int timedCustomerCount = NextInclusive(random, minimumTimedCustomers, maximumTimedCustomers);
                int levelTimedDuration = timedCustomerCount > 0
                    ? NextInclusive(random, batchTimedCustomerMinDuration, batchTimedCustomerMaxDuration)
                    : 0;
                if (!TryBuildBatchTimedCustomers(
                        stacks,
                        columns,
                        rows,
                        sequence,
                        timedCustomerCount,
                        levelTimedDuration,
                        random,
                        out List<TimedCustomerConfig> timedCustomers)) continue;

                LevelDataSO validationAsset = CreateInstance<LevelDataSO>();
                ApplyCandidateData(
                    validationAsset,
                    activeSlots,
                    columns,
                    rows,
                    demands,
                    stacks,
                    sequence,
                    timedCustomers);

                validationAsset.minStackSize = profile.MinStackSize;
                validationAsset.maxStackSize = profile.MaxStackSize;
                LevelValidationReport report = LevelValidator.AnalyzeLevel(validationAsset);
                DestroyImmediate(validationAsset);

                if (!report.SolvableWithoutPowerUps) continue;

                candidate = new BatchLevelCandidate(
                    levelNumber,
                    candidateSeed,
                    activeSlots,
                    columns,
                    rows,
                    profile.MinStackSize,
                    profile.MaxStackSize,
                    demands,
                    stacks,
                    sequence,
                    timedCustomers);
                return true;
            }

            return false;
        }

        private static bool TryBuildSingleTimedCustomers(
            IReadOnlyList<CustomerDemandConfig> demands,
            IReadOnlyList<QueueStackConfig> stacks,
            int columns,
            int rows,
            IReadOnlyList<ItemDataSO> sequence,
            int seed,
            out List<TimedCustomerConfig> timedCustomers,
            out string error)
        {
            timedCustomers = new List<TimedCustomerConfig>();
            HashSet<int> usedIndices = new HashSet<int>();
            System.Random random = new System.Random(unchecked(seed ^ 0x5F3759DF));
            int levelDuration = 0;

            for (int demandIndex = 0; demandIndex < demands.Count; demandIndex++)
            {
                CustomerDemandConfig demand = demands[demandIndex];
                if (!demand.includeTimedCustomers) continue;
                levelDuration = Mathf.RoundToInt(demand.timedCustomerDuration);
                break;
            }

            if (levelDuration <= 0)
            {
                bool hasTimedDemand = false;
                for (int i = 0; i < demands.Count; i++) hasTimedDemand |= demands[i].includeTimedCustomers;
                if (hasTimedDemand)
                {
                    error = "Timed customers require a whole-number duration of at least 1 second.";
                    return false;
                }
            }

            for (int demandIndex = 0; demandIndex < demands.Count; demandIndex++)
            {
                CustomerDemandConfig demand = demands[demandIndex];
                if (!demand.includeTimedCustomers) continue;

                if (demand.timedCustomerCount <= 0 || demand.timedCustomerCount > demand.totalCustomerCount)
                {
                    error = $"Timed count for demand #{demandIndex + 1} must be between 1 and " +
                            $"{demand.totalCustomerCount}.";
                    return false;
                }

                if (!LevelMathUtility.IsItemAccessibleInFirstRows(demand.itemData, stacks, columns, rows, 2))
                {
                    error = $"'{demand.itemData.ItemName}' cannot be timed because it does not appear in queue row 1 or 2.";
                    return false;
                }

                List<int> candidates = new List<int>();
                for (int sequenceIndex = 0; sequenceIndex < sequence.Count; sequenceIndex++)
                {
                    if (sequence[sequenceIndex] == demand.itemData && !usedIndices.Contains(sequenceIndex))
                        candidates.Add(sequenceIndex);
                }

                if (candidates.Count < demand.timedCustomerCount)
                {
                    error = $"Demand #{demandIndex + 1} requests {demand.timedCustomerCount} timed customers, " +
                            $"but only {candidates.Count} matching sequence entries remain.";
                    return false;
                }

                Shuffle(candidates, random);
                for (int i = 0; i < demand.timedCustomerCount; i++)
                {
                    int customerIndex = candidates[i];
                    usedIndices.Add(customerIndex);
                    timedCustomers.Add(new TimedCustomerConfig
                    {
                        customerIndex = customerIndex,
                        timeLimitDuration = levelDuration
                    });
                }
            }

            timedCustomers.Sort((left, right) => left.customerIndex.CompareTo(right.customerIndex));
            error = null;
            return true;
        }

        private static bool TryBuildBatchTimedCustomers(
            IReadOnlyList<QueueStackConfig> stacks,
            int columns,
            int rows,
            IReadOnlyList<ItemDataSO> sequence,
            int timedCustomerCount,
            int levelDuration,
            System.Random random,
            out List<TimedCustomerConfig> timedCustomers)
        {
            timedCustomers = new List<TimedCustomerConfig>(Mathf.Max(0, timedCustomerCount));
            if (timedCustomerCount <= 0) return true;
            if (levelDuration <= 0) return false;

            List<int> eligibleIndices = new List<int>();
            for (int sequenceIndex = 0; sequenceIndex < sequence.Count; sequenceIndex++)
            {
                if (LevelMathUtility.IsItemAccessibleInFirstRows(
                        sequence[sequenceIndex], stacks, columns, rows, 2))
                    eligibleIndices.Add(sequenceIndex);
            }

            if (eligibleIndices.Count < timedCustomerCount) return false;

            Shuffle(eligibleIndices, random);
            for (int i = 0; i < timedCustomerCount; i++)
            {
                timedCustomers.Add(new TimedCustomerConfig
                {
                    customerIndex = eligibleIndices[i],
                    timeLimitDuration = levelDuration
                });
            }

            timedCustomers.Sort((left, right) => left.customerIndex.CompareTo(right.customerIndex));
            return true;
        }

        private void GetBatchTimedCustomerRange(int levelNumber, out int minimum, out int maximum)
        {
            if (levelNumber < batchTimedCustomerStartLevel)
            {
                minimum = 0;
                maximum = 0;
                return;
            }

            if (levelNumber <= 20)
            {
                minimum = batchMidMinTimedCustomers;
                maximum = batchMidMaxTimedCustomers;
                return;
            }

            minimum = batchLateMinTimedCustomers;
            maximum = batchLateMaxTimedCustomers;
        }

        private static List<CustomerDemandConfig> CreateRandomDemandDistribution(
            List<ItemDataSO> available,
            int totalDemand,
            int selectedItemCount,
            int queueSlotCount,
            int minStackSize,
            int maxStackSize,
            System.Random random)
        {
            if (totalDemand < queueSlotCount * minStackSize || totalDemand > queueSlotCount * maxStackSize ||
                totalDemand % LevelMathUtility.StackSizeStep != 0)
                return null;

            List<ItemDataSO> shuffledItems = new List<ItemDataSO>(available);
            Shuffle(shuffledItems, random);

            selectedItemCount = Mathf.Clamp(selectedItemCount, 1, Mathf.Min(shuffledItems.Count, queueSlotCount));
            int[] slotsPerItem = new int[selectedItemCount];
            for (int i = 0; i < selectedItemCount; i++) slotsPerItem[i] = 1;
            for (int slot = selectedItemCount; slot < queueSlotCount; slot++)
            {
                slotsPerItem[random.Next(selectedItemCount)]++;
            }

            int[] counts = new int[selectedItemCount];
            for (int i = 0; i < selectedItemCount; i++) counts[i] = slotsPerItem[i] * minStackSize;

            int remainingDemand = totalDemand - queueSlotCount * minStackSize;
            List<int> candidates = new List<int>(selectedItemCount);
            while (remainingDemand > 0)
            {
                candidates.Clear();
                for (int i = 0; i < selectedItemCount; i++)
                {
                    if (counts[i] < slotsPerItem[i] * maxStackSize) candidates.Add(i);
                }

                if (candidates.Count == 0) return null;
                int selectedIndex = candidates[random.Next(candidates.Count)];
                counts[selectedIndex] += LevelMathUtility.StackSizeStep;
                remainingDemand -= LevelMathUtility.StackSizeStep;
            }

            List<CustomerDemandConfig> demands = new List<CustomerDemandConfig>(selectedItemCount);
            for (int i = 0; i < selectedItemCount; i++)
            {
                demands.Add(new CustomerDemandConfig
                {
                    itemData = shuffledItems[i],
                    totalCustomerCount = counts[i]
                });
            }

            return demands;
        }

        private static int FindClosestFeasibleRowCount(
            int totalDemand,
            int columns,
            int desiredRows,
            int minimumRows,
            int maximumRows,
            int minStackSize,
            int maxStackSize)
        {
            for (int offset = 0; offset <= maximumRows - minimumRows; offset++)
            {
                int lower = desiredRows - offset;
                if (IsGridCapacityFeasible(totalDemand, columns, lower, minimumRows, maximumRows,
                        minStackSize, maxStackSize)) return lower;

                int upper = desiredRows + offset;
                if (upper != lower && IsGridCapacityFeasible(totalDemand, columns, upper, minimumRows, maximumRows,
                        minStackSize, maxStackSize)) return upper;
            }

            return 0;
        }

        private static bool IsGridCapacityFeasible(
            int totalDemand,
            int columns,
            int rows,
            int minimumRows,
            int maximumRows,
            int minStackSize,
            int maxStackSize)
        {
            if (rows < minimumRows || rows > maximumRows) return false;
            int slotCount = columns * rows;
            return totalDemand >= slotCount * minStackSize && totalDemand <= slotCount * maxStackSize;
        }

        private static bool HasExactPerItemConservation(
            List<CustomerDemandConfig> demands,
            List<QueueStackConfig> stacks)
        {
            Dictionary<ItemDataSO, int> balances = new Dictionary<ItemDataSO, int>();
            for (int i = 0; i < demands.Count; i++)
            {
                CustomerDemandConfig demand = demands[i];
                if (demand.itemData == null || demand.totalCustomerCount <= 0) return false;
                balances.TryGetValue(demand.itemData, out int currentBalance);
                balances[demand.itemData] = currentBalance + demand.totalCustomerCount;
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                QueueStackConfig stack = stacks[i];
                if (stack.itemData == null || stack.itemCount <= 0 || !balances.ContainsKey(stack.itemData)) return false;
                balances[stack.itemData] -= stack.itemCount;
            }

            foreach (KeyValuePair<ItemDataSO, int> balance in balances)
            {
                if (balance.Value != 0) return false;
            }

            return true;
        }

        private BatchDifficultyProfile CreateDifficultyProfile(int levelIndex, int availableItemCount)
        {
            if (useMvpSawtoothPreset)
            {
                return CreateMvpSawtoothDifficultyProfile(levelIndex, availableItemCount);
            }

            float normalizedProgress = totalLevelsToGenerate <= 1
                ? 0f
                : levelIndex / (float)(totalLevelsToGenerate - 1);
            float curveProgress = Mathf.Clamp01(batchDifficultyCurve.Evaluate(normalizedProgress));

            int minDemand = SnapStackSize(TierLerpRounded(batchEarlyMinDemand, batchMidMinDemand, batchLateMinDemand, curveProgress));
            int maxDemand = SnapStackSize(TierLerpRounded(batchEarlyMaxDemand, batchMidMaxDemand, batchLateMaxDemand, curveProgress));
            maxDemand = Mathf.Max(minDemand, maxDemand);

            int minActiveSlots = TierLerpRounded(batchEarlyMinActiveEdgeSlots, batchMidMinActiveEdgeSlots, batchLateMinActiveEdgeSlots, curveProgress);
            int maxActiveSlots = TierLerpRounded(batchEarlyMaxActiveEdgeSlots, batchMidMaxActiveEdgeSlots, batchLateMaxActiveEdgeSlots, curveProgress);
            maxActiveSlots = Mathf.Max(minActiveSlots, maxActiveSlots);

            int minStackSize = SnapStackSize(TierLerpRounded(batchEarlyMinStackSize, batchMidMinStackSize, batchLateMinStackSize, curveProgress));
            int maxStackSize = Mathf.Max(minStackSize,
                SnapStackSize(TierLerpRounded(batchEarlyMaxStackSize, batchMidMaxStackSize, batchLateMaxStackSize, curveProgress)));

            int minFoodTypes = Mathf.Clamp(
                TierLerpRounded(batchEarlyMinFoodTypes, batchMidMinFoodTypes, batchLateMinFoodTypes, curveProgress),
                1,
                availableItemCount);
            int maxFoodTypes = Mathf.Clamp(
                TierLerpRounded(batchEarlyMaxFoodTypes, batchMidMaxFoodTypes, batchLateMaxFoodTypes, curveProgress),
                minFoodTypes,
                availableItemCount);

            return new BatchDifficultyProfile(
                minDemand,
                maxDemand,
                minFoodTypes,
                maxFoodTypes,
                minActiveSlots,
                maxActiveSlots,
                batchMinQueueColumns,
                batchMaxQueueColumns,
                batchMinQueueRows,
                batchMaxQueueRows,
                minStackSize,
                maxStackSize);
        }

        private BatchDifficultyProfile CreateMvpSawtoothDifficultyProfile(int levelIndex, int availableItemCount)
        {
            float linearProgress = totalLevelsToGenerate <= 1
                ? 0f
                : levelIndex / (float)(totalLevelsToGenerate - 1);
            float sawtoothProgress = GetMvpSawtoothProgress(levelIndex, linearProgress);

            int targetDemand = SnapStackSize(Mathf.RoundToInt(Mathf.Lerp(25f, 125f, sawtoothProgress)));
            int targetFoodTypes = Mathf.Clamp(3 + Mathf.FloorToInt(linearProgress * 3.01f), 3, availableItemCount);
            int targetActiveSlots = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(4f, 12f, sawtoothProgress)), 3, 20);
            int targetMaxStackSize = Mathf.Max(5,
                SnapStackSize(Mathf.RoundToInt(Mathf.Lerp(10f, 25f, sawtoothProgress))));
            int targetColumns = targetFoodTypes >= 5 ? 4 : 3;

            return new BatchDifficultyProfile(
                targetDemand,
                targetDemand,
                targetFoodTypes,
                targetFoodTypes,
                targetActiveSlots,
                targetActiveSlots,
                targetColumns,
                targetColumns,
                1,
                8,
                5,
                targetMaxStackSize);
        }

        private static float GetMvpSawtoothProgress(int levelIndex, float linearProgress)
        {
            int phase = levelIndex % 3;
            float reliefOffset = phase == 0 ? -0.025f : phase == 1 ? 0.025f : 0.09f;
            return Mathf.Clamp01(linearProgress + reliefOffset);
        }

        private static void ApplyCandidateData(
            LevelDataSO level,
            int activeSlots,
            int columns,
            int rows,
            List<CustomerDemandConfig> demands,
            List<QueueStackConfig> stacks,
            List<ItemDataSO> sequence,
            List<TimedCustomerConfig> timedCustomers)
        {
            level.activeEdgeSlotCount = activeSlots;
            level.columnCount = columns;
            level.calculatedRowCount = rows;
            level.customerDemands = new List<CustomerDemandConfig>(demands);
            level.queueStackConfigs = new List<QueueStackConfig>(stacks);
            level.SetOrderedCustomerSequence(sequence);
            level.SetTimedCustomers(timedCustomers);
        }

        private LevelDataSO SaveBatchCandidate(BatchLevelCandidate candidate, string assetPath)
        {
            LevelDataSO level = AssetDatabase.LoadAssetAtPath<LevelDataSO>(assetPath);
            bool isNewAsset = level == null;
            if (isNewAsset)
            {
                level = CreateInstance<LevelDataSO>();
            }
            else
            {
                Undo.RecordObject(level, "Update Batch Generated Level");
            }

            level.name = $"Level_{candidate.LevelNumber:D2}";
            level.minStackSize = candidate.MinStackSize;
            level.maxStackSize = candidate.MaxStackSize;
            ApplyCandidateData(
                level,
                candidate.ActiveSlots,
                candidate.Columns,
                candidate.Rows,
                candidate.Demands,
                candidate.Stacks,
                candidate.Sequence,
                candidate.TimedCustomers);

            if (isNewAsset)
            {
                AssetDatabase.CreateAsset(level, assetPath);
                Undo.RegisterCreatedObjectUndo(level, "Create Batch Generated Level");
            }

            EditorUtility.SetDirty(level);
            SetGeneratedLabels(level, candidate.Seed, candidate.LevelNumber);
            return level;
        }

        private static void SetGeneratedLabels(LevelDataSO level, int seed, int levelNumber)
        {
            const string seedLabelPrefix = "RestaurantLoopSeed_";
            const string targetFailRateLabelPrefix = "RestaurantLoopTargetFailRate_";
            string[] existingLabels = AssetDatabase.GetLabels(level);
            List<string> updatedLabels = new List<string>(existingLabels.Length + 2);
            for (int i = 0; i < existingLabels.Length; i++)
            {
                if (!existingLabels[i].StartsWith(seedLabelPrefix, StringComparison.Ordinal) &&
                    !existingLabels[i].StartsWith(targetFailRateLabelPrefix, StringComparison.Ordinal))
                {
                    updatedLabels.Add(existingLabels[i]);
                }
            }

            updatedLabels.Add(seedLabelPrefix + seed);
            updatedLabels.Add(targetFailRateLabelPrefix + GetTargetFailRateBand(levelNumber));
            AssetDatabase.SetLabels(level, updatedLabels.ToArray());
        }

        private static string GetTargetFailRateBand(int levelNumber)
        {
            if (levelNumber <= 10) return "0-5";
            if (levelNumber <= 20) return "10-20";
            return "20-30";
        }

        private string AssignToOpenLevelManager(List<LevelDataSO> generatedAssets)
        {
            LevelManager manager = UnityEngine.Object.FindFirstObjectByType<LevelManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                return " No LevelManager was found in the open scene, so automatic assignment was skipped.";
            }

            Undo.RecordObject(manager, "Assign Batch Generated Levels");
            SerializedObject managerSerializedObject = new SerializedObject(manager);
            SerializedProperty sequenceProperty = managerSerializedObject.FindProperty("levelSequence");
            if (sequenceProperty == null || !sequenceProperty.isArray)
            {
                return " The open LevelManager does not expose a serialized levelSequence, so assignment was skipped.";
            }

            managerSerializedObject.Update();
            LevelDataSO tutorialLevel = sequenceProperty.arraySize > 0
                ? sequenceProperty.GetArrayElementAtIndex(0).objectReferenceValue as LevelDataSO
                : null;
            bool preserveTutorial = tutorialLevel != null && tutorialLevel.name == "Level_00";
            int generatedStartIndex = preserveTutorial ? 1 : 0;

            sequenceProperty.arraySize = generatedAssets.Count + generatedStartIndex;
            if (preserveTutorial)
            {
                sequenceProperty.GetArrayElementAtIndex(0).objectReferenceValue = tutorialLevel;
            }

            for (int i = 0; i < generatedAssets.Count; i++)
            {
                sequenceProperty.GetArrayElementAtIndex(i + generatedStartIndex).objectReferenceValue = generatedAssets[i];
            }

            managerSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            if (manager.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            return preserveTutorial
                ? " Assigned them to the open scene LevelManager and preserved Level_00."
                : " Assigned them to the open scene LevelManager.";
        }

        private bool ValidateBatchInputs(out List<ItemDataSO> validItems, out string error)
        {
            NormalizeBatchRanges();
            batchOutputFolder = NormalizeAssetFolder(batchOutputFolder);

            validItems = new List<ItemDataSO>();
            if (availableItems != null)
            {
                for (int i = 0; i < availableItems.Count; i++)
                {
                    ItemDataSO item = availableItems[i];
                    if (item != null && !validItems.Contains(item)) validItems.Add(item);
                }
            }

            if (!IsProjectAssetFolder(batchOutputFolder))
            {
                error = "Target Save Folder must be inside the project's Assets folder.";
                return false;
            }

            if (validItems.Count == 0)
            {
                error = "Add at least one non-null food item to Available Items.";
                return false;
            }

            int requiredFoodTypes = Mathf.Max(
                batchEarlyMinFoodTypes,
                Mathf.Max(batchMidMinFoodTypes, batchLateMinFoodTypes));
            if (validItems.Count < requiredFoodTypes)
            {
                error = $"The progression requires at least {requiredFoodTypes} unique food items, but only " +
                        $"{validItems.Count} valid items are assigned.";
                return false;
            }

            error = null;
            return true;
        }

        private List<string> BuildBatchAssetPaths()
        {
            List<string> paths = new List<string>(totalLevelsToGenerate);
            for (int i = 0; i < totalLevelsToGenerate; i++)
            {
                paths.Add($"{batchOutputFolder}/Level_{startingLevelNumber + i:D2}.asset");
            }

            return paths;
        }

        private bool ValidateBatchAssetPaths(List<string> paths, out string error)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                UnityEngine.Object existingAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(paths[i]);
                if (existingAsset == null) continue;

                if (!(existingAsset is LevelDataSO))
                {
                    error = $"{paths[i]} already exists and is not a LevelDataSO asset.";
                    return false;
                }

                if (!overwriteExistingAssets)
                {
                    error = $"{paths[i]} already exists. Enable Update Existing Assets to replace its generated data safely.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private void BrowseForBatchOutputFolder()
        {
            string selectedFolder = EditorUtility.OpenFolderPanel(
                "Select Batch Level Output Folder",
                Application.dataPath,
                string.Empty);
            if (string.IsNullOrEmpty(selectedFolder)) return;

            string normalizedSelection = selectedFolder.Replace('\\', '/').TrimEnd('/');
            string normalizedAssetsPath = Application.dataPath.Replace('\\', '/').TrimEnd('/');
            if (!normalizedSelection.StartsWith(normalizedAssetsPath, StringComparison.OrdinalIgnoreCase))
            {
                SetBatchMessage("Select a folder inside this project's Assets folder.", MessageType.Error);
                return;
            }

            batchOutputFolder = "Assets" + normalizedSelection.Substring(normalizedAssetsPath.Length);
        }

        private static void EnsureAssetFolderExists(string assetFolder)
        {
            string[] segments = assetFolder.Split('/');
            string currentFolder = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string nextFolder = $"{currentFolder}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(nextFolder))
                {
                    AssetDatabase.CreateFolder(currentFolder, segments[i]);
                }

                currentFolder = nextFolder;
            }
        }

        private void NormalizeBatchRanges()
        {
            totalLevelsToGenerate = Mathf.Max(1, totalLevelsToGenerate);
            startingLevelNumber = Mathf.Max(1, startingLevelNumber);
            batchDifficultyCurve ??= AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            batchTimedCustomerStartLevel = Mathf.Max(1, batchTimedCustomerStartLevel);

            ClampMinMaxTier(ref batchEarlyMinDemand, ref batchEarlyMaxDemand, ref batchMidMinDemand, ref batchMidMaxDemand,
                ref batchLateMinDemand, ref batchLateMaxDemand, 5, 10000);
            ClampMinMaxTier(ref batchEarlyMinActiveEdgeSlots, ref batchEarlyMaxActiveEdgeSlots, ref batchMidMinActiveEdgeSlots, ref batchMidMaxActiveEdgeSlots,
                ref batchLateMinActiveEdgeSlots, ref batchLateMaxActiveEdgeSlots, 3, 20);

            ClampRange(ref batchEarlyMinFoodTypes, ref batchEarlyMaxFoodTypes, 1, 50);
            ClampRange(ref batchMidMinFoodTypes, ref batchMidMaxFoodTypes, 1, 50);
            ClampRange(ref batchLateMinFoodTypes, ref batchLateMaxFoodTypes, 1, 50);
            batchMidMinFoodTypes = Mathf.Max(batchEarlyMinFoodTypes, batchMidMinFoodTypes);
            batchMidMaxFoodTypes = Mathf.Max(batchMidMinFoodTypes, batchMidMaxFoodTypes);
            batchLateMinFoodTypes = Mathf.Max(batchMidMinFoodTypes, batchLateMinFoodTypes);
            batchLateMaxFoodTypes = Mathf.Max(batchLateMaxFoodTypes, batchLateMaxFoodTypes);

            ClampRange(ref batchEarlyMinStackSize, ref batchEarlyMaxStackSize, 5, 500);
            ClampRange(ref batchMidMinStackSize, ref batchMidMaxStackSize, 5, 500);
            ClampRange(ref batchLateMinStackSize, ref batchLateMaxStackSize, 5, 500);

            ClampRange(ref batchMinQueueColumns, ref batchMaxQueueColumns, 2, 4);
            ClampRange(ref batchMinQueueRows, ref batchMaxQueueRows, 1, 15);
            ClampRange(ref batchMaxQueueRows, ref batchMaxQueueRows, batchMinQueueRows, 15);
            ClampRange(ref batchMidMinTimedCustomers, ref batchMidMaxTimedCustomers, 0, 100);
            ClampRange(ref batchLateMinTimedCustomers, ref batchLateMaxTimedCustomers, 0, 100);
            batchTimedCustomerMinDuration = Mathf.Max(1, batchTimedCustomerMinDuration);
            batchTimedCustomerMaxDuration = Mathf.Max(batchTimedCustomerMinDuration, batchTimedCustomerMaxDuration);
            SnapBatchStackSizes();
        }

        private void SnapBatchStackSizes()
        {
            batchEarlyMinDemand = SnapStackSize(batchEarlyMinDemand);
            batchEarlyMaxDemand = Mathf.Max(batchEarlyMinDemand, SnapStackSize(batchEarlyMaxDemand));
            batchMidMinDemand = SnapStackSize(batchMidMinDemand);
            batchMidMaxDemand = Mathf.Max(batchMidMinDemand, SnapStackSize(batchMidMaxDemand));
            batchLateMinDemand = SnapStackSize(batchLateMinDemand);
            batchLateMaxDemand = Mathf.Max(batchLateMinDemand, SnapStackSize(batchLateMaxDemand));

            batchEarlyMinStackSize = SnapStackSize(batchEarlyMinStackSize);
            batchEarlyMaxStackSize = Mathf.Max(batchEarlyMinStackSize, SnapStackSize(batchEarlyMaxStackSize));
            batchMidMinStackSize = SnapStackSize(batchMidMinStackSize);
            batchMidMaxStackSize = Mathf.Max(batchMidMinStackSize, SnapStackSize(batchMidMaxStackSize));
            batchLateMinStackSize = SnapStackSize(batchLateMinStackSize);
            batchLateMaxStackSize = Mathf.Max(batchLateMinStackSize, SnapStackSize(batchLateMaxStackSize));
        }

        private void SetBatchMessage(string message, MessageType type)
        {
            batchMessage = message;
            batchMessageType = type;
            Repaint();
        }

        private void DrawValidationStatus()
        {
            Color statusColor = GetValidationColor(validationStatus);
            GUIStyle statusStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft
            };
            statusStyle.normal.textColor = statusColor;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(validationMessage, statusStyle, GUILayout.MinHeight(30f));
        }

        private static void GetItemPreviewInfo(ItemDataSO item, out string displayName, out Color displayColor)
        {
            if (item == null)
            {
                displayName = "Empty";
                displayColor = new Color(0.25f, 0.25f, 0.25f, 0.75f);
                return;
            }

            string assetName = item.name ?? "";
            string propertyName = !string.IsNullOrEmpty(item.ItemName) ? item.ItemName : "";
            string searchKey = $"{assetName} {propertyName}";

            if (searchKey.IndexOf("Beef", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                displayName = "White Item / Beef";
                displayColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            }
            else if (searchKey.IndexOf("Burger", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                displayName = "Red Item / Burger";
                displayColor = new Color(0.85f, 0.2f, 0.2f, 1f);
            }
            else if (searchKey.IndexOf("Cake", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                displayName = "Purple Item / Cake";
                displayColor = new Color(0.6f, 0.2f, 0.85f, 1f);
            }
            else if (searchKey.IndexOf("Drink", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                displayName = "Blue Item / Drink";
                displayColor = new Color(0.15f, 0.45f, 0.95f, 1f);
            }
            else if (searchKey.IndexOf("Fries", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                displayName = "Yellow Item / Fries";
                displayColor = new Color(0.95f, 0.8f, 0.1f, 1f);
            }
            else if (searchKey.IndexOf("Sushi", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                displayName = "Green Item / Sushi";
                displayColor = new Color(0.15f, 0.85f, 0.25f, 1f);
            }
            else
            {
                displayName = propertyName;
                displayColor = item.UIColor.a > 0.05f ? item.UIColor : Color.gray;
            }
        }

        private void DrawQueuePreview(LevelDataSO level)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Queue Layout Preview", EditorStyles.boldLabel);

            int columns = Mathf.Max(1, level.columnCount);
            int rows = Mathf.Max(0, level.calculatedRowCount);
            if (rows == 0 || level.queueStackConfigs == null || level.queueStackConfigs.Count == 0)
            {
                EditorGUILayout.HelpBox("No generated queue layout.", MessageType.Info);
                return;
            }

            const float cellHeight = 46f;
            float availableWidth = Mathf.Max(200f, position.width - 55f);
            float cellWidth = Mathf.Clamp(availableWidth / columns, 60f, 150f);
            GUIStyle cellStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            for (int row = 0; row < rows; row++)
            {
                Rect rowRect = GUILayoutUtility.GetRect(cellWidth * columns, cellHeight);
                for (int column = 0; column < columns; column++)
                {
                    int stackIndex = column * rows + row;
                    Rect cellRect = new Rect(rowRect.x + column * cellWidth, rowRect.y, cellWidth - 3f, cellHeight - 3f);
                    QueueStackConfig stack = stackIndex < level.queueStackConfigs.Count
                        ? level.queueStackConfigs[stackIndex]
                        : default;

                    GetItemPreviewInfo(stack.itemData, out string displayName, out Color cellColor);
                    EditorGUI.DrawRect(cellRect, cellColor);
                    cellStyle.normal.textColor = GetContrastingTextColor(cellColor);
                    GUI.Label(cellRect, $"C{column + 1} R{row + 1}\n{displayName} × {stack.itemCount}", cellStyle);
                }
            }
        }

        private void DrawCustomerSequencePreview(LevelDataSO level)
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Customer Sequence", EditorStyles.boldLabel);

            sequencePreview.Clear();
            level.CopyResolvedCustomerSequenceTo(sequencePreview);
            if (level.OrderedCustomerSequence.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "This asset uses the deterministic legacy fallback. Generate or sync it to persist an editable explicit sequence.",
                    MessageType.Warning);
            }

            sequenceScrollPosition = EditorGUILayout.BeginScrollView(sequenceScrollPosition, GUILayout.Height(220f));
            for (int i = 0; i < sequencePreview.Count; i++)
            {
                ItemDataSO item = sequencePreview[i];
                GetItemPreviewInfo(item, out string displayName, out Color itemColor);

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    Rect colorRect = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f));
                    EditorGUI.DrawRect(colorRect, itemColor);
                    EditorGUILayout.LabelField($"Customer #{i + 1}", GUILayout.Width(105f));
                    EditorGUILayout.LabelField(displayName);
                    if (level.TryGetTimedCustomer(i, out float duration))
                    {
                        GUIStyle timerStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                        {
                            normal = { textColor = new Color(0.9f, 0.25f, 0.2f) }
                        };
                        EditorGUILayout.LabelField($"⏱ {duration:0.#}s", timerStyle, GUILayout.Width(75f));
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private bool CreateNewLevelAsset()
        {
            string assetPath = EditorUtility.SaveFilePanelInProject(
                "Create Level Data",
                "Level_New",
                "asset",
                "Choose where to save the new LevelDataSO asset.",
                "Assets/_RestaurantLoop/Levels");
            if (string.IsNullOrEmpty(assetPath)) return false;

            LevelDataSO newLevel = CreateInstance<LevelDataSO>();
            AssetDatabase.CreateAsset(newLevel, assetPath);
            AssetDatabase.SaveAssets();
            targetLevel = newLevel;
            Selection.activeObject = newLevel;
            validationStatus = ValidationStatus.NotValidated;
            validationMessage = "New level asset created. Configure inputs and generate its deterministic data.";
            return true;
        }

        private void LoadTargetValues()
        {
            if (targetLevel == null) return;

            activeEdgeSlotCount = targetLevel.activeEdgeSlotCount;
            columnCount = targetLevel.columnCount;
            rowCount = targetLevel.calculatedRowCount;
            minStackSize = targetLevel.minStackSize;
            maxStackSize = targetLevel.maxStackSize;
            demandConfigs = targetLevel.customerDemands != null
                ? new List<CustomerDemandConfig>(targetLevel.customerDemands)
                : new List<CustomerDemandConfig>();

            if (targetLevel.OrderedCustomerSequence != null && targetLevel.OrderedCustomerSequence.Count > 0)
            {
                customCustomerSequence = new List<ItemDataSO>(targetLevel.OrderedCustomerSequence);
            }

            InitializeSerializedProperties(force: true);
            validationStatus = ValidationStatus.NotValidated;
            validationMessage = "Loaded generation inputs from the selected asset.";
        }

        private bool ValidateGenerationInputs(out string error)
        {
            if (demandConfigs == null || demandConfigs.Count == 0)
            {
                error = "Add at least one customer demand configuration.";
                return false;
            }

            for (int i = 0; i < demandConfigs.Count; i++)
            {
                CustomerDemandConfig demand = demandConfigs[i];
                if (demand.itemData == null || demand.totalCustomerCount <= 0)
                {
                    error = $"Customer demand #{i + 1} requires an item and a positive count.";
                    return false;
                }

                if (!demand.includeTimedCustomers) continue;
                if (demand.timedCustomerCount <= 0 || demand.timedCustomerCount > demand.totalCustomerCount)
                {
                    error = $"Timed count for customer demand #{i + 1} must be between 1 and " +
                            $"{demand.totalCustomerCount}.";
                    return false;
                }

                if (demand.timedCustomerDuration <= 0f)
                {
                    error = $"Timed customer demand #{i + 1} requires a positive timer duration.";
                    return false;
                }
            }

            if (!LevelMathUtility.TryCalculateRowsAndPartition(
                    demandConfigs,
                    minStackSize,
                    maxStackSize,
                    columnCount,
                    randomSeed,
                    out int generatedRows,
                    out _,
                    out error))
                return false;

            rowCount = generatedRows;

            error = null;
            return true;
        }

        private void SetValidationStatus(ValidationStatus status, string message)
        {
            validationStatus = status;
            validationMessage = message;
            Repaint();
        }

        private void InitializeSerializedProperties(bool force = false)
        {
            if (!force && windowSerializedObject != null && demandConfigsProperty != null && availableItemsProperty != null && customCustomerSequenceProperty != null) return;
            windowSerializedObject = new SerializedObject(this);
            demandConfigsProperty = windowSerializedObject.FindProperty(nameof(demandConfigs));
            availableItemsProperty = windowSerializedObject.FindProperty(nameof(availableItems));
            customCustomerSequenceProperty = windowSerializedObject.FindProperty(nameof(customCustomerSequence));
        }

        private static void DrawMinMaxInt(
            string label,
            ref int minimum,
            ref int maximum,
            int allowedMinimum,
            int allowedMaximum)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                GUILayout.Label("Min", EditorStyles.miniLabel, GUILayout.Width(24f));
                minimum = EditorGUILayout.IntField(minimum, GUILayout.MinWidth(45f));
                GUILayout.Label("Max", EditorStyles.miniLabel, GUILayout.Width(28f));
                maximum = EditorGUILayout.IntField(maximum, GUILayout.MinWidth(45f));
            }

            ClampRange(ref minimum, ref maximum, allowedMinimum, allowedMaximum);
        }

        private static void ClampRange(ref int minimum, ref int maximum, int allowedMinimum, int allowedMaximum)
        {
            minimum = Mathf.Clamp(minimum, allowedMinimum, allowedMaximum);
            maximum = Mathf.Clamp(maximum, minimum, allowedMaximum);
        }

        private static void ClampMinMaxTier(
            ref int earlyMin, ref int earlyMax,
            ref int midMin, ref int midMax,
            ref int lateMin, ref int lateMax,
            int allowedMinimum, int allowedMaximum)
        {
            ClampRange(ref earlyMin, ref earlyMax, allowedMinimum, allowedMaximum);
            ClampRange(ref midMin, ref midMax, allowedMinimum, allowedMaximum);
            ClampRange(ref lateMin, ref lateMax, allowedMinimum, allowedMaximum);

            midMin = Mathf.Max(earlyMin, midMin);
            midMax = Mathf.Max(earlyMax, midMax);
            lateMin = Mathf.Max(midMin, lateMin);
            lateMax = Mathf.Max(midMax, lateMax);
        }

        private static int LerpRounded(int start, int end, float progress)
        {
            return Mathf.RoundToInt(Mathf.Lerp(start, end, progress));
        }

        private static int TierLerpRounded(int early, int mid, int late, float progress)
        {
            return progress <= 0.5f
                ? LerpRounded(early, mid, progress * 2f)
                : LerpRounded(mid, late, (progress - 0.5f) * 2f);
        }

        private static int NextInclusive(System.Random random, int minimum, int maximum)
        {
            if (minimum >= maximum) return minimum;
            return random.Next(minimum, maximum + 1);
        }

        private static void Shuffle<T>(List<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int selectedIndex = random.Next(0, i + 1);
                (values[i], values[selectedIndex]) = (values[selectedIndex], values[i]);
            }
        }

        private static string NormalizeAssetFolder(string assetFolder)
        {
            if (string.IsNullOrWhiteSpace(assetFolder)) return string.Empty;
            return assetFolder.Trim().Replace('\\', '/').TrimEnd('/');
        }

        private static bool IsProjectAssetFolder(string assetFolder)
        {
            return assetFolder == "Assets" ||
                   (assetFolder.StartsWith("Assets/", StringComparison.Ordinal) &&
                    !assetFolder.Contains("../", StringComparison.Ordinal) &&
                    !assetFolder.EndsWith("/..", StringComparison.Ordinal));
        }

        private static int SnapStackSize(int value)
        {
            return Mathf.Max(5, Mathf.RoundToInt(value / 5f) * 5);
        }

        private static Color GetValidationColor(ValidationStatus status)
        {
            switch (status)
            {
                case ValidationStatus.SolvableWithoutPowerUps: return new Color(0.2f, 0.75f, 0.25f);
                case ValidationStatus.SolvableWithPowerUps: return new Color(0.95f, 0.7f, 0.1f);
                case ValidationStatus.InvalidOrUnsolvable: return new Color(0.9f, 0.2f, 0.2f);
                default: return EditorGUIUtility.isProSkin ? Color.white : Color.black;
            }
        }

        private static Color GetContrastingTextColor(Color background)
        {
            float luminance = background.r * 0.299f + background.g * 0.587f + background.b * 0.114f;
            return luminance > 0.55f ? Color.black : Color.white;
        }

        private sealed class BatchLevelCandidate
        {
            public int LevelNumber { get; }
            public int Seed { get; }
            public int ActiveSlots { get; }
            public int Columns { get; }
            public int Rows { get; }
            public int MinStackSize { get; }
            public int MaxStackSize { get; }
            public List<CustomerDemandConfig> Demands { get; }
            public List<QueueStackConfig> Stacks { get; }
            public List<ItemDataSO> Sequence { get; }
            public List<TimedCustomerConfig> TimedCustomers { get; }

            public BatchLevelCandidate(
                int levelNumber,
                int seed,
                int activeSlots,
                int columns,
                int rows,
                int minStackSize,
                int maxStackSize,
                List<CustomerDemandConfig> demands,
                List<QueueStackConfig> stacks,
                List<ItemDataSO> sequence,
                List<TimedCustomerConfig> timedCustomers)
            {
                LevelNumber = levelNumber;
                Seed = seed;
                ActiveSlots = activeSlots;
                Columns = columns;
                Rows = rows;
                MinStackSize = minStackSize;
                MaxStackSize = maxStackSize;
                Demands = demands;
                Stacks = stacks;
                Sequence = sequence;
                TimedCustomers = timedCustomers;
            }
        }

        private readonly struct PresetDifficultyProfile
        {
            public string DisplayName { get; }
            public int MinDemand { get; }
            public int MaxDemand { get; }
            public int MinFoodTypes { get; }
            public int MaxFoodTypes { get; }
            public int MinColumns { get; }
            public int MaxColumns { get; }
            public int MinActiveSlots { get; }
            public int MaxActiveSlots { get; }
            public int MinStackSize { get; }
            public int MaxStackSize { get; }
            public int MinTimedCustomers { get; }
            public int MaxTimedCustomers { get; }
            public int MinTimerDuration { get; }
            public int MaxTimerDuration { get; }

            private PresetDifficultyProfile(
                string displayName,
                int minDemand,
                int maxDemand,
                int minFoodTypes,
                int maxFoodTypes,
                int minColumns,
                int maxColumns,
                int minActiveSlots,
                int maxActiveSlots,
                int minStackSize,
                int maxStackSize,
                int minTimedCustomers,
                int maxTimedCustomers,
                int minTimerDuration,
                int maxTimerDuration)
            {
                DisplayName = displayName;
                MinDemand = minDemand;
                MaxDemand = maxDemand;
                MinFoodTypes = minFoodTypes;
                MaxFoodTypes = maxFoodTypes;
                MinColumns = minColumns;
                MaxColumns = maxColumns;
                MinActiveSlots = minActiveSlots;
                MaxActiveSlots = maxActiveSlots;
                MinStackSize = minStackSize;
                MaxStackSize = maxStackSize;
                MinTimedCustomers = minTimedCustomers;
                MaxTimedCustomers = maxTimedCustomers;
                MinTimerDuration = minTimerDuration;
                MaxTimerDuration = maxTimerDuration;
            }

            public static PresetDifficultyProfile For(DifficultyPresetTier tier)
            {
                switch (tier)
                {
                    case DifficultyPresetTier.Easy:
                        return new PresetDifficultyProfile(
                            "Easy", 25, 40, 2, 3, 2, 3, 5, 8, 5, 15, 0, 0, 0, 0);
                    case DifficultyPresetTier.Medium:
                        return new PresetDifficultyProfile(
                            "Medium", 45, 75, 3, 4, 3, 3, 8, 12, 5, 20, 3, 6, 15, 20);
                    case DifficultyPresetTier.Hard:
                        return new PresetDifficultyProfile(
                            "Hard", 80, 100, 4, 5, 3, 4, 10, 15, 5, 25, 7, 12, 12, 18);
                    default:
                        return new PresetDifficultyProfile(
                            "Very Hard", 105, 125, 5, 6, 4, 4, 14, 20, 5, 30, 15, 20, 10, 15);
                }
            }
        }

        private readonly struct BatchDifficultyProfile
        {
            public int MinDemand { get; }
            public int MaxDemand { get; }
            public int MinFoodTypes { get; }
            public int MaxFoodTypes { get; }
            public int MinActiveSlots { get; }
            public int MaxActiveSlots { get; }
            public int MinColumns { get; }
            public int MaxColumns { get; }
            public int MinRows { get; }
            public int MaxRows { get; }
            public int MinStackSize { get; }
            public int MaxStackSize { get; }

            public BatchDifficultyProfile(
                int minDemand,
                int maxDemand,
                int minFoodTypes,
                int maxFoodTypes,
                int minActiveSlots,
                int maxActiveSlots,
                int minColumns,
                int maxColumns,
                int minRows,
                int maxRows,
                int minStackSize,
                int maxStackSize)
            {
                MinDemand = minDemand;
                MaxDemand = maxDemand;
                MinFoodTypes = minFoodTypes;
                MaxFoodTypes = maxFoodTypes;
                MinActiveSlots = minActiveSlots;
                MaxActiveSlots = maxActiveSlots;
                MinColumns = minColumns;
                MaxColumns = maxColumns;
                MinRows = minRows;
                MaxRows = maxRows;
                MinStackSize = minStackSize;
                MaxStackSize = maxStackSize;
            }
        }
    }
}
