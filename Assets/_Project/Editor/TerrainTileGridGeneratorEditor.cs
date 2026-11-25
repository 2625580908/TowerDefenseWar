using MRStrategyRTS.Shared;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MRStrategyRTS.Editor
{
    [CustomEditor(typeof(TerrainTileGridGenerator))]
    public sealed class TerrainTileGridGeneratorEditor : UnityEditor.Editor
    {
        private const int CurrentTileLayoutVersion = 1;

        private SerializedProperty terrainBlocksProperty;
        private SerializedProperty autoRebuildInEditorProperty;
        private readonly Dictionary<int, int> selectedBrushByBlock = new();

        private void OnEnable()
        {
            terrainBlocksProperty = serializedObject.FindProperty("terrainBlocks");
            autoRebuildInEditorProperty = serializedObject.FindProperty("autoRebuildInEditor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(autoRebuildInEditorProperty);
            EditorGUILayout.PropertyField(terrainBlocksProperty, new GUIContent("Terrain Blocks"), true);
            var propertiesChanged = EditorGUI.EndChangeCheck();

            if (terrainBlocksProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Add a Terrain Block to paint a tile layout.", MessageType.Info);
            }

            for (var blockIndex = 0; blockIndex < terrainBlocksProperty.arraySize; blockIndex++)
            {
                DrawTileLayout(blockIndex, terrainBlocksProperty.GetArrayElementAtIndex(blockIndex));
            }

            if (serializedObject.ApplyModifiedProperties() || propertiesChanged)
            {
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Tile Grid"))
                {
                    ((TerrainTileGridGenerator)target).Rebuild();
                }

                if (GUILayout.Button("Clear Tile Grid"))
                {
                    ((TerrainTileGridGenerator)target).Clear();
                }
            }
        }

        private void DrawTileLayout(int blockIndex, SerializedProperty blockProperty)
        {
            var rowsProperty = blockProperty.FindPropertyRelative("rows");
            var columnsProperty = blockProperty.FindPropertyRelative("columns");
            var variantsProperty = blockProperty.FindPropertyRelative("tileVariants");
            var tileTypeIndicesProperty = blockProperty.FindPropertyRelative("tileTypeIndices");
            var tileLayoutVersionProperty = blockProperty.FindPropertyRelative("tileLayoutVersion");

            var rows = Mathf.Max(1, rowsProperty.intValue);
            var columns = Mathf.Max(1, columnsProperty.intValue);
            MigrateLegacyTileIndices(tileTypeIndicesProperty, tileLayoutVersionProperty);
            ResizeTileLayout(tileTypeIndicesProperty, rows * columns);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Block {blockIndex + 1} Tile Layout ({columns} x {rows})", EditorStyles.boldLabel);

            if (variantsProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Add tile prefabs to Tile Variants before painting the layout.", MessageType.Warning);
                return;
            }

            DrawTileGenerationSettings(variantsProperty);

            var brushIndex = GetSelectedBrushIndex(blockIndex, variantsProperty.arraySize);
            var brushTileObject = GetTileObject(variantsProperty, brushIndex);
            EditorGUILayout.LabelField(
                $"Current Tile Brush: {GetTileTypeLabel(brushIndex, brushTileObject)}",
                EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                for (var tileTypeIndex = 0; tileTypeIndex < variantsProperty.arraySize; tileTypeIndex++)
                {
                    var tileObject = GetTileObject(variantsProperty, tileTypeIndex);
                    var typeLabel = GetTileTypeLabel(tileTypeIndex, tileObject);
                    var content = GetTileContent(
                        tileObject,
                        GetTilePreview(variantsProperty, tileTypeIndex),
                        $"Use {typeLabel} as the brush");
                    var isSelected = tileTypeIndex == brushIndex;
                    using (new EditorGUILayout.VerticalScope(GUILayout.Width(56f)))
                    {
                        if (GUILayout.Toggle(isSelected, content, "Button", GUILayout.Width(56f), GUILayout.Height(42f)) && !isSelected)
                        {
                            selectedBrushByBlock[blockIndex] = tileTypeIndex;
                            brushIndex = tileTypeIndex;
                        }

                        GUILayout.Label($"{tileTypeIndex + 1:D2}", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(56f));
                    }
                }
            }
            EditorGUILayout.LabelField(GetTileTypeLegend(variantsProperty), EditorStyles.miniLabel);

            for (var row = 0; row < rows; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var column = 0; column < columns; column++)
                    {
                        var typeIndexProperty = tileTypeIndicesProperty.GetArrayElementAtIndex(row * columns + column);
                        var tileObject = GetTileObject(variantsProperty, typeIndexProperty.intValue);
                        if (GUILayout.Button(
                                GetTileContent(
                                    tileObject,
                                    GetTilePreview(variantsProperty, typeIndexProperty.intValue),
                                    $"Paint with {GetTileTypeLabel(brushIndex, GetTileObject(variantsProperty, brushIndex))}"),
                                GUILayout.Width(42f),
                                GUILayout.Height(42f)))
                        {
                            typeIndexProperty.intValue = brushIndex;
                        }
                    }
                }
            }

            EditorGUILayout.LabelField("Choose a tile brush, then click cells to paint.", EditorStyles.miniLabel);
        }

        private static void DrawTileGenerationSettings(SerializedProperty variantsProperty)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tile Type Generation Settings", EditorStyles.boldLabel);
            for (var tileTypeIndex = 0; tileTypeIndex < variantsProperty.arraySize; tileTypeIndex++)
            {
                var variantProperty = variantsProperty.GetArrayElementAtIndex(tileTypeIndex);
                var tagProperty = variantProperty.FindPropertyRelative("generatedTag");
                var colliderProperty = variantProperty.FindPropertyRelative("generateCollider");
                var tileObject = GetTileObject(variantsProperty, tileTypeIndex);

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        GetTileTypeLabel(tileTypeIndex, tileObject),
                        EditorStyles.miniBoldLabel);
                    tagProperty.stringValue = EditorGUILayout.TagField(
                        new GUIContent("Generated Tag"),
                        tagProperty.stringValue);
                    EditorGUILayout.PropertyField(
                        colliderProperty,
                        new GUIContent("Generate Collider"));
                }
            }
        }

        private static void MigrateLegacyTileIndices(
            SerializedProperty tileTypeIndicesProperty,
            SerializedProperty tileLayoutVersionProperty)
        {
            if (tileLayoutVersionProperty.intValue >= CurrentTileLayoutVersion)
            {
                return;
            }

            for (var index = 0; index < tileTypeIndicesProperty.arraySize; index++)
            {
                var tileTypeIndexProperty = tileTypeIndicesProperty.GetArrayElementAtIndex(index);
                if (tileTypeIndexProperty.intValue > 0)
                {
                    tileTypeIndexProperty.intValue--;
                }
            }

            tileLayoutVersionProperty.intValue = CurrentTileLayoutVersion;
        }

        private static void ResizeTileLayout(SerializedProperty tileTypeIndicesProperty, int tileCount)
        {
            var previousCount = tileTypeIndicesProperty.arraySize;
            tileTypeIndicesProperty.arraySize = tileCount;
            for (var index = previousCount; index < tileCount; index++)
            {
                tileTypeIndicesProperty.GetArrayElementAtIndex(index).intValue = -1;
            }
        }

        private int GetSelectedBrushIndex(int blockIndex, int tileTypeCount)
        {
            if (!selectedBrushByBlock.TryGetValue(blockIndex, out var brushIndex))
            {
                brushIndex = 0;
                selectedBrushByBlock[blockIndex] = brushIndex;
            }

            return Mathf.Clamp(brushIndex, 0, tileTypeCount - 1);
        }

        private static Object GetTileObject(SerializedProperty variantsProperty, int tileTypeIndex)
        {
            return tileTypeIndex < 0 || tileTypeIndex >= variantsProperty.arraySize
                ? null
                : variantsProperty.GetArrayElementAtIndex(tileTypeIndex)
                    .FindPropertyRelative("tileObject")
                    .objectReferenceValue;
        }

        private static Texture2D GetTilePreview(SerializedProperty variantsProperty, int tileTypeIndex)
        {
            return tileTypeIndex < 0 || tileTypeIndex >= variantsProperty.arraySize
                ? null
                : variantsProperty.GetArrayElementAtIndex(tileTypeIndex)
                    .FindPropertyRelative("editorPreview")
                    .objectReferenceValue as Texture2D;
        }

        private static GUIContent GetTileContent(Object tileObject, Texture2D editorPreview, string tooltip)
        {
            var image = editorPreview != null
                ? editorPreview
                : tileObject == null
                ? null
                : AssetPreview.GetMiniThumbnail(tileObject);
            return new GUIContent(image, tooltip);
        }

        private static string GetTileName(Object tileObject)
        {
            return tileObject == null ? "empty tile" : tileObject.name;
        }

        private static string GetTileTypeLabel(int tileTypeIndex, Object tilePrefab)
        {
            return $"{tileTypeIndex + 1:D2} - {GetTileName(tilePrefab)}";
        }

        private static string GetTileTypeLegend(SerializedProperty variantsProperty)
        {
            var legend = "Tile Types: ";
            for (var index = 0; index < variantsProperty.arraySize; index++)
            {
                if (index > 0)
                {
                    legend += " | ";
                }

                legend += GetTileTypeLabel(
                    index,
                    GetTileObject(variantsProperty, index));
            }

            return legend;
        }
    }
}
