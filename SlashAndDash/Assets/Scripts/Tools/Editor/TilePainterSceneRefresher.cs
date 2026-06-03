using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TilePainterSceneRefresher
{
    const string Level2ScenePath = "Assets/Scenes/Level2.unity";

    struct WallOpenings
    {
        public bool positiveX;
        public bool negativeX;
        public bool positiveZ;
        public bool negativeZ;

        public bool HasAny => positiveX || negativeX || positiveZ || negativeZ;

        public void Add(Vector3Int direction)
        {
            if (direction == Vector3Int.right)
                positiveX = true;
            else if (direction == Vector3Int.left)
                negativeX = true;
            else if (direction == Vector3Int.forward)
                positiveZ = true;
            else if (direction == Vector3Int.back)
                negativeZ = true;
        }
    }

    [MenuItem("Tools/Slash And Dash/Refresh Loaded Tile Painter Tiles")]
    public static void RefreshLoadedScenesMenu()
    {
        int refreshed = RefreshLoadedScenes();
        Debug.Log("[TilePainterSceneRefresher] Refreshed " + refreshed + " tile painter tiles in loaded scenes.");
    }

    [MenuItem("Tools/Slash And Dash/Refresh Level 2 Tile Painter Tiles")]
    public static void RefreshLevel2Scene()
    {
        TilePainterPrefabBuilder.RebuildPrefabs();

        Scene scene = EditorSceneManager.OpenScene(Level2ScenePath, OpenSceneMode.Single);
        int refreshed = RefreshLoadedScenes();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[TilePainterSceneRefresher] Refreshed " + refreshed + " tile painter tiles in " + Level2ScenePath + ".");
    }

    public static int RefreshLoadedScenes()
    {
        TileGridCell[] tiles = UnityEngine.Object.FindObjectsByType<TileGridCell>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return RefreshTiles(tiles);
    }

    static int RefreshTiles(IReadOnlyList<TileGridCell> tiles)
    {
        Dictionary<string, TileGridCell> grid = new Dictionary<string, TileGridCell>();
        Dictionary<string, TileGridCell> tilesByCoordinate = new Dictionary<string, TileGridCell>();
        Dictionary<TileGridCell, WallOpenings> forcedOpenings = new Dictionary<TileGridCell, WallOpenings>();
        HashSet<string> stairReservedKeys = new HashSet<string>();
        HashSet<Scene> dirtyScenes = new HashSet<Scene>();

        for (int i = 0; i < tiles.Count; i++)
        {
            TileGridCell tile = tiles[i];
            if (tile == null)
                continue;

            string gridKey = MakeGridKey(tile.GridCoordinate, tile.FloorIndex);
            if (!grid.ContainsKey(gridKey))
                grid.Add(gridKey, tile);

            string coordinateKey = MakeCoordinateKey(tile.GridCoordinate);
            if (!tilesByCoordinate.ContainsKey(coordinateKey))
                tilesByCoordinate.Add(coordinateKey, tile);
        }

        BuildStairRefreshContext(tiles, tilesByCoordinate, forcedOpenings, stairReservedKeys);

        int refreshed = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            TileGridCell tile = tiles[i];
            if (tile == null)
                continue;

            Vector3Int coordinate = tile.GridCoordinate;
            int floorIndex = tile.FloorIndex;
            bool hasPositiveX = grid.ContainsKey(MakeGridKey(coordinate + Vector3Int.right, floorIndex));
            bool hasNegativeX = grid.ContainsKey(MakeGridKey(coordinate + Vector3Int.left, floorIndex));
            bool hasPositiveZ = grid.ContainsKey(MakeGridKey(coordinate + Vector3Int.forward, floorIndex));
            bool hasNegativeZ = grid.ContainsKey(MakeGridKey(coordinate + Vector3Int.back, floorIndex));
            bool hasAbove = grid.ContainsKey(MakeGridKey(coordinate + Vector3Int.up, floorIndex));
            bool hasBelow = grid.ContainsKey(MakeGridKey(coordinate + Vector3Int.down, floorIndex));
            WallOpenings forced = GetForcedOpenings(forcedOpenings, tile);

            tile.AutoAssignReferences();
            tile.RefreshForConnections(
                hasPositiveX,
                hasNegativeX,
                hasPositiveZ,
                hasNegativeZ,
                hasAbove,
                hasBelow,
                forced.positiveX,
                forced.negativeX,
                forced.positiveZ,
                forced.negativeZ);

            TilePropGenerator.RefreshTileProps(
                tile,
                hasPositiveX || forced.positiveX,
                hasNegativeX || forced.negativeX,
                hasPositiveZ || forced.positiveZ,
                hasNegativeZ || forced.negativeZ,
                forced.HasAny,
                stairReservedKeys.Contains(MakeGridKey(coordinate, floorIndex)),
                hasBelow);

            EditorUtility.SetDirty(tile);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tile);
            if (tile.gameObject.scene.IsValid())
                dirtyScenes.Add(tile.gameObject.scene);

            refreshed++;
        }

        foreach (Scene scene in dirtyScenes)
            EditorSceneManager.MarkSceneDirty(scene);

        SceneView.RepaintAll();
        return refreshed;
    }

    static void BuildStairRefreshContext(
        IReadOnlyList<TileGridCell> tiles,
        Dictionary<string, TileGridCell> tilesByCoordinate,
        Dictionary<TileGridCell, WallOpenings> forcedOpenings,
        HashSet<string> stairReservedKeys)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            TileGridCell stair = tiles[i];
            if (stair == null || !stair.IsStairRamp)
                continue;

            Vector3Int direction = TileGridCell.GetStairRampDirection(stair.TileKind);
            if (direction == Vector3Int.zero)
                continue;

            int segmentCount = Mathf.Max(1, stair.StairRampSegments);
            AddForcedOpening(forcedOpenings, stair, direction);
            AddForcedOpening(forcedOpenings, stair, -direction);

            for (int offset = 0; offset < segmentCount; offset++)
            {
                Vector3Int reservedCoordinate = stair.GridCoordinate + direction * offset;
                stairReservedKeys.Add(MakeGridKey(reservedCoordinate, stair.FloorIndex));
            }

            Vector3Int landingCoordinate = stair.GridCoordinate + direction * segmentCount + Vector3Int.up;
            if (tilesByCoordinate.TryGetValue(MakeCoordinateKey(landingCoordinate), out TileGridCell landingTile))
                AddForcedOpening(forcedOpenings, landingTile, -direction);
        }
    }

    static void AddForcedOpening(Dictionary<TileGridCell, WallOpenings> forcedOpenings, TileGridCell tile, Vector3Int direction)
    {
        if (tile == null || direction == Vector3Int.zero)
            return;

        WallOpenings openings = GetForcedOpenings(forcedOpenings, tile);
        openings.Add(direction);
        forcedOpenings[tile] = openings;
    }

    static WallOpenings GetForcedOpenings(Dictionary<TileGridCell, WallOpenings> forcedOpenings, TileGridCell tile)
    {
        if (tile != null && forcedOpenings.TryGetValue(tile, out WallOpenings openings))
            return openings;

        return new WallOpenings();
    }

    static string MakeGridKey(Vector3Int coordinate, int floorIndex)
    {
        return floorIndex + ":" + MakeCoordinateKey(coordinate);
    }

    static string MakeCoordinateKey(Vector3Int coordinate)
    {
        return coordinate.x + "," + coordinate.y + "," + coordinate.z;
    }
}
