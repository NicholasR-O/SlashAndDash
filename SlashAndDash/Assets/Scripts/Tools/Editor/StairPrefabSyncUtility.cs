using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class StairPrefabSyncUtility
{
    const string Level2ScenePath = "Assets/Scenes/Level2.unity";
    const string GeneratedRootName = "__TileToolGenerated";

    static readonly TileGridCellKind SourceKind = TileGridCellKind.StairRampPositiveZ;

    static readonly TileGridCellKind[] TargetKinds =
    {
        TileGridCellKind.StairRampPositiveX,
        TileGridCellKind.StairRampNegativeX,
        TileGridCellKind.StairRampNegativeZ
    };

    static readonly string[] DirectionTokens =
    {
        "+X",
        "-X",
        "+Z",
        "-Z"
    };

    static readonly HashSet<string> PreservedDirectActiveNames = new HashSet<string>
    {
        "+XWall",
        "-XWall",
        "+ZWall",
        "-ZWall",
        "Floor",
        "Ceiling"
    };

    struct StairInstanceState
    {
        public string name;
        public bool activeSelf;
        public Transform parent;
        public int siblingIndex;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public Vector3Int coordinate;
        public int floorIndex;
        public float tileSize;
        public float verticalSpacing;
        public TileGridCellKind tileKind;
        public Dictionary<string, bool> directActiveStates;
    }

    [MenuItem("Tools/Slash And Dash/Sync Stair Prefabs From +Z")]
    public static void SyncFromPositiveZMenu()
    {
        int changed = SyncFromPositiveZ(force: true);
        Debug.Log("[StairPrefabSyncUtility] Synced " + changed + " stair prefab(s) from StairRamp_PositiveZ.");
    }

    [MenuItem("Tools/Slash And Dash/Sync Stair Prefabs From +Z And Refresh Level 2")]
    public static void SyncStairPrefabsAndRefreshLevel2()
    {
        int changedPrefabs = SyncFromPositiveZ(force: true);
        int changedSceneTiles = RefreshLevel2StairInstances();
        Debug.Log("[StairPrefabSyncUtility] Synced " + changedPrefabs + " stair prefab(s) and refreshed "
            + changedSceneTiles + " Level2 stair instance(s).");
    }

    public static int SyncFromPositiveZ(bool force)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogWarning("[StairPrefabSyncUtility] Unity is still compiling or importing. Run the stair sync again after it finishes.");
            return 0;
        }

        string sourcePath = TilePainterPrefabBuilder.GetPrefabPath(SourceKind);
        if (!File.Exists(sourcePath))
        {
            Debug.LogWarning("[StairPrefabSyncUtility] Could not find source stair prefab at " + sourcePath + ".");
            return 0;
        }

        DateTime sourceWriteTime = File.GetLastWriteTimeUtc(sourcePath);
        int changed = 0;
        for (int i = 0; i < TargetKinds.Length; i++)
        {
            string targetPath = TilePainterPrefabBuilder.GetPrefabPath(TargetKinds[i]);
            if (!File.Exists(targetPath))
            {
                Debug.LogWarning("[StairPrefabSyncUtility] Could not find target stair prefab at " + targetPath + ".");
                continue;
            }

            if (!force && File.GetLastWriteTimeUtc(targetPath) >= sourceWriteTime)
                continue;

            SyncTargetPrefab(sourcePath, targetPath, TargetKinds[i]);
            changed++;
        }

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        return changed;
    }

    public static int RefreshLevel2StairInstances()
    {
        if (!File.Exists(Level2ScenePath))
        {
            Debug.LogWarning("[StairPrefabSyncUtility] Could not find Level2 scene at " + Level2ScenePath + ".");
            return 0;
        }

        Scene scene = SceneManager.GetSceneByPath(Level2ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return 0;

            scene = EditorSceneManager.OpenScene(Level2ScenePath, OpenSceneMode.Single);
        }

        int changed = RefreshStairInstances(scene);
        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        SceneView.RepaintAll();
        return changed;
    }

    static void SyncTargetPrefab(string sourcePath, string targetPath, TileGridCellKind targetKind)
    {
        GameObject workingRoot = PrefabUtility.LoadPrefabContents(sourcePath);
        try
        {
            float yawDelta = GetYawDeltaFromPositiveZ(targetKind);
            Quaternion rotationDelta = Quaternion.Euler(0f, yawDelta, 0f);

            workingRoot.name = Path.GetFileNameWithoutExtension(targetPath);
            RotateLayoutFromPositiveZ(workingRoot.transform, rotationDelta);
            RenameDirectionalChildren(workingRoot.transform, yawDelta);
            ConfigureTileCell(workingRoot, targetKind);

            PrefabUtility.SaveAsPrefabAsset(workingRoot, targetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(workingRoot);
        }
    }

    static void RotateLayoutFromPositiveZ(Transform root, Quaternion rotationDelta)
    {
        RotateDirectLayoutChildren(root, rotationDelta, skipGeneratedContainers: true);

        Transform generatedRoot = FindDirectChild(root, GeneratedRootName);
        if (generatedRoot != null)
            RotateDirectLayoutChildren(generatedRoot, rotationDelta, skipGeneratedContainers: false);

        Transform propsRoot = FindDirectChild(root, TileGridCell.GeneratedPropsRootName);
        if (propsRoot != null)
            RotateDirectLayoutChildren(propsRoot, rotationDelta, skipGeneratedContainers: false);
    }

    static void RotateDirectLayoutChildren(Transform parent, Quaternion rotationDelta, bool skipGeneratedContainers)
    {
        if (parent == null)
            return;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (skipGeneratedContainers && IsGeneratedContainer(child.name))
                continue;

            child.localPosition = rotationDelta * child.localPosition;
            child.localRotation = rotationDelta * child.localRotation;
        }
    }

    static bool IsGeneratedContainer(string objectName)
    {
        return objectName == GeneratedRootName || objectName == TileGridCell.GeneratedPropsRootName;
    }

    static void RenameDirectionalChildren(Transform root, float yawDelta)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
            children[i].name = RotateDirectionalName(children[i].name, yawDelta);
    }

    static string RotateDirectionalName(string sourceName, float yawDelta)
    {
        string result = sourceName;
        for (int i = 0; i < DirectionTokens.Length; i++)
            result = result.Replace(DirectionTokens[i], "{DIR" + i + "}");

        for (int i = 0; i < DirectionTokens.Length; i++)
        {
            Vector3Int rotated = RotateDirection(ParseDirection(DirectionTokens[i]), yawDelta);
            result = result.Replace("{DIR" + i + "}", FormatDirection(rotated));
        }

        return result;
    }

    static void ConfigureTileCell(GameObject root, TileGridCellKind targetKind)
    {
        TileGridCell tileCell = root.GetComponent<TileGridCell>();
        if (tileCell == null)
            tileCell = root.AddComponent<TileGridCell>();

        tileCell.Configure(Vector3Int.zero, 0, tileCell.TileSize, tileCell.VerticalSpacing, targetKind);
        AssignBaseReferences(tileCell, root.transform);
        EditorUtility.SetDirty(tileCell);
    }

    static int RefreshStairInstances(Scene scene)
    {
        TileGridCell[] allTiles = UnityEngine.Object.FindObjectsByType<TileGridCell>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<TileGridCell> stairTiles = new List<TileGridCell>();
        for (int i = 0; i < allTiles.Length; i++)
        {
            TileGridCell tile = allTiles[i];
            if (tile != null && tile.IsStairRamp && tile.gameObject.scene == scene)
                stairTiles.Add(tile);
        }

        int changed = 0;
        for (int i = 0; i < stairTiles.Count; i++)
        {
            TileGridCell tile = stairTiles[i];
            if (tile != null && RefreshStairInstance(tile, scene))
                changed++;
        }

        return changed;
    }

    static bool RefreshStairInstance(TileGridCell tile, Scene scene)
    {
        GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(tile.gameObject);
        if (root == null)
            root = tile.gameObject;

        StairInstanceState state = CaptureState(tile, root);
        string prefabPath = TilePainterPrefabBuilder.GetPrefabPath(state.tileKind);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[StairPrefabSyncUtility] Could not find stair prefab for " + state.tileKind + " at " + prefabPath + ".");
            return false;
        }

        GameObject refreshedRoot = root;
        UnityEngine.Object source = PrefabUtility.GetCorrespondingObjectFromSource(root);
        string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
        bool isExpectedPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(root) && sourcePath == prefabPath;

        if (isExpectedPrefabInstance)
        {
            PrefabUtility.RevertPrefabInstance(root, InteractionMode.AutomatedAction);
        }
        else
        {
            refreshedRoot = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (refreshedRoot == null)
                refreshedRoot = UnityEngine.Object.Instantiate(prefab);

            UnityEngine.Object.DestroyImmediate(root);
        }

        RestoreState(refreshedRoot, state);

        TileGridCell refreshedTile = refreshedRoot.GetComponent<TileGridCell>();
        if (refreshedTile == null)
            refreshedTile = refreshedRoot.AddComponent<TileGridCell>();

        refreshedTile.Configure(state.coordinate, state.floorIndex, state.tileSize, state.verticalSpacing, state.tileKind);
        AssignBaseReferences(refreshedTile, refreshedRoot.transform);
        RestoreDirectActiveStates(refreshedRoot.transform, state.directActiveStates);

        EditorUtility.SetDirty(refreshedRoot);
        EditorUtility.SetDirty(refreshedTile);
        PrefabUtility.RecordPrefabInstancePropertyModifications(refreshedTile);
        return true;
    }

    static StairInstanceState CaptureState(TileGridCell tile, GameObject root)
    {
        Transform rootTransform = root.transform;
        return new StairInstanceState
        {
            name = root.name,
            activeSelf = root.activeSelf,
            parent = rootTransform.parent,
            siblingIndex = rootTransform.GetSiblingIndex(),
            localPosition = rootTransform.localPosition,
            localRotation = rootTransform.localRotation,
            localScale = rootTransform.localScale,
            coordinate = tile.GridCoordinate,
            floorIndex = tile.FloorIndex,
            tileSize = tile.TileSize,
            verticalSpacing = tile.VerticalSpacing,
            tileKind = tile.TileKind,
            directActiveStates = CaptureDirectActiveStates(rootTransform)
        };
    }

    static void RestoreState(GameObject root, StairInstanceState state)
    {
        Transform rootTransform = root.transform;
        rootTransform.SetParent(state.parent, false);
        rootTransform.localPosition = state.localPosition;
        rootTransform.localRotation = state.localRotation;
        rootTransform.localScale = state.localScale;
        rootTransform.SetSiblingIndex(state.siblingIndex);
        root.name = state.name;
        root.SetActive(state.activeSelf);
    }

    static Dictionary<string, bool> CaptureDirectActiveStates(Transform root)
    {
        Dictionary<string, bool> states = new Dictionary<string, bool>();
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (PreservedDirectActiveNames.Contains(child.name))
                states[child.name] = child.gameObject.activeSelf;
        }

        return states;
    }

    static void RestoreDirectActiveStates(Transform root, Dictionary<string, bool> states)
    {
        if (states == null)
            return;

        foreach (KeyValuePair<string, bool> state in states)
        {
            Transform child = FindDirectChild(root, state.Key);
            if (child != null)
                child.gameObject.SetActive(state.Value);
        }
    }

    static void AssignBaseReferences(TileGridCell tileCell, Transform root)
    {
        SerializedObject serializedTile = new SerializedObject(tileCell);
        SetObjectReference(serializedTile, "positiveXWall", FindDirectChildObject(root, "+XWall"));
        SetObjectReference(serializedTile, "negativeXWall", FindDirectChildObject(root, "-XWall"));
        SetObjectReference(serializedTile, "positiveZWall", FindDirectChildObject(root, "+ZWall"));
        SetObjectReference(serializedTile, "negativeZWall", FindDirectChildObject(root, "-ZWall"));
        SetObjectReference(serializedTile, "floor", FindDirectChildObject(root, "Floor"));
        SetObjectReference(serializedTile, "ceiling", FindDirectChildObject(root, "Ceiling"));
        serializedTile.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetObjectReference(SerializedObject serializedObject, string propertyName, GameObject value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    static GameObject FindDirectChildObject(Transform root, string childName)
    {
        Transform child = FindDirectChild(root, childName);
        return child != null ? child.gameObject : null;
    }

    static Transform FindDirectChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;
        }

        return null;
    }

    static Vector3Int ParseDirection(string prefix)
    {
        switch (prefix)
        {
            case "+X":
                return Vector3Int.right;
            case "-X":
                return Vector3Int.left;
            case "+Z":
                return Vector3Int.forward;
            case "-Z":
                return Vector3Int.back;
            default:
                return Vector3Int.zero;
        }
    }

    static Vector3Int RotateDirection(Vector3Int direction, float yawDelta)
    {
        int steps = Mathf.RoundToInt(yawDelta / 90f);
        steps = ((steps % 4) + 4) % 4;
        Vector3Int result = direction;
        for (int i = 0; i < steps; i++)
            result = new Vector3Int(result.z, 0, -result.x);

        return result;
    }

    static string FormatDirection(Vector3Int direction)
    {
        if (direction == Vector3Int.right)
            return "+X";
        if (direction == Vector3Int.left)
            return "-X";
        if (direction == Vector3Int.forward)
            return "+Z";
        if (direction == Vector3Int.back)
            return "-Z";

        return "+Z";
    }

    static float GetYawDeltaFromPositiveZ(TileGridCellKind kind)
    {
        switch (kind)
        {
            case TileGridCellKind.StairRampPositiveX:
                return 90f;
            case TileGridCellKind.StairRampNegativeX:
                return -90f;
            case TileGridCellKind.StairRampNegativeZ:
                return 180f;
            default:
                return 0f;
        }
    }
}
