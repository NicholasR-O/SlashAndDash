using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class TerrainDetailBrushSetup
{
    const string DetailFolder = "Assets/Prefab/Details";

    static readonly DetailSource[] DetailSources =
    {
        new DetailSource("Assets/Prefab/Props/Grass_1.prefab", "Grass_1_Detail", 0.8f, 1.35f, 0.8f, 1.35f, 6f),
        new DetailSource("Assets/Prefab/Props/Rock_1.prefab", "Rock_1_Detail", 0.75f, 1.2f, 0.75f, 1.2f, 9f),
        new DetailSource("Assets/Prefab/Props/Rock_2.prefab", "Rock_2_Detail", 0.75f, 1.2f, 0.75f, 1.2f, 9f),
        new DetailSource("Assets/Prefab/Props/Rock_3.prefab", "Rock_3_Detail", 0.75f, 1.2f, 0.75f, 1.2f, 9f),
        new DetailSource("Assets/Prefab/Props/Rock_4.prefab", "Rock_4_Detail", 0.75f, 1.2f, 0.75f, 1.2f, 9f),
        new DetailSource("Assets/Prefab/Props/Rock_5.prefab", "Rock_5_Detail", 0.75f, 1.2f, 0.75f, 1.2f, 9f),
        new DetailSource("Assets/Prefab/Props/Rock_6.prefab", "Rock_6_Detail", 0.75f, 1.2f, 0.75f, 1.2f, 9f),
    };

    [MenuItem("Tools/Slash And Dash/Setup Terrain Detail Brushes")]
    public static void SetupTerrainDetailBrushes()
    {
        EnsureFolder(DetailFolder);

        List<DetailSource> createdDetails = new List<DetailSource>();
        foreach (DetailSource detailSource in DetailSources)
        {
            GameObject detailPrefab = CreateOrUpdateDetailPrefab(detailSource);
            if (detailPrefab != null)
                createdDetails.Add(detailSource.WithPrefab(detailPrefab));
        }

        if (createdDetails.Count == 0)
        {
            Debug.LogWarning("[TerrainDetailBrushSetup] No detail prefabs were created.");
            return;
        }

        int terrainDataCount = AddDetailsToTerrainData(createdDetails);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[TerrainDetailBrushSetup] Created or updated " + createdDetails.Count
            + " detail prefabs and registered them on " + terrainDataCount + " TerrainData asset(s).");
    }

    static GameObject CreateOrUpdateDetailPrefab(DetailSource detailSource)
    {
        GameObject sourceRoot = PrefabUtility.LoadPrefabContents(detailSource.SourcePath);
        if (sourceRoot == null)
        {
            Debug.LogWarning("[TerrainDetailBrushSetup] Could not load " + detailSource.SourcePath);
            return null;
        }

        try
        {
            MeshFilter sourceFilter = sourceRoot.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(filter => filter.sharedMesh != null);
            MeshRenderer sourceRenderer = sourceFilter != null
                ? sourceFilter.GetComponent<MeshRenderer>()
                : sourceRoot.GetComponentInChildren<MeshRenderer>(true);

            if (sourceFilter == null || sourceRenderer == null)
            {
                Debug.LogWarning("[TerrainDetailBrushSetup] " + detailSource.SourcePath
                    + " does not contain a MeshFilter and MeshRenderer.");
                return null;
            }

            GameObject detailRoot = new GameObject(detailSource.DetailName);
            MeshFilter detailFilter = detailRoot.AddComponent<MeshFilter>();
            MeshRenderer detailRenderer = detailRoot.AddComponent<MeshRenderer>();

            detailFilter.sharedMesh = sourceFilter.sharedMesh;
            detailRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
            detailRenderer.shadowCastingMode = ShadowCastingMode.Off;
            detailRenderer.receiveShadows = sourceRenderer.receiveShadows;
            detailRenderer.lightProbeUsage = LightProbeUsage.Off;
            detailRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            string detailPath = DetailFolder + "/" + detailSource.DetailName + ".prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(detailRoot, detailPath);
            Object.DestroyImmediate(detailRoot);
            return savedPrefab;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(sourceRoot);
        }
    }

    static int AddDetailsToTerrainData(List<DetailSource> detailSources)
    {
        string[] terrainGuids = AssetDatabase.FindAssets("t:TerrainData", new[] { "Assets" });
        int changedCount = 0;

        foreach (string terrainGuid in terrainGuids)
        {
            string terrainPath = AssetDatabase.GUIDToAssetPath(terrainGuid);
            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);
            if (terrainData == null)
                continue;

            List<DetailPrototype> prototypes = terrainData.detailPrototypes.ToList();
            bool changed = false;

            foreach (DetailSource detailSource in detailSources)
            {
                if (prototypes.Any(prototype => prototype.prototype == detailSource.DetailPrefab
                    || (prototype.prototype != null && prototype.prototype.name == detailSource.DetailPrefab.name)))
                {
                    continue;
                }

                prototypes.Add(CreateDetailPrototype(detailSource));
                changed = true;
            }

            if (!changed)
                continue;

            terrainData.detailPrototypes = prototypes.ToArray();
            terrainData.RefreshPrototypes();
            EditorUtility.SetDirty(terrainData);
            changedCount++;
        }

        return changedCount;
    }

    static DetailPrototype CreateDetailPrototype(DetailSource detailSource)
    {
        return new DetailPrototype
        {
            prototype = detailSource.DetailPrefab,
            usePrototypeMesh = true,
            useInstancing = true,
            renderMode = DetailRenderMode.VertexLit,
            minWidth = detailSource.MinWidth,
            maxWidth = detailSource.MaxWidth,
            minHeight = detailSource.MinHeight,
            maxHeight = detailSource.MaxHeight,
            noiseSpread = detailSource.NoiseSpread,
            healthyColor = Color.white,
            dryColor = Color.white,
        };
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        string name = Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    readonly struct DetailSource
    {
        public readonly string SourcePath;
        public readonly string DetailName;
        public readonly float MinWidth;
        public readonly float MaxWidth;
        public readonly float MinHeight;
        public readonly float MaxHeight;
        public readonly float NoiseSpread;
        public readonly GameObject DetailPrefab;

        public DetailSource(
            string sourcePath,
            string detailName,
            float minWidth,
            float maxWidth,
            float minHeight,
            float maxHeight,
            float noiseSpread)
            : this(sourcePath, detailName, minWidth, maxWidth, minHeight, maxHeight, noiseSpread, null)
        {
        }

        DetailSource(
            string sourcePath,
            string detailName,
            float minWidth,
            float maxWidth,
            float minHeight,
            float maxHeight,
            float noiseSpread,
            GameObject detailPrefab)
        {
            SourcePath = sourcePath;
            DetailName = detailName;
            MinWidth = minWidth;
            MaxWidth = maxWidth;
            MinHeight = minHeight;
            MaxHeight = maxHeight;
            NoiseSpread = noiseSpread;
            DetailPrefab = detailPrefab;
        }

        public DetailSource WithPrefab(GameObject detailPrefab)
        {
            return new DetailSource(
                SourcePath,
                DetailName,
                MinWidth,
                MaxWidth,
                MinHeight,
                MaxHeight,
                NoiseSpread,
                detailPrefab);
        }
    }
}
