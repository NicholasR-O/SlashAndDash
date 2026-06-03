using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TilePainterWindow : EditorWindow
{
    const string DefaultTilePrefabPath = "Assets/Prefab/Castle/Tile.prefab";
    const string DefaultParentName = "Tile Grid";
    const float MinSpacing = 0.01f;
    const float MaxRayDistance = 10000f;

    static readonly TileGridCellKind[] PaintKinds =
    {
        TileGridCellKind.Normal,
        TileGridCellKind.Pit,
        TileGridCellKind.RampPositiveX,
        TileGridCellKind.RampNegativeX,
        TileGridCellKind.RampPositiveZ,
        TileGridCellKind.RampNegativeZ,
        TileGridCellKind.StairRampPositiveX,
        TileGridCellKind.StairRampNegativeX,
        TileGridCellKind.StairRampPositiveZ,
        TileGridCellKind.StairRampNegativeZ
    };

    static readonly string[] PaintKindLabels =
    {
        "Tile",
        "Pit",
        "Ramp +X",
        "Ramp -X",
        "Ramp +Z",
        "Ramp -Z",
        "Stair +X",
        "Stair -X",
        "Stair +Z",
        "Stair -Z"
    };

    static readonly Color NormalPreviewColor = new Color(0.2f, 0.8f, 1f, 0.85f);
    static readonly Color PitPreviewColor = new Color(1f, 0.35f, 0.2f, 0.9f);
    static readonly Color RampPreviewColor = new Color(0.55f, 1f, 0.25f, 0.9f);
    static readonly Color StairPreviewColor = new Color(1f, 0.85f, 0.2f, 0.95f);
    static readonly Color ErasePreviewColor = new Color(1f, 0.2f, 0.2f, 0.95f);

    struct FloorOption
    {
        public int floorIndex;
        public int heightLevel;

        public FloorOption(int newFloorIndex, int newHeightLevel)
        {
            floorIndex = newFloorIndex;
            heightLevel = newHeightLevel;
        }
    }

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

    [SerializeField] GameObject tilePrefab;
    [SerializeField] Transform tileParent;
    [SerializeField] string parentName = DefaultParentName;
    [SerializeField] bool paintEnabled = true;
    [SerializeField] TileGridCellKind paintKind;
    [SerializeField] bool stackOnExistingTileClick = true;
    [SerializeField] Vector3 origin;
    [SerializeField] float tileSize = 60f;
    [SerializeField] float verticalSpacing = 48f;
    [SerializeField] int selectedFloorIndex;
    [SerializeField] int stackLevel;
    [SerializeField] bool hideCeilingWhenTileAbove = true;
    [SerializeField] bool hideFloorWhenTileBelow = true;
    [SerializeField] bool hideFloorWhenPit = true;
    [SerializeField] bool hideCeilingWhenPit;
    [SerializeField] int pitWallDepthSegments = 3;
    [SerializeField] float pitWallThickness = 1.2f;
    [SerializeField] float pitRespawnTriggerHeight = 14f;
    [SerializeField] float pitRespawnDamage = -1f;
    [SerializeField] Material voidMaterial;
    [SerializeField] float rampAngle = 22.5f;
    [SerializeField] float rampThickness = 1.2f;
    [SerializeField] bool hideFloorWhenRamp = true;
    [SerializeField] bool hideCeilingWhenRamp = true;
    [SerializeField] bool openRampExitWall = true;
    [SerializeField] Material rampMaterial;
    [SerializeField] float maxStairRampAngle = 15f;

    [MenuItem("Tools/Slash And Dash/Tile Painter")]
    public static void Open()
    {
        GetWindow<TilePainterWindow>("Tile Painter");
    }

    void OnEnable()
    {
        hideFloorWhenTileBelow = true;

        if (tilePrefab == null)
            tilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTilePrefabPath);
        if (voidMaterial == null)
            voidMaterial = AssetDatabase.LoadAssetAtPath<Material>(TilePainterPrefabBuilder.VoidMaterialPath);

        TilePainterPrefabBuilder.EnsurePrefabsExist();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Tile Painter", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Left click in Scene view to place. Shift + left click erases. Hover an existing tile on the selected floor and click to stack a tile above it.", MessageType.Info);

        tilePrefab = (GameObject)EditorGUILayout.ObjectField("Base Tile Prefab", tilePrefab, typeof(GameObject), false);
        tileParent = (Transform)EditorGUILayout.ObjectField("Tile Parent", tileParent, typeof(Transform), true);
        parentName = EditorGUILayout.TextField("Parent Name", string.IsNullOrWhiteSpace(parentName) ? DefaultParentName : parentName);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Find/Create Parent"))
                tileParent = GetOrCreateParent();

            if (GUILayout.Button("Use Selection"))
                tileParent = Selection.activeTransform;
        }

        EditorGUILayout.Space(6f);
        paintEnabled = EditorGUILayout.Toggle("Paint In Scene View", paintEnabled);
        paintKind = DrawPaintKindToolbar(paintKind);
        DrawFloorSelection();
        stackOnExistingTileClick = EditorGUILayout.Toggle("Click Existing To Stack", stackOnExistingTileClick);

        EditorGUILayout.Space(6f);
        origin = EditorGUILayout.Vector3Field("Grid Origin", origin);
        tileSize = Mathf.Max(MinSpacing, EditorGUILayout.FloatField("Tile Size", tileSize));
        verticalSpacing = Mathf.Max(MinSpacing, EditorGUILayout.FloatField("Vertical Spacing", verticalSpacing));
        stackLevel = EditorGUILayout.IntField("Selected Floor Height", stackLevel);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Stacked Tile Connections", EditorStyles.boldLabel);
        hideCeilingWhenTileAbove = EditorGUILayout.Toggle("Hide Ceiling Below", hideCeilingWhenTileAbove);
        hideFloorWhenTileBelow = EditorGUILayout.Toggle("Hide Floor Above", hideFloorWhenTileBelow);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Pit Tile", EditorStyles.boldLabel);
        hideFloorWhenPit = EditorGUILayout.Toggle("Hide Floor", hideFloorWhenPit);
        hideCeilingWhenPit = EditorGUILayout.Toggle("Hide Ceiling", hideCeilingWhenPit);
        pitWallDepthSegments = Mathf.Max(1, EditorGUILayout.IntField("Wall Depth Segments", pitWallDepthSegments));
        pitWallThickness = Mathf.Max(0.1f, EditorGUILayout.FloatField("Wall Thickness", pitWallThickness));
        pitRespawnTriggerHeight = Mathf.Max(0.1f, EditorGUILayout.FloatField("Respawn Trigger Height", pitRespawnTriggerHeight));
        pitRespawnDamage = EditorGUILayout.FloatField("Respawn Damage", pitRespawnDamage);
        voidMaterial = (Material)EditorGUILayout.ObjectField("Void Material", voidMaterial, typeof(Material), false);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Ramp Tile", EditorStyles.boldLabel);
        rampAngle = EditorGUILayout.Slider("Ramp Angle", rampAngle, 5f, 45f);
        rampThickness = Mathf.Max(0.1f, EditorGUILayout.FloatField("Ramp Thickness", rampThickness));
        hideFloorWhenRamp = EditorGUILayout.Toggle("Hide Floor", hideFloorWhenRamp);
        hideCeilingWhenRamp = EditorGUILayout.Toggle("Hide Ceiling", hideCeilingWhenRamp);
        openRampExitWall = EditorGUILayout.Toggle("Open Launch Wall", openRampExitWall);
        rampMaterial = (Material)EditorGUILayout.ObjectField("Ramp Material", rampMaterial, typeof(Material), false);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Stair Ramp", EditorStyles.boldLabel);
        maxStairRampAngle = EditorGUILayout.Slider("Max Climb Angle", maxStairRampAngle, 8f, 25f);
        EditorGUILayout.LabelField("Segments", GetStairRampSegmentCount().ToString());

        EditorGUILayout.Space(10f);
        if (GUILayout.Button("Refresh Tiles"))
            RefreshAllTiles();
    }

    TileGridCellKind DrawPaintKindToolbar(TileGridCellKind currentKind)
    {
        int selectedIndex = Mathf.Max(0, Array.IndexOf(PaintKinds, currentKind));
        selectedIndex = GUILayout.Toolbar(selectedIndex, PaintKindLabels);
        return PaintKinds[Mathf.Clamp(selectedIndex, 0, PaintKinds.Length - 1)];
    }

    void DrawFloorSelection()
    {
        List<FloorOption> floors = GetFloorOptions();
        int selectedOption = 0;
        string[] labels = new string[floors.Count];
        for (int i = 0; i < floors.Count; i++)
        {
            FloorOption floor = floors[i];
            labels[i] = "Floor " + floor.floorIndex + " (Height " + floor.heightLevel + ")";
            if (floor.floorIndex == selectedFloorIndex)
                selectedOption = i;
        }

        int newSelectedOption = EditorGUILayout.Popup("Floor", selectedOption, labels);
        if (newSelectedOption != selectedOption || !ContainsFloorIndex(floors, selectedFloorIndex))
        {
            FloorOption selectedFloor = floors[Mathf.Clamp(newSelectedOption, 0, floors.Count - 1)];
            selectedFloorIndex = selectedFloor.floorIndex;
            stackLevel = selectedFloor.heightLevel;
        }
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (!paintEnabled)
            return;

        Event current = Event.current;
        if (current == null || current.alt)
            return;

        bool erase = current.shift;
        if (!TryGetPaintTarget(current.mousePosition, erase, out Vector3Int coordinate, out Vector3 worldPosition, out TileGridCell hoveredTile, out bool willStack))
            return;

        DrawPreview(coordinate, worldPosition, erase, willStack);

        if (current.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        bool shouldPaint = current.type == EventType.MouseDown || (current.type == EventType.MouseDrag && !willStack);
        if (shouldPaint && current.button == 0)
        {
            if (erase)
                EraseTile(coordinate);
            else
                PlaceOrUpdateTile(coordinate, worldPosition);

            current.Use();
        }

        if (current.type == EventType.MouseMove)
            sceneView.Repaint();
    }

    bool TryGetPaintTarget(
        Vector2 mousePosition,
        bool erase,
        out Vector3Int coordinate,
        out Vector3 worldPosition,
        out TileGridCell hoveredTile,
        out bool willStack)
    {
        coordinate = Vector3Int.zero;
        worldPosition = Vector3.zero;
        willStack = false;

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        hoveredTile = FindHoveredTile(ray);
        if (hoveredTile != null)
        {
            willStack = ShouldStackFromHover(hoveredTile, erase);
            if (erase)
            {
                coordinate = hoveredTile.GridCoordinate;
            }
            else if (willStack)
            {
                coordinate = GetNextStackCoordinate(hoveredTile.GridCoordinate, hoveredTile.FloorIndex);
            }
            else
            {
                coordinate = new Vector3Int(hoveredTile.GridCoordinate.x, stackLevel, hoveredTile.GridCoordinate.z);
            }

            worldPosition = GridToWorld(coordinate);
            return true;
        }

        float levelY = origin.y + stackLevel * verticalSpacing;
        Plane paintPlane = new Plane(Vector3.up, new Vector3(0f, levelY, 0f));
        if (!paintPlane.Raycast(ray, out float distance))
            return false;

        Vector3 hitPoint = ray.GetPoint(distance);
        Vector3 localPoint = hitPoint - origin;
        int gridX = Mathf.RoundToInt(localPoint.x / tileSize);
        int gridZ = Mathf.RoundToInt(localPoint.z / tileSize);

        coordinate = new Vector3Int(gridX, stackLevel, gridZ);
        worldPosition = GridToWorld(coordinate);
        return true;
    }

    TileGridCell FindHoveredTile(Ray ray)
    {
        if (!stackOnExistingTileClick)
            return null;

        RaycastHit[] hits = Physics.RaycastAll(ray, MaxRayDistance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            TileGridCell tile = hitCollider.GetComponentInParent<TileGridCell>();
            if (tile != null && IsTileInScope(tile))
                return tile;
        }

        return null;
    }

    bool ShouldStackFromHover(TileGridCell hoveredTile, bool erase)
    {
        if (hoveredTile == null || erase || !stackOnExistingTileClick || hoveredTile.IsStairRamp || IsStairRampKind(paintKind))
            return false;

        return hoveredTile.FloorIndex == selectedFloorIndex;
    }

    Vector3Int GetNextStackCoordinate(Vector3Int baseCoordinate, int floorIndex)
    {
        Vector3Int candidate = baseCoordinate + Vector3Int.up;
        while (FindTileAt(candidate, floorIndex) != null)
            candidate += Vector3Int.up;

        return candidate;
    }

    Vector3 GridToWorld(Vector3Int coordinate)
    {
        return origin + new Vector3(coordinate.x * tileSize, coordinate.y * verticalSpacing, coordinate.z * tileSize);
    }

    void DrawPreview(Vector3Int coordinate, Vector3 worldPosition, bool erase, bool willStack)
    {
        Color previousColor = Handles.color;
        Handles.color = erase ? ErasePreviewColor : GetPreviewColor(paintKind);

        float previewHeight = Mathf.Max(1f, verticalSpacing);
        Vector3 previewCenter = worldPosition + Vector3.up * previewHeight * 0.5f;
        Handles.DrawWireCube(previewCenter, new Vector3(tileSize, previewHeight, tileSize));
        Handles.Label(worldPosition + Vector3.up * (previewHeight + 2f), GetPreviewLabel(coordinate, erase, willStack));

        if (!erase && (IsRampKind(paintKind) || IsStairRampKind(paintKind)))
            DrawRampDirectionPreview(worldPosition, paintKind);

        Handles.color = previousColor;
    }

    void DrawRampDirectionPreview(Vector3 worldPosition, TileGridCellKind kind)
    {
        Vector3 direction = GetRampDirection(kind);
        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector3 start = worldPosition + Vector3.up * 2.5f;
        Vector3 end = start + direction.normalized * (tileSize * 0.35f);
        Handles.DrawLine(start, end);
        Handles.ConeHandleCap(0, end, Quaternion.LookRotation(direction.normalized), HandleUtility.GetHandleSize(end) * 0.18f, EventType.Repaint);
    }

    Color GetPreviewColor(TileGridCellKind kind)
    {
        if (kind == TileGridCellKind.Pit)
            return PitPreviewColor;
        if (IsStairRampKind(kind))
            return StairPreviewColor;
        if (IsRampKind(kind))
            return RampPreviewColor;

        return NormalPreviewColor;
    }

    string GetPreviewLabel(Vector3Int coordinate, bool erase, bool willStack)
    {
        if (erase)
            return "Erase " + FormatCoordinate(coordinate);

        string prefix = willStack ? "Stack " : "Place ";
        return prefix + GetKindLabel(paintKind) + " " + FormatCoordinate(coordinate);
    }

    void PlaceOrUpdateTile(Vector3Int coordinate, Vector3 worldPosition)
    {
        if (IsStairRampKind(paintKind))
        {
            PlaceStairRamp(coordinate, worldPosition, paintKind);
            return;
        }

        GameObject prefab = GetPrefabForKind(paintKind);
        if (prefab == null)
        {
            ShowNotification(new GUIContent("Assign a base Tile prefab first."));
            return;
        }

        Transform parent = GetOrCreateParent();
        TileGridCell existingTile = FindTileAt(coordinate, selectedFloorIndex);
        if (IsCoordinateReservedByStair(coordinate, selectedFloorIndex, existingTile))
        {
            ShowNotification(new GUIContent("That space is reserved by a stair ramp."));
            return;
        }

        if (existingTile != null)
        {
            Undo.RecordObject(existingTile.transform, "Move Tile");
            existingTile.transform.position = worldPosition;
            ConfigureTile(existingTile, coordinate, selectedFloorIndex, paintKind);
            Selection.activeGameObject = existingTile.gameObject;
            RefreshAllTiles();
            return;
        }

        GameObject tileInstance = InstantiateTilePrefab(prefab);
        if (tileInstance == null)
            return;

        Undo.RegisterCreatedObjectUndo(tileInstance, "Place Tile");
        if (parent != null)
            Undo.SetTransformParent(tileInstance.transform, parent, "Place Tile");

        Undo.RecordObject(tileInstance.transform, "Place Tile");
        tileInstance.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
        tileInstance.name = GetTileName(coordinate, paintKind);

        TileGridCell tileCell = tileInstance.GetComponent<TileGridCell>();
        if (tileCell == null)
            tileCell = Undo.AddComponent<TileGridCell>(tileInstance);

        ConfigureTile(tileCell, coordinate, selectedFloorIndex, paintKind);
        Selection.activeGameObject = tileInstance;
        RefreshAllTiles();
    }

    void PlaceStairRamp(Vector3Int coordinate, Vector3 worldPosition, TileGridCellKind stairKind)
    {
        GameObject prefab = GetPrefabForKind(stairKind);
        if (prefab == null)
        {
            ShowNotification(new GUIContent("Assign a base Tile prefab first."));
            return;
        }

        int lowerFloorIndex = selectedFloorIndex;
        int segmentCount = GetStairRampSegmentCount();
        Vector3Int direction = GetStairRampDirectionInt(stairKind);
        Vector3Int landingCoordinate = coordinate + direction * segmentCount + Vector3Int.up;
        int newFloorIndex = GetFloorIndexAtHeightOrNext(landingCoordinate.y);
        Transform parent = GetOrCreateParent();

        TileGridCell existingStair = FindTileAt(coordinate, lowerFloorIndex);
        if (!CanPlaceStairFootprint(coordinate, lowerFloorIndex, direction, segmentCount, existingStair))
            return;

        TileGridCell stairCell;
        if (existingStair != null)
        {
            Undo.RecordObject(existingStair.transform, "Move Stair Ramp");
            existingStair.transform.position = worldPosition;
            stairCell = existingStair;
        }
        else
        {
            GameObject stairInstance = InstantiateTilePrefab(prefab);
            if (stairInstance == null)
                return;

            Undo.RegisterCreatedObjectUndo(stairInstance, "Place Stair Ramp");
            if (parent != null)
                Undo.SetTransformParent(stairInstance.transform, parent, "Place Stair Ramp");

            Undo.RecordObject(stairInstance.transform, "Place Stair Ramp");
            stairInstance.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
            stairInstance.name = GetTileName(coordinate, stairKind);

            stairCell = stairInstance.GetComponent<TileGridCell>();
            if (stairCell == null)
                stairCell = Undo.AddComponent<TileGridCell>(stairInstance);
        }

        ConfigureTile(stairCell, coordinate, lowerFloorIndex, stairKind);

        PlaceOrUpdateLandingTile(landingCoordinate, newFloorIndex, parent);

        selectedFloorIndex = newFloorIndex;
        stackLevel = landingCoordinate.y;
        Selection.activeGameObject = stairCell.gameObject;
        RefreshAllTiles();
    }

    void PlaceOrUpdateLandingTile(Vector3Int coordinate, int floorIndex, Transform parent)
    {
        GameObject prefab = GetPrefabForKind(TileGridCellKind.Normal);
        if (prefab == null)
            return;

        Vector3 worldPosition = GridToWorld(coordinate);
        TileGridCell existingTile = FindTileAt(coordinate, floorIndex);
        if (existingTile != null)
        {
            Undo.RecordObject(existingTile.transform, "Move Stair Landing");
            existingTile.transform.position = worldPosition;
            ConfigureTile(existingTile, coordinate, floorIndex, TileGridCellKind.Normal);
            return;
        }

        GameObject tileInstance = InstantiateTilePrefab(prefab);
        if (tileInstance == null)
            return;

        Undo.RegisterCreatedObjectUndo(tileInstance, "Place Stair Landing");
        if (parent != null)
            Undo.SetTransformParent(tileInstance.transform, parent, "Place Stair Landing");

        Undo.RecordObject(tileInstance.transform, "Place Stair Landing");
        tileInstance.transform.SetPositionAndRotation(worldPosition, Quaternion.identity);
        tileInstance.name = GetTileName(coordinate, TileGridCellKind.Normal);

        TileGridCell tileCell = tileInstance.GetComponent<TileGridCell>();
        if (tileCell == null)
            tileCell = Undo.AddComponent<TileGridCell>(tileInstance);

        ConfigureTile(tileCell, coordinate, floorIndex, TileGridCellKind.Normal);
    }

    GameObject GetPrefabForKind(TileGridCellKind kind)
    {
        if (kind == TileGridCellKind.Normal)
            return tilePrefab != null ? tilePrefab : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTilePrefabPath);

        GameObject variantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TilePainterPrefabBuilder.GetPrefabPath(kind));
        if (variantPrefab != null)
            return variantPrefab;

        return tilePrefab != null ? tilePrefab : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTilePrefabPath);
    }

    GameObject InstantiateTilePrefab(GameObject prefab)
    {
        if (PrefabUtility.GetPrefabAssetType(prefab) != PrefabAssetType.NotAPrefab)
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        return Instantiate(prefab);
    }

    void ConfigureTile(TileGridCell tileCell, Vector3Int coordinate, int floorIndex, TileGridCellKind kind)
    {
        Undo.RecordObject(tileCell, "Configure Tile");
        tileCell.AutoAssignReferences();
        tileCell.Configure(coordinate, floorIndex, tileSize, verticalSpacing, kind);
        ApplyFeatureSettings(tileCell);
        tileCell.RefreshForConnections(false, false, false, false, false, false);

        EditorUtility.SetDirty(tileCell);
        PrefabUtility.RecordPrefabInstancePropertyModifications(tileCell);
    }

    void ApplyFeatureSettings(TileGridCell tileCell)
    {
        Material resolvedVoidMaterial = voidMaterial != null
            ? voidMaterial
            : AssetDatabase.LoadAssetAtPath<Material>(TilePainterPrefabBuilder.VoidMaterialPath);

        tileCell.ApplyFeatureSettings(
            hideFloorWhenPit,
            hideCeilingWhenPit,
            pitWallDepthSegments,
            pitWallThickness,
            pitRespawnTriggerHeight,
            pitRespawnDamage,
            resolvedVoidMaterial,
            rampAngle,
            rampThickness,
            hideFloorWhenRamp,
            hideCeilingWhenRamp,
            openRampExitWall,
            rampMaterial,
            GetStairRampSegmentCount(),
            hideCeilingWhenTileAbove,
            hideFloorWhenTileBelow);
    }

    void EraseTile(Vector3Int coordinate)
    {
        TileGridCell tile = FindTileAt(coordinate);
        if (tile == null)
            return;

        Undo.DestroyObjectImmediate(tile.gameObject);
        RefreshAllTiles();
    }

    void RefreshAllTiles()
    {
        List<TileGridCell> tiles = GetTilesInScope();
        Dictionary<string, TileGridCell> grid = new Dictionary<string, TileGridCell>();
        Dictionary<TileGridCell, WallOpenings> forcedOpenings = new Dictionary<TileGridCell, WallOpenings>();
        HashSet<string> stairReservedKeys = new HashSet<string>();

        for (int i = 0; i < tiles.Count; i++)
        {
            TileGridCell tile = tiles[i];
            if (tile == null)
                continue;

            string key = MakeGridKey(tile.GridCoordinate, tile.FloorIndex);
            if (!grid.ContainsKey(key))
                grid.Add(key, tile);
        }

        BuildStairRefreshContext(tiles, forcedOpenings, stairReservedKeys);

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

            Undo.RecordObject(tile, "Refresh Tile Walls");
            tile.AutoAssignReferences();
            tile.SetGridSpacing(tileSize, verticalSpacing);
            ApplyFeatureSettings(tile);
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
        }

        SceneView.RepaintAll();
    }

    void BuildStairRefreshContext(
        List<TileGridCell> tiles,
        Dictionary<TileGridCell, WallOpenings> forcedOpenings,
        HashSet<string> stairReservedKeys)
    {
        for (int i = 0; i < tiles.Count; i++)
        {
            TileGridCell stair = tiles[i];
            if (stair == null || !stair.IsStairRamp)
                continue;

            Vector3Int direction = GetStairRampDirectionInt(stair.TileKind);
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
            TileGridCell landingTile = FindTileAt(landingCoordinate);
            if (landingTile != null)
                AddForcedOpening(forcedOpenings, landingTile, -direction);
        }
    }

    bool CanPlaceStairFootprint(Vector3Int coordinate, int floorIndex, Vector3Int direction, int segmentCount, TileGridCell existingStair)
    {
        if (direction == Vector3Int.zero)
            return false;

        for (int offset = 0; offset < segmentCount; offset++)
        {
            Vector3Int footprintCoordinate = coordinate + direction * offset;
            TileGridCell existingTile = FindTileAt(footprintCoordinate, floorIndex);
            if (existingTile != null && existingTile != existingStair)
            {
                ShowNotification(new GUIContent("Stair path needs empty tiles."));
                return false;
            }

            if (IsCoordinateReservedByStair(footprintCoordinate, floorIndex, existingStair))
            {
                ShowNotification(new GUIContent("That space is reserved by another stair ramp."));
                return false;
            }
        }

        return true;
    }

    bool IsCoordinateReservedByStair(Vector3Int coordinate, int floorIndex, TileGridCell ignoreTile)
    {
        List<TileGridCell> tiles = GetTilesInScope();
        for (int i = 0; i < tiles.Count; i++)
        {
            TileGridCell stair = tiles[i];
            if (stair == null || stair == ignoreTile || !stair.IsStairRamp || stair.FloorIndex != floorIndex)
                continue;

            Vector3Int direction = GetStairRampDirectionInt(stair.TileKind);
            int segmentCount = Mathf.Max(1, stair.StairRampSegments);
            for (int offset = 0; offset < segmentCount; offset++)
            {
                if (stair.GridCoordinate + direction * offset == coordinate)
                    return true;
            }
        }

        return false;
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

    List<FloorOption> GetFloorOptions()
    {
        List<TileGridCell> tiles = GetTilesInScope();
        Dictionary<int, int> floorHeights = new Dictionary<int, int>();
        floorHeights[0] = 0;

        for (int i = 0; i < tiles.Count; i++)
        {
            TileGridCell tile = tiles[i];
            if (tile == null)
                continue;

            if (!floorHeights.TryGetValue(tile.FloorIndex, out int existingHeight)
                || tile.GridCoordinate.y < existingHeight)
            {
                floorHeights[tile.FloorIndex] = tile.GridCoordinate.y;
            }
        }

        List<FloorOption> floors = new List<FloorOption>();
        foreach (KeyValuePair<int, int> entry in floorHeights)
            floors.Add(new FloorOption(entry.Key, entry.Value));

        floors.Sort((a, b) =>
        {
            int heightCompare = a.heightLevel.CompareTo(b.heightLevel);
            return heightCompare != 0 ? heightCompare : a.floorIndex.CompareTo(b.floorIndex);
        });

        return floors;
    }

    bool ContainsFloorIndex(List<FloorOption> floors, int floorIndex)
    {
        for (int i = 0; i < floors.Count; i++)
        {
            if (floors[i].floorIndex == floorIndex)
                return true;
        }

        return false;
    }

    int GetNextFloorIndex()
    {
        int maxFloorIndex = 0;
        List<TileGridCell> tiles = GetTilesInScope();
        for (int i = 0; i < tiles.Count; i++)
        {
            TileGridCell tile = tiles[i];
            if (tile != null)
                maxFloorIndex = Mathf.Max(maxFloorIndex, tile.FloorIndex);
        }

        return maxFloorIndex + 1;
    }

    int GetFloorIndexAtHeightOrNext(int heightLevel)
    {
        List<FloorOption> floors = GetFloorOptions();
        for (int i = 0; i < floors.Count; i++)
        {
            if (floors[i].heightLevel == heightLevel)
                return floors[i].floorIndex;
        }

        return GetNextFloorIndex();
    }

    int GetStairRampSegmentCount()
    {
        float angle = Mathf.Clamp(maxStairRampAngle, 8f, 25f);
        float runPerLevel = verticalSpacing / Mathf.Tan(angle * Mathf.Deg2Rad);
        return Mathf.Max(1, Mathf.CeilToInt(runPerLevel / Mathf.Max(MinSpacing, tileSize)));
    }

    TileGridCell FindTileAt(Vector3Int coordinate)
    {
        return FindTileAt(coordinate, -1);
    }

    TileGridCell FindTileAt(Vector3Int coordinate, int floorIndex)
    {
        List<TileGridCell> tiles = GetTilesInScope();
        for (int i = 0; i < tiles.Count; i++)
        {
            TileGridCell tile = tiles[i];
            if (tile != null && tile.GridCoordinate == coordinate && (floorIndex < 0 || tile.FloorIndex == floorIndex))
                return tile;
        }

        return null;
    }

    bool IsTileInScope(TileGridCell tile)
    {
        if (tile == null)
            return false;
        if (tileParent == null)
            return true;

        return tile.transform == tileParent || tile.transform.IsChildOf(tileParent);
    }

    List<TileGridCell> GetTilesInScope()
    {
        TileGridCell[] tiles;
        if (tileParent != null)
        {
            tiles = tileParent.GetComponentsInChildren<TileGridCell>(true);
        }
        else
        {
            tiles = FindObjectsByType<TileGridCell>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        return new List<TileGridCell>(tiles);
    }

    Transform GetOrCreateParent()
    {
        if (tileParent != null)
            return tileParent;

        string cleanParentName = string.IsNullOrWhiteSpace(parentName) ? DefaultParentName : parentName;
        GameObject parentObject = GameObject.Find(cleanParentName);
        if (parentObject == null)
        {
            parentObject = new GameObject(cleanParentName);
            Undo.RegisterCreatedObjectUndo(parentObject, "Create Tile Parent");
        }

        tileParent = parentObject.transform;
        return tileParent;
    }

    static string GetTileName(Vector3Int coordinate, TileGridCellKind kind)
    {
        return kind == TileGridCellKind.Pit
            ? "PitTile " + FormatCoordinate(coordinate)
            : (IsRampKind(kind) || IsStairRampKind(kind))
                ? GetKindLabel(kind).Replace(" ", "") + " " + FormatCoordinate(coordinate)
                : "Tile " + FormatCoordinate(coordinate);
    }

    static string GetKindLabel(TileGridCellKind kind)
    {
        int index = Array.IndexOf(PaintKinds, kind);
        return index >= 0 ? PaintKindLabels[index] : kind.ToString();
    }

    static bool IsRampKind(TileGridCellKind kind)
    {
        return kind == TileGridCellKind.RampPositiveX
            || kind == TileGridCellKind.RampNegativeX
            || kind == TileGridCellKind.RampPositiveZ
            || kind == TileGridCellKind.RampNegativeZ;
    }

    static bool IsStairRampKind(TileGridCellKind kind)
    {
        return kind == TileGridCellKind.StairRampPositiveX
            || kind == TileGridCellKind.StairRampNegativeX
            || kind == TileGridCellKind.StairRampPositiveZ
            || kind == TileGridCellKind.StairRampNegativeZ;
    }

    static Vector3 GetRampDirection(TileGridCellKind kind)
    {
        switch (kind)
        {
            case TileGridCellKind.RampPositiveX:
                return Vector3.right;
            case TileGridCellKind.RampNegativeX:
                return Vector3.left;
            case TileGridCellKind.RampPositiveZ:
                return Vector3.forward;
            case TileGridCellKind.RampNegativeZ:
            case TileGridCellKind.StairRampNegativeZ:
                return Vector3.back;
            case TileGridCellKind.StairRampPositiveX:
                return Vector3.right;
            case TileGridCellKind.StairRampNegativeX:
                return Vector3.left;
            case TileGridCellKind.StairRampPositiveZ:
                return Vector3.forward;
            default:
                return Vector3.zero;
        }
    }

    static Vector3Int GetStairRampDirectionInt(TileGridCellKind kind)
    {
        switch (kind)
        {
            case TileGridCellKind.StairRampPositiveX:
                return Vector3Int.right;
            case TileGridCellKind.StairRampNegativeX:
                return Vector3Int.left;
            case TileGridCellKind.StairRampPositiveZ:
                return Vector3Int.forward;
            case TileGridCellKind.StairRampNegativeZ:
                return Vector3Int.back;
            default:
                return Vector3Int.zero;
        }
    }

    static string MakeGridKey(Vector3Int coordinate, int floorIndex)
    {
        return floorIndex + ":" + coordinate.x + "," + coordinate.y + "," + coordinate.z;
    }

    static string FormatCoordinate(Vector3Int coordinate)
    {
        return "(" + coordinate.x + ", " + coordinate.y + ", " + coordinate.z + ")";
    }
}
