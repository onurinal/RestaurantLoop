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
        private static readonly string[] GeneratorTabs = { "Single Level Generator", "Batch Level Generator" };

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

        [SerializeField, Range(3, 20)] private int activeEdgeSlotCount = 6;
        [SerializeField, Range(1, 10)] private int rackSlotCount = 5;
        [SerializeField, Range(1, 8)] private int columnCount = 3;
        [SerializeField] private int minStackSize = 10;
        [SerializeField] private int maxStackSize = 40;

        [SerializeField] private int selectedTab;
        [SerializeField] private string batchOutputFolder = DefaultBatchOutputFolder;
        [SerializeField] private int totalLevelsToGenerate = 30;
        [SerializeField] private int startingLevelNumber = 1;
        [SerializeField] private int baseRandomSeed;
        [SerializeField] private List<ItemDataSO> availableItems = new List<ItemDataSO>();
        [SerializeField] private AnimationCurve batchDifficultyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private int batchStartDemand = 25;
        [SerializeField] private int batchEndDemand = 120;
        [SerializeField] private int batchStartMinFoodTypes = 2;
        [SerializeField] private int batchStartMaxFoodTypes = 3;
        [SerializeField] private int batchEndMinFoodTypes = 5;
        [SerializeField] private int batchEndMaxFoodTypes = 6;
        [SerializeField] private int batchStartActiveEdgeSlots = 6;
        [SerializeField] private int batchEndActiveEdgeSlots = 10;
        [SerializeField] private int batchStartRackSlots = 5;
        [SerializeField] private int batchEndRackSlots = 5;
        [SerializeField] private int batchStartQueueColumns = 3;
        [SerializeField] private int batchEndQueueColumns = 3;
        [SerializeField] private int batchStartMinStackSize = 5;
        [SerializeField] private int batchStartMaxStackSize = 10;
        [SerializeField] private int batchEndMinStackSize = 5;
        [SerializeField] private int batchEndMaxStackSize = 20;
        [SerializeField] private int batchMinQueueRows = 1;
        [SerializeField] private int batchMaxQueueRows = 10;
        [SerializeField] private bool overwriteExistingAssets;
        [SerializeField] private bool assignToOpenLevelManager;

        private readonly List<ItemDataSO> sequencePreview = new List<ItemDataSO>();
        private SerializedObject windowSerializedObject;
        private SerializedProperty demandConfigsProperty;
        private SerializedProperty availableItemsProperty;
        private Vector2 mainScrollPosition;
        private Vector2 batchScrollPosition;
        private Vector2 sequenceScrollPosition;
        private ValidationStatus validationStatus;
        private string validationMessage = "Generate or select a level, then validate it.";
        private MessageType batchMessageType = MessageType.Info;
        private string batchMessage = "Configure the batch ranges, then generate deterministic level assets.";

        [MenuItem("Tools/RestaurantLoop/Level Generator")]
        public static void ShowWindow()
        {
            LevelGeneratorWindow window = GetWindow<LevelGeneratorWindow>("Level Generator");
            window.minSize = new Vector2(480f, 650f);
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
            else
            {
                DrawBatchLevelTab();
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
            if (GUILayout.Button($"Generate All {Mathf.Max(1, totalLevelsToGenerate)} Levels", GUILayout.Height(38f)))
            {
                GenerateBatchLevels();
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(batchMessage, batchMessageType);
            EditorGUILayout.EndScrollView();
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

        private void DrawBatchProgressionSettings()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Difficulty Progression", EditorStyles.boldLabel);
            batchDifficultyCurve = EditorGUILayout.CurveField(
                new GUIContent("Progression Curve", "Maps normalized batch progress to difficulty. Curve values are clamped to 0-1."),
                batchDifficultyCurve);

            DrawStartEndInt("Total Customer Demand", ref batchStartDemand, ref batchEndDemand, 1, 10000);
            DrawStartEndInt("Active Edge Slots", ref batchStartActiveEdgeSlots, ref batchEndActiveEdgeSlots, 3, 20);
            DrawStartEndInt("Rack Slots", ref batchStartRackSlots, ref batchEndRackSlots, 1, 10);
            DrawStartEndInt("Queue Columns", ref batchStartQueueColumns, ref batchEndQueueColumns, 1, 8);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Food Variety", EditorStyles.miniBoldLabel);
            DrawMinMaxInt("Early Level Types", ref batchStartMinFoodTypes, ref batchStartMaxFoodTypes, 1, 50);
            DrawMinMaxInt("Late Level Types", ref batchEndMinFoodTypes, ref batchEndMaxFoodTypes, 1, 50);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Queue Layout Constraints", EditorStyles.miniBoldLabel);
            DrawMinMaxInt("Early Level Stack Size", ref batchStartMinStackSize, ref batchStartMaxStackSize, 5, 500);
            DrawMinMaxInt("Late Level Stack Size", ref batchEndMinStackSize, ref batchEndMaxStackSize, 5, 500);
            DrawMinMaxInt("Allowed Queue Rows", ref batchMinQueueRows, ref batchMaxQueueRows, 1, 15);

            batchStartMinStackSize = SnapStackSize(batchStartMinStackSize);
            batchStartMaxStackSize = Mathf.Max(batchStartMinStackSize, SnapStackSize(batchStartMaxStackSize));
            batchEndMinStackSize = SnapStackSize(batchEndMinStackSize);
            batchEndMaxStackSize = Mathf.Max(batchEndMinStackSize, SnapStackSize(batchEndMaxStackSize));
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

            EditorGUILayout.Space(4f);
            activeEdgeSlotCount = EditorGUILayout.IntSlider("Active Edge Slots", activeEdgeSlotCount, 3, 20);
            rackSlotCount = EditorGUILayout.IntSlider("Rack Slots", rackSlotCount, 1, 10);
            columnCount = EditorGUILayout.IntSlider("Queue Columns", columnCount, 1, 8);
            minStackSize = EditorGUILayout.IntField("Minimum Stack Size", minStackSize);
            maxStackSize = EditorGUILayout.IntField("Maximum Stack Size", maxStackSize);

            minStackSize = SnapStackSize(minStackSize);
            maxStackSize = Mathf.Max(minStackSize, SnapStackSize(maxStackSize));
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(10f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate Level Data", GUILayout.Height(34f))) GenerateLevelData();

                using (new EditorGUI.DisabledScope(targetLevel == null))
                {
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

            Undo.RecordObject(targetLevel, "Generate Deterministic Level Data");
            targetLevel.activeEdgeSlotCount = activeEdgeSlotCount;
            targetLevel.rackSlotCount = rackSlotCount;
            targetLevel.columnCount = columnCount;
            targetLevel.minStackSize = minStackSize;
            targetLevel.maxStackSize = maxStackSize;
            targetLevel.customerDemands = new List<CustomerDemandConfig>(demandConfigs);
            targetLevel.queueStackConfigs = LevelMathUtility.PartitionDemandToStacks(
                targetLevel.customerDemands,
                minStackSize,
                maxStackSize,
                columnCount,
                out int calculatedRows,
                randomSeed);
            targetLevel.calculatedRowCount = calculatedRows;
            targetLevel.SetOrderedCustomerSequence(
                LevelMathUtility.GenerateDeterministicCustomerSequence(targetLevel.customerDemands, randomSeed));

            EditorUtility.SetDirty(targetLevel);
            AssetDatabase.SaveAssets();
            SetValidationStatus(ValidationStatus.NotValidated,
                $"Generated {targetLevel.queueStackConfigs.Count} queue stacks and {targetLevel.OrderedCustomerSequence.Count} customers with seed {randomSeed}.");
            Repaint();
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

            LevelValidationReport report = LevelValidator.AnalyzeLevel(targetLevel);
            if (report.SolvableWithoutPowerUps)
            {
                SetValidationStatus(ValidationStatus.SolvableWithoutPowerUps, "Solvable without power-ups.");
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
                        $"Level_{levelNumber:D2}: generating and validating demand {profile.TotalDemand}, " +
                        $"variety {profile.MinFoodTypes}-{profile.MaxFoodTypes}...",
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
                            $"Level_{levelNumber:D2} could not be made solvable after {MaxSolvabilityAttemptsPerLevel:N0} attempts. " +
                            "Broaden the stack-size or food variety ranges.",
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
                    $"Generated {generatedAssets.Count} validated levels in {batchOutputFolder}." + assignmentMessage,
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
            for (int attempt = 0; attempt < MaxSolvabilityAttemptsPerLevel; attempt++)
            {
                int candidateSeed = unchecked(baseRandomSeed + levelIndex + attempt * 1000);
                if (acceptedSeeds.Contains(candidateSeed)) continue;

                System.Random random = new System.Random(candidateSeed);
                int foodTypeCount = NextInclusive(random, profile.MinFoodTypes, profile.MaxFoodTypes);
                int columns = profile.Columns;

                List<CustomerDemandConfig> demands = CreateRandomDemandDistribution(
                    validItems,
                    profile.TotalDemand,
                    foodTypeCount,
                    random);

                List<QueueStackConfig> stacks = LevelMathUtility.PartitionDemandToStacks(
                    demands,
                    profile.MinStackSize,
                    profile.MaxStackSize,
                    columns,
                    out int rows,
                    candidateSeed);

                if (rows > batchMaxQueueRows) continue;
                if (!HasExactPerItemConservation(demands, stacks)) continue;

                List<ItemDataSO> sequence = LevelMathUtility.GenerateDeterministicCustomerSequence(demands, candidateSeed);
                LevelDataSO validationAsset = CreateInstance<LevelDataSO>();
                ApplyCandidateData(
                    validationAsset,
                    profile.ActiveSlots,
                    profile.RackSlots,
                    columns,
                    rows,
                    demands,
                    stacks,
                    sequence);

                validationAsset.minStackSize = profile.MinStackSize;
                validationAsset.maxStackSize = profile.MaxStackSize;
                LevelValidationReport report = LevelValidator.AnalyzeLevel(validationAsset);
                DestroyImmediate(validationAsset);
                if (!report.SolvableWithoutPowerUps) continue;

                candidate = new BatchLevelCandidate(
                    levelNumber,
                    candidateSeed,
                    profile.ActiveSlots,
                    profile.RackSlots,
                    columns,
                    rows,
                    profile.MinStackSize,
                    profile.MaxStackSize,
                    demands,
                    stacks,
                    sequence);
                return true;
            }

            candidate = null;
            return false;
        }

        private static List<CustomerDemandConfig> CreateRandomDemandDistribution(
            List<ItemDataSO> available,
            int totalDemand,
            int selectedItemCount,
            System.Random random)
        {
            List<ItemDataSO> shuffledItems = new List<ItemDataSO>(available);
            Shuffle(shuffledItems, random);

            selectedItemCount = Mathf.Clamp(selectedItemCount, 1, Mathf.Min(shuffledItems.Count, totalDemand));
            int[] counts = new int[selectedItemCount];
            int countPerItem = totalDemand / selectedItemCount;
            int remainder = totalDemand % selectedItemCount;
            for (int i = 0; i < selectedItemCount; i++)
            {
                counts[i] = countPerItem + (i < remainder ? 1 : 0);
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

        private BatchDifficultyProfile CreateDifficultyProfile(int levelIndex, int availableItemCount)
        {
            float normalizedProgress = totalLevelsToGenerate <= 1
                ? 0f
                : levelIndex / (float)(totalLevelsToGenerate - 1);
            float curveProgress = Mathf.Clamp01(batchDifficultyCurve.Evaluate(normalizedProgress));
            int activeSlots = LerpRounded(batchStartActiveEdgeSlots, batchEndActiveEdgeSlots, curveProgress);
            int rackSlots = LerpRounded(batchStartRackSlots, batchEndRackSlots, curveProgress);
            int columns = LerpRounded(batchStartQueueColumns, batchEndQueueColumns, curveProgress);
            int totalDemand = LerpRounded(batchStartDemand, batchEndDemand, curveProgress);
            int minFoodTypes = Mathf.Clamp(
                LerpRounded(batchStartMinFoodTypes, batchEndMinFoodTypes, curveProgress),
                1,
                Mathf.Min(availableItemCount, totalDemand));
            int maxFoodTypes = Mathf.Clamp(
                LerpRounded(batchStartMaxFoodTypes, batchEndMaxFoodTypes, curveProgress),
                minFoodTypes,
                Mathf.Min(availableItemCount, totalDemand));
            int minStackSize = SnapStackSize(
                LerpRounded(batchStartMinStackSize, batchEndMinStackSize, curveProgress));
            int maxStackSize = Mathf.Max(
                minStackSize,
                SnapStackSize(LerpRounded(batchStartMaxStackSize, batchEndMaxStackSize, curveProgress)));

            return new BatchDifficultyProfile(
                totalDemand,
                minFoodTypes,
                maxFoodTypes,
                activeSlots,
                rackSlots,
                columns,
                minStackSize,
                maxStackSize);
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

        private static void ApplyCandidateData(
            LevelDataSO level,
            int activeSlots,
            int rackSlots,
            int columns,
            int rows,
            List<CustomerDemandConfig> demands,
            List<QueueStackConfig> stacks,
            List<ItemDataSO> sequence)
        {
            level.activeEdgeSlotCount = activeSlots;
            level.rackSlotCount = rackSlots;
            level.columnCount = columns;
            level.calculatedRowCount = rows;
            level.customerDemands = new List<CustomerDemandConfig>(demands);
            level.queueStackConfigs = new List<QueueStackConfig>(stacks);
            level.SetOrderedCustomerSequence(sequence);
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
                candidate.RackSlots,
                candidate.Columns,
                candidate.Rows,
                candidate.Demands,
                candidate.Stacks,
                candidate.Sequence);

            if (isNewAsset)
            {
                AssetDatabase.CreateAsset(level, assetPath);
                Undo.RegisterCreatedObjectUndo(level, "Create Batch Generated Level");
            }

            EditorUtility.SetDirty(level);
            SetGeneratedSeedLabel(level, candidate.Seed);
            return level;
        }

        private static void SetGeneratedSeedLabel(LevelDataSO level, int seed)
        {
            const string seedLabelPrefix = "RestaurantLoopSeed_";
            string[] existingLabels = AssetDatabase.GetLabels(level);
            List<string> updatedLabels = new List<string>(existingLabels.Length + 1);
            for (int i = 0; i < existingLabels.Length; i++)
            {
                if (!existingLabels[i].StartsWith(seedLabelPrefix, StringComparison.Ordinal))
                {
                    updatedLabels.Add(existingLabels[i]);
                }
            }

            updatedLabels.Add(seedLabelPrefix + seed);
            AssetDatabase.SetLabels(level, updatedLabels.ToArray());
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
            sequenceProperty.arraySize = generatedAssets.Count;
            for (int i = 0; i < generatedAssets.Count; i++)
            {
                sequenceProperty.GetArrayElementAtIndex(i).objectReferenceValue = generatedAssets[i];
            }

            managerSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            if (manager.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            return " Assigned them to the open scene LevelManager.";
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

            int requiredFoodTypes = Mathf.Max(batchStartMinFoodTypes, batchEndMinFoodTypes);
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
            ClampStartEnd(ref batchStartDemand, ref batchEndDemand, 1, 10000);
            ClampStartEnd(ref batchStartActiveEdgeSlots, ref batchEndActiveEdgeSlots, 3, 20);
            ClampStartEnd(ref batchStartRackSlots, ref batchEndRackSlots, 1, 10);
            ClampStartEnd(ref batchStartQueueColumns, ref batchEndQueueColumns, 1, 8);
            ClampRange(ref batchStartMinFoodTypes, ref batchStartMaxFoodTypes, 1, 50);
            ClampRange(ref batchEndMinFoodTypes, ref batchEndMaxFoodTypes, 1, 50);
            batchEndMinFoodTypes = Mathf.Max(batchStartMinFoodTypes, batchEndMinFoodTypes);
            batchEndMaxFoodTypes = Mathf.Max(batchEndMinFoodTypes, batchEndMaxFoodTypes);
            ClampRange(ref batchStartMinStackSize, ref batchStartMaxStackSize, 5, 500);
            ClampRange(ref batchEndMinStackSize, ref batchEndMaxStackSize, 5, 500);
            batchEndMinStackSize = Mathf.Max(batchStartMinStackSize, batchEndMinStackSize);
            batchEndMaxStackSize = Mathf.Max(batchEndMinStackSize, batchEndMaxStackSize);
            ClampRange(ref batchMinQueueRows, ref batchMaxQueueRows, 1, 15);
            batchStartMinStackSize = SnapStackSize(batchStartMinStackSize);
            batchStartMaxStackSize = Mathf.Max(batchStartMinStackSize, SnapStackSize(batchStartMaxStackSize));
            batchEndMinStackSize = SnapStackSize(batchEndMinStackSize);
            batchEndMaxStackSize = Mathf.Max(batchEndMinStackSize, SnapStackSize(batchEndMaxStackSize));
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
            rackSlotCount = targetLevel.rackSlotCount;
            columnCount = targetLevel.columnCount;
            minStackSize = targetLevel.minStackSize;
            maxStackSize = targetLevel.maxStackSize;
            demandConfigs = targetLevel.customerDemands != null
                ? new List<CustomerDemandConfig>(targetLevel.customerDemands)
                : new List<CustomerDemandConfig>();
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
                if (demandConfigs[i].itemData == null || demandConfigs[i].totalCustomerCount <= 0)
                {
                    error = $"Customer demand #{i + 1} requires an item and a positive count.";
                    return false;
                }
            }

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
            if (!force && windowSerializedObject != null && demandConfigsProperty != null && availableItemsProperty != null) return;
            windowSerializedObject = new SerializedObject(this);
            demandConfigsProperty = windowSerializedObject.FindProperty(nameof(demandConfigs));
            availableItemsProperty = windowSerializedObject.FindProperty(nameof(availableItems));
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

        private static void DrawStartEndInt(
            string label,
            ref int start,
            ref int end,
            int allowedMinimum,
            int allowedMaximum)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                GUILayout.Label("Start", EditorStyles.miniLabel, GUILayout.Width(34f));
                start = EditorGUILayout.IntField(start, GUILayout.MinWidth(45f));
                GUILayout.Label("End", EditorStyles.miniLabel, GUILayout.Width(28f));
                end = EditorGUILayout.IntField(end, GUILayout.MinWidth(45f));
            }

            ClampStartEnd(ref start, ref end, allowedMinimum, allowedMaximum);
        }

        private static void ClampRange(ref int minimum, ref int maximum, int allowedMinimum, int allowedMaximum)
        {
            minimum = Mathf.Clamp(minimum, allowedMinimum, allowedMaximum);
            maximum = Mathf.Clamp(maximum, minimum, allowedMaximum);
        }

        private static void ClampStartEnd(ref int start, ref int end, int allowedMinimum, int allowedMaximum)
        {
            start = Mathf.Clamp(start, allowedMinimum, allowedMaximum);
            end = Mathf.Clamp(end, start, allowedMaximum);
        }

        private static int LerpRounded(int start, int end, float progress)
        {
            return Mathf.RoundToInt(Mathf.Lerp(start, end, progress));
        }

        private static int NextInclusive(System.Random random, int minimum, int maximum)
        {
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
            public int RackSlots { get; }
            public int Columns { get; }
            public int Rows { get; }
            public int MinStackSize { get; }
            public int MaxStackSize { get; }
            public List<CustomerDemandConfig> Demands { get; }
            public List<QueueStackConfig> Stacks { get; }
            public List<ItemDataSO> Sequence { get; }

            public BatchLevelCandidate(
                int levelNumber,
                int seed,
                int activeSlots,
                int rackSlots,
                int columns,
                int rows,
                int minStackSize,
                int maxStackSize,
                List<CustomerDemandConfig> demands,
                List<QueueStackConfig> stacks,
                List<ItemDataSO> sequence)
            {
                LevelNumber = levelNumber;
                Seed = seed;
                ActiveSlots = activeSlots;
                RackSlots = rackSlots;
                Columns = columns;
                Rows = rows;
                MinStackSize = minStackSize;
                MaxStackSize = maxStackSize;
                Demands = demands;
                Stacks = stacks;
                Sequence = sequence;
            }
        }

        private readonly struct BatchDifficultyProfile
        {
            public int TotalDemand { get; }
            public int MinFoodTypes { get; }
            public int MaxFoodTypes { get; }
            public int ActiveSlots { get; }
            public int RackSlots { get; }
            public int Columns { get; }
            public int MinStackSize { get; }
            public int MaxStackSize { get; }

            public BatchDifficultyProfile(
                int totalDemand,
                int minFoodTypes,
                int maxFoodTypes,
                int activeSlots,
                int rackSlots,
                int columns,
                int minStackSize,
                int maxStackSize)
            {
                TotalDemand = totalDemand;
                MinFoodTypes = minFoodTypes;
                MaxFoodTypes = maxFoodTypes;
                ActiveSlots = activeSlots;
                RackSlots = rackSlots;
                Columns = columns;
                MinStackSize = minStackSize;
                MaxStackSize = maxStackSize;
            }
        }
    }
}