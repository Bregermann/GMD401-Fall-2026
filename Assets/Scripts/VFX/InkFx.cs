using System.Collections.Generic;
using UnityEngine;

// Builds ink toon particle effects in code from small recipes, using the Kenney CC0 shapes in Resources/VFX/Kenney
// and the Stratafield/Ink Particle shader. One recipe is one layer of an effect, written like:
//   InkFx.Layer("smoke_05").Count(6).Size(0.3f, 0.5f).Speed(1f, 2f).Life(0.4f, 0.6f).Tint(colour).Grow(1.6f).Play(position);
// Particles erode away over their life (alpha drives the ink shader's erosion), so everything ends in the toon style.
// ThemedEffects puts layers together into whole effects.
public class InkFx
{
    // How a layer is drawn
    public enum Look
    {
        Ink,      // Flat colour, shade band, dark ink edge (smoke, slashes, chunks)
        InkGlow,  // Ink look with a bright core (magic, light, moon)
        Glow,     // Additive light only, no ink (sparkles, flashes, beams)
        Flat      // Flat colour and shade band, no ink edge (small bits that would be all outline)
    }

    // Draw order: above the tokens (3) and below the damage badge (20). Ground effects go under the tokens
    public const int AboveUnits = 12;
    public const int OnGround = -12;

    private string texture;
    private Look look = Look.Ink;
    private int count = 1;
    private float rate;                 // > 0 = keeps emitting this many per second (loops and trails)
    private Vector2 life = new Vector2(0.4f, 0.6f);
    private Vector2 speed = Vector2.zero;
    private Vector2 size = new Vector2(0.3f, 0.3f);
    private float grow = 1f;            // Size at the end of its life, times the start size
    private float popIn;                // > 0: starts this much smaller and pops up quickly
    private Color tint = Color.white, tint2 = Color.white;
    private float radius;               // Spawn spread
    private Vector3? direction;         // Null = outward in every direction across the screen
    private float cone = 25f;           // Spread around the direction, degrees
    private float gravity;
    private Vector2 spin;               // Degrees per second (random between)
    private bool randomRotation = true;
    private float fixedRotation;        // Screen angle when not random (degrees, counter-clockwise)
    private float stretch;              // > 0: stretched along its movement (streaks, sparks)
    private bool flat;                  // Lies on the board instead of facing the camera
    private float delay;
    private float drag;                 // 0-1, slows particles down over their life
    private bool loop;
    private int order = AboveUnits;
    private float steps = 6f;           // Erosion frames (0 = smooth)
    private float inkWidth = 0.1f;
    private bool local;                 // Particles move with the parent (projectile cores) instead of staying behind

    private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<(string, Look, float, float), Material> materials = new Dictionary<(string, Look, float, float), Material>();
    private static Shader shader;

    // ---- Recipe ----

    public static InkFx Layer(string kenneyTexture) => new InkFx { texture = kenneyTexture };

    public InkFx As(Look value) { look = value; return this; }
    public InkFx Count(int value) { count = value; return this; }
    public InkFx Rate(float perSecond) { rate = perSecond; return this; }
    public InkFx Life(float min, float max) { life = new Vector2(min, max); return this; }
    public InkFx Speed(float min, float max) { speed = new Vector2(min, max); return this; }
    public InkFx Size(float min, float max) { size = new Vector2(min, max); return this; }
    public InkFx Grow(float endMultiplier) { grow = endMultiplier; return this; }
    public InkFx PopIn(float amount = 0.5f) { popIn = amount; return this; }
    public InkFx Tint(Color colour) { tint = tint2 = colour; return this; }
    public InkFx Tint(Color a, Color b) { tint = a; tint2 = b; return this; }
    public InkFx Spread(float value) { radius = value; return this; }
    public InkFx Toward(Vector3 value, float coneDegrees = 25f) { direction = value; cone = coneDegrees; return this; }
    public InkFx Gravity(float value) { gravity = value; return this; }
    public InkFx Spin(float min, float max) { spin = new Vector2(min, max); return this; }
    public InkFx Upright() { randomRotation = false; return this; }
    public InkFx Rotated(float degrees) { randomRotation = false; fixedRotation = degrees; return this; }
    public InkFx Streak(float lengthScale = 0.15f) { stretch = lengthScale; return this; }
    public InkFx OnBoard() { flat = true; order = OnGround; return this; }
    public InkFx After(float seconds) { delay = seconds; return this; }
    public InkFx Drag(float value) { drag = value; return this; }
    public InkFx Looping(float perSecond) { loop = true; rate = perSecond; return this; }
    public InkFx Order(int value) { order = value; return this; }
    public InkFx Smooth() { steps = 0f; return this; }
    public InkFx Ink(float width) { inkWidth = width; return this; }
    public InkFx Local() { local = true; return this; }

    // Adds a texture made in code (e.g. an arrow or shield shape) so layers can use it by name
    public static void Register(string name, Texture2D texture) => textures[name] = texture;
    public static bool Has(string name) => textures.TryGetValue(name, out Texture2D texture) && texture != null;

    // ---- Playing ----

    // Builds and plays the layer. With a parent it's attached (moves with it), otherwise it stays where it started.
    // One-off layers remove themselves when done
    public ParticleSystem Play(Vector3 position, Transform parent = null)
    {
        GameObject layerObject = new GameObject("Ink " + texture);
        layerObject.transform.position = position;
        if (parent != null) layerObject.transform.SetParent(parent, true);

        ParticleSystem particles = layerObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Camera cam = Camera.main;
        Quaternion screen = cam != null ? cam.transform.rotation : Quaternion.Euler(90f, 0f, 0f);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = loop;
        main.duration = loop ? 1f : Mathf.Max(0.05f, life.y);
        main.startDelay = delay;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(tint, tint2);
        // Billboard rotation turns clockwise for positive values, so negate to keep "counter-clockwise degrees"
        main.startRotation = randomRotation ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f) : new ParticleSystem.MinMaxCurve(-fixedRotation * Mathf.Deg2Rad);
        main.gravityModifier = gravity;
        main.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World; // World: trails stay behind
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.Max(count, Mathf.CeilToInt(rate * life.y) + 4);
        main.stopAction = loop ? ParticleSystemStopAction.None : ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate;
        if (count > 0 && !loop) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        // Where particles start and which way they go
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        if (direction.HasValue)
        {
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = cone;
            shape.radius = Mathf.Max(0.001f, radius);
            shape.rotation = Vector3.zero;
            layerObject.transform.rotation = Quaternion.LookRotation(direction.Value.sqrMagnitude > 0.0001f ? direction.Value : Vector3.up);
        }
        else if (flat)
        {
            shape.shapeType = ParticleSystemShapeType.Circle; // Across the board
            shape.radius = Mathf.Max(0.001f, radius);
            layerObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Circle; // Across the screen, so outward reads clearly from the camera
            shape.radius = Mathf.Max(0.001f, radius);
            layerObject.transform.rotation = screen;
        }

        // Size over life: optional pop in, then grow or shrink
        ParticleSystem.SizeOverLifetimeModule sizeOverLife = particles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve curve = popIn > 0f
            ? new AnimationCurve(new Keyframe(0f, 1f - popIn), new Keyframe(0.12f, 1.08f), new Keyframe(1f, grow))
            : new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, grow));
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

        // Alpha over life = how much shape is left, so the ink shader erodes it away
        ParticleSystem.ColorOverLifetimeModule colourOverLife = particles.colorOverLifetime;
        colourOverLife.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.35f), new GradientAlphaKey(0f, 1f) });
        colourOverLife.color = fade;

        if (spin != Vector2.zero)
        {
            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(spin.x * Mathf.Deg2Rad, spin.y * Mathf.Deg2Rad);
        }

        if (drag > 0f)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule limit = particles.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 0.01f;
            limit.dampen = drag;
        }

        ParticleSystemRenderer renderer = layerObject.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = MaterialFor(texture, look, steps, inkWidth);
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (stretch > 0f)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = stretch;
            renderer.lengthScale = 1f;
        }
        else renderer.renderMode = flat ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;

        particles.Play();
        return particles;
    }

    // ---- Helpers for whole effects ----

    // Stops a looping effect (or a moving projectile) and lets its particles finish before removing it
    public static void Stop(GameObject effect, float cleanupAfter = 2f)
    {
        if (effect == null) return;
        effect.transform.SetParent(null, true);
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            ParticleSystem.MainModule main = particles.main;
            main.stopAction = ParticleSystemStopAction.None;
        }
        Object.Destroy(effect, cleanupAfter);
    }

    // An empty object to hold several layers (so a whole effect can be moved, followed or stopped at once)
    public static GameObject Group(string name, Vector3 position, float destroyAfter = 0f)
    {
        GameObject group = new GameObject(name);
        group.transform.position = position;
        if (destroyAfter > 0f) Object.Destroy(group, destroyAfter);
        return group;
    }

    // A whole shape as a sprite drawn in the ink style (for things that aren't particles, like orbiting stun stars)
    public static SpriteRenderer Sprite(string name, Look look, Transform parent, float size, Color colour, int order = AboveUnits)
    {
        Texture2D texture = Texture(name);
        GameObject spriteObject = new GameObject("Ink " + name);
        spriteObject.transform.SetParent(parent, false);
        SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
        renderer.sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width / size);
        renderer.sharedMaterial = MaterialFor(name, look, 0f, 0.1f);
        renderer.color = colour; // Alpha 1 = the whole shape
        renderer.sortingOrder = order;
        return renderer;
    }

    // Material for a shape and look, made once and reused
    static Material MaterialFor(string name, Look look, float steps, float inkWidth)
    {
        if (materials.TryGetValue((name, look, steps, inkWidth), out Material cached) && cached != null) return cached;
        if (shader == null) shader = Shader.Find("Stratafield/Ink Particle");

        Material material = new Material(shader) { name = "Ink " + name + " " + look };
        material.mainTexture = Texture(name);
        material.SetFloat("_Steps", steps);
        bool additive = look == Look.Glow;
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        material.SetFloat("_InkWidth", look == Look.Ink || look == Look.InkGlow ? inkWidth : 0f);
        material.SetFloat("_Shade", look == Look.Glow ? 0f : 0.3f);
        material.SetFloat("_Glow", look == Look.InkGlow ? 0.7f : look == Look.Glow ? 0.6f : 0f);
        materials[(name, look, steps, inkWidth)] = material;
        return material;
    }

    static Texture2D Texture(string name)
    {
        if (textures.TryGetValue(name, out Texture2D cached) && cached != null) return cached;
        Texture2D texture = Resources.Load<Texture2D>("VFX/Kenney/" + name);
        if (texture == null)
        {
            Debug.LogWarning($"InkFx: no texture Resources/VFX/Kenney/{name}, using white");
            texture = Texture2D.whiteTexture;
        }
        textures[name] = texture;
        return texture;
    }
}

// Keeps an effect on a moving card or token (instead of parenting it to the card, whose odd scale would squash it).
// Removes the effect, letting its particles finish, when the card is gone
public class FollowCard : MonoBehaviour
{
    public CardUnit target;
    public float height;       // How far up the token (0 = feet, 1 = top)

    void LateUpdate()
    {
        if (target == null) { InkFx.Stop(gameObject, 2f); enabled = false; return; }
        transform.position = ThemedEffects.OnUnit(target, height);
    }
}
