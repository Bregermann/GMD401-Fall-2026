using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Simple effects built in code so combat has feedback before real art exists.
// Each one has its own texture, shape and movement so they're easy to tell apart.
// Movement goes across the screen (the camera looks straight down, so "up" in the world can't be seen).
// Swap them out per attack with the effect prefab slots on AttackData, or replace these later.
public static class PlaceholderEffects
{
    // Colours match the card colour coding (red physical, blue magic, green heal)
    public static readonly Color PhysicalColor = new Color(1f, 0.35f, 0.25f);
    public static readonly Color MagicColor = new Color(0.35f, 0.6f, 1f);
    public static readonly Color HealColor = new Color(0.35f, 1f, 0.45f);
    public static readonly Color BurnColor = new Color(1f, 0.55f, 0.1f);
    public static readonly Color PoisonColor = new Color(0.6f, 0.9f, 0.15f);
    public static readonly Color StunColor = new Color(1f, 0.9f, 0.25f);
    public static readonly Color BuffColor = new Color(0.4f, 1f, 0.9f);
    public static readonly Color DebuffColor = new Color(0.85f, 0.45f, 1f);
    public static readonly Color DefeatColor = new Color(0.85f, 0.85f, 0.85f);

    private static readonly Color PoisonPurple = new Color(0.6f, 0.3f, 0.9f);

    private enum Shape { Dot, Ring, Star, Plus, Shard }
    private static readonly Dictionary<Shape, Material> materials = new Dictionary<Shape, Material>();

    public static Color ColorFor(DamageType type)
    {
        switch (type)
        {
            case DamageType.Magic: return MagicColor;
            case DamageType.Heal:  return HealColor;
            default:               return PhysicalColor;
        }
    }

    // ---- Attack hits ----

    // Fast sparks streaking outward with a quick white flash
    public static GameObject PhysicalHit(Vector3 position)
    {
        ParticleSystem sparks = NewSystem("Hit_Physical", position, Shape.Dot);
        ParticleSystem.MainModule main = sparks.main;
        main.startLifetime = Range(0.12f, 0.28f);
        main.startSpeed = Range(3f, 6f);
        main.startSize = Range(0.05f, 0.09f);
        ScreenCircle(sparks, 0.05f);
        Burst(sparks, 22);
        Fade(sparks, Gradient(new[] { Color.white, PhysicalColor, PhysicalColor * 0.6f }, new[] { 1f, 1f, 0f }));

        ParticleSystemRenderer renderer = sparks.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch; // Streaks along their direction
        renderer.lengthScale = 2f;
        renderer.velocityScale = 0.06f;

        Flash(position, Color.white, 0.9f, sparks.transform);
        return Finish(sparks);
    }

    // Blue shockwave ring with orbs swirling out
    public static GameObject MagicHit(Vector3 position)
    {
        ParticleSystem ring = Shockwave(position, MagicColor, 1.6f, 0.45f, null);

        ParticleSystem orbs = NewSystem("Hit_Magic_Orbs", position, Shape.Dot, ring.transform);
        ParticleSystem.MainModule main = orbs.main;
        main.startLifetime = Range(0.4f, 0.6f);
        main.startSize = Range(0.08f, 0.14f);
        ScreenCircle(orbs, 0.2f);
        Burst(orbs, 14);
        Swirl(orbs, 8f, 1.2f);
        Fade(orbs, Gradient(new[] { Color.white, MagicColor }, new[] { 1f, 0f }));
        orbs.Play();

        return Finish(ring);
    }

    // Burst of green plus signs drifting up, with a soft pulse
    public static GameObject HealHit(Vector3 position)
    {
        ParticleSystem pluses = NewSystem("Hit_Heal", position, Shape.Plus);
        ParticleSystem.MainModule main = pluses.main;
        main.startLifetime = Range(0.6f, 0.9f);
        main.startSpeed = Range(0.5f, 1.2f);
        main.startSize = Range(0.1f, 0.16f);
        ScreenCircle(pluses, 0.1f);
        Burst(pluses, 12);
        Drift(pluses, 0.6f);
        Fade(pluses, Gradient(new[] { Color.white, HealColor }, new[] { 0f, 1f, 1f, 0f }));

        Shockwave(position, HealColor, 1f, 0.5f, pluses.transform);
        return Finish(pluses);
    }

    // Expanding ring, e.g. for attacks centred on the user
    public static GameObject Pulse(Vector3 position, Color color, float size)
    {
        return Finish(Shockwave(position, color, size, 0.5f, null));
    }

    // ---- Status effects (loop = keep going as an aura) ----

    // Flickering flames licking up the screen, yellow to orange to red to smoke
    public static GameObject Burn(Vector3 position, bool loop)
    {
        ParticleSystem flames = NewSystem(loop ? "Aura_Burn" : "Burn", position, Shape.Dot);
        ParticleSystem.MainModule main = flames.main;
        main.startLifetime = Range(0.35f, 0.6f);
        main.startSpeed = Range(0.6f, 1.2f);
        main.startSize = Range(0.12f, 0.22f);

        ParticleSystem.ShapeModule shape = flames.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.15f;
        shape.rotation = Quaternion.LookRotation(ScreenUp()).eulerAngles; // Cone points up the screen

        Fade(flames, Gradient(
            new[] { new Color(1f, 0.95f, 0.5f), new Color(1f, 0.55f, 0.1f), new Color(0.8f, 0.15f, 0.05f), new Color(0.25f, 0.25f, 0.25f) },
            new[] { 1f, 0.9f, 0f }));
        SizeCurve(flames, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f)));
        Wobble(flames, 0.4f, 3f);

        return Emit(flames, loop, 18f, 30);
    }

    // Slow bubble rings that wobble, grow and pop
    public static GameObject Poison(Vector3 position, bool loop)
    {
        ParticleSystem bubbles = NewSystem(loop ? "Aura_Poison" : "Poison", position, Shape.Ring);
        ParticleSystem.MainModule main = bubbles.main;
        main.startLifetime = Range(0.8f, 1.3f);
        main.startSpeed = Range(0.05f, 0.15f);
        main.startSize = Range(0.12f, 0.24f);
        ScreenCircle(bubbles, 0.3f);
        Drift(bubbles, 0.25f);
        Fade(bubbles, Gradient(new[] { PoisonColor, PoisonPurple }, new[] { 0f, 1f, 1f, 0f }));
        SizeCurve(bubbles, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.85f, 1f), new Keyframe(1f, 1.4f))); // Pops at the end
        Wobble(bubbles, 0.15f, 1.5f);

        return Emit(bubbles, loop, 5f, 12);
    }

    // Twinkling plus signs drifting up the screen
    public static GameObject Regeneration(Vector3 position, bool loop)
    {
        ParticleSystem pluses = NewSystem(loop ? "Aura_Regeneration" : "Regeneration", position, Shape.Plus);
        ParticleSystem.MainModule main = pluses.main;
        main.startLifetime = Range(0.9f, 1.3f);
        main.startSize = Range(0.07f, 0.12f);
        ScreenCircle(pluses, 0.3f);
        Drift(pluses, 0.35f);
        Fade(pluses, Gradient(new[] { Color.white, HealColor }, new[] { 0f, 1f, 0.3f, 1f, 0.3f, 0f })); // Twinkle

        if (!loop) Shockwave(position, HealColor, 1.2f, 0.6f, pluses.transform);
        return Emit(pluses, loop, 4f, 14);
    }

    // Stars circling the card. faint = Stagger (fewer, dimmer, slower)
    public static GameObject Stars(Vector3 position, bool loop, bool faint)
    {
        ParticleSystem stars = NewSystem(faint ? "Aura_Stagger" : loop ? "Aura_Stun" : "Stun", position, Shape.Star);
        ParticleSystem.MainModule main = stars.main;
        main.startLifetime = loop ? Range(1f, 1.2f) : Range(0.7f, 0.9f);
        main.startSize = faint ? Range(0.08f, 0.1f) : Range(0.13f, 0.18f);
        main.startColor = faint ? new Color(1f, 1f, 1f, 0.6f) : Color.white;

        ScreenCircle(stars, 0.35f);
        ParticleSystem.ShapeModule shape = stars.shape;
        shape.radiusThickness = 0f; // Start on the edge of the circle

        Swirl(stars, faint ? 2f : 4f, loop ? 0f : 0.8f);
        Spin(stars, 3f);
        Fade(stars, Gradient(new[] { Color.white, StunColor }, new[] { 0f, 1f, 0.6f, 1f, 0f }));

        return Emit(stars, loop, faint ? 1.5f : 5f, 8);
    }

    // ---- Defeat ----

    // Grey shards spinning and scattering
    public static GameObject Shatter(Vector3 position)
    {
        ParticleSystem shards = NewSystem("Defeat_Shatter", position, Shape.Shard);
        ParticleSystem.MainModule main = shards.main;
        main.startLifetime = Range(0.6f, 1f);
        main.startSpeed = Range(1.2f, 3f);
        main.startSize = Range(0.07f, 0.15f);
        main.startRotation = Range(0f, Mathf.PI * 2f);
        ScreenCircle(shards, 0.2f);
        Burst(shards, 30);
        Spin(shards, 6f);
        Fade(shards, Gradient(new[] { DefeatColor, new Color(0.35f, 0.35f, 0.35f) }, new[] { 1f, 1f, 0f }));

        ParticleSystem.LimitVelocityOverLifetimeModule slow = shards.limitVelocityOverLifetime;
        slow.enabled = true;
        slow.limit = 0.3f;
        slow.dampen = 0.15f; // Shards slow down as they fly

        Flash(position, Color.white, 1f, shards.transform);
        return Finish(shards);
    }

    // ---- Projectiles ----

    // Physical is a short fast streak. Magic and heals are a glowing orb with a sparkly trail.
    // CombatEffects moves it, then calls Stop
    public static GameObject Projectile(Vector3 position, DamageType type)
    {
        GameObject root = new GameObject("Projectile");
        root.transform.position = position;
        Color color = ColorFor(type);

        if (type == DamageType.Physical)
        {
            ParticleSystem streak = NewSystem("Projectile_Streak", position, Shape.Dot, root.transform);
            ParticleSystem.MainModule streakMain = streak.main;
            streakMain.loop = true;
            streakMain.startLifetime = 0.1f;
            streakMain.startSize = Range(0.1f, 0.14f);
            SetRate(streak, 150f);
            Fade(streak, Gradient(new[] { Color.white, color }, new[] { 1f, 0f }));
            streak.Play();
            return root;
        }

        // Bright core that stays on the projectile
        ParticleSystem core = NewSystem("Projectile_Core", position, Shape.Dot, root.transform);
        ParticleSystem.MainModule coreMain = core.main;
        coreMain.loop = true;
        coreMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        coreMain.startLifetime = 0.08f;
        coreMain.startSize = 0.35f;
        coreMain.startColor = Color.Lerp(color, Color.white, 0.5f);
        SetRate(core, 60f);
        core.Play();

        // Sparkles left behind
        ParticleSystem sparkles = NewSystem("Projectile_Trail", position, type == DamageType.Heal ? Shape.Plus : Shape.Star, root.transform);
        ParticleSystem.MainModule sparkleMain = sparkles.main;
        sparkleMain.loop = true;
        sparkleMain.startLifetime = Range(0.3f, 0.5f);
        sparkleMain.startSpeed = Range(0.2f, 0.5f);
        sparkleMain.startSize = Range(0.06f, 0.1f);
        ScreenCircle(sparkles, 0.05f);
        Spin(sparkles, 4f);
        SetRate(sparkles, 40f);
        Fade(sparkles, Gradient(new[] { Color.white, color }, new[] { 1f, 0f }));
        sparkles.Play();

        return root;
    }

    // Stops a looping effect (aura or projectile) and removes it once its particles fade
    public static void Stop(GameObject effect)
    {
        if (effect == null) return;
        effect.transform.SetParent(null, true);
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>())
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Object.Destroy(effect, 1.5f);
    }

    // ---- Text ----

    // Text facing the camera, e.g. "-320". CombatEffects moves and fades it
    public static TextMeshPro Text(string text, Vector3 position, Color color, float fontSize)
    {
        GameObject textObject = new GameObject("Effect_Text");
        TextMeshPro tmp = textObject.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(10f, 2f); // Wide so it stays on one line
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = Color.black;

        textObject.transform.position = position;
        if (Camera.main != null) textObject.transform.rotation = Camera.main.transform.rotation;
        return tmp;
    }

    // ---- Building blocks ----

    // A stopped particle system so settings can be changed, then each effect plays it
    private static ParticleSystem NewSystem(string name, Vector3 position, Shape texture, Transform parent = null)
    {
        GameObject effectObject = new GameObject(name);
        effectObject.transform.position = position;
        if (parent != null) effectObject.transform.SetParent(parent, true);

        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 1f;
        main.startSpeed = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // Trails stay behind when it moves
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;      // World size even on a scaled card

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false; // Effects that need a shape turn it on

        effectObject.GetComponent<ParticleSystemRenderer>().material = GetMaterial(texture);
        return particles;
    }

    // Expanding ring that fades out
    private static ParticleSystem Shockwave(Vector3 position, Color color, float size, float lifetime, Transform parent)
    {
        ParticleSystem ring = NewSystem("Shockwave", position, Shape.Ring, parent);
        ParticleSystem.MainModule main = ring.main;
        main.startLifetime = lifetime;
        main.startSize = size;
        Burst(ring, 1);
        SizeCurve(ring, new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 3f), new Keyframe(1f, 1f)));
        Fade(ring, Gradient(new[] { Color.white, color }, new[] { 1f, 0f }));
        ring.Play();
        return ring;
    }

    // Single quick bright dot
    private static void Flash(Vector3 position, Color color, float size, Transform parent)
    {
        ParticleSystem flash = NewSystem("Flash", position, Shape.Dot, parent);
        ParticleSystem.MainModule main = flash.main;
        main.startLifetime = 0.12f;
        main.startSize = size;
        Burst(flash, 1);
        Fade(flash, Gradient(new[] { color, color }, new[] { 0.9f, 0f }));
        flash.Play();
    }

    // Plays as a one-off burst (removes itself) or a looping aura
    private static GameObject Emit(ParticleSystem particles, bool loop, float rate, int burstCount)
    {
        if (loop)
        {
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            SetRate(particles, rate);
            particles.Play();
            return particles.gameObject;
        }

        Burst(particles, burstCount);
        return Finish(particles);
    }

    private static GameObject Finish(ParticleSystem particles)
    {
        particles.Play();
        Object.Destroy(particles.gameObject, 3f);
        return particles.gameObject;
    }

    private static void Burst(ParticleSystem particles, int count)
    {
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    private static void SetRate(ParticleSystem particles, float rate)
    {
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate;
    }

    // Emits from a flat circle facing the camera, so particles spread across the screen
    private static void ScreenCircle(ParticleSystem particles, float radius)
    {
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.rotation = ScreenRotation().eulerAngles;
    }

    // Constant drift up the screen
    private static void Drift(ParticleSystem particles, float speed)
    {
        Vector3 up = ScreenUp() * speed;
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = up.x;
        velocity.y = up.y;
        velocity.z = up.z;
    }

    // Circles around the effect's centre on screen, optionally moving outward
    private static void Swirl(ParticleSystem particles, float orbitSpeed, float outward)
    {
        Vector3 axis = ScreenRotation() * Vector3.forward * orbitSpeed;
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.orbitalX = axis.x;
        velocity.orbitalY = axis.y;
        velocity.orbitalZ = axis.z;
        velocity.radial = outward;
    }

    // Each particle spins, random direction
    private static void Spin(ParticleSystem particles, float speed)
    {
        ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = Range(-speed, speed);
    }

    // Random jitter, for flicker and wobble
    private static void Wobble(ParticleSystem particles, float strength, float frequency)
    {
        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = strength;
        noise.frequency = frequency;
        noise.scrollSpeed = 1f;
    }

    private static void Fade(ParticleSystem particles, Gradient gradient)
    {
        ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = gradient;
    }

    private static void SizeCurve(ParticleSystem particles, AnimationCurve curve)
    {
        ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private static ParticleSystem.MinMaxCurve Range(float min, float max) => new ParticleSystem.MinMaxCurve(min, max);

    // Colours and alphas spread evenly over the particle's life
    private static Gradient Gradient(Color[] colors, float[] alphas)
    {
        GradientColorKey[] colorKeys = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++)
            colorKeys[i] = new GradientColorKey(colors[i], colors.Length == 1 ? 0f : i / (colors.Length - 1f));

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[alphas.Length];
        for (int i = 0; i < alphas.Length; i++)
            alphaKeys[i] = new GradientAlphaKey(alphas[i], alphas.Length == 1 ? 0f : i / (alphas.Length - 1f));

        Gradient gradient = new Gradient();
        gradient.SetKeys(colorKeys, alphaKeys);
        return gradient;
    }

    private static Quaternion ScreenRotation() => Camera.main != null ? Camera.main.transform.rotation : Quaternion.Euler(90f, 0f, 0f);
    private static Vector3 ScreenUp() => Camera.main != null ? Camera.main.transform.up : Vector3.forward;

    // ---- Textures ----

    // Sprites/Default is always included in builds and tints by particle colour
    private static Material GetMaterial(Shape shape)
    {
        if (materials.TryGetValue(shape, out Material existing) && existing != null) return existing;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        Material material = new Material(shader);
        material.mainTexture = MakeTexture(shape);
        materials[shape] = material;
        return material;
    }

    // Draws each shape into a small white texture with soft edges
    private static Texture2D MakeTexture(Shape shape)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                // -1 to 1 across the texture
                float x = (px + 0.5f) / size * 2f - 1f;
                float y = (py + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(x * x + y * y);
                texture.SetPixel(px, py, new Color(1f, 1f, 1f, ShapeAlpha(shape, x, y, distance)));
            }
        }
        texture.Apply();
        return texture;
    }

    private static float ShapeAlpha(Shape shape, float x, float y, float distance)
    {
        switch (shape)
        {
            case Shape.Ring:
                float ring = (distance - 0.75f) / 0.12f;
                return distance > 1f ? 0f : Mathf.Exp(-ring * ring);

            case Shape.Star: // 4-point sparkle
                float arms = Mathf.Clamp01(1f - Mathf.Abs(x * y) * 20f) * Mathf.Clamp01(1f - distance);
                float core = Mathf.Clamp01(1f - distance * 2.5f);
                return Mathf.Max(arms, core);

            case Shape.Plus:
                float across = Mathf.Clamp01((0.22f - Mathf.Abs(y)) * 20f) * Mathf.Clamp01((0.75f - Mathf.Abs(x)) * 20f);
                float down = Mathf.Clamp01((0.22f - Mathf.Abs(x)) * 20f) * Mathf.Clamp01((0.75f - Mathf.Abs(y)) * 20f);
                return Mathf.Max(across, down);

            case Shape.Shard: // Diamond
                return Mathf.Clamp01((0.7f - (Mathf.Abs(x) + Mathf.Abs(y) * 0.6f)) * 15f);

            default: // Soft dot
                float dot = Mathf.Clamp01(1f - distance);
                return dot * dot;
        }
    }
}
