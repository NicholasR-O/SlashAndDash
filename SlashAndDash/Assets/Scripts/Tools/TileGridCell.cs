using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum TileGridCellKind
{
    Normal,
    Pit,
    RampPositiveX,
    RampNegativeX,
    RampPositiveZ,
    RampNegativeZ,
    StairRampPositiveX,
    StairRampNegativeX,
    StairRampPositiveZ,
    StairRampNegativeZ
}

[DisallowMultipleComponent]
public class TileGridCell : MonoBehaviour
{
    const string GeneratedRootName = "__TileToolGenerated";
    public const string GeneratedPropsRootName = "__TileToolProps";
    const float MinSpacing = 0.01f;
    const float GeneratedBoxTextureWorldUnitsPerUv = 6f;
    const float GeneratedSurfaceTextureWorldUnitsPerUv = 1f;
    const float GeneratedRampSurfaceY = 0.5f;
    const float GeneratedRampSeamOverlap = 0.08f;
    const string GeneratedRampMeshSuffix = " Tile Tool Mesh";

    [Header("Grid")]
    [SerializeField] Vector3Int gridCoordinate;
    [SerializeField] int floorIndex;
    [SerializeField] TileGridCellKind tileKind;
    [SerializeField] float tileSize = 60f;
    [SerializeField] float verticalSpacing = 48f;

    [Header("Walls")]
    [SerializeField] GameObject positiveXWall;
    [SerializeField] GameObject negativeXWall;
    [SerializeField] GameObject positiveZWall;
    [SerializeField] GameObject negativeZWall;

    [Header("Surfaces")]
    [SerializeField] GameObject floor;
    [SerializeField] GameObject ceiling;
    [SerializeField] bool hideCeilingWhenStackedAbove = true;
    [SerializeField] bool hideFloorWhenStackedBelow = true;

    [Header("Pit")]
    [SerializeField] bool hideFloorWhenPit = true;
    [SerializeField] bool hideCeilingWhenPit;
    [SerializeField, Min(1)] int pitWallDepthSegments = 3;
    [SerializeField, Min(0.1f)] float pitWallThickness = 1.2f;
    [SerializeField, Min(0.1f)] float pitRespawnTriggerHeight = 14f;
    [SerializeField] float pitRespawnDamage = -1f;
    [SerializeField] Material voidMaterial;

    [Header("Ramp")]
    [SerializeField, Range(5f, 45f)] float rampAngle = 22.5f;
    [SerializeField, Min(0.1f)] float rampThickness = 1.2f;
    [SerializeField] bool hideFloorWhenRamp = true;
    [SerializeField] bool hideCeilingWhenRamp = true;
    [SerializeField] bool openRampExitWall = true;
    [SerializeField] Material rampMaterial;

    [Header("Stair Ramp")]
    [SerializeField, Min(1)] int stairRampSegments = 3;

    public Vector3Int GridCoordinate => gridCoordinate;
    public int FloorIndex => floorIndex;
    public TileGridCellKind TileKind => tileKind;
    public float TileSize => tileSize;
    public float VerticalSpacing => verticalSpacing;
    public int StairRampSegments => stairRampSegments;
    public bool IsPit => tileKind == TileGridCellKind.Pit;
    public bool IsRamp => GetRampDirection(tileKind) != Vector3Int.zero;
    public bool IsStairRamp => GetStairRampDirection(tileKind) != Vector3Int.zero;

    public void Configure(Vector3Int coordinate, int newFloorIndex, float newTileSize, float newVerticalSpacing, TileGridCellKind newTileKind)
    {
        gridCoordinate = coordinate;
        floorIndex = Mathf.Max(0, newFloorIndex);
        tileSize = Mathf.Max(MinSpacing, newTileSize);
        verticalSpacing = Mathf.Max(MinSpacing, newVerticalSpacing);
        tileKind = newTileKind;
    }

    public void Configure(Vector3Int coordinate, float newTileSize, float newVerticalSpacing, TileGridCellKind newTileKind)
    {
        Configure(coordinate, floorIndex, newTileSize, newVerticalSpacing, newTileKind);
    }

    public void ApplyFeatureSettings(
        bool newHideFloorWhenPit,
        bool newHideCeilingWhenPit,
        int newPitWallDepthSegments,
        float newPitWallThickness,
        float newPitRespawnTriggerHeight,
        float newPitRespawnDamage,
        Material newVoidMaterial,
        float newRampAngle,
        float newRampThickness,
        bool newHideFloorWhenRamp,
        bool newHideCeilingWhenRamp,
        bool newOpenRampExitWall,
        Material newRampMaterial,
        int newStairRampSegments,
        bool newHideCeilingWhenStackedAbove,
        bool newHideFloorWhenStackedBelow)
    {
        hideFloorWhenPit = newHideFloorWhenPit;
        hideCeilingWhenPit = newHideCeilingWhenPit;
        pitWallDepthSegments = Mathf.Max(1, newPitWallDepthSegments);
        pitWallThickness = Mathf.Max(0.1f, newPitWallThickness);
        pitRespawnTriggerHeight = Mathf.Max(0.1f, newPitRespawnTriggerHeight);
        pitRespawnDamage = newPitRespawnDamage;
        voidMaterial = newVoidMaterial;

        rampAngle = Mathf.Clamp(newRampAngle, 5f, 45f);
        rampThickness = Mathf.Max(0.1f, newRampThickness);
        hideFloorWhenRamp = newHideFloorWhenRamp;
        hideCeilingWhenRamp = newHideCeilingWhenRamp;
        openRampExitWall = newOpenRampExitWall;
        rampMaterial = newRampMaterial;
        stairRampSegments = Mathf.Max(1, newStairRampSegments);

        hideCeilingWhenStackedAbove = newHideCeilingWhenStackedAbove;
        hideFloorWhenStackedBelow = newHideFloorWhenStackedBelow;
    }

    public void SetGridSpacing(float newTileSize, float newVerticalSpacing)
    {
        tileSize = Mathf.Max(MinSpacing, newTileSize);
        verticalSpacing = Mathf.Max(MinSpacing, newVerticalSpacing);
    }

    public void SetTileKind(TileGridCellKind newTileKind)
    {
        tileKind = newTileKind;
    }

    public void RefreshForConnections(
        bool connectedPositiveX,
        bool connectedNegativeX,
        bool connectedPositiveZ,
        bool connectedNegativeZ,
        bool connectedAbove,
        bool connectedBelow)
    {
        RefreshForConnections(
            connectedPositiveX,
            connectedNegativeX,
            connectedPositiveZ,
            connectedNegativeZ,
            connectedAbove,
            connectedBelow,
            false,
            false,
            false,
            false);
    }

    public void RefreshForConnections(
        bool connectedPositiveX,
        bool connectedNegativeX,
        bool connectedPositiveZ,
        bool connectedNegativeZ,
        bool connectedAbove,
        bool connectedBelow,
        bool forceOpenPositiveX,
        bool forceOpenNegativeX,
        bool forceOpenPositiveZ,
        bool forceOpenNegativeZ)
    {
        Vector3Int rampDirection = GetRampDirection(tileKind);
        if (rampDirection == Vector3Int.zero)
            rampDirection = GetStairRampDirection(tileKind);

        bool isStairRamp = IsStairRamp;
        bool stairAlongX = isStairRamp && Mathf.Abs(rampDirection.x) > 0;
        bool stairAlongZ = isStairRamp && Mathf.Abs(rampDirection.z) > 0;
        bool openPositiveX = connectedPositiveX || forceOpenPositiveX || (openRampExitWall && rampDirection == Vector3Int.right) || stairAlongX;
        bool openNegativeX = connectedNegativeX || forceOpenNegativeX || (openRampExitWall && rampDirection == Vector3Int.left) || stairAlongX;
        bool openPositiveZ = connectedPositiveZ || forceOpenPositiveZ || (openRampExitWall && rampDirection == Vector3Int.forward) || stairAlongZ;
        bool openNegativeZ = connectedNegativeZ || forceOpenNegativeZ || (openRampExitWall && rampDirection == Vector3Int.back) || stairAlongZ;

        AutoAssignReferences();
        RefreshGeneratedFeatures(openPositiveX, openNegativeX, openPositiveZ, openNegativeZ);

        SetObjectActive(positiveXWall, !openPositiveX);
        SetObjectActive(negativeXWall, !openNegativeX);
        SetObjectActive(positiveZWall, !openPositiveZ);
        SetObjectActive(negativeZWall, !openNegativeZ);

        bool floorVisible = !(tileKind == TileGridCellKind.Pit && hideFloorWhenPit)
            && !((IsRamp || IsStairRamp) && hideFloorWhenRamp)
            && !(connectedBelow && hideFloorWhenStackedBelow);
        bool ceilingVisible = !(tileKind == TileGridCellKind.Pit && hideCeilingWhenPit)
            && !((IsRamp || IsStairRamp) && hideCeilingWhenRamp)
            && !(connectedAbove && hideCeilingWhenStackedAbove);

        SetObjectActive(floor, floorVisible);
        SetObjectActive(ceiling, ceilingVisible);
    }

    public void RefreshGeneratedFeatures()
    {
        RefreshGeneratedFeatures(false, false, false, false);
    }

    void RefreshGeneratedFeatures(bool openPositiveX, bool openNegativeX, bool openPositiveZ, bool openNegativeZ)
    {
        if (tileKind == TileGridCellKind.Normal)
        {
            DestroyGeneratedRoot();
            return;
        }

        Transform root = RecreateGeneratedRoot();
        if (tileKind == TileGridCellKind.Pit)
        {
            BuildPitFeatures(root, openPositiveX, openNegativeX, openPositiveZ, openNegativeZ);
            return;
        }

        if (IsStairRamp)
        {
            BuildStairRampFeatures(root, GetStairRampDirection(tileKind));
            return;
        }

        BuildRampFeatures(root, GetRampDirection(tileKind));
    }

    public void AutoAssignReferences()
    {
        positiveXWall = positiveXWall != null ? positiveXWall : FindChildObject("+XWall", "X+Wall", "PositiveXWall", "RightWall", "EastWall");
        negativeXWall = negativeXWall != null ? negativeXWall : FindChildObject("-XWall", "X-Wall", "NegativeXWall", "LeftWall", "WestWall");
        positiveZWall = positiveZWall != null ? positiveZWall : FindChildObject("+ZWall", "Z+Wall", "PositiveZWall", "ForwardWall", "NorthWall");
        negativeZWall = negativeZWall != null ? negativeZWall : FindChildObject("-ZWall", "Z-Wall", "NegativeZWall", "BackWall", "SouthWall");
        floor = floor != null ? floor : FindChildObject("Floor", "Ground");
        ceiling = ceiling != null ? ceiling : FindChildObject("Ceiling", "Roof");

        AssignMissingWallsByPosition();
    }

    void Reset()
    {
        AutoAssignReferences();
    }

    void OnValidate()
    {
        tileSize = Mathf.Max(MinSpacing, tileSize);
        verticalSpacing = Mathf.Max(MinSpacing, verticalSpacing);
        pitWallDepthSegments = Mathf.Max(1, pitWallDepthSegments);
        pitWallThickness = Mathf.Max(0.1f, pitWallThickness);
        pitRespawnTriggerHeight = Mathf.Max(0.1f, pitRespawnTriggerHeight);
        rampAngle = Mathf.Clamp(rampAngle, 5f, 45f);
        rampThickness = Mathf.Max(0.1f, rampThickness);
        stairRampSegments = Mathf.Max(1, stairRampSegments);
    }

    void BuildPitFeatures(Transform root, bool openPositiveX, bool openNegativeX, bool openPositiveZ, bool openNegativeZ)
    {
        float depth = Mathf.Max(verticalSpacing, verticalSpacing * pitWallDepthSegments);
        float halfSize = tileSize * 0.5f;
        float wallHeight = depth;
        float wallCenterY = -depth * 0.5f;
        Material wallMaterial = GetFirstMaterial(positiveXWall, negativeXWall, positiveZWall, negativeZWall);
        Material blackMaterial = voidMaterial != null ? voidMaterial : CreateRuntimeMaterial(Color.black);

        CreateBox(root, "Pit +X Shaft Wall", new Vector3(halfSize, wallCenterY, 0f), Quaternion.identity,
            new Vector3(pitWallThickness, wallHeight, tileSize + pitWallThickness), wallMaterial, copyCollision: true, copyGroundTag: false);
        CreateBox(root, "Pit -X Shaft Wall", new Vector3(-halfSize, wallCenterY, 0f), Quaternion.identity,
            new Vector3(pitWallThickness, wallHeight, tileSize + pitWallThickness), wallMaterial, copyCollision: true, copyGroundTag: false);
        CreateBox(root, "Pit +Z Shaft Wall", new Vector3(0f, wallCenterY, halfSize), Quaternion.identity,
            new Vector3(tileSize + pitWallThickness, wallHeight, pitWallThickness), wallMaterial, copyCollision: true, copyGroundTag: false);
        CreateBox(root, "Pit -Z Shaft Wall", new Vector3(0f, wallCenterY, -halfSize), Quaternion.identity,
            new Vector3(tileSize + pitWallThickness, wallHeight, pitWallThickness), wallMaterial, copyCollision: true, copyGroundTag: false);

        GetConnectedArea(tileSize, 1f, openPositiveX, openNegativeX, openPositiveZ, openNegativeZ, out Vector3 voidCenter, out Vector3 voidSize);
        GameObject voidPlane = CreateBox(root, "Black Void", voidCenter + new Vector3(0f, -depth - 0.08f, 0f), Quaternion.identity,
            new Vector3(voidSize.x, 0.16f, voidSize.z), blackMaterial, copyCollision: false, copyGroundTag: false);
        voidPlane.isStatic = true;

        GetConnectedArea(tileSize, 1f, openPositiveX, openNegativeX, openPositiveZ, openNegativeZ, out Vector3 triggerCenter, out Vector3 triggerSize);
        GameObject trigger = new GameObject("Pit Respawn Trigger");
        trigger.transform.SetParent(root, false);
        trigger.transform.localPosition = triggerCenter + new Vector3(0f, -depth + pitRespawnTriggerHeight * 0.5f, 0f);
        BoxCollider triggerCollider = trigger.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(triggerSize.x, pitRespawnTriggerHeight, triggerSize.z);
        TileVoidRespawnTrigger respawnTrigger = trigger.AddComponent<TileVoidRespawnTrigger>();
        respawnTrigger.SetRespawnDamage(pitRespawnDamage);
    }

    static void GetConnectedArea(
        float fullSize,
        float baseFraction,
        bool openPositiveX,
        bool openNegativeX,
        bool openPositiveZ,
        bool openNegativeZ,
        out Vector3 center,
        out Vector3 size)
    {
        float halfFullSize = fullSize * 0.5f;
        float halfBaseSize = fullSize * Mathf.Clamp01(baseFraction) * 0.5f;
        float minX = openNegativeX ? -halfFullSize : -halfBaseSize;
        float maxX = openPositiveX ? halfFullSize : halfBaseSize;
        float minZ = openNegativeZ ? -halfFullSize : -halfBaseSize;
        float maxZ = openPositiveZ ? halfFullSize : halfBaseSize;

        center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
        size = new Vector3(Mathf.Max(MinSpacing, maxX - minX), 0f, Mathf.Max(MinSpacing, maxZ - minZ));
    }

    void BuildRampFeatures(Transform root, Vector3Int direction)
    {
        if (direction == Vector3Int.zero)
            return;

        float clampedAngle = Mathf.Clamp(rampAngle, 5f, 45f);
        float runLength = tileSize + GeneratedRampSeamOverlap * 2f;
        float heightDifference = tileSize * Mathf.Tan(clampedAngle * Mathf.Deg2Rad);
        Material surfaceMaterial = rampMaterial != null ? rampMaterial : GetFirstMaterial(floor, ceiling);

        GameObject ramp = CreateRampWedge(
            root,
            "Ramp Surface",
            direction,
            Vector3.zero,
            runLength,
            tileSize + GeneratedRampSeamOverlap * 2f,
            GeneratedRampSurfaceY,
            GeneratedRampSurfaceY + heightDifference,
            surfaceMaterial,
            copyCollision: true,
            copyGroundTag: true);
        CopyGroundLayerAndTag(ramp);
    }

    void BuildStairRampFeatures(Transform root, Vector3Int direction)
    {
        if (direction == Vector3Int.zero)
            return;

        int segmentCount = Mathf.Max(1, stairRampSegments);
        float length = tileSize * segmentCount;
        Vector3 localDirection = new Vector3(direction.x, direction.y, direction.z);
        Vector3 centerOffset = localDirection * ((length - tileSize) * 0.5f);
        float runLength = length + GeneratedRampSeamOverlap * 2f;
        float corridorWidth = tileSize + GeneratedRampSeamOverlap * 2f;
        Material surfaceMaterial = rampMaterial != null ? rampMaterial : GetFirstMaterial(floor, ceiling);
        Material ceilingMaterial = surfaceMaterial;
        Material wallMaterial = GetFirstMaterial(positiveXWall, negativeXWall, positiveZWall, negativeZWall, floor);
        float lowSurfaceY = GeneratedRampSurfaceY;
        float highSurfaceY = verticalSpacing + GeneratedRampSurfaceY;
        float lowCeilingY = verticalSpacing - GeneratedRampSurfaceY;
        float highCeilingY = verticalSpacing * 2f - GeneratedRampSurfaceY;

        GameObject ramp = CreateRampWedge(
            root,
            "Stair Ramp Surface",
            direction,
            centerOffset,
            runLength,
            corridorWidth,
            lowSurfaceY,
            highSurfaceY,
            surfaceMaterial,
            copyCollision: true,
            copyGroundTag: true);
        CopyGroundLayerAndTag(ramp);

        CreateSlopedSlab(
            root,
            "Stair Corridor Ceiling",
            direction,
            centerOffset,
            runLength,
            corridorWidth,
            lowCeilingY,
            highCeilingY,
            Mathf.Max(0.1f, rampThickness),
            ceilingMaterial,
            copyCollision: true,
            copyGroundTag: false);

        CreateRepeatedStairSideWalls(root, direction, segmentCount, wallMaterial);
    }

    Transform RecreateGeneratedRoot()
    {
        return RecreateChildRoot(GeneratedRootName);
    }

    void DestroyGeneratedRoot()
    {
        DestroyChildRoot(GeneratedRootName);
    }

    public Transform RecreateGeneratedPropsRoot()
    {
        return RecreateChildRoot(GeneratedPropsRootName);
    }

    public void DestroyGeneratedPropsRoot()
    {
        DestroyChildRoot(GeneratedPropsRootName);
    }

    Transform RecreateChildRoot(string rootName)
    {
        DestroyChildRoot(rootName);

        GameObject rootObject = new GameObject(rootName);
        rootObject.transform.SetParent(transform, false);
        rootObject.transform.localPosition = Vector3.zero;
        rootObject.transform.localRotation = Quaternion.identity;
        rootObject.transform.localScale = Vector3.one;
        return rootObject.transform;
    }

    void DestroyChildRoot(string rootName)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || child.name != rootName)
                continue;

            DestroyGeneratedMeshes(child);
            DestroyTileObject(child.gameObject);
        }
    }

    GameObject CreateBox(
        Transform parent,
        string objectName,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        Material material,
        bool copyCollision,
        bool copyGroundTag)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = objectName;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localRotation = localRotation;
        box.transform.localScale = localScale;

        Renderer renderer = box.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
        ApplyBoxTextureTiling(box, localScale);

        Collider collider = box.GetComponent<Collider>();
        if (!copyCollision && collider != null)
            DestroyTileObject(collider);

        if (copyGroundTag)
            CopyGroundLayerAndTag(box);

        return box;
    }

    GameObject CreateRampWedge(
        Transform parent,
        string objectName,
        Vector3Int direction,
        Vector3 centerOffset,
        float runLength,
        float width,
        float lowY,
        float highY,
        Material material,
        bool copyCollision,
        bool copyGroundTag)
    {
        Vector3 forward = GetHorizontalDirection(direction);
        if (forward == Vector3.zero)
            return null;

        Vector3 lateral = Vector3.Cross(Vector3.up, forward).normalized;
        float halfRun = Mathf.Max(MinSpacing, runLength) * 0.5f;
        float halfWidth = Mathf.Max(MinSpacing, width) * 0.5f;

        Vector3 lowCenter = centerOffset - forward * halfRun;
        Vector3 highCenter = centerOffset + forward * halfRun;
        Vector3 lowLeft = lowCenter - lateral * halfWidth + Vector3.up * lowY;
        Vector3 lowRight = lowCenter + lateral * halfWidth + Vector3.up * lowY;
        Vector3 highBottomLeft = highCenter - lateral * halfWidth + Vector3.up * lowY;
        Vector3 highBottomRight = highCenter + lateral * halfWidth + Vector3.up * lowY;
        Vector3 highTopLeft = highCenter - lateral * halfWidth + Vector3.up * highY;
        Vector3 highTopRight = highCenter + lateral * halfWidth + Vector3.up * highY;

        Mesh mesh = new Mesh
        {
            name = objectName + GeneratedRampMeshSuffix
        };

        List<Vector3> vertices = new List<Vector3>(24);
        List<int> triangles = new List<int>(36);
        List<Vector2> uvs = new List<Vector2>(24);

        AddQuad(vertices, triangles, uvs, lowLeft, highTopLeft, highTopRight, lowRight, Vector3.up);
        AddQuad(vertices, triangles, uvs, highBottomLeft, highBottomRight, highTopRight, highTopLeft, forward);
        AddQuad(vertices, triangles, uvs, lowRight, highBottomRight, highBottomLeft, lowLeft, Vector3.down);
        AddTriangle(vertices, triangles, uvs, lowLeft, highBottomLeft, highTopLeft, -lateral);
        AddTriangle(vertices, triangles, uvs, lowRight, highTopRight, highBottomRight, lateral);

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        return CreateGeneratedMeshObject(parent, objectName, mesh, material, copyCollision, copyGroundTag);
    }

    GameObject CreateSlopedSlab(
        Transform parent,
        string objectName,
        Vector3Int direction,
        Vector3 centerOffset,
        float runLength,
        float width,
        float lowCenterY,
        float highCenterY,
        float thickness,
        Material material,
        bool copyCollision,
        bool copyGroundTag)
    {
        Vector3 forward = GetHorizontalDirection(direction);
        if (forward == Vector3.zero)
            return null;

        Vector3 lateral = Vector3.Cross(Vector3.up, forward).normalized;
        float halfRun = Mathf.Max(MinSpacing, runLength) * 0.5f;
        float halfWidth = Mathf.Max(MinSpacing, width) * 0.5f;
        float halfThickness = Mathf.Max(0.01f, thickness) * 0.5f;

        Vector3 lowCenter = centerOffset - forward * halfRun;
        Vector3 highCenter = centerOffset + forward * halfRun;

        Vector3 lowBottomLeft = lowCenter - lateral * halfWidth + Vector3.up * (lowCenterY - halfThickness);
        Vector3 lowBottomRight = lowCenter + lateral * halfWidth + Vector3.up * (lowCenterY - halfThickness);
        Vector3 lowTopLeft = lowCenter - lateral * halfWidth + Vector3.up * (lowCenterY + halfThickness);
        Vector3 lowTopRight = lowCenter + lateral * halfWidth + Vector3.up * (lowCenterY + halfThickness);
        Vector3 highBottomLeft = highCenter - lateral * halfWidth + Vector3.up * (highCenterY - halfThickness);
        Vector3 highBottomRight = highCenter + lateral * halfWidth + Vector3.up * (highCenterY - halfThickness);
        Vector3 highTopLeft = highCenter - lateral * halfWidth + Vector3.up * (highCenterY + halfThickness);
        Vector3 highTopRight = highCenter + lateral * halfWidth + Vector3.up * (highCenterY + halfThickness);

        Mesh mesh = new Mesh
        {
            name = objectName + GeneratedRampMeshSuffix
        };

        List<Vector3> vertices = new List<Vector3>(32);
        List<int> triangles = new List<int>(48);
        List<Vector2> uvs = new List<Vector2>(32);

        AddQuad(vertices, triangles, uvs, lowTopLeft, highTopLeft, highTopRight, lowTopRight, Vector3.up);
        AddQuad(vertices, triangles, uvs, lowBottomRight, highBottomRight, highBottomLeft, lowBottomLeft, Vector3.down);
        AddQuad(vertices, triangles, uvs, highBottomLeft, highBottomRight, highTopRight, highTopLeft, forward);
        AddQuad(vertices, triangles, uvs, lowBottomRight, lowBottomLeft, lowTopLeft, lowTopRight, -forward);
        AddQuad(vertices, triangles, uvs, lowBottomLeft, highBottomLeft, highTopLeft, lowTopLeft, -lateral);
        AddQuad(vertices, triangles, uvs, lowBottomRight, lowTopRight, highTopRight, highBottomRight, lateral);

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        return CreateGeneratedMeshObject(parent, objectName, mesh, material, copyCollision, copyGroundTag);
    }

    void CreateRepeatedStairSideWalls(Transform parent, Vector3Int direction, int segmentCount, Material fallbackMaterial)
    {
        if (parent == null || direction == Vector3Int.zero)
            return;

        if (Mathf.Abs(direction.x) > 0)
        {
            CreateStairSideWallSet(parent, GetWallForDirection(Vector3Int.forward), direction, segmentCount, "+Z", fallbackMaterial);
            CreateStairSideWallSet(parent, GetWallForDirection(Vector3Int.back), direction, segmentCount, "-Z", fallbackMaterial);
        }
        else
        {
            CreateStairSideWallSet(parent, GetWallForDirection(Vector3Int.right), direction, segmentCount, "+X", fallbackMaterial);
            CreateStairSideWallSet(parent, GetWallForDirection(Vector3Int.left), direction, segmentCount, "-X", fallbackMaterial);
        }
    }

    void CreateStairSideWallSet(
        Transform parent,
        GameObject sourceWall,
        Vector3Int stairDirection,
        int segmentCount,
        string wallLabel,
        Material fallbackMaterial)
    {
        CreateRepeatedWallSegments(parent, sourceWall, stairDirection, segmentCount, wallLabel, fallbackMaterial);
        CreateUpperStairSideWall(parent, sourceWall, wallLabel, fallbackMaterial);
    }

    void CreateRepeatedWallSegments(
        Transform parent,
        GameObject sourceWall,
        Vector3Int stairDirection,
        int segmentCount,
        string wallLabel,
        Material fallbackMaterial)
    {
        if (parent == null || sourceWall == null || stairDirection == Vector3Int.zero)
            return;

        Vector3 stepOffset = GetHorizontalDirection(stairDirection) * tileSize;
        int clampedSegmentCount = Mathf.Max(1, segmentCount);
        for (int segment = 1; segment < clampedSegmentCount; segment++)
        {
            Vector3 segmentPosition = sourceWall.transform.localPosition + stepOffset * segment;
            CreateStairSideWallCopy(parent, sourceWall, "Stair " + wallLabel + " Wall Segment " + (segment + 1), segmentPosition, fallbackMaterial);
            CreateStairSideWallCopy(parent, sourceWall, "Stair " + wallLabel + " Wall Segment " + (segment + 1) + " Upper",
                segmentPosition + Vector3.up * (verticalSpacing - GeneratedRampSurfaceY), fallbackMaterial);
        }
    }

    void CreateUpperStairSideWall(
        Transform parent,
        GameObject sourceWall,
        string wallLabel,
        Material fallbackMaterial)
    {
        if (parent == null || sourceWall == null)
            return;

        CreateStairSideWallCopy(parent, sourceWall, "Stair " + wallLabel + " Upper Wall",
            sourceWall.transform.localPosition + Vector3.up * verticalSpacing, fallbackMaterial);
    }

    static GameObject CreateStairSideWallCopy(
        Transform parent,
        GameObject sourceWall,
        string objectName,
        Vector3 localPosition,
        Material fallbackMaterial)
    {
        GameObject wall = UnityEngine.Object.Instantiate(sourceWall);
        wall.name = objectName;
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = localPosition;
        wall.transform.localRotation = sourceWall.transform.localRotation;
        wall.transform.localScale = sourceWall.transform.localScale;
        wall.SetActive(true);

        if (fallbackMaterial != null)
            ApplyFallbackMaterial(wall, fallbackMaterial);

        return wall;
    }

    GameObject CreateGeneratedMeshObject(
        Transform parent,
        string objectName,
        Mesh mesh,
        Material material,
        bool copyCollision,
        bool copyGroundTag)
    {
        GameObject generated = new GameObject(objectName);
        generated.transform.SetParent(parent, false);
        generated.transform.localPosition = Vector3.zero;
        generated.transform.localRotation = Quaternion.identity;
        generated.transform.localScale = Vector3.one;

        MeshFilter meshFilter = generated.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer renderer = generated.AddComponent<MeshRenderer>();
        if (material != null)
            renderer.sharedMaterial = material;

        if (copyCollision)
        {
            MeshCollider collider = generated.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        if (copyGroundTag)
            CopyGroundLayerAndTag(generated);

        return generated;
    }

    GameObject GetWallForDirection(Vector3Int direction)
    {
        if (direction == Vector3Int.right)
            return positiveXWall;
        if (direction == Vector3Int.left)
            return negativeXWall;
        if (direction == Vector3Int.forward)
            return positiveZWall;
        if (direction == Vector3Int.back)
            return negativeZWall;

        return null;
    }

    static void ApplyFallbackMaterial(GameObject root, Material fallbackMaterial)
    {
        if (root == null || fallbackMaterial == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && renderer.sharedMaterial == null)
                renderer.sharedMaterial = fallbackMaterial;
        }
    }

    static void AddQuad(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector3 outwardNormal,
        float textureWorldUnitsPerUv = GeneratedSurfaceTextureWorldUnitsPerUv)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);

        float unitsPerUv = Mathf.Max(0.01f, textureWorldUnitsPerUv);
        float uLength = Vector3.Distance(a, d) / unitsPerUv;
        float vLength = Vector3.Distance(a, b) / unitsPerUv;
        uvs.Add(new Vector2(0f, 0f));
        uvs.Add(new Vector2(0f, vLength));
        uvs.Add(new Vector2(uLength, vLength));
        uvs.Add(new Vector2(uLength, 0f));

        AddOrientedTriangle(triangles, vertices, start, start + 1, start + 2, outwardNormal);
        AddOrientedTriangle(triangles, vertices, start, start + 2, start + 3, outwardNormal);
    }

    static void AddTriangle(
        List<Vector3> vertices,
        List<int> triangles,
        List<Vector2> uvs,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 outwardNormal)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);

        float unitsPerTile = Mathf.Max(0.01f, GeneratedSurfaceTextureWorldUnitsPerUv);
        uvs.Add(Vector2.zero);
        uvs.Add(new Vector2(Vector3.Distance(a, b) / unitsPerTile, 0f));
        uvs.Add(new Vector2(0f, Vector3.Distance(a, c) / unitsPerTile));

        AddOrientedTriangle(triangles, vertices, start, start + 1, start + 2, outwardNormal);
    }

    static void AddOrientedTriangle(List<int> triangles, List<Vector3> vertices, int a, int b, int c, Vector3 outwardNormal)
    {
        Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
        if (Vector3.Dot(normal, outwardNormal) < 0f)
        {
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(b);
        }
        else
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }
    }

    static void ApplyBoxTextureTiling(GameObject box, Vector3 localScale)
    {
        MeshFilter meshFilter = box != null ? box.GetComponent<MeshFilter>() : null;
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return;

        Mesh mesh = Instantiate(meshFilter.sharedMesh);
        mesh.name = box.name + " Tiled Mesh";

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        if (vertices == null || normals == null || vertices.Length == 0 || normals.Length != vertices.Length)
        {
            DestroyTileObject(mesh);
            return;
        }

        Vector3 scaledSize = new Vector3(Mathf.Abs(localScale.x), Mathf.Abs(localScale.y), Mathf.Abs(localScale.z));
        float unitsPerTile = Mathf.Max(0.01f, GeneratedBoxTextureWorldUnitsPerUv);
        Vector2[] uvs = new Vector2[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 scaledVertex = Vector3.Scale(vertices[i], scaledSize);
            Vector3 normal = normals[i];
            Vector3 absNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));

            if (absNormal.y >= absNormal.x && absNormal.y >= absNormal.z)
                uvs[i] = new Vector2(scaledVertex.x, scaledVertex.z) / unitsPerTile;
            else if (absNormal.x >= absNormal.z)
                uvs[i] = new Vector2(scaledVertex.z, scaledVertex.y) / unitsPerTile;
            else
                uvs[i] = new Vector2(scaledVertex.x, scaledVertex.y) / unitsPerTile;
        }

        mesh.uv = uvs;
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
    }

    static void DestroyGeneratedMeshes(Transform root)
    {
        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        HashSet<Mesh> meshesToDestroy = new HashSet<Mesh>();
        for (int i = 0; i < meshFilters.Length; i++)
        {
            Mesh mesh = meshFilters[i] != null ? meshFilters[i].sharedMesh : null;
            if (IsGeneratedMesh(mesh))
                meshesToDestroy.Add(mesh);
        }

        foreach (Mesh mesh in meshesToDestroy)
            DestroyTileObject(mesh);
    }

    static bool IsGeneratedMesh(Mesh mesh)
    {
        return mesh != null
            && (mesh.name.EndsWith(" Tiled Mesh", StringComparison.Ordinal)
                || mesh.name.EndsWith(GeneratedRampMeshSuffix, StringComparison.Ordinal));
    }

    void CopyGroundLayerAndTag(GameObject target)
    {
        if (target == null || floor == null)
            return;

        target.layer = floor.layer;
        try
        {
            target.tag = floor.tag;
        }
        catch (UnityException)
        {
            target.tag = "Untagged";
        }
    }

    GameObject FindChildObject(params string[] names)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < names.Length; i++)
        {
            for (int j = 0; j < children.Length; j++)
            {
                Transform child = children[j];
                if (child == transform || IsGeneratedChild(child))
                    continue;

                if (string.Equals(child.name, names[i], StringComparison.OrdinalIgnoreCase))
                    return child.gameObject;
            }
        }

        for (int i = 0; i < names.Length; i++)
        {
            for (int j = 0; j < children.Length; j++)
            {
                Transform child = children[j];
                if (child == transform || IsGeneratedChild(child))
                    continue;

                if (child.name.IndexOf(names[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return child.gameObject;
            }
        }

        return null;
    }

    void AssignMissingWallsByPosition()
    {
        Transform bestPositiveX = null;
        Transform bestNegativeX = null;
        Transform bestPositiveZ = null;
        Transform bestNegativeZ = null;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (IsGeneratedRootName(child.name) || child.name.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            Vector3 localPosition = child.localPosition;
            if (Mathf.Abs(localPosition.x) >= Mathf.Abs(localPosition.z))
            {
                if (localPosition.x >= 0f && (bestPositiveX == null || localPosition.x > bestPositiveX.localPosition.x))
                    bestPositiveX = child;
                else if (localPosition.x < 0f && (bestNegativeX == null || localPosition.x < bestNegativeX.localPosition.x))
                    bestNegativeX = child;
            }
            else
            {
                if (localPosition.z >= 0f && (bestPositiveZ == null || localPosition.z > bestPositiveZ.localPosition.z))
                    bestPositiveZ = child;
                else if (localPosition.z < 0f && (bestNegativeZ == null || localPosition.z < bestNegativeZ.localPosition.z))
                    bestNegativeZ = child;
            }
        }

        if (positiveXWall == null && bestPositiveX != null)
            positiveXWall = bestPositiveX.gameObject;
        if (negativeXWall == null && bestNegativeX != null)
            negativeXWall = bestNegativeX.gameObject;
        if (positiveZWall == null && bestPositiveZ != null)
            positiveZWall = bestPositiveZ.gameObject;
        if (negativeZWall == null && bestNegativeZ != null)
            negativeZWall = bestNegativeZ.gameObject;
    }

    static Vector3Int GetRampDirection(TileGridCellKind kind)
    {
        switch (kind)
        {
            case TileGridCellKind.RampPositiveX:
                return Vector3Int.right;
            case TileGridCellKind.RampNegativeX:
                return Vector3Int.left;
            case TileGridCellKind.RampPositiveZ:
                return Vector3Int.forward;
            case TileGridCellKind.RampNegativeZ:
                return Vector3Int.back;
            default:
                return Vector3Int.zero;
        }
    }

    public static Vector3Int GetStairRampDirection(TileGridCellKind kind)
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

    static Quaternion GetRampRotation(Vector3Int direction, float angle)
    {
        if (direction == Vector3Int.right)
            return Quaternion.Euler(0f, 0f, angle);
        if (direction == Vector3Int.left)
            return Quaternion.Euler(0f, 0f, -angle);
        if (direction == Vector3Int.forward)
            return Quaternion.Euler(-angle, 0f, 0f);
        if (direction == Vector3Int.back)
            return Quaternion.Euler(angle, 0f, 0f);

        return Quaternion.identity;
    }

    static Vector3 GetHorizontalDirection(Vector3Int direction)
    {
        Vector3 result = new Vector3(direction.x, 0f, direction.z);
        return result.sqrMagnitude > 0.0001f ? result.normalized : Vector3.zero;
    }

    bool IsGeneratedChild(Transform child)
    {
        if (child == null || IsGeneratedRootName(child.name))
            return true;

        Transform parent = child.parent;
        while (parent != null && parent != transform)
        {
            if (IsGeneratedRootName(parent.name))
                return true;

            parent = parent.parent;
        }

        return false;
    }

    static bool IsGeneratedRootName(string objectName)
    {
        return objectName == GeneratedRootName || objectName == GeneratedPropsRootName;
    }

    static Material GetFirstMaterial(params GameObject[] sources)
    {
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null)
                continue;

            Renderer renderer = sources[i].GetComponentInChildren<Renderer>(true);
            if (renderer != null && renderer.sharedMaterial != null)
                return renderer.sharedMaterial;
        }

        return null;
    }

    static Material CreateRuntimeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            color = color
        };
        material.name = "Runtime Tile Material";
        return material;
    }

    static void SetObjectActive(GameObject target, bool active)
    {
        if (target == null || target.activeSelf == active)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            Undo.RecordObject(target, "Refresh Tile Connections");
#endif

        target.SetActive(active);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
#endif
    }

    static void DestroyTileObject(UnityEngine.Object target)
    {
        if (target == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEngine.Object.DestroyImmediate(target);
            return;
        }
#endif

        UnityEngine.Object.Destroy(target);
    }
}
