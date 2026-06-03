using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CastlePropPrefabBuilder
{
    public const string SourcePropFolder = "Assets/Models/CastleProps";
    public const string GeneratedPropPrefabFolder = "Assets/Prefab/Castle/TileTool/Props";

    [InitializeOnLoadMethod]
    static void QueuePrefabCheck()
    {
        EditorApplication.delayCall += EnsurePrefabsExist;
    }

    [MenuItem("Tools/Slash And Dash/Create Missing Castle Prop Prefabs")]
    public static void EnsurePrefabsExist()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsurePrefabsExist;
            return;
        }

        EnsureFolders();

        bool createdAny = false;
        IReadOnlyList<TilePropGenerator.PropDefinition> definitions = TilePropGenerator.AllPropDefinitions;
        for (int i = 0; i < definitions.Count; i++)
            createdAny |= EnsurePrefabExists(definitions[i], saveAssets: false);

        if (createdAny)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/Slash And Dash/Rebuild Castle Prop Prefabs")]
    public static void RebuildPrefabs()
    {
        bool rebuild = EditorUtility.DisplayDialog(
            "Rebuild Castle Prop Prefabs",
            "This will replace the generated castle prop prefabs and overwrite any collider edits on those prefabs.",
            "Rebuild",
            "Cancel");
        if (!rebuild)
            return;

        EnsureFolders();

        IReadOnlyList<TilePropGenerator.PropDefinition> definitions = TilePropGenerator.AllPropDefinitions;
        for (int i = 0; i < definitions.Count; i++)
            CreateOrReplacePrefab(definitions[i]);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static string GetPrefabPath(string assetName)
    {
        return GeneratedPropPrefabFolder + "/" + assetName + ".prefab";
    }

    public static bool EnsurePrefabExists(string assetName)
    {
        if (!TilePropGenerator.TryGetPropDefinition(assetName, out TilePropGenerator.PropDefinition definition))
            return false;

        bool created = EnsurePrefabExists(definition, saveAssets: true);
        return AssetDatabase.LoadAssetAtPath<GameObject>(GetPrefabPath(assetName)) != null || created;
    }

    static bool EnsurePrefabExists(TilePropGenerator.PropDefinition definition, bool saveAssets)
    {
        if (definition == null)
            return false;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(GetPrefabPath(definition.assetName)) != null)
            return false;

        EnsureFolders();
        bool created = CreateOrReplacePrefab(definition);
        if (created && saveAssets)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        return created;
    }

    static bool CreateOrReplacePrefab(TilePropGenerator.PropDefinition definition)
    {
        string sourcePath = SourcePropFolder + "/" + definition.assetName + ".obj";
        GameObject sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (sourceModel == null)
        {
            Debug.LogWarning("[CastlePropPrefabBuilder] Could not find castle prop model at " + sourcePath + ".");
            return false;
        }

        GameObject root = new GameObject(definition.assetName);
        GameObject model = PrefabUtility.InstantiatePrefab(sourceModel) as GameObject;
        if (model == null)
            model = Object.Instantiate(sourceModel);

        model.name = definition.assetName + "_Model";
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        if (!TryGetRendererBounds(model, out Bounds sourceBounds) || sourceBounds.size.y <= 0.001f)
        {
            Object.DestroyImmediate(root);
            Debug.LogWarning("[CastlePropPrefabBuilder] Could not read renderer bounds for " + definition.assetName + ".");
            return false;
        }

        float scale = Mathf.Clamp(definition.targetHeight / sourceBounds.size.y, 0.02f, 80f);
        model.transform.localScale = Vector3.one * scale;

        if (!TryGetRendererBounds(root, out Bounds scaledBounds))
        {
            Object.DestroyImmediate(root);
            return false;
        }

        Vector3 offset = definition.placement == TilePropGenerator.PropPlacement.Floor
            ? new Vector3(-scaledBounds.center.x, -scaledBounds.min.y, -scaledBounds.center.z)
            : -scaledBounds.center;
        model.transform.position += offset;

        RemoveChildColliders(root);
        FitBoxColliderToRenderers(root);

        PrefabUtility.SaveAsPrefabAsset(root, GetPrefabPath(definition.assetName));
        Object.DestroyImmediate(root);
        return true;
    }

    static void EnsureFolders()
    {
        EnsureFolder("Assets/Prefab");
        EnsureFolder("Assets/Prefab/Castle");
        EnsureFolder("Assets/Prefab/Castle/TileTool");
        EnsureFolder(GeneratedPropPrefabFolder);
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

    static void RemoveChildColliders(GameObject wrapper)
    {
        Collider[] colliders = wrapper.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].gameObject != wrapper)
                Object.DestroyImmediate(colliders[i]);
        }
    }

    static void FitBoxColliderToRenderers(GameObject wrapper)
    {
        if (!TryGetLocalRendererBounds(wrapper.transform, out Bounds localBounds))
            return;

        BoxCollider collider = wrapper.AddComponent<BoxCollider>();
        collider.center = localBounds.center;
        collider.size = localBounds.size;
    }

    static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = new Bounds();
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
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
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            EncapsulateLocalPoint(root, min, ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(min.x, min.y, max.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(min.x, max.y, min.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(min.x, max.y, max.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(max.x, min.y, min.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(max.x, min.y, max.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(root, new Vector3(max.x, max.y, min.z), ref localBounds, ref hasBounds);
            EncapsulateLocalPoint(root, max, ref localBounds, ref hasBounds);
        }

        return hasBounds;
    }

    static void EncapsulateLocalPoint(Transform root, Vector3 worldPoint, ref Bounds bounds, ref bool hasBounds)
    {
        Vector3 localPoint = root.InverseTransformPoint(worldPoint);
        if (!hasBounds)
        {
            bounds = new Bounds(localPoint, Vector3.zero);
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(localPoint);
    }
}
