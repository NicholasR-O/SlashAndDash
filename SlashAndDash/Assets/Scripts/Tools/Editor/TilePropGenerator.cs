using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TilePropGenerator
{
    const float DecorationChance = 0.48f;
    const int MinRoomProps = 10;
    const int MaxRoomProps = 18;
    const float FloorInset = 4f;
    const float FloorPadding = 0.35f;
    const float EdgeDepthMin = 10f;
    const float EdgeDepthMax = 16f;
    const float EdgeDepthFraction = 0.22f;
    const float WallInset = 1.8f;
    const float TabletopInset = 0.45f;
    const float TabletopPadding = 0.12f;
    const float TabletopPlacementChance = 0.42f;
    const float BrickStackChance = 0.4f;
    const float HorseStatueAcceptChance = 0.08f;
    const int MaxPlacementFailures = 240;
    const int MaxBrickStackHeight = 4;

    public enum PropPlacement
    {
        Floor,
        WallHigh,
        WallFloor
    }

    public sealed class PropDefinition
    {
        public readonly string assetName;
        public readonly PropPlacement placement;
        public readonly float targetHeight;
        public readonly float wallHeightFraction;
        public readonly int weight;

        public PropDefinition(string newAssetName, PropPlacement newPlacement, float newTargetHeight, float newWallHeightFraction, int newWeight)
        {
            assetName = newAssetName;
            placement = newPlacement;
            targetHeight = newTargetHeight;
            wallHeightFraction = newWallHeightFraction;
            weight = Mathf.Max(1, newWeight);
        }
    }

    struct WallSlot
    {
        public readonly Vector3 localPosition;
        public readonly Quaternion localRotation;

        public WallSlot(Vector3 newLocalPosition, Quaternion newLocalRotation)
        {
            localPosition = newLocalPosition;
            localRotation = newLocalRotation;
        }
    }

    struct FloorEdgeBand
    {
        public readonly float minX;
        public readonly float maxX;
        public readonly float minZ;
        public readonly float maxZ;
        public readonly float inwardYaw;
        public readonly float parallelYaw;
        public readonly int normalAxis;
        public readonly float outwardSign;

        public FloorEdgeBand(
            float newMinX,
            float newMaxX,
            float newMinZ,
            float newMaxZ,
            float newInwardYaw,
            float newParallelYaw,
            int newNormalAxis,
            float newOutwardSign)
        {
            minX = newMinX;
            maxX = newMaxX;
            minZ = newMinZ;
            maxZ = newMaxZ;
            inwardYaw = newInwardYaw;
            parallelYaw = newParallelYaw;
            normalAxis = newNormalAxis;
            outwardSign = newOutwardSign;
        }
    }

    struct PropBounds
    {
        public readonly Bounds localBounds;
        public readonly float bottom;
        public readonly float top;
        public readonly bool isValid;

        public PropBounds(Bounds newLocalBounds)
        {
            localBounds = newLocalBounds;
            bottom = newLocalBounds.min.y;
            top = newLocalBounds.max.y;
            isValid = newLocalBounds.size.x > 0.001f
                && newLocalBounds.size.y > 0.001f
                && newLocalBounds.size.z > 0.001f;
        }
    }

    struct PlacementRect
    {
        public readonly float minX;
        public readonly float maxX;
        public readonly float minZ;
        public readonly float maxZ;

        public PlacementRect(float newMinX, float newMaxX, float newMinZ, float newMaxZ)
        {
            minX = newMinX;
            maxX = newMaxX;
            minZ = newMinZ;
            maxZ = newMaxZ;
        }

        public bool FitsInside(float minAllowedX, float maxAllowedX, float minAllowedZ, float maxAllowedZ)
        {
            return minX >= minAllowedX
                && maxX <= maxAllowedX
                && minZ >= minAllowedZ
                && maxZ <= maxAllowedZ;
        }

        public bool Overlaps(PlacementRect other, float padding)
        {
            return minX < other.maxX + padding
                && maxX > other.minX - padding
                && minZ < other.maxZ + padding
                && maxZ > other.minZ - padding;
        }
    }

    sealed class TableSurface
    {
        public readonly Vector3 localPosition;
        public readonly float yaw;
        public readonly PropBounds bounds;
        public readonly List<PlacementRect> occupiedRects = new List<PlacementRect>();

        public TableSurface(Vector3 newLocalPosition, float newYaw, PropBounds newBounds)
        {
            localPosition = newLocalPosition;
            yaw = newYaw;
            bounds = newBounds;
        }
    }

    static readonly PropDefinition[] FloorProps =
    {
        new PropDefinition("Barrel", PropPlacement.Floor, 5.2f, 0f, 7),
        new PropDefinition("Barrel2", PropPlacement.Floor, 5.2f, 0f, 7),
        new PropDefinition("Crate", PropPlacement.Floor, 4.8f, 0f, 6),
        new PropDefinition("Bag_Coins", PropPlacement.Floor, 1.4f, 0f, 4),
        new PropDefinition("Bag_Standing", PropPlacement.Floor, 2.6f, 0f, 4),
        new PropDefinition("Bucket", PropPlacement.Floor, 2.8f, 0f, 3),
        new PropDefinition("Vase", PropPlacement.Floor, 2.7f, 0f, 3),
        new PropDefinition("Chest", PropPlacement.Floor, 4.2f, 0f, 4),
        new PropDefinition("Coin_Pile", PropPlacement.Floor, 0.9f, 0f, 4),
        new PropDefinition("Brick", PropPlacement.Floor, 0.8f, 0f, 5),
        new PropDefinition("Cobweb", PropPlacement.Floor, 1.8f, 0f, 3),
        new PropDefinition("Cobweb2", PropPlacement.Floor, 1.8f, 0f, 3),
        new PropDefinition("Banner", PropPlacement.Floor, 13f, 0f, 2),
        new PropDefinition("Table_Small", PropPlacement.Floor, 3.8f, 0f, 3),
        new PropDefinition("Table_Big", PropPlacement.Floor, 3.8f, 0f, 2),
        new PropDefinition("Chair", PropPlacement.Floor, 3.8f, 0f, 4),
        new PropDefinition("Statue_Horse", PropPlacement.Floor, 9.5f, 0f, 1)
    };

    static readonly PropDefinition[] TabletopProps =
    {
        new PropDefinition("Bag_Coins", PropPlacement.Floor, 1.4f, 0f, 4),
        new PropDefinition("Bag_Standing", PropPlacement.Floor, 2.6f, 0f, 2),
        new PropDefinition("Coin_Pile", PropPlacement.Floor, 0.9f, 0f, 5)
    };

    static readonly PropDefinition[] HighWallProps =
    {
        new PropDefinition("Banner_wall", PropPlacement.WallHigh, 11f, 0.58f, 4),
        new PropDefinition("Torch", PropPlacement.WallHigh, 5.2f, 0.58f, 5)
    };

    static readonly PropDefinition[] FloorWallProps =
    {
        new PropDefinition("Sword_WallMount", PropPlacement.WallFloor, 4.8f, 0f, 3)
    };

    static readonly PropDefinition[] AllProps = BuildAllPropDefinitions();
    static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();
    static readonly Dictionary<string, PropBounds> PropBoundsCache = new Dictionary<string, PropBounds>();

    public static IReadOnlyList<PropDefinition> AllPropDefinitions => AllProps;

    public static void RefreshTileProps(
        TileGridCell tile,
        bool openPositiveX,
        bool openNegativeX,
        bool openPositiveZ,
        bool openNegativeZ,
        bool hasStairPortal,
        bool reservedByStair,
        bool hasTileBelow)
    {
        if (tile == null)
            return;

        if (tile.TileKind != TileGridCellKind.Normal || reservedByStair || hasTileBelow)
        {
            tile.DestroyGeneratedPropsRoot();
            return;
        }

        int seed = BuildSeed(tile, openPositiveX, openNegativeX, openPositiveZ, openNegativeZ, hasStairPortal, reservedByStair, hasTileBelow);
        System.Random random = new System.Random(seed);

        if (random.NextDouble() > DecorationChance)
        {
            tile.DestroyGeneratedPropsRoot();
            return;
        }

        List<FloorEdgeBand> floorEdgeBands = hasStairPortal
            ? new List<FloorEdgeBand>()
            : BuildFloorEdgeBands(tile.TileSize, openPositiveX, openNegativeX, openPositiveZ, openNegativeZ);
        bool canPlaceFloorProps = floorEdgeBands.Count > 0;
        List<WallSlot> wallSlots = BuildWallSlots(tile.TileSize, tile.VerticalSpacing, openPositiveX, openNegativeX, openPositiveZ, openNegativeZ);
        Transform root = tile.RecreateGeneratedPropsRoot();
        List<PlacementRect> floorRects = new List<PlacementRect>();
        List<TableSurface> tableSurfaces = new List<TableSurface>();
        int desiredProps = random.Next(MinRoomProps, MaxRoomProps + 1);
        int createdProps = 0;
        int failedAttempts = 0;

        while (createdProps < desiredProps && failedAttempts < MaxPlacementFailures)
        {
            int createdThisAttempt = 0;

            if (tableSurfaces.Count > 0 && random.NextDouble() < TabletopPlacementChance * 0.45f)
                createdThisAttempt = TryPlaceTabletopProp(root, tableSurfaces, random);

            if (createdThisAttempt == 0)
            {
                bool useWall = wallSlots.Count > 0 && random.NextDouble() < 0.18f;
                if (useWall)
                    createdThisAttempt = TryPlaceWallProp(root, tile, wallSlots, random);

                if (createdThisAttempt == 0 && canPlaceFloorProps)
                {
                    createdThisAttempt = TryPlaceFloorProp(
                        root,
                        tile,
                        floorEdgeBands,
                        floorRects,
                        tableSurfaces,
                        random,
                        desiredProps - createdProps);
                }

                if (createdThisAttempt == 0 && !useWall)
                    createdThisAttempt = TryPlaceWallProp(root, tile, wallSlots, random);
            }

            if (createdThisAttempt > 0)
            {
                createdProps += createdThisAttempt;
                failedAttempts = 0;
            }
            else
            {
                failedAttempts++;
            }
        }

        if (root.childCount == 0)
            tile.DestroyGeneratedPropsRoot();
    }

    static int TryPlaceFloorProp(
        Transform root,
        TileGridCell tile,
        List<FloorEdgeBand> floorEdgeBands,
        List<PlacementRect> floorRects,
        List<TableSurface> tableSurfaces,
        System.Random random,
        int remainingProps)
    {
        if (remainingProps <= 0)
            return 0;

        PropDefinition definition = ChooseFloorProp(random);
        if (!TryGetPropBounds(definition, out PropBounds propBounds))
            return 0;

        if (!TryFindFloorPlacement(tile.TileSize, definition, propBounds, floorEdgeBands, random, floorRects, out Vector3 localPosition, out float yaw, out PlacementRect rect))
            return 0;

        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        if (!CreateProp(root, definition, localPosition, rotation))
            return 0;

        floorRects.Add(rect);
        int createdProps = 1;

        if (IsTable(definition))
        {
            TableSurface tableSurface = new TableSurface(localPosition, yaw, propBounds);
            tableSurfaces.Add(tableSurface);
            if (createdProps < remainingProps && random.NextDouble() < TabletopPlacementChance)
                createdProps += TryPlaceTabletopProp(root, tableSurface, random);
        }
        else if (IsBrick(definition) && createdProps < remainingProps)
        {
            createdProps += TryPlaceBrickStack(root, definition, localPosition, yaw, propBounds, tile.VerticalSpacing, random, remainingProps - createdProps);
        }

        return createdProps;
    }

    static int TryPlaceWallProp(Transform root, TileGridCell tile, List<WallSlot> wallSlots, System.Random random)
    {
        if (wallSlots.Count == 0)
            return 0;

        PropDefinition[] source = random.NextDouble() < 0.72f ? HighWallProps : FloorWallProps;
        PropDefinition definition = ChooseWeighted(source, random);
        if (!TryGetPropBounds(definition, out PropBounds propBounds))
            return 0;

        for (int attempts = 0; attempts < wallSlots.Count; attempts++)
        {
            int slotIndex = random.Next(wallSlots.Count);
            WallSlot slot = wallSlots[slotIndex];
            Vector3 localPosition = slot.localPosition;

            if (definition.placement == PropPlacement.WallFloor)
            {
                localPosition.y = -propBounds.bottom;
            }
            else
            {
                float minY = -propBounds.bottom;
                float maxY = tile.VerticalSpacing - propBounds.top;
                localPosition.y = Mathf.Clamp(tile.VerticalSpacing * definition.wallHeightFraction, minY, maxY);
            }

            if (!CreateProp(root, definition, localPosition, slot.localRotation))
                continue;

            wallSlots.RemoveAt(slotIndex);
            return 1;
        }

        return 0;
    }

    static int TryPlaceTabletopProp(Transform root, List<TableSurface> tableSurfaces, System.Random random)
    {
        if (tableSurfaces.Count == 0)
            return 0;

        int startIndex = random.Next(tableSurfaces.Count);
        for (int i = 0; i < tableSurfaces.Count; i++)
        {
            TableSurface surface = tableSurfaces[(startIndex + i) % tableSurfaces.Count];
            int created = TryPlaceTabletopProp(root, surface, random);
            if (created > 0)
                return created;
        }

        return 0;
    }

    static int TryPlaceTabletopProp(Transform root, TableSurface surface, System.Random random)
    {
        PropDefinition definition = ChooseWeighted(TabletopProps, random);
        if (!TryGetPropBounds(definition, out PropBounds propBounds))
            return 0;

        for (int attempts = 0; attempts < 24; attempts++)
        {
            float relativeYaw = random.Next(4) * 90f;
            Vector2 footprintSize = GetRotatedFootprintSize(propBounds.localBounds, relativeYaw);
            Vector2 centerOffset = Rotate(new Vector2(propBounds.localBounds.center.x, propBounds.localBounds.center.z), relativeYaw);

            float minAllowedX = surface.bounds.localBounds.min.x + TabletopInset;
            float maxAllowedX = surface.bounds.localBounds.max.x - TabletopInset;
            float minAllowedZ = surface.bounds.localBounds.min.z + TabletopInset;
            float maxAllowedZ = surface.bounds.localBounds.max.z - TabletopInset;
            float minPivotX = minAllowedX + footprintSize.x * 0.5f - centerOffset.x;
            float maxPivotX = maxAllowedX - footprintSize.x * 0.5f - centerOffset.x;
            float minPivotZ = minAllowedZ + footprintSize.y * 0.5f - centerOffset.y;
            float maxPivotZ = maxAllowedZ - footprintSize.y * 0.5f - centerOffset.y;

            if (minPivotX > maxPivotX || minPivotZ > maxPivotZ)
                return 0;

            Vector2 tableLocalPivot = new Vector2(
                RandomRange(random, minPivotX, maxPivotX),
                RandomRange(random, minPivotZ, maxPivotZ));
            PlacementRect rect = GetFootprintRect(propBounds.localBounds, tableLocalPivot, relativeYaw);

            if (!rect.FitsInside(minAllowedX, maxAllowedX, minAllowedZ, maxAllowedZ)
                || OverlapsAny(rect, surface.occupiedRects, TabletopPadding))
            {
                continue;
            }

            Vector2 worldOffset = Rotate(tableLocalPivot, surface.yaw);
            Vector3 localPosition = new Vector3(
                surface.localPosition.x + worldOffset.x,
                surface.localPosition.y + surface.bounds.top - propBounds.bottom,
                surface.localPosition.z + worldOffset.y);
            Quaternion rotation = Quaternion.Euler(0f, surface.yaw + relativeYaw, 0f);

            if (!CreateProp(root, definition, localPosition, rotation))
                return 0;

            surface.occupiedRects.Add(rect);
            return 1;
        }

        return 0;
    }

    static int TryPlaceBrickStack(
        Transform root,
        PropDefinition definition,
        Vector3 basePosition,
        float baseYaw,
        PropBounds propBounds,
        float verticalSpacing,
        System.Random random,
        int remainingProps)
    {
        if (remainingProps <= 0 || random.NextDouble() > BrickStackChance)
            return 0;

        int maxExtraBricks = Mathf.Min(remainingProps, MaxBrickStackHeight - 1);
        int extraBricks = random.Next(1, maxExtraBricks + 1);
        int createdProps = 0;
        float nextBottom = basePosition.y + propBounds.top;

        for (int i = 0; i < extraBricks; i++)
        {
            Vector3 localPosition = basePosition;
            localPosition.y = nextBottom - propBounds.bottom;
            if (localPosition.y + propBounds.top > verticalSpacing - 1f)
                break;

            float yaw = baseYaw + random.Next(2) * 90f;
            if (!CreateProp(root, definition, localPosition, Quaternion.Euler(0f, yaw, 0f)))
                break;

            createdProps++;
            nextBottom = localPosition.y + propBounds.top;
        }

        return createdProps;
    }

    static bool TryFindFloorPlacement(
        float tileSize,
        PropDefinition definition,
        PropBounds propBounds,
        List<FloorEdgeBand> floorEdgeBands,
        System.Random random,
        List<PlacementRect> occupiedRects,
        out Vector3 localPosition,
        out float yaw,
        out PlacementRect rect)
    {
        localPosition = Vector3.zero;
        yaw = 0f;
        rect = new PlacementRect();

        if (floorEdgeBands == null || floorEdgeBands.Count == 0)
            return false;

        for (int attempts = 0; attempts < 64; attempts++)
        {
            FloorEdgeBand edge = floorEdgeBands[random.Next(floorEdgeBands.Count)];
            yaw = ChooseFloorYaw(definition, edge, random);
            Vector2 footprintSize = GetRotatedFootprintSize(propBounds.localBounds, yaw);
            Vector2 centerOffset = Rotate(new Vector2(propBounds.localBounds.center.x, propBounds.localBounds.center.z), yaw);

            float minPivotX = edge.minX + footprintSize.x * 0.5f - centerOffset.x;
            float maxPivotX = edge.maxX - footprintSize.x * 0.5f - centerOffset.x;
            float minPivotZ = edge.minZ + footprintSize.y * 0.5f - centerOffset.y;
            float maxPivotZ = edge.maxZ - footprintSize.y * 0.5f - centerOffset.y;

            if (minPivotX > maxPivotX || minPivotZ > maxPivotZ)
                continue;

            Vector2 pivot = PickEdgeBiasedPivot(edge, random, minPivotX, maxPivotX, minPivotZ, maxPivotZ);

            localPosition = new Vector3(
                pivot.x,
                -propBounds.bottom,
                pivot.y);
            rect = GetFootprintRect(propBounds.localBounds, new Vector2(localPosition.x, localPosition.z), yaw);

            if (rect.FitsInside(edge.minX, edge.maxX, edge.minZ, edge.maxZ)
                && !OverlapsAny(rect, occupiedRects, FloorPadding))
            {
                return true;
            }
        }

        return false;
    }

    static List<FloorEdgeBand> BuildFloorEdgeBands(float tileSize, bool openPositiveX, bool openNegativeX, bool openPositiveZ, bool openNegativeZ)
    {
        float halfSize = tileSize * 0.5f - FloorInset;
        float edgeDepth = Mathf.Clamp(tileSize * EdgeDepthFraction, EdgeDepthMin, EdgeDepthMax);
        edgeDepth = Mathf.Min(edgeDepth, halfSize);
        List<FloorEdgeBand> edgeBands = new List<FloorEdgeBand>();

        if (halfSize <= 0f || edgeDepth <= 0f)
            return edgeBands;

        if (!openPositiveX)
            edgeBands.Add(new FloorEdgeBand(halfSize - edgeDepth, halfSize, -halfSize, halfSize, -90f, 0f, 0, 1f));
        if (!openNegativeX)
            edgeBands.Add(new FloorEdgeBand(-halfSize, -halfSize + edgeDepth, -halfSize, halfSize, 90f, 0f, 0, -1f));
        if (!openPositiveZ)
            edgeBands.Add(new FloorEdgeBand(-halfSize, halfSize, halfSize - edgeDepth, halfSize, 180f, 90f, 1, 1f));
        if (!openNegativeZ)
            edgeBands.Add(new FloorEdgeBand(-halfSize, halfSize, -halfSize, -halfSize + edgeDepth, 0f, 90f, 1, -1f));

        return edgeBands;
    }

    static Vector2 PickEdgeBiasedPivot(
        FloorEdgeBand edge,
        System.Random random,
        float minPivotX,
        float maxPivotX,
        float minPivotZ,
        float maxPivotZ)
    {
        float pivotX = RandomRange(random, minPivotX, maxPivotX);
        float pivotZ = RandomRange(random, minPivotZ, maxPivotZ);

        if (edge.normalAxis == 0)
        {
            if (edge.outwardSign > 0f)
                pivotX = RandomRange(random, Mathf.Lerp(minPivotX, maxPivotX, 0.45f), maxPivotX);
            else
                pivotX = RandomRange(random, minPivotX, Mathf.Lerp(minPivotX, maxPivotX, 0.55f));
        }
        else
        {
            if (edge.outwardSign > 0f)
                pivotZ = RandomRange(random, Mathf.Lerp(minPivotZ, maxPivotZ, 0.45f), maxPivotZ);
            else
                pivotZ = RandomRange(random, minPivotZ, Mathf.Lerp(minPivotZ, maxPivotZ, 0.55f));
        }

        return new Vector2(pivotX, pivotZ);
    }

    static float ChooseFloorYaw(PropDefinition definition, FloorEdgeBand edge, System.Random random)
    {
        if (IsLongEdgeProp(definition))
            return edge.parallelYaw + random.Next(2) * 180f + RandomRange(random, -10f, 10f);

        if (IsBrick(definition))
            return edge.parallelYaw + random.Next(2) * 90f + RandomRange(random, -8f, 8f);

        if (IsLooseSmallProp(definition))
            return edge.inwardYaw + RandomRange(random, -34f, 34f);

        return edge.inwardYaw + RandomRange(random, -16f, 16f);
    }

    static List<WallSlot> BuildWallSlots(float tileSize, float verticalSpacing, bool openPositiveX, bool openNegativeX, bool openPositiveZ, bool openNegativeZ)
    {
        float halfSize = tileSize * 0.5f - WallInset;
        float sideOffset = tileSize * 0.22f;
        float defaultY = verticalSpacing * 0.5f;
        List<WallSlot> slots = new List<WallSlot>();

        if (!openPositiveX)
        {
            slots.Add(new WallSlot(new Vector3(halfSize, defaultY, sideOffset), Quaternion.Euler(0f, -90f, 0f)));
            slots.Add(new WallSlot(new Vector3(halfSize, defaultY, -sideOffset), Quaternion.Euler(0f, -90f, 0f)));
        }

        if (!openNegativeX)
        {
            slots.Add(new WallSlot(new Vector3(-halfSize, defaultY, sideOffset), Quaternion.Euler(0f, 90f, 0f)));
            slots.Add(new WallSlot(new Vector3(-halfSize, defaultY, -sideOffset), Quaternion.Euler(0f, 90f, 0f)));
        }

        if (!openPositiveZ)
        {
            slots.Add(new WallSlot(new Vector3(sideOffset, defaultY, halfSize), Quaternion.Euler(0f, 180f, 0f)));
            slots.Add(new WallSlot(new Vector3(-sideOffset, defaultY, halfSize), Quaternion.Euler(0f, 180f, 0f)));
        }

        if (!openNegativeZ)
        {
            slots.Add(new WallSlot(new Vector3(sideOffset, defaultY, -halfSize), Quaternion.identity));
            slots.Add(new WallSlot(new Vector3(-sideOffset, defaultY, -halfSize), Quaternion.identity));
        }

        return slots;
    }

    static bool CreateProp(Transform root, PropDefinition definition, Vector3 localPosition, Quaternion localRotation)
    {
        GameObject prefab = LoadPropPrefab(definition.assetName);
        if (prefab == null)
            return false;

        GameObject prop = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (prop == null)
            prop = UnityEngine.Object.Instantiate(prefab);

        prop.name = definition.assetName + " Prop";
        prop.transform.SetParent(root, false);
        prop.transform.localPosition = localPosition;
        prop.transform.localRotation = localRotation;
        prop.transform.localScale = Vector3.one;
        return true;
    }

    static GameObject LoadPropPrefab(string assetName)
    {
        if (PrefabCache.TryGetValue(assetName, out GameObject cachedPrefab))
            return cachedPrefab;

        CastlePropPrefabBuilder.EnsurePrefabExists(assetName);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CastlePropPrefabBuilder.GetPrefabPath(assetName));
        PrefabCache[assetName] = prefab;
        return prefab;
    }

    public static bool TryGetPropDefinition(string assetName, out PropDefinition definition)
    {
        for (int i = 0; i < AllProps.Length; i++)
        {
            if (string.Equals(AllProps[i].assetName, assetName, StringComparison.OrdinalIgnoreCase))
            {
                definition = AllProps[i];
                return true;
            }
        }

        definition = null;
        return false;
    }

    static PropDefinition ChooseFloorProp(System.Random random)
    {
        PropDefinition definition = ChooseWeighted(FloorProps, random);
        if (!IsHorseStatue(definition) || random.NextDouble() < HorseStatueAcceptChance)
            return definition;

        for (int i = 0; i < 16; i++)
        {
            definition = ChooseWeighted(FloorProps, random);
            if (!IsHorseStatue(definition))
                return definition;
        }

        return FloorProps[0];
    }

    static PropDefinition ChooseWeighted(PropDefinition[] definitions, System.Random random)
    {
        int totalWeight = 0;
        for (int i = 0; i < definitions.Length; i++)
            totalWeight += definitions[i].weight;

        int roll = random.Next(totalWeight);
        for (int i = 0; i < definitions.Length; i++)
        {
            roll -= definitions[i].weight;
            if (roll < 0)
                return definitions[i];
        }

        return definitions[definitions.Length - 1];
    }

    static bool TryGetPropBounds(PropDefinition definition, out PropBounds propBounds)
    {
        if (PropBoundsCache.TryGetValue(definition.assetName, out propBounds))
            return propBounds.isValid;

        GameObject prefab = LoadPropPrefab(definition.assetName);
        if (prefab == null)
        {
            propBounds = new PropBounds();
            PropBoundsCache[definition.assetName] = propBounds;
            return false;
        }

        Bounds localBounds;
        bool hasBounds = TryGetLocalColliderBounds(prefab, out localBounds)
            || TryGetLocalRendererBounds(prefab.transform, out localBounds);

        propBounds = hasBounds ? new PropBounds(localBounds) : new PropBounds();
        PropBoundsCache[definition.assetName] = propBounds;
        return propBounds.isValid;
    }

    static bool TryGetLocalColliderBounds(GameObject root, out Bounds localBounds)
    {
        localBounds = new Bounds();
        bool hasBounds = false;
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
                continue;

            if (!TryGetColliderLocalBounds(collider, out Bounds colliderLocalBounds))
                continue;

            Matrix4x4 colliderToRoot = root.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
            EncapsulateTransformedBounds(colliderToRoot, colliderLocalBounds, ref localBounds, ref hasBounds);
        }

        return hasBounds;
    }

    static bool TryGetColliderLocalBounds(Collider collider, out Bounds localBounds)
    {
        localBounds = new Bounds();

        BoxCollider boxCollider = collider as BoxCollider;
        if (boxCollider != null)
        {
            localBounds = new Bounds(boxCollider.center, boxCollider.size);
            return true;
        }

        SphereCollider sphereCollider = collider as SphereCollider;
        if (sphereCollider != null)
        {
            float diameter = sphereCollider.radius * 2f;
            localBounds = new Bounds(sphereCollider.center, new Vector3(diameter, diameter, diameter));
            return true;
        }

        CapsuleCollider capsuleCollider = collider as CapsuleCollider;
        if (capsuleCollider != null)
        {
            Vector3 size = new Vector3(capsuleCollider.radius * 2f, capsuleCollider.radius * 2f, capsuleCollider.radius * 2f);
            if (capsuleCollider.direction == 0)
                size.x = capsuleCollider.height;
            else if (capsuleCollider.direction == 1)
                size.y = capsuleCollider.height;
            else
                size.z = capsuleCollider.height;

            localBounds = new Bounds(capsuleCollider.center, size);
            return true;
        }

        MeshCollider meshCollider = collider as MeshCollider;
        if (meshCollider != null && meshCollider.sharedMesh != null)
        {
            localBounds = meshCollider.sharedMesh.bounds;
            return true;
        }

        return false;
    }

    static bool TryGetLocalRendererBounds(Transform root, out Bounds localBounds)
    {
        localBounds = new Bounds();
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds worldBounds = renderer.bounds;
            EncapsulateWorldBounds(root, worldBounds, ref localBounds, ref hasBounds);
        }

        return hasBounds;
    }

    static void EncapsulateTransformedBounds(Matrix4x4 matrix, Bounds sourceBounds, ref Bounds targetBounds, ref bool hasBounds)
    {
        Vector3 min = sourceBounds.min;
        Vector3 max = sourceBounds.max;
        EncapsulateLocalPoint(matrix.MultiplyPoint3x4(min), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(matrix.MultiplyPoint3x4(new Vector3(min.x, min.y, max.z)), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(matrix.MultiplyPoint3x4(new Vector3(min.x, max.y, min.z)), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(matrix.MultiplyPoint3x4(new Vector3(min.x, max.y, max.z)), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(matrix.MultiplyPoint3x4(new Vector3(max.x, min.y, min.z)), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(matrix.MultiplyPoint3x4(new Vector3(max.x, min.y, max.z)), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(matrix.MultiplyPoint3x4(new Vector3(max.x, max.y, min.z)), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(matrix.MultiplyPoint3x4(max), ref targetBounds, ref hasBounds);
    }

    static void EncapsulateWorldBounds(Transform root, Bounds sourceBounds, ref Bounds targetBounds, ref bool hasBounds)
    {
        Vector3 min = sourceBounds.min;
        Vector3 max = sourceBounds.max;
        EncapsulateLocalPoint(root.InverseTransformPoint(min), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(root.InverseTransformPoint(new Vector3(min.x, min.y, max.z)), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(root.InverseTransformPoint(new Vector3(min.x, max.y, min.z)), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(root.InverseTransformPoint(new Vector3(min.x, max.y, max.z)), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(root.InverseTransformPoint(new Vector3(max.x, min.y, min.z)), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(root.InverseTransformPoint(new Vector3(max.x, min.y, max.z)), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(root.InverseTransformPoint(new Vector3(max.x, max.y, min.z)), ref targetBounds, ref hasBounds);
        EncapsulateLocalPoint(root.InverseTransformPoint(max), ref targetBounds, ref hasBounds);
    }

    static void EncapsulateLocalPoint(Vector3 localPoint, ref Bounds bounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            bounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(localPoint);
    }

    static Vector2 GetRotatedFootprintSize(Bounds localBounds, float yaw)
    {
        float radians = yaw * Mathf.Deg2Rad;
        float cos = Mathf.Abs(Mathf.Cos(radians));
        float sin = Mathf.Abs(Mathf.Sin(radians));
        Vector3 size = localBounds.size;
        return new Vector2(size.x * cos + size.z * sin, size.x * sin + size.z * cos);
    }

    static PlacementRect GetFootprintRect(Bounds localBounds, Vector2 pivotPosition, float yaw)
    {
        Vector2 footprintSize = GetRotatedFootprintSize(localBounds, yaw);
        Vector2 centerOffset = Rotate(new Vector2(localBounds.center.x, localBounds.center.z), yaw);
        Vector2 center = pivotPosition + centerOffset;
        return new PlacementRect(
            center.x - footprintSize.x * 0.5f,
            center.x + footprintSize.x * 0.5f,
            center.y - footprintSize.y * 0.5f,
            center.y + footprintSize.y * 0.5f);
    }

    static Vector2 Rotate(Vector2 value, float yaw)
    {
        float radians = yaw * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
    }

    static bool OverlapsAny(PlacementRect rect, List<PlacementRect> occupiedRects, float padding)
    {
        for (int i = 0; i < occupiedRects.Count; i++)
        {
            if (rect.Overlaps(occupiedRects[i], padding))
                return true;
        }

        return false;
    }

    static float RandomRange(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    static bool IsLongEdgeProp(PropDefinition definition)
    {
        return IsTable(definition)
            || string.Equals(definition.assetName, "Chest", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.assetName, "Crate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.assetName, "Banner", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsLooseSmallProp(PropDefinition definition)
    {
        return string.Equals(definition.assetName, "Bag_Coins", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.assetName, "Bag_Standing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.assetName, "Barrel", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.assetName, "Barrel2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.assetName, "Bucket", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.assetName, "Coin_Pile", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.assetName, "Cobweb", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.assetName, "Cobweb2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.assetName, "Vase", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsTable(PropDefinition definition)
    {
        return string.Equals(definition.assetName, "Table_Small", StringComparison.OrdinalIgnoreCase)
            || string.Equals(definition.assetName, "Table_Big", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsBrick(PropDefinition definition)
    {
        return string.Equals(definition.assetName, "Brick", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsHorseStatue(PropDefinition definition)
    {
        return string.Equals(definition.assetName, "Statue_Horse", StringComparison.OrdinalIgnoreCase);
    }

    static int BuildSeed(
        TileGridCell tile,
        bool openPositiveX,
        bool openNegativeX,
        bool openPositiveZ,
        bool openNegativeZ,
        bool hasStairPortal,
        bool reservedByStair,
        bool hasTileBelow)
    {
        int connectionMask = 0;
        if (openPositiveX)
            connectionMask |= 1;
        if (openNegativeX)
            connectionMask |= 2;
        if (openPositiveZ)
            connectionMask |= 4;
        if (openNegativeZ)
            connectionMask |= 8;
        if (hasStairPortal)
            connectionMask |= 16;
        if (reservedByStair)
            connectionMask |= 32;
        if (hasTileBelow)
            connectionMask |= 64;

        unchecked
        {
            int seed = 486187739;
            seed = seed * 31 + tile.FloorIndex;
            seed = seed * 31 + tile.GridCoordinate.x;
            seed = seed * 31 + tile.GridCoordinate.y;
            seed = seed * 31 + tile.GridCoordinate.z;
            seed = seed * 31 + (int)tile.TileKind;
            seed = seed * 31 + connectionMask;
            return seed;
        }
    }

    static PropDefinition[] BuildAllPropDefinitions()
    {
        PropDefinition[] definitions = new PropDefinition[FloorProps.Length + HighWallProps.Length + FloorWallProps.Length];
        Array.Copy(FloorProps, definitions, FloorProps.Length);
        Array.Copy(HighWallProps, 0, definitions, FloorProps.Length, HighWallProps.Length);
        Array.Copy(FloorWallProps, 0, definitions, FloorProps.Length + HighWallProps.Length, FloorWallProps.Length);

        Array.Sort(definitions, (a, b) => string.Compare(a.assetName, b.assetName, StringComparison.OrdinalIgnoreCase));
        return definitions;
    }
}
