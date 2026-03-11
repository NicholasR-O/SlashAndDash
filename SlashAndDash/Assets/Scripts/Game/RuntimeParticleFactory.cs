using UnityEngine;

public static class RuntimeParticleFactory
{
    static Material sharedParticleMaterial;
    static Material sharedExplosionSphereMaterial;
    static Mesh sharedSphereMesh;

    public static ParticleSystem CreateDrivingDust(Transform parent, string name, Color color)
    {
        ParticleSystem particles = CreateEmitter(parent, name);
        ConfigureDustLikeParticles(
            particles,
            color,
            lifetimeMin: 0.45f,
            lifetimeMax: 0.72f,
            sizeMin: 0.18f,
            sizeMax: 0.34f,
            speedMin: 0.4f,
            speedMax: 1.15f,
            maxParticles: 420);
        return particles;
    }

    public static ParticleSystem CreateBoostDust(Transform parent, string name, Color color)
    {
        ParticleSystem particles = CreateEmitter(parent, name);
        ConfigureDustLikeParticles(
            particles,
            color,
            lifetimeMin: 0.42f,
            lifetimeMax: 0.68f,
            sizeMin: 0.2f,
            sizeMax: 0.38f,
            speedMin: 0.6f,
            speedMax: 1.45f,
            maxParticles: 500);
        return particles;
    }

    public static ParticleSystem CreateDriftSparkles(Transform parent, string name)
    {
        ParticleSystem particles = CreateEmitter(parent, name);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.36f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.55f, 1.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.96f, 0.72f, 0.95f));
        main.gravityModifier = 0.03f;
        main.maxParticles = 280;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.42f;
        shape.rotation = new Vector3(0f, 180f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
        velocity.z = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(new Color(1f, 1f, 1f), 0f), new GradientColorKey(new Color(1f, 0.9f, 0.45f), 0.45f), new GradientColorKey(new Color(1f, 0.85f, 0.25f), 1f) },
            new[] { new GradientAlphaKey(0.92f, 0f), new GradientAlphaKey(0.8f, 0.35f), new GradientAlphaKey(0f, 1f) }));

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1.1f),
            new Keyframe(0.65f, 0.9f),
            new Keyframe(1f, 0.25f)));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.28f;
        noise.frequency = 0.95f;
        noise.scrollSpeed = 0.25f;

        return particles;
    }

    public static ParticleSystem CreateEnemyThrownTrail(Transform parent, string name)
    {
        ParticleSystem particles = CreateEmitter(parent, name);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.46f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.55f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f));
        main.maxParticles = 340;
        main.gravityModifier = 0.02f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.14f;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.03f, 0.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.35f, 0.4f), new GradientAlphaKey(0f, 1f) }));

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0.3f)));

        return particles;
    }

    public static void SpawnEnemyExplosionPulse(Vector3 position, float radius)
    {
        GameObject effectObject = new GameObject("EnemyExplosionPulse");
        effectObject.transform.position = position + Vector3.up * 0.08f;

        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 1.05f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.95f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.startColor = new Color(1f, 0.5f, 0.1f, 0.5f);
        main.maxParticles = 1;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.01f;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        float targetSize = Mathf.Max(1.6f, radius * 2.2f);
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.18f),
            new Keyframe(0.55f, targetSize * 0.62f),
            new Keyframe(1f, targetSize)));

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(new Color(1f, 0.58f, 0.15f), 0f), new GradientColorKey(new Color(1f, 0.34f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0.46f, 0.55f), new GradientAlphaKey(0f, 1f) }));

        AssignMaterialIfAvailable(particles);
        ConfigureExplosionSphereRenderer(particles);
        particles.Play();
        Object.Destroy(effectObject, 1.8f);
    }

    static void ConfigureDustLikeParticles(
        ParticleSystem particles,
        Color baseColor,
        float lifetimeMin,
        float lifetimeMax,
        float sizeMin,
        float sizeMax,
        float speedMin,
        float speedMax,
        int maxParticles)
    {
        ParticleSystem.MainModule main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startColor = new ParticleSystem.MinMaxGradient(baseColor);
        main.gravityModifier = 0.16f;
        main.maxParticles = maxParticles;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 13f;
        shape.radius = 0.34f;
        shape.rotation = new Vector3(0f, 180f, 0f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.56f);
        velocity.z = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(baseColor.a, 0f), new GradientAlphaKey(baseColor.a * 0.45f, 0.55f), new GradientAlphaKey(0f, 1f) }));

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.85f),
            new Keyframe(0.7f, 1.2f),
            new Keyframe(1f, 1.45f)));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 0.85f;
        noise.scrollSpeed = 0.3f;
    }

    static ParticleSystem CreateEmitter(Transform parent, string name)
    {
        GameObject emitterObject = new GameObject(name);
        emitterObject.transform.SetParent(parent, false);

        ParticleSystem particles = emitterObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        AssignMaterialIfAvailable(particles);
        return particles;
    }

    static void AssignMaterialIfAvailable(ParticleSystem particles)
    {
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            return;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        Material material = GetSharedParticleMaterial();
        if (material != null)
            renderer.sharedMaterial = material;
    }

    static void ConfigureExplosionSphereRenderer(ParticleSystem particles)
    {
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        if (renderer == null)
            return;

        Mesh sphereMesh = GetSharedSphereMesh();
        if (sphereMesh == null)
            return;

        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = sphereMesh;
        renderer.alignment = ParticleSystemRenderSpace.World;

        Material explosionMaterial = GetExplosionSphereMaterial();
        if (explosionMaterial != null)
            renderer.sharedMaterial = explosionMaterial;
    }

    static Material GetSharedParticleMaterial()
    {
        if (sharedParticleMaterial != null)
            return sharedParticleMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        sharedParticleMaterial = new Material(shader);
        sharedParticleMaterial.name = "RuntimeParticleMaterial";
        return sharedParticleMaterial;
    }

    static Mesh GetSharedSphereMesh()
    {
        if (sharedSphereMesh != null)
            return sharedSphereMesh;

        GameObject tempSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        MeshFilter filter = tempSphere.GetComponent<MeshFilter>();
        if (filter != null)
            sharedSphereMesh = filter.sharedMesh;

        if (Application.isPlaying)
            Object.Destroy(tempSphere);
        else
            Object.DestroyImmediate(tempSphere);

        return sharedSphereMesh;
    }

    static Material GetExplosionSphereMaterial()
    {
        if (sharedExplosionSphereMaterial != null)
            return sharedExplosionSphereMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return GetSharedParticleMaterial();

        sharedExplosionSphereMaterial = new Material(shader);
        sharedExplosionSphereMaterial.name = "RuntimeExplosionSphereMaterial";
        ForceTransparentAlphaBlend(sharedExplosionSphereMaterial);
        return sharedExplosionSphereMaterial;
    }

    static void ForceTransparentAlphaBlend(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SurfaceType"))
            material.SetFloat("_SurfaceType", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    static Gradient CreateGradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(colors, alphas);
        return gradient;
    }
}
