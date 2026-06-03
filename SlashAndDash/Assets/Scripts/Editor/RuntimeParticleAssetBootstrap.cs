using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RuntimeParticleAssetBootstrap
{
    const string ParticleFolder = "Assets/Particles";
    const string ParticleMaterialPath = ParticleFolder + "/ParticleBillboard.mat";
    const string DrivingDustPrefabPath = ParticleFolder + "/DrivingDustParticles.prefab";
    const string BoostDustPrefabPath = ParticleFolder + "/BoostDustParticles.prefab";
    const string WheelDustPrefabPath = ParticleFolder + "/WheelDustParticles.prefab";
    const string DriftSparklePrefabPath = ParticleFolder + "/DriftSparkleParticles.prefab";

    static RuntimeParticleAssetBootstrap()
    {
        EditorApplication.delayCall += EnsureParticleAssets;
    }

    static void EnsureParticleAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnsureFolder();
        Material material = EnsureParticleMaterial();

        EnsureParticlePrefab(
            DrivingDustPrefabPath,
            () => RuntimeParticleFactory.CreateDrivingDust(null, "DrivingDustParticles", new Color(0.78f, 0.73f, 0.64f, 0.62f)),
            material);
        EnsureParticlePrefab(
            BoostDustPrefabPath,
            () => RuntimeParticleFactory.CreateBoostDust(null, "BoostDustParticles", new Color(1f, 0.83f, 0.3f, 0.72f)),
            material);
        EnsureParticlePrefab(
            WheelDustPrefabPath,
            () => RuntimeParticleFactory.CreateWheelDust(null, "WheelDustParticles", new Color(0.78f, 0.73f, 0.64f, 0.62f)),
            material);
        EnsureParticlePrefab(
            DriftSparklePrefabPath,
            () => RuntimeParticleFactory.CreateDriftSparkles(null, "DriftSparkleParticles"),
            material);

        AssetDatabase.SaveAssets();
    }

    static void EnsureFolder()
    {
        if (AssetDatabase.IsValidFolder(ParticleFolder))
            return;

        string absolutePath = Path.Combine(Application.dataPath, "Particles");
        Directory.CreateDirectory(absolutePath);
        AssetDatabase.Refresh();
    }

    static Material EnsureParticleMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        material = new Material(shader)
        {
            name = "ParticleBillboard"
        };

        AssetDatabase.CreateAsset(material, ParticleMaterialPath);
        return material;
    }

    static void EnsureParticlePrefab(string prefabPath, System.Func<ParticleSystem> createParticleSystem, Material material)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            return;

        ParticleSystem particles = createParticleSystem();
        if (particles == null)
            return;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        PrefabUtility.SaveAsPrefabAsset(particles.gameObject, prefabPath);
        Object.DestroyImmediate(particles.gameObject);
    }
}
