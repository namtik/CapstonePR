using UnityEditor;
using UnityEngine;

public static class SashindoMapPatchSettings
{
    private const string DefaultRelicDataGuid = "501ec3b48a054c7f9e6b1d40fdf166cc";

    [MenuItem("CONTEXT/MapGenerator/Apply Sashindo Map Patch Settings")]
    private static void ApplyFromInspector(MenuCommand command)
    {
        Apply((MapGenerator)command.context);
    }

    public static void Apply(MapGenerator generator)
    {
        Undo.RecordObject(generator, "Apply Sashindo Map Patch Settings");
        generator.totalColumns = 9;
        generator.eliteChance = 0.2f;
        generator.eliteColumnMin = 2;
        generator.minNodesPerColumn = 3;
        generator.maxNodesPerColumn = 4;
        generator.shopSettings = Settings(2, 2, 3, 5);
        generator.eventSettings = Settings(2, 3, 3, 8);
        generator.relicSettings = Settings(2, 2, 3, 8);
        generator.restSettings = Settings(2, 2, 3, 8);
        PrefabUtility.RecordPrefabInstancePropertyModifications(generator);
        EditorUtility.SetDirty(generator);
        AssignDefaultRelicRound(generator);
    }

    private static void AssignDefaultRelicRound(MapGenerator generator)
    {
        var generatorData = new SerializedObject(generator);
        var config = generatorData.FindProperty("roundDataConfig").objectReferenceValue as RoundDataConfig;
        if (config == null || config.relicData != null) return;

        string assetPath = AssetDatabase.GUIDToAssetPath(DefaultRelicDataGuid);
        var relicRound = AssetDatabase.LoadAssetAtPath<RelicRoundData>(assetPath);
        if (relicRound == null)
        {
            Debug.LogWarning("Sashindo map patch could not load the default relic round data.", generator);
            return;
        }

        Undo.RecordObject(config, "Assign Sashindo Relic Round Data");
        config.relicData = relicRound;
        EditorUtility.SetDirty(config);
    }

    private static MapGenerator.SpecialNodePlacementSettings Settings(int min, int max, int firstColumn, int lastColumn)
    {
        return new MapGenerator.SpecialNodePlacementSettings
        {
            minNodes = min,
            maxNodes = max,
            columnMin = firstColumn,
            columnMax = lastColumn
        };
    }
}