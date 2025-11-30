using UnityEngine;

public class HitParticleSpawner : MonoBehaviour
{
    [Header("Particle Settings")]
    public int perfectParticleCount = 120;
    public int goodParticleCount = 80;
    public int okayParticleCount = 40;
    
    [Header("Performance Settings")]
    public bool useExpensiveRendering = true;
    
    private ParticleSystem ps;
    private ParticleSystem.MainModule mainModule;
    
    void Start()
    {
        ps = GetComponent<ParticleSystem>();
        mainModule = ps.main;
        
        // Create a custom particle material that will cause performance bottleneck
        SetupExpensiveParticles();
    }
    
    private void SetupExpensiveParticles()
    {
        // Set particle count based on this object's name (determines quality)
        int particleCount = 40; // default to okay

        if(gameObject.name.Contains("Perfect"))
        {
            particleCount = 150; // Increased for more performance hit
        }
        else if(gameObject.name.Contains("Good"))
        {
            particleCount = 100; // Increased for performance hit
        }
        else if(gameObject.name.Contains("Okay"))
        {
            particleCount = 60; // Increased for some performance impact
        }

        mainModule.maxParticles = particleCount;

        // Configure particle system to cause performance bottleneck
        var emission = ps.emission;
        emission.rateOverTime = particleCount; // Emit all particles at once for performance hit
        
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;
        
        var velocity = ps.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(-5f, 5f);
        velocity.y = new ParticleSystem.MinMaxCurve(-5f, 5f);
        velocity.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
        
        // Add force over lifetime to increase complexity
        var force = ps.forceOverLifetime;
        force.enabled = true;
        force.x = new ParticleSystem.MinMaxCurve(-5f, 5f);
        force.y = new ParticleSystem.MinMaxCurve(-5f, 5f);
        force.z = new ParticleSystem.MinMaxCurve(-5f, 5f);

        // Add turbulence to increase complexity
        var limitVelocity = ps.limitVelocityOverLifetime;
        limitVelocity.enabled = true;
        limitVelocity.space = ParticleSystemSimulationSpace.World;
        limitVelocity.dampen = 0.1f;

        // Add collision to increase complexity
        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.quality = ParticleSystemCollisionQuality.High;
        collision.collidesWith = -1; // Collide with everything (all layers)
        collision.dampen = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        collision.bounce = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        collision.lifetimeLoss = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);

        // Add color over lifetime for additional rendering cost
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var colorGradient = new ParticleSystem.MinMaxGradient();
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0.0f),
                new GradientColorKey(Color.yellow, 0.25f),
                new GradientColorKey(Color.red, 0.5f),
                new GradientColorKey(new Color(1f, 0.5f, 0f), 0.75f),
                new GradientColorKey(Color.clear, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.8f, 0.2f),
                new GradientAlphaKey(0.6f, 0.4f),
                new GradientAlphaKey(0.4f, 0.6f),
                new GradientAlphaKey(0.2f, 0.8f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorGradient.mode = ParticleSystemGradientMode.Gradient;
        colorGradient.gradient = gradient;
        color.color = colorGradient;

        // Add size over lifetime for additional complexity
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.05f),
            new Keyframe(0.2f, 0.3f),
            new Keyframe(0.5f, 0.5f),
            new Keyframe(0.8f, 0.2f),
            new Keyframe(1f, 0.0f)
        ));

        // Add rotation for additional complexity
        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-360f, 360f);

        // Add noise for additional complexity
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        noise.frequency = 2f;
        noise.octaveCount = 3;
        noise.quality = ParticleSystemNoiseQuality.High;

        // Lifetime settings
        mainModule.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        mainModule.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        mainModule.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
    }
}