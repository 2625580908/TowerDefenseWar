using System.Collections.Generic;
using System.Linq;
using MRStrategyRTS.Config;
using MRStrategyRTS.Shared;
using UnityEditor;
using UnityEngine;

namespace MRStrategyRTS.Editor
{
    /// <summary>
    /// Central editor for tower-defense level, wave, and monster definitions.
    /// </summary>
    public sealed class TowerDefenseLevelEditorWindow : EditorWindow
    {
        private enum ConfigTab
        {
            Levels,
            Waves,
            Monsters,
            Towers,
            Unlocks
        }

        private const string LevelsFolder = "Assets/_Project/Data/Levels";
        private const string WavesFolder = "Assets/_Project/Data/Waves";
        private const string MonstersFolder = "Assets/_Project/Data/Monsters";
        private const string TowersFolder = "Assets/_Project/Data/Towers";

        private ConfigTab activeTab;
        private ScriptableObject selectedDefinition;
        private UnityEditor.Editor selectedInspector;
        private WaveDefinition waveToAdd;
        private Vector2 assetListScrollPosition;
        private Vector2 inspectorScrollPosition;
        [SerializeField] private LevelProgressManager levelProgressManager;

        [MenuItem("Tools/MR Strategy RTS/关卡数值编辑器")]
        private static void Open()
        {
            var window = GetWindow<TowerDefenseLevelEditorWindow>();
            window.titleContent = new GUIContent("关卡数值编辑器");
            window.minSize = new Vector2(760f, 500f);
            window.Show();
        }

        private void OnDisable()
        {
            if (selectedInspector != null)
            {
                DestroyImmediate(selectedInspector);
            }
        }

        private void OnProjectChange()
        {
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space();

            if (activeTab == ConfigTab.Unlocks)
            {
                DrawUnlockConfiguration();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawAssetList();
                DrawInspector();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                activeTab = (ConfigTab)GUILayout.Toolbar(
                    (int)activeTab,
                    new[] { "关卡", "波次", "怪物", "防御塔", "解锁配置" },
                    EditorStyles.toolbarButton);

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(52f)))
                {
                    Repaint();
                }
            }
        }

        private void DrawUnlockConfiguration()
        {
            EditorGUILayout.LabelField("关卡解锁配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "按顺序配置 6 个关卡。第 1 关默认解锁；通关第 N 关后解锁第 N+1 关。",
                MessageType.Info);

            levelProgressManager = (LevelProgressManager)EditorGUILayout.ObjectField(
                "Level Progress Manager",
                levelProgressManager,
                typeof(LevelProgressManager),
                true);

            if (levelProgressManager == null
                && GUILayout.Button("查找当前场景的进度管理器", GUILayout.Height(25f)))
            {
                levelProgressManager = FindFirstObjectByType<LevelProgressManager>(
                    FindObjectsInactive.Include);
            }

            if (levelProgressManager == null)
            {
                EditorGUILayout.HelpBox(
                    "请将场景中的 LevelProgressManager 拖入上方字段。",
                    MessageType.Warning);
                return;
            }

            var serializedManager = new SerializedObject(levelProgressManager);
            var levelsProperty = serializedManager.FindProperty("levels");
            if (levelsProperty == null || !levelsProperty.isArray)
            {
                EditorGUILayout.HelpBox(
                    "LevelProgressManager 中未找到关卡列表字段。",
                    MessageType.Error);
                return;
            }

            serializedManager.Update();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("解锁顺序", EditorStyles.boldLabel);
            for (var index = 0; index < levelsProperty.arraySize; index++)
            {
                var levelProperty = levelsProperty.GetArrayElementAtIndex(index);
                EditorGUILayout.PropertyField(levelProperty, new GUIContent($"第 {index + 1} 关"));
            }

            if (serializedManager.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(levelProgressManager);
                AssetDatabase.SaveAssets();
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("本地进度（Edit 测试）", EditorStyles.boldLabel);
            DrawUnlockProgressTools(levelsProperty);
        }

        private static void DrawUnlockProgressTools(SerializedProperty levelsProperty)
        {
            const string completionKeyPrefix = "MRStrategyRTS.LevelProgress.Completed.";
            for (var index = 0; index < levelsProperty.arraySize; index++)
            {
                var completed = PlayerPrefs.GetInt($"{completionKeyPrefix}{index}", 0) == 1;
                var unlocked = index == 0
                    || PlayerPrefs.GetInt($"{completionKeyPrefix}{index - 1}", 0) == 1;
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"第 {index + 1} 关", GUILayout.Width(70f));
                    EditorGUILayout.LabelField(
                        completed ? "已通关" : unlocked ? "已解锁" : "未解锁",
                        GUILayout.Width(70f));
                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledScope(completed))
                    {
                        if (GUILayout.Button("设为通关", GUILayout.Width(80f)))
                        {
                            PlayerPrefs.SetInt($"{completionKeyPrefix}{index}", 1);
                            PlayerPrefs.Save();
                        }
                    }

                    using (new EditorGUI.DisabledScope(!completed))
                    {
                        if (GUILayout.Button("清除通关", GUILayout.Width(80f)))
                        {
                            PlayerPrefs.DeleteKey($"{completionKeyPrefix}{index}");
                            PlayerPrefs.Save();
                        }
                    }
                }
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("清除所有本地关卡进度", GUILayout.Height(26f)))
            {
                for (var index = 0; index < levelsProperty.arraySize; index++)
                {
                    PlayerPrefs.DeleteKey($"{completionKeyPrefix}{index}");
                }

                PlayerPrefs.Save();
            }
        }

        private void DrawAssetList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(230f)))
            {
                EditorGUILayout.LabelField(GetTabTitle(), EditorStyles.boldLabel);
                if (GUILayout.Button(GetCreateButtonText(), GUILayout.Height(28f)))
                {
                    CreateDefinitionForActiveTab();
                }

                EditorGUILayout.Space(4f);
                assetListScrollPosition = EditorGUILayout.BeginScrollView(assetListScrollPosition);
                foreach (var definition in GetDefinitionsForActiveTab())
                {
                    var isSelected = definition == selectedDefinition;
                    var style = new GUIStyle(EditorStyles.miniButton)
                    {
                        alignment = TextAnchor.MiddleLeft
                    };
                    if (GUILayout.Toggle(isSelected, definition.name, style) && !isSelected)
                    {
                        SelectDefinition(definition);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawInspector()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (selectedDefinition == null)
                {
                    EditorGUILayout.HelpBox(
                        "从左侧选择一个配置资产，或创建新的关卡、波次、怪物配置。",
                        MessageType.Info);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(selectedDefinition.name, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("定位", GUILayout.Width(52f)))
                    {
                        EditorGUIUtility.PingObject(selectedDefinition);
                        Selection.activeObject = selectedDefinition;
                    }
                }

                inspectorScrollPosition = EditorGUILayout.BeginScrollView(inspectorScrollPosition);
                UnityEditor.Editor.CreateCachedEditor(
                    selectedDefinition,
                    null,
                    ref selectedInspector);
                EditorGUI.BeginChangeCheck();
                selectedInspector.OnInspectorGUI();
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(selectedDefinition);
                    AssetDatabase.SaveAssets();
                }

                DrawDefinitionActions();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawDefinitionActions()
        {
            EditorGUILayout.Space();
            if (selectedDefinition is LevelDefinition level)
            {
                DrawLevelActions(level);
                return;
            }

            if (selectedDefinition is WaveDefinition wave)
            {
                DrawWaveActions(wave);
            }
        }

        private void DrawLevelActions(LevelDefinition level)
        {
            EditorGUILayout.LabelField("关卡快捷操作", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("新建波次并加入关卡"))
                {
                    var wave = CreateDefinition<WaveDefinition>(WavesFolder, "New Wave");
                    Undo.RecordObject(level, "Add Wave To Level");
                    level.waves.Add(wave);
                    EditorUtility.SetDirty(level);
                    AssetDatabase.SaveAssets();
                    activeTab = ConfigTab.Waves;
                    SelectDefinition(wave);
                }

                if (GUILayout.Button("检查配置"))
                {
                    ValidateLevel(level);
                }

                if (GUILayout.Button("删除当前关卡"))
                {
                    DeleteLevel(level);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                waveToAdd = (WaveDefinition)EditorGUILayout.ObjectField(
                    "已有波次",
                    waveToAdd,
                    typeof(WaveDefinition),
                    false);
                using (new EditorGUI.DisabledScope(waveToAdd == null))
                {
                    if (GUILayout.Button("添加到关卡", GUILayout.Width(90f)))
                    {
                        Undo.RecordObject(level, "Add Existing Wave To Level");
                        level.waves.Add(waveToAdd);
                        EditorUtility.SetDirty(level);
                        AssetDatabase.SaveAssets();
                    }
                }
            }
        }

        private void DrawWaveActions(WaveDefinition wave)
        {
            EditorGUILayout.LabelField("波次快捷操作", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加怪物生成条目"))
                {
                    Undo.RecordObject(wave, "Add Monster Spawn Entry");
                    wave.monsterSpawns.Add(new MonsterSpawnEntry());
                    EditorUtility.SetDirty(wave);
                    AssetDatabase.SaveAssets();
                }

                if (GUILayout.Button("新建怪物配置"))
                {
                    var monster = CreateDefinition<MonsterDefinition>(MonstersFolder, "New Monster");
                    activeTab = ConfigTab.Monsters;
                    SelectDefinition(monster);
                }

                if (GUILayout.Button("删除当前波次"))
                {
                    DeleteWave(wave);
                }
            }
        }

        private void ValidateLevel(LevelDefinition level)
        {
            var messages = new List<string>();
            if (level.waves.Count == 0)
            {
                messages.Add("关卡没有配置波次。");
            }

            for (var waveIndex = 0; waveIndex < level.waves.Count; waveIndex++)
            {
                var wave = level.waves[waveIndex];
                if (wave == null)
                {
                    messages.Add($"波次 {waveIndex + 1} 引用为空。");
                    continue;
                }

                if (wave.monsterSpawns.Count == 0)
                {
                    messages.Add($"{wave.name} 没有怪物生成条目。");
                }

                for (var spawnIndex = 0; spawnIndex < wave.monsterSpawns.Count; spawnIndex++)
                {
                    var spawn = wave.monsterSpawns[spawnIndex];
                    if (spawn.monster == null)
                    {
                        messages.Add($"{wave.name} 的第 {spawnIndex + 1} 个条目没有怪物类型。");
                    }
                    else if (spawn.monster.prefab == null)
                    {
                        messages.Add($"{spawn.monster.name} 没有配置 Prefab。");
                    }
                }
            }

            if (messages.Count == 0)
            {
                EditorUtility.DisplayDialog("关卡配置检查", "配置完整。", "确定");
                return;
            }

            EditorUtility.DisplayDialog(
                "关卡配置检查",
                string.Join("\n", messages),
                "确定");
        }

        private void DeleteLevel(LevelDefinition level)
        {
            var assetPath = AssetDatabase.GetAssetPath(level);
            if (string.IsNullOrEmpty(assetPath)
                || !EditorUtility.DisplayDialog(
                    "删除关卡",
                    $"确定删除关卡配置“{level.name}”？\n\n不会删除它引用的波次和怪物配置。",
                    "删除",
                    "取消"))
            {
                return;
            }

            if (!AssetDatabase.DeleteAsset(assetPath))
            {
                EditorUtility.DisplayDialog("删除关卡", "无法删除该关卡配置。", "确定");
                return;
            }

            AssetDatabase.SaveAssets();
            if (selectedInspector != null)
            {
                DestroyImmediate(selectedInspector);
                selectedInspector = null;
            }

            selectedDefinition = null;
            Repaint();
        }

        private void DeleteWave(WaveDefinition wave)
        {
            var assetPath = AssetDatabase.GetAssetPath(wave);
            var levelsUsingWave = FindDefinitions<LevelDefinition>()
                .OfType<LevelDefinition>()
                .Where(level => level.waves.Contains(wave))
                .ToList();
            var referenceMessage = levelsUsingWave.Count == 0
                ? string.Empty
                : $"\n\n将从 {levelsUsingWave.Count} 个关卡配置中移除此波次。";
            if (string.IsNullOrEmpty(assetPath)
                || !EditorUtility.DisplayDialog(
                    "删除波次",
                    $"确定删除波次配置“{wave.name}”？{referenceMessage}",
                    "删除",
                    "取消"))
            {
                return;
            }

            foreach (var level in levelsUsingWave)
            {
                Undo.RecordObject(level, "Remove Deleted Wave From Level");
                level.waves.RemoveAll(candidate => candidate == wave);
                EditorUtility.SetDirty(level);
            }

            if (!AssetDatabase.DeleteAsset(assetPath))
            {
                EditorUtility.DisplayDialog("删除波次", "无法删除该波次配置。", "确定");
                return;
            }

            AssetDatabase.SaveAssets();
            if (selectedInspector != null)
            {
                DestroyImmediate(selectedInspector);
                selectedInspector = null;
            }

            selectedDefinition = null;
            Repaint();
        }

        private void CreateDefinitionForActiveTab()
        {
            switch (activeTab)
            {
                case ConfigTab.Levels:
                    SelectDefinition(CreateDefinition<LevelDefinition>(LevelsFolder, "New Level"));
                    break;
                case ConfigTab.Waves:
                    SelectDefinition(CreateDefinition<WaveDefinition>(WavesFolder, "New Wave"));
                    break;
                case ConfigTab.Monsters:
                    SelectDefinition(CreateDefinition<MonsterDefinition>(MonstersFolder, "New Monster"));
                    break;
                case ConfigTab.Towers:
                    SelectDefinition(CreateDefinition<TowerDefinition>(TowersFolder, "New Tower"));
                    break;
            }
        }

        private static T CreateDefinition<T>(string folder, string defaultName)
            where T : ScriptableObject
        {
            EnsureFolder(folder);
            var definition = CreateInstance<T>();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{defaultName}.asset");
            AssetDatabase.CreateAsset(definition, path);
            AssetDatabase.SaveAssets();
            return definition;
        }

        private void SelectDefinition(ScriptableObject definition)
        {
            selectedDefinition = definition;
            inspectorScrollPosition = Vector2.zero;
            if (definition != null)
            {
                Selection.activeObject = definition;
                EditorGUIUtility.PingObject(definition);
            }

            Repaint();
        }

        private IEnumerable<ScriptableObject> GetDefinitionsForActiveTab()
        {
            switch (activeTab)
            {
                case ConfigTab.Levels:
                    return FindDefinitions<LevelDefinition>();
                case ConfigTab.Waves:
                    return FindDefinitions<WaveDefinition>();
                case ConfigTab.Monsters:
                    return FindDefinitions<MonsterDefinition>();
                case ConfigTab.Towers:
                    return FindDefinitions<TowerDefinition>();
                default:
                    return Enumerable.Empty<ScriptableObject>();
            }
        }

        private static IEnumerable<ScriptableObject> FindDefinitions<T>()
            where T : ScriptableObject
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(definition => definition != null)
                .OrderBy(definition => definition.name);
        }

        private string GetTabTitle()
        {
            switch (activeTab)
            {
                case ConfigTab.Levels:
                    return "关卡配置";
                case ConfigTab.Waves:
                    return "波次配置";
                case ConfigTab.Monsters:
                    return "怪物配置";
                case ConfigTab.Towers:
                    return "防御塔配置";
                default:
                    return string.Empty;
            }
        }

        private string GetCreateButtonText()
        {
            switch (activeTab)
            {
                case ConfigTab.Levels:
                    return "新建关卡配置";
                case ConfigTab.Waves:
                    return "新建波次配置";
                case ConfigTab.Monsters:
                    return "新建怪物配置";
                case ConfigTab.Towers:
                    return "新建防御塔配置";
                default:
                    return "新建";
            }
        }

        private static void EnsureFolder(string folder)
        {
            var parts = folder.Split('/');
            var currentFolder = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var nextFolder = $"{currentFolder}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(nextFolder))
                {
                    AssetDatabase.CreateFolder(currentFolder, parts[index]);
                }

                currentFolder = nextFolder;
            }
        }
    }
}
