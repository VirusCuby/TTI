using UnityEngine;

/// <summary>
/// Efectos de partículas creados por código (sin prefabs ni assets): chispas para el
/// cortocircuito y humo para un LED quemado. Usan el shader Sprites/Default, que siempre
/// está incluido en el build.
/// </summary>
public static class CircuitEffects
{
    private static Material particleMaterial;

    private static Material ParticleMaterial()
    {
        if (particleMaterial == null)
            particleMaterial = new Material(Shader.Find("Sprites/Default"));
        return particleMaterial;
    }

    private static ParticleSystem Build(string name, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = ParticleMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    private static Gradient FadeOut(Color color, float startAlpha)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(startAlpha * 0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    /// <summary>Chispas amarillas y naranjas que saltan en todas direcciones (cortocircuito).</summary>
    public static ParticleSystem CreateSparks(Vector3 position)
    {
        var ps = Build("Chispas", position);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.06f, 0.24f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.008f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.45f), new Color(1f, 0.45f, 0.1f));
        main.gravityModifier = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 150;

        var emission = ps.emission;
        emission.rateOverTime = 110f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.006f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeOut(Color.white, 1f));

        ps.Play();
        return ps;
    }

    /// <summary>Humo gris que sube lentamente (LED quemado).</summary>
    public static ParticleSystem CreateSmoke(Vector3 position)
    {
        var ps = Build("Humo", position);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.010f, 0.020f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.35f, 0.35f, 0.38f), new Color(0.6f, 0.6f, 0.62f));
        main.gravityModifier = -0.02f; // sube
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;

        var emission = ps.emission;
        emission.rateOverTime = 16f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.004f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // el cono apunta hacia arriba (+Y)

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(FadeOut(Color.white, 0.55f));

        ps.Play();
        return ps;
    }
}
