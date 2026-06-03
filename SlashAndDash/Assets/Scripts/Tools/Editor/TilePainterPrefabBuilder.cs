using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TilePainterPrefabBuilder
{
    public const string BaseTilePrefabPath = "Assets/Prefab/Castle/Tile.prefab";
    public const string GeneratedPrefabFolder = "Assets/Prefab/Castle/TileTool";
    public const string GeneratedMeshFolder = GeneratedPrefabFolder + "/Meshes";
    public const string VoidMaterialPath = "Assets/Materials/TileTool/BlackVoid.mat";

    const float DefaultTileSize = 60f;
    const float DefaultVerticalSpacing = 48f;
    const float DefaultRampAngle = 22.5f;
    const float DefaultRampThickness = 1.2f;
    const float DefaultPitWallThickness = 1.2f;
    const float DefaultPitRespawnTriggerHeight = 14f;

    [InitializeOnLoadMethod]
    static void QueuePrefabCheck()
    {
        EditorApplication.delayCall += EnsurePrefabsExist;
    }

    [MenuItem("Tools/Slash And Dash/Rebuild Tile Painter Prefabs")]
    public static void RebuildPrefabs()
    {
        EnsureFolders();
        EnsureVoidMaterial();

        CreateOrReplacePrefab(TileGridCellKind.Pit);
        CreateOrReplacePrefab(TileGridCellKind.RampPositiveX);
        CreateOrReplacePrefab(TileGridCellKind.RampNegativeX);
        CreateOrReplacePrefab(TileGridCellKind.RampPositiveZ);
        CreateOrReplacePrefab(TileGridCellKind.RampNegativeZ);
        CreateOrReplacePrefab(TileGridCellKind.StairRampPositiveX);
        CreateOrReplacePrefab(TileGridCellKind.StairRampNegativeX);
        CreateOrReplacePrefab(TileGridCellKind.StairRampPositiveZ);
        CreateOrReplacePrefab(TileGridCellKind.StairRampNegativeZ);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void EnsurePrefabsExist()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsurePrefabsExist;
            return;
        }

        GameObject baseTile = AssetDatabase.LoadAssetAtPath<GameObject>(BaseTilePrefabPath);
        if (baseTile == null)
            return;

        EnsureFolders();
        EnsureVoidMaterial();

        CreatePrefabIfMissing(TileGridCellKind.Pit);
        CreatePrefabIfMissing(TileGridCellKind.RampPositiveX);
        CreatePrefabIfMissing(TileGridCellKind.RampNegativeX);
        CreatePrefabIfMissing(TileGridCellKind.RampPositiveZ);
        CreatePrefabIfMissing(TileGridCellKind.RampNegativeZ);
        CreatePrefabIfMissing(TileGridCellKind.StairRampPositiveX);
        CreatePrefabIfMissing(TileGridCellKind.StairRampNegativeX);
        CreatePrefabIfMissing(TileGridCellKind.StairRampPositiveZ);
        CreatePrefabIfMissing(TileGridCellKind.StairRampNegativeZ);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static string GetPrefabPath(TileGridCellKind kind)
    {
        switch (kind)
        {
            case TileGridCellKind.Pit:
                return GeneratedPrefabFolder + "/PitTile.prefab";
            case TileGridCellKind.RampPositiveX:
                return GeneratedPrefabFolder + "/RampTile_PositiveX.prefab";
            case TileGridCellKind.RampNegativeX:
                return GeneratedPrefabFolder + "/RampTile_NegativeX.prefab";
            case TileGridCellKind.RampPositiveZ:
                return GeneratedPrefabFolder + "/RampTile_PositiveZ.prefab";
            case TileGridCellKind.RampNegativeZ:
                return GeneratedPrefabFolder + "/RampTile_NegativeZ.prefab";
            case TileGridCellKind.StairRampPositiveX:
                return GeneratedPrefabFolder + "/StairRamp_PositiveX.prefab";
            case TileGridCellKind.StairRampNegativeX:
                return GeneratedPrefabFolder + "/StairRamp_NegativeX.prefab";
            case TileGridCellKind.StairRampPositiveZ:
                return GeneratedPrefabFolder + "/StairRamp_PositiveZ.prefab";
            case TileGridCellKind.StairRampNegativeZ:
                return GeneratedPrefabFolder + "/StairRamp_NegativeZ.prefab";
            default:
                return BaseTilePrefabPath;
        }
    }

    static void CreatePrefabIfMissing(TileGridCellKind kind)
    {
        string path = GetPrefabPath(kind);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;

        CreateOrReplacePrefab(kind);
    }

    static void CreateOrReplacePrefab(TileGridCellKind kind)
    {
        GameObject baseTile = AssetDatabase.LoadAssetAtPath<GameObject>(BaseTilePrefabPath);
        if (baseTile == null)
        {
            Debug.LogWarning("[TilePainterPrefabBuilder] Could not find base tile prefab at " + BaseTilePrefabPath + ".");
            return;
        }

        string path = GetPrefabPath(kind);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(baseTile);
        if (instance == null)
            instance = UnityEngine.Object.Instantiate(baseTile);

        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.name = Path.GetFileNameWithoutExtension(path);

        TileGridCell tileCell = instance.GetComponent<TileGridCell>();
        if (tileCell == null)
            tileCell = instance.AddComponent<TileGridCell>();

        tileCell.AutoAssignReferences();
        tileCell.Configure(Vector3Int.zero, 0, DefaultTileSize, DefaultVerticalSpacing, kind);
        tileCell.ApplyFeatureSettings(
            newHideFloorWhenPit: true,
            newHideCeilingWhenPit: false,
            newPitWallDepthSegments: 3,
            newPitWallThickness: DefaultPitWallThickness,
            newPitRespawnTriggerHeight: DefaultPitRespawnTriggerHeight,
            newPitRespawnDamage: -1f,
            newVoidMaterial: EnsureVoidMaterial(),
            newRampAngle: DefaultRampAngle,
            newRampThickness: DefaultRampThickness,
            newHideFloorWhenRamp: true,
            newHideCeilingWhenRamp: true,
            newOpenRampExitWall: true,
            newRampMaterial: null,
            newStairRampSegments: 3,
            newHideCeilingWhenStackedAbove: true,
            newHideFloorWhenStackedBelow: true);
        tileCell.RefreshForConnections(false, false, false, false, false, false);
        PersistGeneratedMeshes(instance, kind);

        PrefabUtility.SaveAsPrefabAsset(instance, path);
        UnityEngine.Object.DestroyImmediate(instance);
    }

    static void PersistGeneratedMeshes(GameObject root, TileGridCellKind kind)
    {
        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            Mesh sourceMesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (!IsGeneratedMesh(sourceMesh))
                continue;

            string assetName = SanitizeAssetName(kind + "_" + meshFilter.gameObject.name);
            string assetPath = GeneratedMeshFolder + "/" + assetName + ".asset";
            Mesh assetMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (assetMesh == null)
            {
                assetMesh = new Mesh();
                AssetDatabase.CreateAsset(assetMesh, assetPath);
            }

            EditorUtility.CopySerialized(sourceMesh, assetMesh);
            assetMesh.name = assetName;
            EditorUtility.SetDirty(assetMesh);

            meshFilter.sharedMesh = assetMesh;
            MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();
            if (meshCollider != null)
                meshCollider.sharedMesh = assetMesh;
        }
    }

    static bool IsGeneratedMesh(Mesh mesh)
    {
        return mesh != null
            && (mesh.name.EndsWith(" Tile Tool Mesh", StringComparison.Ordinal)
                || mesh.name.EndsWith(" Tiled Mesh", StringComparison.Ordinal));
    }

    static string SanitizeAssetName(string assetName)
    {
        if (string.IsNullOrEmpty(assetName))
            return "TileToolMesh";

        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = assetName.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0 || char.IsWhiteSpace(chars[i]))
                chars[i] = '_';
        }

        return new string(chars);
    }

    static void EnsureFolders()
    {
        EnsureFolder("Assets/Prefab");
        EnsureFolder("Assets/Prefab/Castle");
        EnsureFolder(GeneratedPrefabFolder);
        EnsureFolder(GeneratedMeshFolder);
        EnsureFolder("Assets/Materials");
        EnsureFolder("Assets/Materials/TileTool");
    }

    static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath);
        string folderName = Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            return;

        parent = parent.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folderName);
    }

    static Material EnsureVoidMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(VoidMaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        material = new Material(shader)
        {
            name = "BlackVoid",
            color = Color.black
        };
        AssetDatabase.CreateAsset(material, VoidMaterialPath);
        return material;
    }
}
