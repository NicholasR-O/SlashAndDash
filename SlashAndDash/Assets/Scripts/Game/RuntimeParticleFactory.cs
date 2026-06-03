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

    public static ParticleSystem CreateWheelDust(Transform parent, string name, Color color)
    {
        ParticleSystem particles = CreateEmitter(parent, name);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.42f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(color);
        main.gravityModifier = 0.62f;
        main.maxParticles = 900;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.12f;
        shape.rotation = Vector3.zero;

        ParticleSystem.InheritVelocityModule inheritVelocity = particles.inheritVelocity;
        inheritVelocity.enabled = true;
        inheritVelocity.mode = ParticleSystemInheritVelocityMode.Initial;
        inheritVelocity.curve = new ParticleSystem.MinMaxCurve(0.08f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        velocity.z = new ParticleSystem.MinMaxCurve(1.7f, 3.6f);
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);

        ParticleSystem.ColorOverLifetimeModule particleColor = particles.colorOverLifetime;
        particleColor.enabled = true;
        particleColor.color = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a * 0.65f, 0.45f), new GradientAlphaKey(0f, 1f) }));

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.55f),
            new Keyframe(0.55f, 1.25f),
            new Keyframe(1f, 1.6f)));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.16f;
        noise.frequency = 1.15f;
        noise.scrollSpeed = 0.35f;

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
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);

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
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);

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

    public static void SpawnCollisionSparks(Vector3 position, Vector3 normal, float impactSpeed, Texture texture = null)
    {
        Vector3 sparkNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        GameObject effectObject = new GameObject("CollisionSparks");
        effectObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(sparkNormal, Vector3.up));

        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float impactRatio = Mathf.Clamp01(impactSpeed / 24f);
        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.34f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, Mathf.Lerp(0.34f, 0.58f, impactRatio));
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, Mathf.Lerp(0.42f, 0.72f, impactRatio));
        main.startSpeed = new ParticleSystem.MinMaxCurve(Mathf.Lerp(6.5f, 9.5f, impactRatio), Mathf.Lerp(12f, 19f, impactRatio));
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.24f, 1f), new Color(1f, 1f, 0.82f, 1f));
        main.gravityModifier = 0.75f;
        main.maxParticles = 240;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(Mathf.Lerp(30f, 76f, impactRatio))) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 42f;
        shape.radius = 0.14f;
        shape.rotation = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-4.2f, 4.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 1.55f);
        velocity.z = new ParticleSystem.MinMaxCurve(0.8f, 3.3f);
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.55f, 0.08f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.92f, 0.28f), new GradientAlphaKey(0f, 1f) }));

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1.15f),
            new Keyframe(0.35f, 1f),
            new Keyframe(0.78f, 0.52f),
            new Keyframe(1f, 0.08f)));

        ParticleSystem.TrailModule trails = particles.trails;
        trails.enabled = true;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.16f);
        trails.ratio = 0.78f;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1.15f),
            new Keyframe(0.35f, 0.82f),
            new Keyframe(1f, 0f)));
        trails.colorOverTrail = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.55f, 0.08f), 1f) },
            new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }));

        AssignMaterialIfAvailable(particles);
        Material textureMaterial = null;
        if (texture != null)
        {
            textureMaterial = CreateParticleTextureMaterial("Collision Spark Texture Material", texture, additive: true);
            AssignMaterialIfAvailable(particles, textureMaterial);
        }

        particles.Play();
        float lifetime = main.duration + main.startLifetime.constantMax + trails.lifetime.constantMax + 0.2f;
        Object.Destroy(effectObject, lifetime);
        if (textureMaterial != null)
            Object.Destroy(textureMaterial, lifetime);
    }

    public static void SpawnDashBurst(Vector3 position, Vector3 dashDirection, Color color, Texture texture = null)
    {
        Vector3 direction = dashDirection.sqrMagnitude > 0.0001f ? dashDirection.normalized : Vector3.forward;
        Vector3 trailDirection = -direction;
        GameObject effectObject = new GameObject("DashBurstParticles");
        effectObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(trailDirection, Vector3.up));

        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        Color startColor = color;
        if (startColor.a <= 0f)
            startColor.a = 0.85f;

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.18f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.52f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4.8f, 9.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(startColor, Color.white);
        main.gravityModifier = 0f;
        main.maxParticles = 120;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)34) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.7f;
        shape.rotation = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.12f, 0.28f);
        velocity.z = new ParticleSystem.MinMaxCurve(1.8f, 4.5f);
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);

        ParticleSystem.ColorOverLifetimeModule particleColor = particles.colorOverLifetime;
        particleColor.enabled = true;
        particleColor.color = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(startColor.r, startColor.g, startColor.b), 1f) },
            new[] { new GradientAlphaKey(startColor.a, 0f), new GradientAlphaKey(startColor.a * 0.6f, 0.42f), new GradientAlphaKey(0f, 1f) }));

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1.15f),
            new Keyframe(0.45f, 0.82f),
            new Keyframe(1f, 0.08f)));

        ParticleSystem.TrailModule trails = particles.trails;
        trails.enabled = true;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.16f);
        trails.ratio = 0.62f;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.75f),
            new Keyframe(1f, 0f)));
        trails.colorOverTrail = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(new Color(startColor.r, startColor.g, startColor.b), 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(startColor.a * 0.75f, 0f), new GradientAlphaKey(0f, 1f) }));

        AssignMaterialIfAvailable(particles);
        Material textureMaterial = null;
        if (texture != null)
        {
            textureMaterial = CreateParticleTextureMaterial("Dash Burst Texture Material", texture);
            AssignMaterialIfAvailable(particles, textureMaterial);
        }

        particles.Play();
        float lifetime = main.duration + main.startLifetime.constantMax + trails.lifetime.constantMax + 0.25f;
        Object.Destroy(effectObject, lifetime);
        if (textureMaterial != null)
            Object.Destroy(textureMaterial, lifetime);
    }

    public static void SpawnEnemyExplosionPulse(Vector3 position, float radius)
    {
        float safeRadius = Mathf.Max(1f, radius);
        GameObject effectObject = new GameObject("EnemyExplosion");
        effectObject.transform.position = position + Vector3.up * 0.08f;

        SpawnEnemyExplosionFlash(effectObject.transform, safeRadius);
        SpawnEnemyExplosionShockwave(effectObject.transform, safeRadius);
        SpawnEnemyExplosionSparks(effectObject.transform, safeRadius);
        SpawnEnemyExplosionDebris(effectObject.transform, safeRadius);
        SpawnEnemyExplosionSmoke(effectObject.transform, safeRadius);
        SpawnEnemyExplosionLight(effectObject.transform, safeRadius);

        Object.Destroy(effectObject, 2.6f);
    }

    static void SpawnEnemyExplosionFlash(Transform parent, float radius)
    {
        ParticleSystem particles = CreateChildEmitter(parent, "ExplosionCoreFlash", Vector3.zero, Quaternion.identity);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.18f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 0.16f;
        main.startSpeed = 0f;
        main.startSize = Mathf.Max(1.2f, radius * 0.58f);
        main.startColor = new Color(1f, 0.88f, 0.38f, 0.92f);
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
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.15f),
            new Keyframe(0.22f, 1.18f),
            new Keyframe(1f, 0.2f)));

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.34f, 0.04f), 1f) },
            new[] { new GradientAlphaKey(0.98f, 0f), new GradientAlphaKey(0.86f, 0.16f), new GradientAlphaKey(0f, 1f) }));

        ConfigureExplosionSphereRenderer(particles);
        particles.Play();
    }

    static void SpawnEnemyExplosionShockwave(Transform parent, float radius)
    {
        ParticleSystem particles = CreateChildEmitter(parent, "ExplosionShockwave", Vector3.up * 0.03f, Quaternion.identity);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.75f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 0.62f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.startColor = new Color(1f, 0.5f, 0.08f, 0.5f);
        main.maxParticles = 2;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 1),
            new ParticleSystem.Burst(0.08f, 1)
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.01f;

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        float targetSize = Mathf.Max(3f, radius * 2.8f);
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.08f),
            new Keyframe(0.35f, targetSize * 0.56f),
            new Keyframe(1f, targetSize)));

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(new Color(1f, 0.62f, 0.16f), 0f), new GradientColorKey(new Color(1f, 0.18f, 0.02f), 1f) },
            new[] { new GradientAlphaKey(0.52f, 0f), new GradientAlphaKey(0.3f, 0.38f), new GradientAlphaKey(0f, 1f) }));

        ConfigureExplosionSphereRenderer(particles);
        particles.Play();
    }

    static void SpawnEnemyExplosionSparks(Transform parent, float radius)
    {
        ParticleSystem particles = CreateChildEmitter(parent, "ExplosionSparkTrails", Vector3.up * 0.18f, Random.rotation);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.22f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.52f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.4f + 4f, radius * 2.7f + 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.68f, 0.16f, 1f), Color.white);
        main.gravityModifier = 0.65f;
        main.maxParticles = 180;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.RoundToInt(Mathf.Lerp(52f, 96f, Mathf.Clamp01(radius / 7f)))) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.48f, radius * 0.12f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.55f, 0.55f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 1.8f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.55f, 0.55f);
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.35f, 0.03f), 0.55f), new GradientColorKey(new Color(0.85f, 0.08f, 0.02f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.25f), new GradientAlphaKey(0f, 1f) }));

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1.18f),
            new Keyframe(0.38f, 0.82f),
            new Keyframe(1f, 0.04f)));

        ParticleSystem.TrailModule trails = particles.trails;
        trails.enabled = true;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.14f);
        trails.ratio = 0.68f;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.72f),
            new Keyframe(1f, 0f)));
        trails.colorOverTrail = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.22f, 0.02f), 1f) },
            new[] { new GradientAlphaKey(0.82f, 0f), new GradientAlphaKey(0f, 1f) }));

        AssignMaterialIfAvailable(particles);
        particles.Play();
    }

    static void SpawnEnemyExplosionDebris(Transform parent, float radius)
    {
        ParticleSystem particles = CreateChildEmitter(parent, "ExplosionDebrisChunks", Vector3.up * 0.1f, Random.rotation);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.12f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 1.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.9f + 2.8f, radius * 1.55f + 5.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.36f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.28f, 0.16f, 0.08f, 1f), new Color(0.9f, 0.38f, 0.06f, 1f));
        main.gravityModifier = 1.75f;
        main.maxParticles = 90;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.02f, (short)Mathf.RoundToInt(Mathf.Lerp(22f, 48f, Mathf.Clamp01(radius / 7f)))) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.6f, radius * 0.16f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 3.2f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(new Color(1f, 0.52f, 0.12f), 0f), new GradientColorKey(new Color(0.2f, 0.13f, 0.08f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.86f, 0.55f), new GradientAlphaKey(0f, 1f) }));

        AssignMaterialIfAvailable(particles);
        particles.Play();
    }

    static void SpawnEnemyExplosionSmoke(Transform parent, float radius)
    {
        ParticleSystem particles = CreateChildEmitter(parent, "ExplosionSmokePlume", Vector3.up * 0.12f, Quaternion.identity);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 0.32f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startDelay = 0.04f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.82f, 1.55f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, radius * 0.72f + 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.22f, radius * 0.58f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.18f, 0.15f, 0.13f, 0.72f), new Color(0.78f, 0.35f, 0.08f, 0.5f));
        main.gravityModifier = -0.05f;
        main.maxParticles = 80;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0.04f, (short)Mathf.RoundToInt(Mathf.Lerp(18f, 34f, Mathf.Clamp01(radius / 7f)))) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.15f, radius * 0.28f);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.45f, 1.9f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);

        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(CreateGradient(
            new[] { new GradientColorKey(new Color(0.95f, 0.44f, 0.1f), 0f), new GradientColorKey(new Color(0.16f, 0.14f, 0.13f), 0.3f), new GradientColorKey(new Color(0.06f, 0.055f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0.62f, 0f), new GradientAlphaKey(0.48f, 0.34f), new GradientAlphaKey(0f, 1f) }));

        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.42f),
            new Keyframe(0.32f, 1.05f),
            new Keyframe(1f, 1.85f)));

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.72f;
        noise.frequency = 0.7f;
        noise.scrollSpeed = 0.32f;

        AssignMaterialIfAvailable(particles);
        particles.Play();
    }

    static void SpawnEnemyExplosionLight(Transform parent, float radius)
    {
        GameObject lightObject = new GameObject("ExplosionLight");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = Vector3.up * 0.2f;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.28f, 0.05f, 1f);
        light.intensity = Mathf.Lerp(4f, 8f, Mathf.Clamp01(radius / 7f));
        light.range = Mathf.Max(5f, radius * 2.4f);
        light.shadows = LightShadows.None;
        light.bounceIntensity = 0.35f;

        ExplosionLightFade fade = lightObject.AddComponent<ExplosionLightFade>();
        fade.Initialize(light, 0.22f);
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
        main.gravityModifier = 0.62f;
        main.maxParticles = maxParticles;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 15f;
        shape.radius = 0.22f;
        shape.rotation = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.12f, 0.16f);
        velocity.z = new ParticleSystem.MinMaxCurve(1.8f, 4.2f);
        velocity.speedModifier = new ParticleSystem.MinMaxCurve(1f, 1f);

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

    static ParticleSystem CreateChildEmitter(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
    {
        GameObject emitterObject = new GameObject(name);
        emitterObject.transform.SetParent(parent, false);
        emitterObject.transform.localPosition = localPosition;
        emitterObject.transform.localRotation = localRotation;

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
        {
            renderer.sharedMaterial = material;
            renderer.trailMaterial = material;
        }
    }

    static void AssignMaterialIfAvailable(ParticleSystem particles, Material material)
    {
        ParticleSystemRenderer renderer = particles != null ? particles.GetComponent<ParticleSystemRenderer>() : null;
        if (renderer == null || material == null)
            return;

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.trailMaterial = material;
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
        ForceTransparentAlphaBlend(sharedParticleMaterial);
        return sharedParticleMaterial;
    }

    static Material CreateParticleTextureMaterial(string materialName, Texture texture, bool additive = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = materialName
        };

        if (additive)
            ForceTransparentAdditive(material);
        else
            ForceTransparentAlphaBlend(material);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);

        return material;
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

    static void ForceTransparentAdditive(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SurfaceType"))
            material.SetFloat("_SurfaceType", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 2f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
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

    sealed class ExplosionLightFade : MonoBehaviour
    {
        Light targetLight;
        float startIntensity;
        float duration = 0.22f;
        float age;

        public void Initialize(Light light, float fadeDuration)
        {
            targetLight = light;
            startIntensity = targetLight != null ? targetLight.intensity : 0f;
            duration = Mathf.Max(0.01f, fadeDuration);
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            if (targetLight != null)
                targetLight.intensity = Mathf.Lerp(startIntensity, 0f, t * t);

            if (t >= 1f)
                Destroy(gameObject);
        }
    }
}
