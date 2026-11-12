using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Effects built in code so combat has feedback before real art exists. CombatEffects decides when they play.
// Built from layers: a bright glowing core (additive), coloured sparks or shapes, a shockwave, smoke or embers, and a light flash.
// Bursts spray away from the attacker, and k (intensity) scales hits with damage so big hits look big.
// Movement goes across the screen (the camera looks straight down, so "up" in the world can't be seen).
// Swap any of them out with the Effect Library or the effect prefab slots on AttackData.
public static class PlaceholderEffects
{
    // Colours match the card colour coding (red physical, blue magic, green heal)
    public static readonly Color PhysicalColor = new Color(1f, 0.45f, 0.2f);
    public static readonly Color MagicColor = new Color(0.55f, 0.5f, 1f);
    public static readonly Color HealColor = new Color(0.35f, 1f, 0.5f);
    public static readonly Color BurnColor = new Color(1f, 0.55f, 0.1f);
    public static readonly Color PoisonColor = new Color(0.6f, 0.9f, 0.15f);
    public static readonly Color StunColor = new Color(1f, 0.9f, 0.25f);
    public static readonly Color BuffColor = new Color(0.4f, 1f, 0.9f);
    public static readonly Color DebuffColor = new Color(0.85f, 0.45f, 1f);
    public static readonly Color DefeatColor = new Color(0.85f, 0.85f, 0.85f);
    public static readonly Color BonusColor = new Color(1f, 0.85f, 0.2f);

    private static readonly Color PoisonPurple = new Color(0.6f, 0.3f, 0.9f);
    private static readonly Color MagicViolet = new Color(0.8f, 0.4f, 1f);
    private static readonly Color SmokeColor = new Color(0.35f, 0.33f, 0.32f);

    private enum Shape { Dot, Ring, Star, Plus, Shard, Arrow, Cross, Link, Slash, Rune }
    private static readonly Dictionary<(Shape, bool), Material> materials = new Dictionary<(Shape, bool), Material>();
    private static readonly Dictionary<Shape, Texture2D> textures = new Dictionary<Shape, Texture2D>();

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

    // White-hot flash, a slash arc, sparks spraying away from the attacker, a shockwave, debris and smoke
    public static GameObject PhysicalHit(Vector3 position, Vector3 direction, float k)
    {
        ParticleSystem flash = Flash(position, Color.white, 1f * k, 0.09f, null);
        Transform root = flash.transform;
        Flash(position, PhysicalColor, 0.55f * k, 0.18f, root);

        // Slash arc across the hit, bulging toward the target
        ParticleSystem slash = NewSystem("Hit_Slash", position, Shape.Slash, root, true);
        ParticleSystem.MainModule slashMain = slash.main;
        slashMain.startLifetime = 0.2f;
        slashMain.startSize = 1.3f * k;
        slashMain.startRotation = ScreenAngle(direction);
        Burst(slash, 1);
        SizeCurve(slash, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 1.15f)));
        Fade(slash, Gradient(new[] { Color.white, PhysicalColor }, new[] { 1f, 0f }));
        slash.Play();

        Sparks(position, direction, Color.white, PhysicalColor, Mathf.RoundToInt(10 + 8 * k), 3f * k, 7f * k, root);
        Shockwave(position, PhysicalColor, 1.1f * k, 0.25f, root, true);
        Debris(position, direction, new Color(0.55f, 0.35f, 0.25f), Mathf.RoundToInt(3 + 3 * k), root);
        Smoke(position, 3, 0.35f * k, root);
        LightFlash(position, new Color(1f, 0.6f, 0.3f), 3f * k, 2.5f, 0.25f);
        return Finish(flash);
    }

    // Violet flash, spinning rune circles, sparkles spraying and swirling, a soft light
    public static GameObject MagicHit(Vector3 position, Vector3 direction, float k)
    {
        ParticleSystem flash = Flash(position, new Color(0.9f, 0.85f, 1f), 1.1f * k, 0.1f, null);
        Transform root = flash.transform;
        Flash(position, MagicColor, 0.7f * k, 0.25f, root);

        Rune(position, MagicColor, 1.4f * k, 0.5f, 2.5f, root);
        Rune(position, MagicViolet, 0.9f * k, 0.4f, -3.5f, root);

        ParticleSystem sparkles = NewSystem("Hit_Magic_Sparkles", position, Shape.Star, root, true);
        ParticleSystem.MainModule main = sparkles.main;
        main.startLifetime = Range(0.4f, 0.7f);
        main.startSpeed = Range(1.5f * k, 3.5f * k);
        main.startSize = Range(0.08f, 0.15f);
        Cone(sparkles, direction, 60f, 0.1f);
        Burst(sparkles, Mathf.RoundToInt(12 + 8 * k));
        Swirl(sparkles, 3f, 0f);
        Spin(sparkles, 4f);
        Slow(sparkles, 0.12f);
        Fade(sparkles, Gradient(new[] { Color.white, MagicColor, MagicViolet }, new[] { 1f, 1f, 0f }));
        sparkles.Play();

        Shockwave(position, MagicViolet, 1.2f * k, 0.35f, root, true);
        LightFlash(position, new Color(0.55f, 0.55f, 1f), 3f * k, 2.5f, 0.35f);
        return Finish(flash);
    }

    // Soft green glow, rising plus signs, a gentle ring
    public static GameObject HealHit(Vector3 position, float k)
    {
        ParticleSystem glow = NewSystem("Hit_Heal", position, Shape.Dot, null, true);
        ParticleSystem.MainModule glowMain = glow.main;
        glowMain.startLifetime = 0.45f;
        glowMain.startSize = 1.2f * k;
        Burst(glow, 1);
        Fade(glow, Gradient(new[] { HealColor, HealColor }, new[] { 0f, 0.8f, 0f }));
        Transform root = glow.transform;

        ParticleSystem pluses = NewSystem("Hit_Heal_Pluses", position, Shape.Plus, root, true);
        ParticleSystem.MainModule main = pluses.main;
        main.startLifetime = Range(0.7f, 1.1f);
        main.startSpeed = Range(0.2f, 0.6f);
        main.startSize = Range(0.1f, 0.16f);
        ScreenCircle(pluses, 0.3f);
        Burst(pluses, Mathf.RoundToInt(10 + 6 * k));
        Drift(pluses, 0.8f);
        Fade(pluses, Gradient(new[] { Color.white, HealColor }, new[] { 0f, 1f, 1f, 0f }));
        pluses.Play();

        Shockwave(position, HealColor, 1f * k, 0.5f, root, true);
        LightFlash(position, HealColor, 2f * k, 2f, 0.4f);
        return Finish(glow);
    }

    // Expanding ring, e.g. for attacks centred on the user
    public static GameObject Pulse(Vector3 position, Color color, float size)
    {
        ParticleSystem ring = Shockwave(position, color, size, 0.5f, null, true);
        Rune(position, color, size * 0.6f, 0.5f, 2f, ring.transform);
        LightFlash(position, color, 2f, size, 0.4f);
        return Finish(ring);
    }

    // Particles pulled in to a growing glow over 'duration', the wind-up before a ranged or magic attack
    public static GameObject Charge(Vector3 position, DamageType type, float duration)
    {
        Color color = ColorFor(type);
        ParticleSystem core = NewSystem("Charge", position, Shape.Dot, null, true);
        ParticleSystem.MainModule coreMain = core.main;
        coreMain.startLifetime = duration + 0.05f;
        coreMain.startSize = 0.45f;
        Burst(core, 1);
        SizeCurve(core, new AnimationCurve(new Keyframe(0f, 0.1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0.6f)));
        Fade(core, Gradient(new[] { color, Color.white }, new[] { 0.3f, 1f, 0.6f }));

        // Motes start on a ring and fly inward
        ParticleSystem motes = NewSystem("Charge_Motes", position, type == DamageType.Heal ? Shape.Plus : Shape.Dot, core.transform, true);
        ParticleSystem.MainModule main = motes.main;
        main.loop = false;
        main.duration = Mathf.Max(0.05f, duration * 0.7f);
        main.startLifetime = duration * 0.6f;
        main.startSize = Range(0.05f, 0.09f);
        ScreenCircle(motes, 0.7f);
        ParticleSystem.ShapeModule shape = motes.shape;
        shape.radiusThickness = 0f;
        ParticleSystem.VelocityOverLifetimeModule velocity = motes.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.radial = -0.7f / (duration * 0.6f); // Arrive at the centre as they die
        SetRate(motes, 30f / duration);
        Fade(motes, Gradient(new[] { color, Color.white }, new[] { 0f, 1f, 1f }));
        motes.Play();

        core.Play();
        Object.Destroy(core.gameObject, duration + 1f);
        return core.gameObject;
    }

    // Puff kicked up behind a card that's knocked back
    public static GameObject Dust(Vector3 position, Vector3 direction)
    {
        ParticleSystem dust = NewSystem("Dust", position, Shape.Dot, null, false);
        ParticleSystem.MainModule main = dust.main;
        main.startLifetime = Range(0.4f, 0.7f);
        main.startSpeed = Range(0.5f, 1.5f);
        main.startSize = Range(0.15f, 0.3f);
        Cone(dust, -direction, 50f, 0.2f);
        Burst(dust, 8);
        Slow(dust, 0.15f);
        SizeCurve(dust, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.4f)));
        Fade(dust, Gradient(new[] { new Color(0.75f, 0.68f, 0.55f), new Color(0.6f, 0.55f, 0.45f) }, new[] { 0.55f, 0f }));
        return Finish(dust);
    }

    // ---- Status effects (loop = keep going as an aura) ----

    // Glowing flames licking up the screen, embers, smoke, and a flickering light while it lasts
    public static GameObject Burn(Vector3 position, bool loop)
    {
        ParticleSystem flames = NewSystem(loop ? "Aura_Burn" : "Burn", position, Shape.Dot, null, true);
        ParticleSystem.MainModule main = flames.main;
        main.startLifetime = Range(0.35f, 0.6f);
        main.startSpeed = Range(0.6f, 1.2f);
        main.startSize = Range(0.14f, 0.24f);
        Cone(flames, ScreenUp(), 18f, 0.15f);
        Fade(flames, Gradient(
            new[] { new Color(1f, 0.95f, 0.6f), new Color(1f, 0.55f, 0.1f), new Color(0.9f, 0.15f, 0.05f) },
            new[] { 1f, 0.9f, 0f }));
        SizeCurve(flames, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.2f)));
        Wobble(flames, 0.4f, 3f);

        // Embers: tiny bright sparks rising further
        ParticleSystem embers = NewSystem("Burn_Embers", position, Shape.Dot, flames.transform, true);
        ParticleSystem.MainModule emberMain = embers.main;
        emberMain.loop = loop;
        emberMain.startLifetime = Range(0.6f, 1f);
        emberMain.startSpeed = Range(0.8f, 1.6f);
        emberMain.startSize = Range(0.03f, 0.06f);
        Cone(embers, ScreenUp(), 30f, 0.25f);
        Wobble(embers, 0.6f, 2f);
        Fade(embers, Gradient(new[] { new Color(1f, 0.85f, 0.4f), BurnColor }, new[] { 1f, 0f }));
        if (loop) SetRate(embers, 8f); else Burst(embers, 12);
        embers.Play();

        Smoke(position + ScreenUp() * 0.2f, loop ? 0 : 3, 0.3f, flames.transform);

        if (loop)
        {
            // Light that stays and flickers while the card burns
            Light light = LightFlash(position, BurnColor, 1.2f, 1.5f, 0f);
            light.GetComponent<FadeLight>().flicker = 0.45f;
            light.transform.SetParent(flames.transform, true);
        }
        else LightFlash(position, BurnColor, 2.5f, 2f, 0.35f);

        return Emit(flames, loop, 18f, 30);
    }

    // Bubble rings that wobble, grow and pop, inside a drifting green mist
    public static GameObject Poison(Vector3 position, bool loop)
    {
        ParticleSystem bubbles = NewSystem(loop ? "Aura_Poison" : "Poison", position, Shape.Ring, null, false);
        ParticleSystem.MainModule main = bubbles.main;
        main.startLifetime = Range(0.8f, 1.3f);
        main.startSpeed = Range(0.05f, 0.15f);
        main.startSize = Range(0.12f, 0.24f);
        ScreenCircle(bubbles, 0.3f);
        Drift(bubbles, 0.25f);
        Fade(bubbles, Gradient(new[] { PoisonColor, PoisonPurple }, new[] { 0f, 1f, 1f, 0f }));
        SizeCurve(bubbles, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.85f, 1f), new Keyframe(1f, 1.4f))); // Pops at the end
        Wobble(bubbles, 0.15f, 1.5f);

        // Mist: big soft clouds, very faint
        ParticleSystem mist = NewSystem("Poison_Mist", position, Shape.Dot, bubbles.transform, false);
        ParticleSystem.MainModule mistMain = mist.main;
        mistMain.loop = loop;
        mistMain.startLifetime = Range(1.2f, 1.8f);
        mistMain.startSize = Range(0.5f, 0.8f);
        ScreenCircle(mist, 0.3f);
        Drift(mist, 0.12f);
        Wobble(mist, 0.1f, 0.5f);
        Fade(mist, Gradient(new[] { PoisonColor, PoisonPurple }, new[] { 0f, 0.25f, 0f }));
        if (loop) SetRate(mist, 2f); else Burst(mist, 5);
        mist.Play();

        if (!loop) LightFlash(position, PoisonColor, 1.5f, 1.8f, 0.4f);
        return Emit(bubbles, loop, 5f, 12);
    }

    // Glowing plus signs twinkling up the screen
    public static GameObject Regeneration(Vector3 position, bool loop)
    {
        ParticleSystem pluses = NewSystem(loop ? "Aura_Regeneration" : "Regeneration", position, Shape.Plus, null, true);
        ParticleSystem.MainModule main = pluses.main;
        main.startLifetime = Range(0.9f, 1.3f);
        main.startSize = Range(0.07f, 0.12f);
        ScreenCircle(pluses, 0.3f);
        Drift(pluses, 0.35f);
        Fade(pluses, Gradient(new[] { Color.white, HealColor }, new[] { 0f, 1f, 0.3f, 1f, 0.3f, 0f })); // Twinkle

        if (!loop)
        {
            Shockwave(position, HealColor, 1.2f, 0.6f, pluses.transform, true);
            LightFlash(position, HealColor, 2f, 2f, 0.4f);
        }
        return Emit(pluses, loop, 4f, 14);
    }

    // Glowing stars circling the card. faint = Stagger (fewer, dimmer, slower). Stun also gets a halo
    public static GameObject Stars(Vector3 position, bool loop, bool faint)
    {
        ParticleSystem stars = NewSystem(faint ? "Aura_Stagger" : loop ? "Aura_Stun" : "Stun", position, Shape.Star, null, true);
        ParticleSystem.MainModule main = stars.main;
        main.startLifetime = loop ? Range(1f, 1.2f) : Range(0.7f, 0.9f);
        main.startSize = faint ? Range(0.08f, 0.1f) : Range(0.14f, 0.2f);
        main.startColor = faint ? new Color(1f, 1f, 1f, 0.6f) : Color.white;

        ScreenCircle(stars, 0.35f);
        ParticleSystem.ShapeModule shape = stars.shape;
        shape.radiusThickness = 0f; // Start on the edge of the circle

        Swirl(stars, faint ? 2f : 4f, loop ? 0f : 0.8f);
        Spin(stars, 3f);
        Fade(stars, Gradient(new[] { Color.white, StunColor }, new[] { 0f, 1f, 0.6f, 1f, 0f }));

        if (!faint)
        {
            // Halo: a slowly turning rune ring that moves with the card
            ParticleSystem halo = NewSystem("Stun_Halo", position, Shape.Rune, stars.transform, true);
            ParticleSystem.MainModule haloMain = halo.main;
            haloMain.loop = loop;
            haloMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            haloMain.startLifetime = 1.4f;
            haloMain.startSize = 0.9f;
            if (loop) SetRate(halo, 1f); else Burst(halo, 1);
            Spin(halo, 1.5f);
            Fade(halo, Gradient(new[] { StunColor, StunColor }, new[] { 0f, 0.5f, 0.5f, 0f }));
            halo.Play();
        }
        if (!loop) LightFlash(position, StunColor, 2f, 2f, 0.35f);

        return Emit(stars, loop, faint ? 1.5f : 5f, 8);
    }

    // ---- Buff and debuff auras (looping) ----

    private static readonly Color AttackUpColor = new Color(1f, 0.55f, 0.15f);
    private static readonly Color AttackDownColor = new Color(0.75f, 0.15f, 0.2f);
    private static readonly Color DefenseUpColor = new Color(1f, 0.85f, 0.3f);
    private static readonly Color DefenseDownColor = new Color(0.6f, 0.6f, 0.65f);
    private static readonly Color SpeedUpColor = new Color(0.5f, 0.95f, 1f);
    private static readonly Color SpeedDownColor = new Color(0.3f, 0.45f, 0.9f);
    private static readonly Color HinderedColor = new Color(0.65f, 0.65f, 0.7f);
    private static readonly Color HealBlockColor = new Color(1f, 0.25f, 0.3f);

    // Glowing orange arrows rising up the screen
    public static GameObject AttackUp(Vector3 position)
    {
        return Arrows("Aura_AttackUp", position, AttackUpColor, 0.5f, true);
    }

    // Dark red arrows sinking down the screen
    public static GameObject AttackDown(Vector3 position)
    {
        return Arrows("Aura_AttackDown", position, AttackDownColor, -0.4f, false);
    }

    // Gold ring around the card that slowly pulses, with sparkles on its edge
    public static GameObject DefenseUp(Vector3 position)
    {
        ParticleSystem ring = ShieldRing("Aura_DefenseUp", position, DefenseUpColor, new[] { 0f, 0.7f, 0.7f, 0f }, true);

        ParticleSystem sparkles = NewSystem("Aura_DefenseUp_Sparkles", position, Shape.Star, ring.transform, true);
        ParticleSystem.MainModule main = sparkles.main;
        main.loop = true;
        main.startLifetime = Range(0.4f, 0.6f);
        main.startSize = Range(0.06f, 0.1f);
        ScreenCircle(sparkles, 0.55f);
        ParticleSystem.ShapeModule shape = sparkles.shape;
        shape.radiusThickness = 0f; // On the ring's edge
        SetRate(sparkles, 6f);
        Fade(sparkles, Gradient(new[] { Color.white, DefenseUpColor }, new[] { 0f, 1f, 0f }));
        sparkles.Play();

        return ring.gameObject;
    }

    // Grey ring that flickers like it's breaking, with shards falling off
    public static GameObject DefenseDown(Vector3 position)
    {
        ParticleSystem ring = ShieldRing("Aura_DefenseDown", position, DefenseDownColor, new[] { 0f, 0.6f, 0.1f, 0.5f, 0f, 0.4f, 0f }, false);

        ParticleSystem shards = NewSystem("Aura_DefenseDown_Shards", position, Shape.Shard, ring.transform, false);
        ParticleSystem.MainModule main = shards.main;
        main.loop = true;
        main.startLifetime = Range(0.7f, 1f);
        main.startSize = Range(0.06f, 0.1f);
        main.startRotation = Range(0f, Mathf.PI * 2f);
        ScreenCircle(shards, 0.55f);
        ParticleSystem.ShapeModule shape = shards.shape;
        shape.radiusThickness = 0f;
        Drift(shards, -0.4f);
        Spin(shards, 3f);
        SetRate(shards, 4f);
        Fade(shards, Gradient(new[] { DefenseDownColor, DefenseDownColor * 0.5f }, new[] { 1f, 0f }));
        shards.Play();

        return ring.gameObject;
    }

    // Glowing cyan speed lines streaking sideways across the card
    public static GameObject SpeedUp(Vector3 position)
    {
        ParticleSystem lines = NewSystem("Aura_SpeedUp", position, Shape.Dot, null, true);
        ParticleSystem.MainModule main = lines.main;
        main.loop = true;
        main.startLifetime = Range(0.15f, 0.3f);
        main.startSpeed = Range(2.5f, 3.5f);
        main.startSize = Range(0.03f, 0.05f);
        Cone(lines, ScreenRight(), 0f, 0.4f); // Lines start spread up and down the card

        ParticleSystemRenderer renderer = lines.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 4f;
        renderer.velocityScale = 0.05f;

        SetRate(lines, 14f);
        Fade(lines, Gradient(new[] { Color.white, SpeedUpColor }, new[] { 0f, 1f, 0f }));
        lines.Play();
        return lines.gameObject;
    }

    // Slow heavy blue drips sinking down
    public static GameObject SpeedDown(Vector3 position)
    {
        ParticleSystem drips = NewSystem("Aura_SpeedDown", position, Shape.Dot, null, false);
        ParticleSystem.MainModule main = drips.main;
        main.loop = true;
        main.startLifetime = Range(1f, 1.4f);
        main.startSize = Range(0.07f, 0.11f);
        ScreenCircle(drips, 0.3f);
        Drift(drips, -0.25f);
        SizeCurve(drips, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.6f)));
        SetRate(drips, 4f);
        Fade(drips, Gradient(new[] { SpeedDownColor, SpeedDownColor * 0.6f }, new[] { 0f, 1f, 0f }));
        drips.Play();
        return drips.gameObject;
    }

    // Grey chain links circling slowly around the card
    public static GameObject Hindered(Vector3 position)
    {
        ParticleSystem links = NewSystem("Aura_Hindered", position, Shape.Link, null, false);
        ParticleSystem.MainModule main = links.main;
        main.loop = true;
        main.startLifetime = Range(1.3f, 1.6f);
        main.startSize = Range(0.12f, 0.16f);
        main.startRotation = Range(0f, Mathf.PI * 2f);
        ScreenCircle(links, 0.45f);
        ParticleSystem.ShapeModule shape = links.shape;
        shape.radiusThickness = 0f;
        Swirl(links, 1.2f, 0f);
        Spin(links, 1f);
        SetRate(links, 6f);
        Fade(links, Gradient(new[] { HinderedColor, HinderedColor }, new[] { 0f, 1f, 1f, 0f }));
        links.Play();
        return links.gameObject;
    }

    // Glowing red X marks pulsing over the card
    public static GameObject HealBlock(Vector3 position)
    {
        ParticleSystem crosses = NewSystem("Aura_HealBlock", position, Shape.Cross, null, true);
        ParticleSystem.MainModule main = crosses.main;
        main.loop = true;
        main.startLifetime = Range(0.9f, 1.1f);
        main.startSize = Range(0.16f, 0.2f);
        ScreenCircle(crosses, 0.35f);
        SizeCurve(crosses, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.8f)));
        SetRate(crosses, 2.5f);
        Fade(crosses, Gradient(new[] { HealBlockColor, HealBlockColor }, new[] { 0f, 1f, 0f }));
        crosses.Play();
        return crosses.gameObject;
    }

    // Arrows drifting up (positive speed) or down (negative, flipped to point down)
    private static GameObject Arrows(string name, Vector3 position, Color color, float speed, bool glow)
    {
        ParticleSystem arrows = NewSystem(name, position, Shape.Arrow, null, glow);
        ParticleSystem.MainModule main = arrows.main;
        main.loop = true;
        main.startLifetime = Range(0.8f, 1.1f);
        main.startSize = Range(0.12f, 0.18f);
        if (speed < 0f) main.startRotation = Mathf.PI; // Point down
        ScreenCircle(arrows, 0.35f);
        Drift(arrows, speed);
        SetRate(arrows, speed > 0f ? 5f : 4f);
        Fade(arrows, Gradient(new[] { Color.white, color }, new[] { 0f, 1f, 1f, 0f }));
        arrows.Play();
        return arrows.gameObject;
    }

    // Big ring around the card that follows it, alpha keys set the pulse or flicker
    private static ParticleSystem ShieldRing(string name, Vector3 position, Color color, float[] alphas, bool glow)
    {
        ParticleSystem ring = NewSystem(name, position, Shape.Ring, null, glow);
        ParticleSystem.MainModule main = ring.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local; // Moves with the card
        main.startLifetime = 1.6f;
        main.startSize = 1.15f;
        SizeCurve(ring, new AnimationCurve(new Keyframe(0f, 0.95f), new Keyframe(1f, 1.05f)));
        SetRate(ring, 1.2f);
        Fade(ring, Gradient(new[] { color, color }, alphas));
        ring.Play();
        return ring;
    }

    // ---- Defeat ----

    // Bright flash, grey shards spinning out, a ring and a puff of smoke
    public static GameObject Shatter(Vector3 position)
    {
        ParticleSystem flash = Flash(position, Color.white, 1.4f, 0.15f, null);
        Transform root = flash.transform;

        ParticleSystem shards = NewSystem("Defeat_Shatter", position, Shape.Shard, root, false);
        ParticleSystem.MainModule main = shards.main;
        main.startLifetime = Range(0.6f, 1f);
        main.startSpeed = Range(1.2f, 3f);
        main.startSize = Range(0.07f, 0.15f);
        main.startRotation = Range(0f, Mathf.PI * 2f);
        ScreenCircle(shards, 0.2f);
        Burst(shards, 30);
        Spin(shards, 6f);
        Slow(shards, 0.15f);
        Fade(shards, Gradient(new[] { DefeatColor, new Color(0.35f, 0.35f, 0.35f) }, new[] { 1f, 1f, 0f }));
        shards.Play();

        Shockwave(position, Color.white, 1.8f, 0.4f, root, true);
        Smoke(position, 6, 0.5f, root);
        LightFlash(position, Color.white, 4f, 3f, 0.4f);
        return Finish(flash);
    }

    // ---- Projectiles ----

    // Glowing head with a smooth trail and sparkles behind. Physical is small and fast-looking, magic and heals are orbs.
    // CombatEffects moves it, then calls Stop
    public static GameObject Projectile(Vector3 position, DamageType type)
    {
        GameObject root = new GameObject("Projectile");
        root.transform.position = position;
        Color color = ColorFor(type);
        bool physical = type == DamageType.Physical;

        // Smooth trail
        TrailRenderer trail = root.AddComponent<TrailRenderer>();
        trail.sharedMaterial = GetMaterial(Shape.Dot, true);
        trail.time = physical ? 0.12f : 0.25f;
        trail.minVertexDistance = 0.02f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, physical ? 0.14f : 0.22f), new Keyframe(1f, 0f));
        trail.colorGradient = Gradient(new[] { Color.white, color }, new[] { 1f, 0f });

        // Bright core that stays on the projectile
        ParticleSystem core = NewSystem("Projectile_Core", position, Shape.Dot, root.transform, true);
        ParticleSystem.MainModule coreMain = core.main;
        coreMain.loop = true;
        coreMain.simulationSpace = ParticleSystemSimulationSpace.Local;
        coreMain.startLifetime = 0.08f;
        coreMain.startSize = physical ? 0.25f : 0.4f;
        coreMain.startColor = Color.Lerp(color, Color.white, 0.5f);
        SetRate(core, 60f);
        core.Play();

        // Sparkles left behind
        if (!physical)
        {
            ParticleSystem sparkles = NewSystem("Projectile_Trail", position, type == DamageType.Heal ? Shape.Plus : Shape.Star, root.transform, true);
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
        }
        return root;
    }

    // Stops a looping effect (aura or projectile) and removes it once its particles and trail fade
    public static void Stop(GameObject effect)
    {
        if (effect == null) return;
        effect.transform.SetParent(null, true);
        ParticleSystem[] systems = effect.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem particles in systems)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        foreach (TrailRenderer trail in effect.GetComponentsInChildren<TrailRenderer>())
            trail.emitting = false;
        foreach (Light light in effect.GetComponentsInChildren<Light>())
            Object.Destroy(light.gameObject);

        bool fades = systems.Length > 0 || effect.GetComponentInChildren<TrailRenderer>() != null;
        Object.Destroy(effect, fades ? 1.5f : 0f); // Nothing to fade otherwise
    }

    // ---- Lights ----

    // A point light that flashes and fades over 'duration' (0 = stays, for auras)
    public static Light LightFlash(Vector3 position, Color color, float intensity, float range, float duration)
    {
        GameObject lightObject = new GameObject("Effect_Light");
        lightObject.transform.position = position + Vector3.up * 0.4f; // A little above so it lights the card and board
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        FadeLight fade = lightObject.AddComponent<FadeLight>();
        fade.duration = duration > 0f ? duration : 1f;
        return light;
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

    // A stopped particle system so settings can be changed, then each effect plays it. glow = additive (bright, adds light)
    private static ParticleSystem NewSystem(string name, Vector3 position, Shape texture, Transform parent, bool glow)
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

        effectObject.GetComponent<ParticleSystemRenderer>().material = GetMaterial(texture, glow);
        return particles;
    }

    // Single quick bright dot
    private static ParticleSystem Flash(Vector3 position, Color color, float size, float lifetime, Transform parent)
    {
        ParticleSystem flash = NewSystem("Flash", position, Shape.Dot, parent, true);
        ParticleSystem.MainModule main = flash.main;
        main.startLifetime = lifetime;
        main.startSize = size;
        Burst(flash, 1);
        SizeCurve(flash, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(1f, 1.2f)));
        Fade(flash, Gradient(new[] { color, color }, new[] { 1f, 0f }));
        flash.Play();
        return flash;
    }

    // Expanding ring that fades out
    private static ParticleSystem Shockwave(Vector3 position, Color color, float size, float lifetime, Transform parent, bool glow)
    {
        ParticleSystem ring = NewSystem("Shockwave", position, Shape.Ring, parent, glow);
        ParticleSystem.MainModule main = ring.main;
        main.startLifetime = lifetime;
        main.startSize = size;
        Burst(ring, 1);
        SizeCurve(ring, new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 3f), new Keyframe(1f, 1f)));
        Fade(ring, Gradient(new[] { Color.white, color }, new[] { 1f, 0f }));
        ring.Play();
        return ring;
    }

    // Spinning rune circle (spin is radians per second, negative turns the other way)
    private static void Rune(Vector3 position, Color color, float size, float lifetime, float spin, Transform parent)
    {
        ParticleSystem rune = NewSystem("Rune", position, Shape.Rune, parent, true);
        ParticleSystem.MainModule main = rune.main;
        main.startLifetime = lifetime;
        main.startSize = size;
        Burst(rune, 1);
        ParticleSystem.RotationOverLifetimeModule rotation = rune.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = spin;
        SizeCurve(rune, new AnimationCurve(new Keyframe(0f, 0.4f, 0f, 2.5f), new Keyframe(1f, 1f)));
        Fade(rune, Gradient(new[] { Color.white, color }, new[] { 1f, 0.8f, 0f }));
        rune.Play();
    }

    // Glowing streaks spraying away from the attacker in a cone
    private static void Sparks(Vector3 position, Vector3 direction, Color from, Color to, int count, float minSpeed, float maxSpeed, Transform parent)
    {
        ParticleSystem sparks = NewSystem("Sparks", position, Shape.Dot, parent, true);
        ParticleSystem.MainModule main = sparks.main;
        main.startLifetime = Range(0.12f, 0.3f);
        main.startSpeed = Range(minSpeed, maxSpeed);
        main.startSize = Range(0.04f, 0.07f);
        Cone(sparks, direction, 35f, 0.05f);
        Burst(sparks, count);
        Slow(sparks, 0.2f);
        Fade(sparks, Gradient(new[] { from, to, to * 0.6f }, new[] { 1f, 1f, 0f }));

        ParticleSystemRenderer renderer = sparks.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch; // Streaks along their direction
        renderer.lengthScale = 2.5f;
        renderer.velocityScale = 0.06f;
        sparks.Play();
    }

    // Small chunks tumbling away from the hit
    private static void Debris(Vector3 position, Vector3 direction, Color color, int count, Transform parent)
    {
        ParticleSystem debris = NewSystem("Debris", position, Shape.Shard, parent, false);
        ParticleSystem.MainModule main = debris.main;
        main.startLifetime = Range(0.4f, 0.6f);
        main.startSpeed = Range(1.5f, 3f);
        main.startSize = Range(0.04f, 0.08f);
        main.startRotation = Range(0f, Mathf.PI * 2f);
        Cone(debris, direction, 45f, 0.05f);
        Burst(debris, count);
        Spin(debris, 8f);
        Slow(debris, 0.2f);
        Fade(debris, Gradient(new[] { color, color }, new[] { 1f, 0f }));
        debris.Play();
    }

    // Soft grey puffs drifting up and fading (count 0 = keep emitting, for auras)
    private static void Smoke(Vector3 position, int count, float size, Transform parent)
    {
        ParticleSystem smoke = NewSystem("Smoke", position, Shape.Dot, parent, false);
        ParticleSystem.MainModule main = smoke.main;
        main.loop = count == 0;
        main.startLifetime = Range(0.7f, 1.1f);
        main.startSize = Range(size * 0.7f, size);
        main.startRotation = Range(0f, Mathf.PI * 2f);
        ScreenCircle(smoke, 0.15f);
        Drift(smoke, 0.3f);
        Wobble(smoke, 0.15f, 1f);
        SizeCurve(smoke, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.5f)));
        Fade(smoke, Gradient(new[] { SmokeColor, SmokeColor }, new[] { 0f, 0.35f, 0f }));
        if (count == 0) SetRate(smoke, 3f); else Burst(smoke, count);
        smoke.Play();
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
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count)) });
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

    // Emits in a cone pointing along a direction (e.g. away from the attacker)
    private static void Cone(ParticleSystem particles, Vector3 direction, float angle, float radius)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = ScreenUp();
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = angle;
        shape.radius = radius;
        shape.rotation = Quaternion.LookRotation(direction.normalized).eulerAngles;
    }

    // Constant drift up the screen (negative = down)
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

    // Particles slow down over their life (drag)
    private static void Slow(ParticleSystem particles, float dampen)
    {
        ParticleSystem.LimitVelocityOverLifetimeModule limit = particles.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 0f;
        limit.dampen = dampen;
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
    private static Vector3 ScreenRight() => Camera.main != null ? Camera.main.transform.right : Vector3.right;

    // Particle rotation (radians) that turns a texture's "up" to face a world direction on screen
    private static float ScreenAngle(Vector3 direction)
    {
        float x = Vector3.Dot(direction, ScreenRight());
        float y = Vector3.Dot(direction, ScreenUp());
        return Mathf.PI / 2f - Mathf.Atan2(y, x);
    }

    // ---- Materials and textures ----

    // Normal (Sprites/Default, always in builds) or glow (additive, Resources/VFX/VFX_Glow). Glow falls back to normal if missing
    private static Material GetMaterial(Shape shape, bool glow)
    {
        if (materials.TryGetValue((shape, glow), out Material existing) && existing != null) return existing;

        Material baseMaterial = glow ? Resources.Load<Material>("VFX/VFX_Glow") : null;
        Material material = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Sprites/Default"));
        Texture2D texture = GetTexture(shape);
        material.mainTexture = texture;
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);

        materials[(shape, glow)] = material;
        return material;
    }

    private static Texture2D GetTexture(Shape shape)
    {
        if (textures.TryGetValue(shape, out Texture2D existing) && existing != null) return existing;
        Texture2D texture = MakeTexture(shape);
        textures[shape] = texture;
        return texture;
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

            case Shape.Arrow: // Chevron pointing up
                float chevron = Mathf.Abs(y - (0.35f - Mathf.Abs(x)));
                return Mathf.Clamp01((0.16f - chevron) * 14f) * Mathf.Clamp01((0.65f - Mathf.Abs(x)) * 14f);

            case Shape.Cross: // X
                float stroke = Mathf.Min(Mathf.Abs(x - y), Mathf.Abs(x + y));
                return Mathf.Clamp01((0.2f - stroke) * 12f) * Mathf.Clamp01((0.8f - distance) * 10f);

            case Shape.Link: // Chain link (tall oval ring)
                float oval = Mathf.Sqrt((x / 0.45f) * (x / 0.45f) + (y / 0.8f) * (y / 0.8f));
                float edge = (oval - 0.8f) / 0.16f;
                return Mathf.Exp(-edge * edge);

            case Shape.Slash: // Crescent bulging up, thick in the middle and tapering to points
                float outer = Mathf.Clamp01((0.9f - distance) * 15f);
                float inner = Mathf.Clamp01((Mathf.Sqrt(x * x + (y + 0.3f) * (y + 0.3f)) - 0.85f) * 15f);
                return outer * inner * Mathf.Clamp01((y + 0.2f) * 2.5f);

            case Shape.Rune: // Ring with tick marks and a thin inner ring
                float outerRing = Mathf.Exp(-Mathf.Pow((distance - 0.8f) / 0.05f, 2f));
                float innerRing = 0.6f * Mathf.Exp(-Mathf.Pow((distance - 0.5f) / 0.03f, 2f));
                float angle = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
                float tickAngle = Mathf.Abs(Mathf.Repeat(angle + 22.5f, 45f) - 22.5f);
                float ticks = distance > 0.58f && distance < 0.72f ? Mathf.Clamp01(1f - tickAngle / 4f) : 0f;
                return Mathf.Clamp01(outerRing + innerRing + ticks);

            default: // Soft dot
                float dot = Mathf.Clamp01(1f - distance);
                return dot * dot;
        }
    }
}
