using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The Inked Star Chart attack effects. Each VfxTheme has its own wind-up, projectile, impact and comic words,
// signature moves get one-off effects on top, and status effects get ink-style auras. Every effect follows
// anticipation -> impact -> quick fade: the impact frame is the biggest and most contrasting, then it erodes away.
// Built from InkFx layers (Kenney CC0 shapes + the ink particle shader). CombatEffects calls these.
public static class ThemedEffects
{
    // ---- Palette per theme ----

    private struct Style
    {
        public Color main, accent;
        public string[] words;
        public Style(Color main, Color accent, params string[] words) { this.main = main; this.accent = accent; this.words = words; }
    }

    private static Style StyleOf(VfxTheme theme)
    {
        switch (theme)
        {
            case VfxTheme.Slash:  return new Style(new Color(0.94f, 0.97f, 1f), new Color(0.55f, 0.85f, 1f), "SLASH!", "SHNK!", "SWISH!");
            case VfxTheme.Claw:   return new Style(new Color(1f, 0.93f, 0.85f), new Color(1f, 0.35f, 0.3f), "RRIP!", "SCRITCH!", "SLASH!");
            case VfxTheme.Blunt:  return new Style(new Color(1f, 0.62f, 0.25f), new Color(1f, 0.87f, 0.35f), "BONK!", "WHAM!", "POW!");
            case VfxTheme.Pierce: return new Style(new Color(0.96f, 0.9f, 0.75f), new Color(1f, 0.72f, 0.3f), "THWIP!", "SHUNK!");
            case VfxTheme.Ice:    return new Style(new Color(0.72f, 0.93f, 1f), new Color(1f, 1f, 1f), "CRACK!", "KSSH!", "FREEZE!");
            case VfxTheme.Water:  return new Style(new Color(0.3f, 0.62f, 1f), new Color(0.68f, 0.92f, 1f), "SPLOOSH!", "SPLASH!");
            case VfxTheme.Wind:   return new Style(new Color(0.78f, 1f, 0.86f), new Color(1f, 1f, 1f), "WHOOSH!", "FWOOM!");
            case VfxTheme.Light:  return new Style(new Color(1f, 0.93f, 0.55f), new Color(1f, 1f, 0.92f), "SHING!", "FLASH!", "GLEAM!");
            case VfxTheme.Lunar:  return new Style(new Color(0.66f, 0.58f, 1f), new Color(1f, 0.89f, 0.55f), "ZWOOM!", "TWINKLE!", "MEOW!");
            case VfxTheme.Nature: return new Style(new Color(1f, 0.56f, 0.8f), new Color(0.55f, 0.92f, 0.45f), "BLOOM!", "FWIP!");
            case VfxTheme.Tech:   return new Style(new Color(0.45f, 0.95f, 1f), new Color(1f, 0.85f, 0.3f), "BZZT!", "KRZZT!", "VRRR!");
            case VfxTheme.Trick:  return new Style(new Color(0.82f, 0.5f, 1f), new Color(1f, 0.86f, 0.35f), "TA-DA!", "POOF!", "ZAP!");
            case VfxTheme.Food:   return new Style(new Color(1f, 0.76f, 0.42f), new Color(0.5f, 1f, 0.6f), "NOM!", "YUM!", "MUNCH!");
            case VfxTheme.Rally:  return new Style(new Color(1f, 0.45f, 0.3f), new Color(1f, 0.86f, 0.35f), "RAWR!", "HUAH!", "GRR!");
            case VfxTheme.Gas:    return new Style(new Color(0.7f, 0.93f, 0.48f), new Color(0.84f, 0.6f, 1f), "PFFT!", "WHIFF!");
            default:              return new Style(Color.white, InkArt.GoldLight, "POW!");
        }
    }

    // Glowing themes keep a soft bright core on top of the ink look
    private static InkFx.Look Magic(VfxTheme theme) =>
        theme == VfxTheme.Light || theme == VfxTheme.Lunar || theme == VfxTheme.Trick || theme == VfxTheme.Tech ? InkFx.Look.InkGlow : InkFx.Look.Ink;

    // ---- Positions ----

    // A point on a unit: 0 = its feet, 1 = the top of its token. A little toward the camera so effects sit in front of it
    public static Vector3 OnUnit(CardUnit unit, float height)
    {
        if (unit == null) return Vector3.zero;
        Vector3 toCamera = Camera.main != null ? -Camera.main.transform.forward : Vector3.up;
        if (UnitTokens.IsToken(unit))
            return Vector3.Lerp(UnitTokens.Feet(unit), UnitTokens.Top(unit), height) + toCamera * 0.12f;
        return unit.transform.position + Vector3.up * (0.05f + 0.35f * height) + toCamera * 0.05f;
    }

    static Vector3 ScreenUp => BoardView.CameraRotation * Vector3.up;
    static Vector3 ScreenRight => BoardView.CameraRotation * Vector3.right;

    // Angle of a world direction as seen on screen (degrees, 0 = right, 90 = up)
    static float ScreenAngle(Vector3 direction)
    {
        return Mathf.Atan2(Vector3.Dot(direction, ScreenUp), Vector3.Dot(direction, ScreenRight)) * Mathf.Rad2Deg;
    }

    static string Pick(params string[] options) => options[Random.Range(0, options.Length)];

    // ---- Wind-up (ranged, magic and self attacks) ----

    // Theme-coloured bits gather in toward the attacker while a ring tightens, then the attack fires
    public static void Charge(VfxTheme theme, Vector3 position, float duration)
    {
        Style style = StyleOf(theme);
        InkFx.Layer("star_04").As(InkFx.Look.Glow).Count(10).Spread(0.55f).Speed(-2.2f, -1.6f).Size(0.08f, 0.14f)
             .Life(duration * 0.8f, duration).Tint(style.accent, style.main).Play(position);
        InkFx.Layer("circle_02").As(InkFx.Look.Ink).Count(1).Size(0.9f, 0.9f).Grow(0.25f).Life(duration, duration)
             .Tint(style.main).Upright().Ink(0.06f).Play(position);
        if (theme == VfxTheme.Rally)
            InkFx.Layer("fire_01").As(InkFx.Look.Glow).Count(1).Size(0.6f, 0.6f).Grow(1.4f).Life(duration, duration).Tint(style.main).Play(position);
    }

    // ---- Projectiles ----

    // A moving object with a core that travels with it and a trail it leaves behind. CombatEffects moves it.
    // Returns null when the signature draws its own travel (Lunar Beam, Sweeping Tide)
    public static GameObject Projectile(VfxTheme theme, VfxSignature signature, Vector3 start)
    {
        if (signature == VfxSignature.LunarBeam || signature == VfxSignature.SweepingTide) return null;
        Style style = StyleOf(theme);
        GameObject group = InkFx.Group("Projectile " + theme, start, 6f);
        Transform parent = group.transform;

        if (signature == VfxSignature.BuzzsawSpinner)
        {
            Shapes.Ensure();
            InkFx.Layer("gen_saw").As(InkFx.Look.InkGlow).Looping(14f).Local().Size(0.55f, 0.55f).Life(0.12f, 0.12f).Spin(1400f, 1600f).Tint(new Color(0.75f, 0.85f, 0.95f)).Play(start, parent);
            InkFx.Layer("trace_02").As(InkFx.Look.Glow).Looping(40f).Size(0.05f, 0.08f).Speed(2f, 4f).Life(0.15f, 0.25f).Streak(0.08f).Tint(style.accent).Play(start, parent);
            return group;
        }

        switch (theme)
        {
            case VfxTheme.Lunar:
                Core("slash_03", InkFx.Look.InkGlow, 0.5f, style.main, 600f, start, parent);
                Trail("star_04", InkFx.Look.Glow, 0.1f, style.accent, 30f, start, parent);
                break;
            case VfxTheme.Ice:
                Core("flame_05", InkFx.Look.Ink, 0.4f, style.main, 0f, start, parent);
                Trail("whitePuff02", InkFx.Look.Ink, 0.14f, style.main, 24f, start, parent);
                break;
            case VfxTheme.Water:
                Core("circle_05", InkFx.Look.Flat, 0.4f, style.main, 0f, start, parent);
                Trail("circle_05", InkFx.Look.Flat, 0.08f, style.accent, 30f, start, parent);
                break;
            case VfxTheme.Wind:
                Core("twirl_01", InkFx.Look.Ink, 0.5f, style.main, 900f, start, parent);
                Trail("trace_03", InkFx.Look.Glow, 0.12f, style.accent, 26f, start, parent);
                break;
            case VfxTheme.Light:
                Core("star_06", InkFx.Look.Glow, 0.6f, style.main, 200f, start, parent);
                Trail("star_04", InkFx.Look.Glow, 0.1f, style.accent, 36f, start, parent);
                break;
            case VfxTheme.Tech:
                Core("spark_02", InkFx.Look.Glow, 0.55f, style.main, 700f, start, parent);
                Trail("trace_02", InkFx.Look.Glow, 0.07f, style.accent, 30f, start, parent);
                break;
            case VfxTheme.Trick:
                Core("symbol_02", InkFx.Look.InkGlow, 0.38f, style.accent, 500f, start, parent);
                Trail("star_04", InkFx.Look.Glow, 0.09f, style.main, 30f, start, parent);
                break;
            case VfxTheme.Nature:
                Core("flame_06", InkFx.Look.Ink, 0.32f, style.main, 700f, start, parent);
                Trail("flame_05", InkFx.Look.Ink, 0.09f, style.accent, 18f, start, parent);
                break;
            case VfxTheme.Gas:
                Core("whitePuff04", InkFx.Look.Ink, 0.4f, style.main, 120f, start, parent);
                Trail("whitePuff01", InkFx.Look.Ink, 0.18f, style.accent, 18f, start, parent);
                break;
            case VfxTheme.Food:
                Core("symbol_01", InkFx.Look.Ink, 0.32f, style.accent, 300f, start, parent);
                Trail("star_04", InkFx.Look.Glow, 0.08f, style.main, 24f, start, parent);
                break;
            default: // Slash, Claw, Blunt, Pierce, Rally: a solid inked bolt with a dusty trail
                Core("star_09", InkFx.Look.InkGlow, 0.38f, style.main, 300f, start, parent);
                Trail("whitePuff03", InkFx.Look.Ink, 0.14f, style.main, 18f, start, parent);
                break;
        }
        return group;
    }

    static void Core(string texture, InkFx.Look look, float size, Color colour, float spin, Vector3 at, Transform parent)
    {
        InkFx layer = InkFx.Layer(texture).As(look).Looping(16f).Local().Size(size, size).Life(0.12f, 0.12f).Tint(colour).Smooth();
        if (spin > 0f) layer.Spin(spin * 0.9f, spin * 1.1f);
        layer.Play(at, parent);
    }

    static void Trail(string texture, InkFx.Look look, float size, Color colour, float rate, Vector3 at, Transform parent)
    {
        InkFx.Layer(texture).As(look).Looping(rate).Size(size * 0.7f, size).Speed(0.1f, 0.4f).Life(0.25f, 0.4f).Grow(0.4f)
             .Spin(-200f, 200f).Tint(colour).Play(at, parent);
    }

    // ---- Impacts ----

    // The hit itself, on one unit. k = 0.6 for small hits up to 1.6 for big ones. dir = from the attacker to the target
    public static void Impact(VfxTheme theme, Vector3 p, Vector3 dir, float k, bool heal)
    {
        Style s = StyleOf(theme);
        Vector3 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : ScreenUp;
        float angle = ScreenAngle(d);

        switch (theme)
        {
            case VfxTheme.Slash:
                // Two crossing blade arcs, a glint and sparks thrown along the swing
                Arc("slash_03", p, angle + 35f, 0.95f * k, s.main);
                Arc("slash_03", p, angle - 35f, 0.85f * k, s.accent, 0.05f);
                Flash(p, s.accent, 0.6f * k);
                Sparks(p, d, s.main, Mathf.RoundToInt(8 * k));
                Puffs(p, s.main, 3, 0.25f * k);
                break;

            case VfxTheme.Claw:
                // Three raking claw marks, torn red flecks
                InkFx.Layer("scratch_01").As(InkFx.Look.Ink).Count(1).Size(1.0f * k, 1.0f * k).PopIn(0.7f).Grow(1.1f).Life(0.3f, 0.32f)
                     .Rotated(angle + Random.Range(-20f, 20f)).Tint(s.main).Play(p);
                Chunks(p, d, s.accent, Mathf.RoundToInt(7 * k), "dirt_01", 0.05f, 0.09f);
                Flash(p, s.accent, 0.45f * k);
                Puffs(p, s.main, 3, 0.22f * k);
                break;

            case VfxTheme.Blunt:
                // Big impact star, a shockwave, a ring of dust and debris, and a dent on the board
                Star(p, s.accent, 0.8f * k);
                Ring(p, s.main, 1.1f * k, 0.3f);
                Puffs(p, new Color(1f, 0.95f, 0.88f), Mathf.RoundToInt(6 * k), 0.3f * k);
                Chunks(p, d, new Color(0.7f, 0.55f, 0.4f), Mathf.RoundToInt(7 * k), "dirt_02", 0.07f, 0.13f);
                InkFx.Layer("scorch_02").As(InkFx.Look.Flat).OnBoard().Count(1).Size(0.7f * k, 0.7f * k).PopIn(0.5f).Grow(1.15f).Life(0.8f, 0.9f)
                     .Tint(new Color(0.12f, 0.08f, 0.2f)).Play(p - Vector3.up * 0.3f);
                break;

            case VfxTheme.Pierce:
                // A streak straight through, a burst out the far side
                InkFx.Layer("trace_05").As(InkFx.Look.InkGlow).Count(1).Size(1.2f * k, 1.2f * k).Life(0.18f, 0.2f).Grow(1.2f)
                     .Rotated(angle - 90f).Tint(s.main).Play(p);
                Sparks(p, d, s.accent, Mathf.RoundToInt(10 * k));
                Ring(p, s.main, 0.6f * k, 0.22f);
                break;

            case VfxTheme.Ice:
                // Shards burst out, frost puffs, a cold glint
                InkFx.Layer("flame_05").As(InkFx.Look.Ink).Count(Mathf.RoundToInt(9 * k)).Toward(d + ScreenUp * 0.6f, 60f).Speed(2.5f, 5f)
                     .Gravity(0.9f).Drag(0.3f).Size(0.12f, 0.22f).Life(0.45f, 0.65f).Spin(-500f, 500f).Tint(s.main, s.accent).Play(p);
                Puffs(p, new Color(0.85f, 0.95f, 1f), 5, 0.28f * k);
                Ring(p, s.main, 0.9f * k, 0.28f);
                InkFx.Layer("star_07").As(InkFx.Look.Glow).Count(1).Size(0.8f * k, 0.8f * k).PopIn(0.6f).Life(0.2f, 0.22f).Rotated(15f).Tint(s.accent).Play(p);
                break;

            case VfxTheme.Water:
                // A splash crown, droplets falling back down, and a ripple on the board
                InkFx.Layer("muzzle_02").As(InkFx.Look.Ink).Count(5).Toward(ScreenUp, 55f).Speed(1.4f, 2.4f).Gravity(1f).Size(0.3f * k, 0.45f * k)
                     .Life(0.35f, 0.45f).Tint(s.main, s.accent).Play(p);
                InkFx.Layer("circle_05").As(InkFx.Look.Flat).Count(Mathf.RoundToInt(12 * k)).Toward(ScreenUp, 70f).Speed(2f, 4f).Gravity(2.2f)
                     .Size(0.05f, 0.1f).Life(0.4f, 0.6f).Tint(s.accent).Play(p);
                InkFx.Layer("circle_02").As(InkFx.Look.Ink).OnBoard().Count(2).Size(0.4f, 0.5f).Grow(3f).Life(0.5f, 0.6f).Tint(s.main).Ink(0.06f).Play(p - Vector3.up * 0.3f);
                break;

            case VfxTheme.Wind:
                // Spinning gusts, fast streaks blowing past, a leaf or two
                InkFx.Layer("twirl_02").As(InkFx.Look.Ink).Count(2).Size(0.6f * k, 0.8f * k).PopIn(0.5f).Grow(1.5f).Spin(500f, 800f).Life(0.35f, 0.4f).Tint(s.main).Play(p);
                InkFx.Layer("trace_06").As(InkFx.Look.Glow).Count(Mathf.RoundToInt(8 * k)).Toward(d, 30f).Spread(0.3f).Speed(5f, 8f).Size(0.06f, 0.1f)
                     .Streak(0.08f).Life(0.15f, 0.25f).Tint(s.accent).Play(p - d * 0.4f);
                InkFx.Layer("flame_05").As(InkFx.Look.Ink).Count(3).Toward(d + ScreenUp, 60f).Speed(1.5f, 3f).Size(0.08f, 0.12f).Spin(-600f, 600f)
                     .Gravity(0.4f).Life(0.5f, 0.7f).Tint(new Color(0.5f, 0.85f, 0.4f)).Play(p);
                break;

            case VfxTheme.Light:
                // A blinding star, a spinning compass glint and scattered sparkles
                InkFx.Layer("light_01").As(InkFx.Look.Glow).Count(1).Size(1.3f * k, 1.3f * k).Life(0.2f, 0.25f).Grow(1.3f).Tint(s.main).Smooth().Play(p);
                InkFx.Layer("magic_03").As(InkFx.Look.InkGlow).Count(1).Size(0.9f * k, 0.9f * k).PopIn(0.6f).Spin(80f, 120f).Life(0.4f, 0.45f).Tint(s.main).Play(p);
                Sparkles(p, s.accent, Mathf.RoundToInt(10 * k), 0.3f);
                break;

            case VfxTheme.Lunar:
                // A crescent cut, a spinning star rune and gold twinkles
                Arc("slash_03", p, angle + 90f, 0.9f * k, s.main);
                InkFx.Layer("magic_02").As(InkFx.Look.InkGlow).Count(1).Size(0.95f * k, 0.95f * k).PopIn(0.5f).Spin(120f, 160f).Life(0.45f, 0.5f).Tint(s.main).Play(p);
                Sparkles(p, s.accent, Mathf.RoundToInt(10 * k), 0.35f);
                Flash(p, s.main, 0.7f * k);
                break;

            case VfxTheme.Nature:
                // A burst of petals and leaves, with a bloom ring
                Petals(p, d, s.main, Mathf.RoundToInt(10 * k));
                InkFx.Layer("flame_05").As(InkFx.Look.Ink).Count(4).Toward(ScreenUp, 80f).Speed(1.2f, 2.5f).Gravity(0.3f).Size(0.08f, 0.12f)
                     .Spin(-400f, 400f).Life(0.5f, 0.7f).Tint(s.accent).Play(p);
                Ring(p, s.main, 0.8f * k, 0.3f);
                break;

            case VfxTheme.Tech:
                // Electric crackle, lightning forks, metal bits and a digital grid flash
                InkFx.Layer("spark_02").As(InkFx.Look.Glow).Count(2).Size(0.7f * k, 0.9f * k).Life(0.15f, 0.2f).Tint(s.main).Play(p);
                InkFx.Layer("spark_05").As(InkFx.Look.Glow).Count(3).Size(0.5f, 0.7f).Life(0.12f, 0.18f).Tint(s.main).Play(p);
                InkFx.Layer("window_04").As(InkFx.Look.InkGlow).Count(1).Size(0.6f * k, 0.6f * k).PopIn(0.6f).Life(0.2f, 0.22f).Rotated(45f).Tint(s.main).Play(p);
                Chunks(p, d, new Color(0.62f, 0.66f, 0.72f), Mathf.RoundToInt(6 * k), "dirt_03", 0.06f, 0.1f);
                Sparks(p, d, s.accent, Mathf.RoundToInt(10 * k));
                break;

            case VfxTheme.Trick:
                // POOF! A puff of smoke, a magic rune and a shower of gold stars
                Puffs(p, new Color(1f, 0.95f, 1f), 6, 0.32f * k);
                InkFx.Layer("magic_05").As(InkFx.Look.InkGlow).Count(1).Size(0.8f * k, 0.8f * k).PopIn(0.6f).Life(0.25f, 0.3f).Tint(s.main).Play(p);
                InkFx.Layer("symbol_02").As(InkFx.Look.Ink).Count(Mathf.RoundToInt(8 * k)).Toward(ScreenUp, 75f).Speed(1.5f, 3f).Gravity(0.7f)
                     .Size(0.08f, 0.14f).Spin(-360f, 360f).Life(0.5f, 0.7f).Tint(s.accent).Play(p);
                break;

            case VfxTheme.Food:
                // Crumbs and a happy burst (Gourmand's heals add hearts below)
                Chunks(p, ScreenUp, new Color(0.85f, 0.65f, 0.4f), 8, "dirt_01", 0.04f, 0.07f);
                Star(p, s.main, 0.5f * k);
                break;

            case VfxTheme.Rally:
                // A roar: shockwaves and a flare of fighting spirit
                Ring(p, s.main, 1.2f * k, 0.3f);
                Ring(p, s.accent, 0.9f * k, 0.3f, 0.08f);
                InkFx.Layer("fire_01").As(InkFx.Look.Glow).Count(1).Size(0.9f * k, 0.9f * k).Life(0.3f, 0.35f).Grow(1.4f).Tint(s.main).Play(p);
                InkFx.Layer("trace_04").As(InkFx.Look.Glow).Count(8).Toward(ScreenUp, 35f).Spread(0.3f).Speed(3f, 5f).Size(0.06f, 0.09f).Streak(0.07f).Life(0.2f, 0.3f).Tint(s.accent).Play(p);
                break;

            case VfxTheme.Gas:
                // A cloud that hangs and drifts, with bubbles
                InkFx.Layer("whitePuff07").As(InkFx.Look.Ink).Count(Mathf.RoundToInt(7 * k)).Spread(0.2f).Speed(0.3f, 0.8f).Drag(0.3f).Size(0.3f, 0.5f)
                     .PopIn(0.5f).Grow(1.8f).Life(0.9f, 1.3f).Spin(-40f, 40f).Tint(s.main, s.accent).Play(p);
                InkFx.Layer("circle_05").As(InkFx.Look.Flat).Count(6).Toward(ScreenUp, 60f).Speed(0.5f, 1f).Size(0.05f, 0.09f).Life(0.7f, 1f).Tint(s.accent).Play(p);
                break;

            default:
                Star(p, s.accent, 0.6f * k);
                Puffs(p, s.main, 4, 0.25f * k);
                break;
        }

        // A matching Cartoon FX accent on top (when the pack is set up)
        switch (theme)
        {
            case VfxTheme.Slash:  Cfx(x => x.slashCross, p, 0.8f * k); break;
            case VfxTheme.Claw:   Cfx(x => x.redHit, p, 0.7f * k); break;
            case VfxTheme.Blunt:  Cfx(x => x.groundHit, p - Vector3.up * 0.3f, 0.9f * k); Cfx(x => x.yellowHit, p, 0.55f * k); break;
            case VfxTheme.Pierce: Cfx(x => x.miscHit, p, 0.6f * k); break;
            case VfxTheme.Ice:    Cfx(x => x.iceHit, p, 0.8f * k); break;
            case VfxTheme.Water:  Cfx(x => x.waterSplash, p - Vector3.up * 0.2f, 0.8f * k); break;
            case VfxTheme.Light:  Cfx(x => x.lightHit, p, 0.8f * k); break;
            case VfxTheme.Lunar:  Cfx(x => x.glowingImpact, p, 0.6f * k); break;
            case VfxTheme.Nature: Cfx(x => x.leavesHit, p, 0.8f * k); break;
            case VfxTheme.Tech:   Cfx(x => x.electricHit, p, 0.8f * k); break;
            case VfxTheme.Trick:  Cfx(x => x.magicPoof, p, 0.9f * k); break;
            case VfxTheme.Rally:  Cfx(x => x.miscHit, p, 0.7f * k); break;
            case VfxTheme.Gas:    Cfx(x => x.poisonCloud, p, 0.7f * k); break;
        }

        if (heal) HealBurst(p, k);
    }

    // Area attacks: a ring across the ground the size of the area, plus dust thrown out along it
    public static void AreaBurst(VfxTheme theme, Vector3 centre, int areaSize)
    {
        Style s = StyleOf(theme);
        float size = (areaSize * 2f + 1f) * 0.9f;
        Vector3 ground = centre - Vector3.up * 0.3f;
        InkFx.Layer("circle_02").As(Magic(theme)).OnBoard().Count(1).Size(size * 0.3f, size * 0.3f).Grow(3.3f).Life(0.45f, 0.5f).Tint(s.main).Ink(0.05f).Play(ground);
        InkFx.Layer("whitePuff05").As(InkFx.Look.Ink).OnBoard().Count(10).Spread(size * 0.15f).Speed(size * 0.6f, size * 0.9f).Drag(0.5f)
             .Size(0.25f, 0.4f).Grow(1.6f).Life(0.4f, 0.55f).Tint(Color.Lerp(s.main, Color.white, 0.5f)).Play(ground);
    }

    // ---- Comic words ----

    // A big inked word on the main hit, chosen from the theme's words (or given). Bigger for bigger hits
    public static void Word(VfxTheme theme, Vector3 position, float k, string word = null)
    {
        Style s = StyleOf(theme);
        ComicWord.Show(word ?? Pick(s.words), s.accent, position, Mathf.Lerp(0.8f, 1.35f, Mathf.InverseLerp(0.6f, 1.6f, k)));
    }

    // ---- Signatures ----

    // Extra one-off effects for big moves, timed so their main moment lands at 'impact' seconds from now.
    // runner plays the timed parts (CombatEffects passes itself)
    public static void Signature(VfxSignature signature, VfxTheme theme, CardUnit attacker, CardUnit target, float impact, MonoBehaviour runner)
    {
        if (signature == VfxSignature.None || attacker == null) return;
        Style s = StyleOf(theme);
        Vector3 from = OnUnit(attacker, 0.5f);
        Vector3 at = target != null ? OnUnit(target, 0.5f) : from;
        Vector3 ground = target != null ? UnitTokens.Feet(target) : UnitTokens.Feet(attacker);

        // Big Cartoon FX moments for some signatures (when the pack is set up)
        switch (signature)
        {
            case VfxSignature.Starfall:          runner.StartCoroutine(CfxLater(x => x.fallingStars, ground + Vector3.up * 0.2f, 1.2f, Mathf.Max(0f, impact - 0.4f))); break;
            case VfxSignature.GlacialShatter:    runner.StartCoroutine(CfxLater(x => x.iceCross, at, 1.2f, impact)); break;
            case VfxSignature.BuzzsawSpinner:    runner.StartCoroutine(CfxLater(x => x.sparksRain, at, 0.8f, impact)); break;
            case VfxSignature.RadiantPunishment: runner.StartCoroutine(CfxLater(x => x.lightHit, at, 1.5f, impact)); break;
            case VfxSignature.AllInSlam:         runner.StartCoroutine(CfxLater(x => x.groundHit, ground, 1.6f, impact)); break;
            case VfxSignature.Roar:              Cfx(x => x.miscHit, from, 1.2f); break;
            case VfxSignature.SweepingTide:      runner.StartCoroutine(CfxLater(x => x.waterSplash, at, 1.3f, impact)); break;
            case VfxSignature.GardeniaBloom:     runner.StartCoroutine(CfxLater(x => x.leavesHit, from, 1.3f, impact)); break;
        }

        switch (signature)
        {
            case VfxSignature.Starfall:
                // Five falling stars streak down onto the area and burst on landing
                for (int i = 0; i < 5; i++)
                {
                    Vector3 landing = ground + new Vector3(Random.Range(-0.9f, 0.9f), 0.2f, Random.Range(-0.9f, 0.9f));
                    runner.StartCoroutine(Meteor(landing, s, Mathf.Max(0f, impact - 0.35f + i * 0.07f)));
                }
                break;

            case VfxSignature.LunarBeam:
                // A beam of moonlight from the attacker to the target, drawn as a quick run of glowing blobs
                runner.StartCoroutine(Beam(from, at, s.main, s.accent, Mathf.Max(0f, impact - 0.15f)));
                break;

            case VfxSignature.GlacialShatter:
                InkFx.Layer("flame_05").As(InkFx.Look.Ink).After(impact).Count(18).Speed(3f, 6f).Gravity(1f).Drag(0.3f).Size(0.18f, 0.32f)
                     .Life(0.5f, 0.8f).Spin(-500f, 500f).Tint(s.main, s.accent).Play(at);
                InkFx.Layer("whitePuff05").As(InkFx.Look.Ink).OnBoard().After(impact).Count(8).Spread(0.2f).Speed(1.5f, 2.5f).Drag(0.5f)
                     .Size(0.35f, 0.5f).Grow(1.6f).Life(0.8f, 1f).Tint(new Color(0.85f, 0.95f, 1f)).Play(ground);
                InkFx.Layer("circle_02").As(InkFx.Look.Ink).After(impact + 0.05f).Count(1).Size(0.5f, 0.5f).Grow(4f).Life(0.4f, 0.45f).Upright().Tint(s.main).Ink(0.05f).Play(at);
                break;

            case VfxSignature.BuzzsawSpinner:
                // The saw is the projectile; on contact it grinds a fountain of sparks
                InkFx.Layer("trace_02").As(InkFx.Look.Glow).After(impact).Count(26).Toward(ScreenUp, 60f).Speed(3f, 6f).Gravity(1.8f)
                     .Size(0.05f, 0.08f).Streak(0.08f).Life(0.35f, 0.55f).Tint(s.accent, Color.white).Play(at);
                break;

            case VfxSignature.RadiantPunishment:
                // A pillar of light drops from the sky onto the target
                for (int i = 0; i < 7; i++)
                    InkFx.Layer("light_01").As(InkFx.Look.Glow).After(Mathf.Max(0f, impact - 0.2f) + i * 0.025f).Count(1).Size(0.8f, 0.8f).Grow(1.3f)
                         .Life(0.35f, 0.4f).Tint(s.main).Smooth().Play(at + Vector3.up * (2.4f - i * 0.35f));
                InkFx.Layer("star_06").As(InkFx.Look.Glow).After(impact).Count(1).Size(2f, 2f).Life(0.25f, 0.3f).Grow(1.4f).Tint(s.accent).Play(at);
                InkFx.Layer("circle_02").As(InkFx.Look.InkGlow).OnBoard().After(impact).Count(1).Size(0.5f, 0.5f).Grow(4f).Life(0.45f, 0.5f).Tint(s.main).Ink(0.05f).Play(ground);
                break;

            case VfxSignature.HurricaneSlash:
                // A small tornado spins up around the target while blades cut through it
                for (int i = 0; i < 4; i++)
                    InkFx.Layer("twirl_01").As(InkFx.Look.Ink).After(Mathf.Max(0f, impact - 0.15f) + i * 0.06f).Count(1).Size(0.6f + i * 0.2f, 0.6f + i * 0.2f)
                         .Spin(700f, 900f).Grow(1.3f).Life(0.5f, 0.55f).Tint(s.main).Play(at + ScreenUp * (i * 0.18f - 0.2f));
                for (int i = 0; i < 3; i++)
                    InkFx.Layer("slash_03").As(InkFx.Look.InkGlow).After(impact + i * 0.08f).Count(1).Size(1f, 1f).PopIn(0.7f).Life(0.2f, 0.22f)
                         .Rotated(Random.Range(0f, 360f)).Tint(Color.white).Play(at);
                break;

            case VfxSignature.SweepingTide:
                // A wave rolls along the ground from the attacker to the target, then crashes
                runner.StartCoroutine(Wave(from, at, s, Mathf.Max(0f, impact - 0.3f)));
                break;

            case VfxSignature.Roar:
                for (int i = 0; i < 3; i++)
                    InkFx.Layer("circle_02").As(InkFx.Look.Ink).After(i * 0.1f).Count(1).Size(0.6f, 0.6f).Grow(5f).Life(0.5f, 0.55f).Upright()
                         .Tint(i % 2 == 0 ? s.main : s.accent).Ink(0.05f).Play(from);
                ComicWord.Show("RAAAWR!", s.accent, OnUnit(attacker, 1.1f), 1.5f);
                break;

            case VfxSignature.GardeniaBloom:
                // A big flower opens and petals spiral out over the allies
                InkFx.Layer("magic_01").As(InkFx.Look.InkGlow).After(impact).Count(1).Size(1.6f, 1.6f).PopIn(0.8f).Spin(60f, 80f).Life(0.8f, 0.9f).Tint(s.main).Play(from);
                Petals(from, ScreenUp, s.main, 26, impact);
                HealBurst(from, 1.3f, impact);
                break;

            case VfxSignature.AllInSlam:
                InkFx.Layer("scorch_03").As(InkFx.Look.Flat).OnBoard().After(impact).Count(1).Size(1.4f, 1.4f).PopIn(0.5f).Grow(1.1f).Life(1.1f, 1.2f)
                     .Tint(new Color(0.12f, 0.08f, 0.2f)).Play(ground);
                InkFx.Layer("circle_02").As(InkFx.Look.Ink).After(impact).Count(1).Size(0.6f, 0.6f).Grow(5f).Life(0.45f, 0.5f).Upright().Tint(s.main).Ink(0.05f).Play(at);
                Chunks(at, ScreenUp, new Color(0.7f, 0.55f, 0.4f), 16, "dirt_02", 0.09f, 0.16f, impact);
                ComicWord.Show("KA-BLAM!", s.accent, OnUnit(target != null ? target : attacker, 1.1f), 1.55f, impact);
                break;
        }
    }

    // True when a signature shows its own travel, so no ordinary projectile flies
    public static bool HasOwnTravel(VfxSignature signature) => signature == VfxSignature.LunarBeam || signature == VfxSignature.SweepingTide;

    // A signature with its own big word, so the theme's word is skipped
    public static bool HasOwnWord(VfxSignature signature) => signature == VfxSignature.Roar || signature == VfxSignature.AllInSlam;

    static IEnumerator Meteor(Vector3 landing, Style s, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Vector3 sky = landing + Vector3.up * 3.2f + ScreenUp * 1.2f;
        GameObject star = InkFx.Group("Meteor", sky, 3f);
        Core("star_09", InkFx.Look.InkGlow, 0.45f, s.accent, 400f, sky, star.transform);
        Trail("star_04", InkFx.Look.Glow, 0.12f, s.main, 40f, sky, star.transform);
        const float fall = 0.35f;
        for (float t = 0f; t < fall; t += Time.deltaTime)
        {
            if (star == null) yield break;
            star.transform.position = Vector3.Lerp(sky, landing, (t / fall) * (t / fall));
            yield return null;
        }
        InkFx.Stop(star, 1f);
        Star(landing, s.accent, 0.6f);
        InkFx.Layer("circle_02").As(InkFx.Look.InkGlow).OnBoard().Count(1).Size(0.3f, 0.3f).Grow(3f).Life(0.35f, 0.4f).Tint(s.main).Ink(0.05f).Play(landing - Vector3.up * 0.15f);
        Sparkles(landing, s.accent, 6, 0.2f);
    }

    static IEnumerator Beam(Vector3 from, Vector3 to, Color main, Color accent, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        float length = Vector3.Distance(from, to);
        int steps = Mathf.Max(4, Mathf.RoundToInt(length / 0.22f));
        for (int i = 0; i <= steps; i++)
        {
            Vector3 point = Vector3.Lerp(from, to, i / (float)steps);
            InkFx.Layer("light_03").As(InkFx.Look.Glow).Count(1).Size(0.55f, 0.6f).Life(0.35f, 0.4f).Grow(0.5f).Tint(main).Smooth().Play(point);
            if (i % 2 == 0) InkFx.Layer("star_04").As(InkFx.Look.Glow).Count(1).Speed(0.3f, 0.8f).Size(0.1f, 0.14f).Life(0.4f, 0.5f).Tint(accent).Play(point);
            if (i % 3 == 0) yield return null; // Sweeps out over a few frames
        }
    }

    static IEnumerator Wave(Vector3 from, Vector3 to, Style s, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        const float travel = 0.3f;
        for (float t = 0f; t < travel; t += 0.03f)
        {
            Vector3 point = Vector3.Lerp(from, to, t / travel) - Vector3.up * 0.15f;
            InkFx.Layer("muzzle_04").As(InkFx.Look.Ink).Count(2).Toward(ScreenUp, 30f).Speed(0.8f, 1.5f).Gravity(0.8f).Size(0.35f, 0.5f)
                 .Life(0.35f, 0.45f).Tint(s.main, s.accent).Play(point);
            yield return new WaitForSeconds(0.03f);
        }
        InkFx.Layer("muzzle_02").As(InkFx.Look.Ink).Count(7).Toward(ScreenUp, 50f).Speed(2f, 3.5f).Gravity(1.2f).Size(0.45f, 0.65f).Life(0.45f, 0.55f).Tint(s.main, s.accent).Play(to);
    }

    // ---- Status effects ----

    // Same order as CombatEffects' aura slots
    public enum AuraKind { Burn, Poison, Regeneration, Stun, Stagger, Hindered, AttackUp, AttackDown, DefenseUp, DefenseDown, SpeedUp, SpeedDown, HealBlock }

    static readonly Color BurnColor = new Color(1f, 0.55f, 0.15f), BurnHot = new Color(1f, 0.88f, 0.3f);
    static readonly Color PoisonColor = new Color(0.62f, 0.92f, 0.25f), PoisonPurple = new Color(0.7f, 0.4f, 0.95f);
    static readonly Color RegenColor = new Color(0.45f, 1f, 0.55f);
    static readonly Color StunColor = new Color(1f, 0.88f, 0.3f);

    // Looping ink effect that follows a unit while it has a status effect
    public static GameObject Aura(AuraKind kind, CardUnit unit)
    {
        Shapes.Ensure();
        Vector3 at = OnUnit(unit, 0.5f);
        GameObject group = InkFx.Group("Aura " + kind, at);
        FollowCard follow = group.AddComponent<FollowCard>();
        follow.target = unit;
        follow.height = 0.5f;
        Transform t = group.transform;
        Vector3 up = ScreenUp;

        switch (kind)
        {
            case AuraKind.Burn:
                follow.height = 0.2f;
                InkFx.Layer("flame_05").As(InkFx.Look.Ink).Looping(7f).Spread(0.22f).Toward(up, 12f).Speed(0.5f, 0.8f).PopIn(0.6f)
                     .Size(0.14f, 0.22f).Life(0.45f, 0.65f).Tint(BurnColor, BurnHot).Upright().Play(at, t);
                InkFx.Layer("star_04").As(InkFx.Look.Glow).Looping(4f).Spread(0.25f).Toward(up, 25f).Speed(0.6f, 1.1f).Size(0.05f, 0.08f).Life(0.5f, 0.8f).Tint(BurnHot).Play(at, t);
                break;
            case AuraKind.Poison:
                InkFx.Layer("circle_05").As(InkFx.Look.Flat).Looping(5f).Spread(0.25f).Toward(up, 20f).Speed(0.3f, 0.5f).Size(0.05f, 0.1f).Life(0.7f, 0.9f).Tint(PoisonColor).Play(at, t);
                InkFx.Layer("whitePuff06").As(InkFx.Look.Ink).Looping(1.2f).Spread(0.2f).Speed(0.05f, 0.15f).Size(0.22f, 0.3f).Grow(1.5f).Life(0.9f, 1.1f).Tint(PoisonColor, PoisonPurple).Play(at, t);
                break;
            case AuraKind.Regeneration:
                InkFx.Layer("gen_plus").As(InkFx.Look.Ink).Looping(2.5f).Spread(0.25f).Toward(up, 15f).Speed(0.3f, 0.45f).PopIn(0.6f).Size(0.1f, 0.14f).Life(0.9f, 1.1f).Upright().Tint(RegenColor).Play(at, t);
                InkFx.Layer("star_04").As(InkFx.Look.Glow).Looping(3f).Spread(0.3f).Toward(up, 30f).Speed(0.2f, 0.4f).Size(0.05f, 0.08f).Life(0.6f, 0.8f).Tint(RegenColor).Play(at, t);
                break;
            case AuraKind.Stun:
            case AuraKind.Stagger:
                // Cartoon stars circling over the head (fewer and dimmer when only staggered)
                follow.height = 1.02f;
                bool stunned = kind == AuraKind.Stun;
                OrbitStars orbit = group.AddComponent<OrbitStars>();
                orbit.Build(stunned ? 3 : 2, stunned ? StunColor : new Color(0.75f, 0.75f, 0.85f), stunned ? 0.16f : 0.12f, stunned ? 240f : 140f);
                break;
            case AuraKind.Hindered:
                follow.height = 0.35f;
                InkFx.Layer("gen_chevron").As(InkFx.Look.Ink).Looping(1.6f).Spread(0.2f).Toward(-up, 0f).Speed(0.25f, 0.35f).Size(0.14f, 0.16f).Life(0.8f, 0.9f)
                     .Rotated(180f).Tint(new Color(0.65f, 0.65f, 0.72f)).Play(at + up * 0.25f, t);
                break;
            case AuraKind.AttackUp:
                InkFx.Layer("gen_chevron").As(InkFx.Look.Ink).Looping(2.5f).Spread(0.25f).Toward(up, 0f).Speed(0.45f, 0.6f).PopIn(0.6f).Size(0.12f, 0.15f).Life(0.7f, 0.8f)
                     .Upright().Tint(new Color(1f, 0.5f, 0.25f)).Play(at - up * 0.2f, t);
                break;
            case AuraKind.AttackDown:
                InkFx.Layer("gen_chevron").As(InkFx.Look.Ink).Looping(2f).Spread(0.25f).Toward(-up, 0f).Speed(0.35f, 0.5f).Size(0.12f, 0.15f).Life(0.7f, 0.8f)
                     .Rotated(180f).Tint(new Color(0.7f, 0.18f, 0.25f)).Play(at + up * 0.2f, t);
                break;
            case AuraKind.DefenseUp:
                InkFx.Layer("gen_shield").As(InkFx.Look.InkGlow).Looping(1.1f).Local().Size(0.42f, 0.42f).PopIn(0.4f).Grow(1.15f).Life(0.9f, 0.9f).Upright().Tint(InkArt.GoldLight).Play(at, t);
                break;
            case AuraKind.DefenseDown:
                InkFx.Layer("gen_shield").As(InkFx.Look.Ink).Looping(0.9f).Toward(-up, 0f).Speed(0.15f, 0.2f).Size(0.36f, 0.36f).Grow(0.8f).Life(1f, 1f).Upright()
                     .Tint(new Color(0.55f, 0.55f, 0.62f)).Play(at, t);
                break;
            case AuraKind.SpeedUp:
                InkFx.Layer("trace_03").As(InkFx.Look.Glow).Looping(7f).Spread(0.3f).Toward(up, 5f).Speed(1.2f, 1.8f).Size(0.05f, 0.08f).Streak(0.12f).Life(0.3f, 0.4f).Tint(new Color(0.55f, 0.95f, 1f)).Play(at - up * 0.3f, t);
                break;
            case AuraKind.SpeedDown:
                InkFx.Layer("circle_05").As(InkFx.Look.Flat).Looping(3f).Spread(0.25f).Toward(-up, 5f).Speed(0.2f, 0.3f).Gravity(0.4f).Size(0.06f, 0.09f).Life(0.8f, 1f)
                     .Tint(new Color(0.3f, 0.45f, 0.9f)).Play(at + up * 0.35f, t);
                break;
            case AuraKind.HealBlock:
                InkFx.Layer("gen_cross").As(InkFx.Look.Ink).Looping(0.9f).Local().Size(0.28f, 0.28f).PopIn(0.5f).Life(1f, 1f).Upright().Tint(new Color(1f, 0.28f, 0.35f)).Play(at + up * 0.2f, t);
                break;
        }
        return group;
    }

    // One-off burst when a status effect ticks (burn damage, poison, regeneration, losing a turn to stun)
    public static void Tick(AuraKind kind, CardUnit unit)
    {
        Shapes.Ensure();
        Vector3 at = OnUnit(unit, 0.45f);
        switch (kind)
        {
            case AuraKind.Burn:
                InkFx.Layer("flame_06").As(InkFx.Look.Ink).Count(9).Spread(0.2f).Toward(ScreenUp, 30f).Speed(1f, 2f).PopIn(0.6f).Size(0.2f, 0.32f).Life(0.4f, 0.55f).Upright().Tint(BurnColor, BurnHot).Play(at);
                Puffs(at, new Color(0.35f, 0.3f, 0.35f), 3, 0.25f);
                Cfx(x => x.fireHit, at, 0.55f);
                break;
            case AuraKind.Poison:
                Cfx(x => x.poisonCloud, at, 0.5f);
                InkFx.Layer("whitePuff06").As(InkFx.Look.Ink).Count(5).Spread(0.15f).Speed(0.4f, 0.9f).Drag(0.4f).Size(0.3f, 0.42f).Grow(1.5f).Life(0.6f, 0.8f).Tint(PoisonColor, PoisonPurple).Play(at);
                InkFx.Layer("circle_05").As(InkFx.Look.Flat).Count(8).Toward(ScreenUp, 60f).Speed(0.8f, 1.5f).Size(0.06f, 0.1f).Life(0.6f, 0.8f).Tint(PoisonColor).Play(at);
                break;
            case AuraKind.Regeneration:
                HealBurst(at, 1f);
                break;
            case AuraKind.Stun:
                InkFx.Layer("symbol_02").As(InkFx.Look.Ink).Count(6).Toward(ScreenUp, 70f).Speed(1.5f, 2.5f).Gravity(0.6f).Size(0.12f, 0.18f).Spin(-400f, 400f).Life(0.6f, 0.8f).Tint(StunColor).Play(OnUnit(unit, 0.95f));
                break;
        }
    }

    // ---- Defeat ----

    // Ink splat on the board, the unit's frame shatters in its team colour, its star crest flies off, and "KO!"
    public static void Defeat(CardUnit unit)
    {
        if (unit == null) return;
        Vector3 at = OnUnit(unit, 0.5f);
        Vector3 feet = UnitTokens.Feet(unit);
        Color team = unit.Instance != null ? InkArt.PlayerColor(unit.Instance.owner) : Color.white;

        InkFx.Layer("whitePuff08").As(InkFx.Look.Flat).OnBoard().Count(1).Size(0.9f, 0.9f).PopIn(0.6f).Grow(1.2f).Life(1.3f, 1.4f).Tint(InkArt.Ink).Play(feet + Vector3.up * 0.02f);
        Chunks(at, ScreenUp, team, 12, "dirt_02", 0.09f, 0.15f);
        Chunks(at, ScreenUp, InkArt.Gold, 6, "dirt_03", 0.06f, 0.1f);
        Puffs(at, Color.white, 6, 0.32f);
        InkFx.Layer("symbol_02").As(InkFx.Look.Ink).Count(1).Toward(ScreenUp + ScreenRight * Random.Range(-0.4f, 0.4f), 5f).Speed(3.5f, 4f).Gravity(1.6f)
             .Size(0.26f, 0.26f).Spin(500f, 700f).Life(0.9f, 1f).Tint(InkArt.GoldLight).Play(OnUnit(unit, 1f));
        ComicWord.Show("KO!", InkArt.Gold, OnUnit(unit, 1.05f), 1.4f);
        Cfx(x => x.defeatPoof, at, 0.8f);
    }

    // ---- Building blocks ----

    // One of the Cartoon FX Remaster effects (CartoonFxSet), if the pack is set up; skipped otherwise
    static void Cfx(System.Func<CartoonFxSet, GameObject> pick, Vector3 p, float size = 1f)
    {
        CartoonFxSet set = CartoonFxSet.Instance;
        if (set != null) CartoonFxSet.Play(pick(set), p, size);
    }

    static IEnumerator CfxLater(System.Func<CartoonFxSet, GameObject> pick, Vector3 p, float size, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Cfx(pick, p, size);
    }

    static void Flash(Vector3 p, Color c, float size)
    {
        InkFx.Layer("star_06").As(InkFx.Look.Glow).Count(1).Size(size, size).Life(0.12f, 0.15f).Grow(1.3f).Rotated(Random.Range(0f, 45f)).Tint(c).Smooth().Play(p);
    }

    static void Star(Vector3 p, Color c, float size)
    {
        InkFx.Layer("star_09").As(InkFx.Look.InkGlow).Count(1).Size(size, size).PopIn(0.6f).Grow(1.25f).Life(0.2f, 0.24f).Rotated(Random.Range(0f, 45f)).Tint(c).Play(p);
    }

    static void Ring(Vector3 p, Color c, float size, float life, float delay = 0f)
    {
        InkFx.Layer("circle_02").As(InkFx.Look.Ink).After(delay).Count(1).Size(size * 0.3f, size * 0.3f).Grow(3.3f).Life(life, life).Upright().Tint(c).Ink(0.06f).Play(p);
    }

    static void Arc(string texture, Vector3 p, float angle, float size, Color c, float delay = 0f)
    {
        InkFx.Layer(texture).As(InkFx.Look.Ink).After(delay).Count(1).Size(size, size).PopIn(0.7f).Grow(1.15f).Life(0.2f, 0.24f).Rotated(angle).Tint(c).Play(p);
    }

    static void Puffs(Vector3 p, Color c, int count, float size)
    {
        InkFx.Layer("whitePuff0" + Random.Range(0, 6)).As(InkFx.Look.Ink).Count(count).Spread(0.05f).Speed(0.8f, 1.8f).Drag(0.45f).Size(size * 0.7f, size)
             .PopIn(0.5f).Grow(1.5f).Life(0.35f, 0.55f).Tint(c).Play(p);
    }

    static void Sparks(Vector3 p, Vector3 d, Color c, int count)
    {
        InkFx.Layer("trace_02").As(InkFx.Look.Glow).Count(Mathf.Max(1, count)).Toward(d + ScreenUp * 0.3f, 45f).Speed(4f, 7f).Drag(0.4f)
             .Size(0.05f, 0.09f).Streak(0.06f).Life(0.15f, 0.25f).Tint(c).Play(p);
    }

    static void Chunks(Vector3 p, Vector3 d, Color c, int count, string texture, float min, float max, float delay = 0f)
    {
        InkFx.Layer(texture).As(InkFx.Look.Ink).After(delay).Count(Mathf.Max(1, count)).Toward(d + ScreenUp * 0.8f, 55f).Speed(2f, 4f).Gravity(1.4f)
             .Size(min, max).Life(0.4f, 0.6f).Spin(-500f, 500f).Tint(c).Play(p);
    }

    static void Sparkles(Vector3 p, Color c, int count, float spread)
    {
        InkFx.Layer("star_04").As(InkFx.Look.Glow).Count(Mathf.Max(1, count)).Spread(spread).Speed(0.5f, 1.5f).Drag(0.3f).Size(0.07f, 0.13f)
             .Life(0.35f, 0.6f).Tint(c).Play(p);
    }

    static void Petals(Vector3 p, Vector3 d, Color c, int count, float delay = 0f)
    {
        InkFx.Layer("flame_06").As(InkFx.Look.Ink).After(delay).Count(Mathf.Max(1, count)).Toward(d + ScreenUp, 80f).Speed(1.5f, 3.2f).Gravity(0.35f).Drag(0.2f)
             .Size(0.1f, 0.16f).Spin(-500f, 500f).Life(0.6f, 0.9f).Tint(c, Color.Lerp(c, Color.white, 0.4f)).Play(p);
    }

    // Green pluses and hearts floating up
    static void HealBurst(Vector3 p, float k, float delay = 0f)
    {
        Shapes.Ensure();
        InkFx.Layer("gen_plus").As(InkFx.Look.Ink).After(delay).Count(Mathf.RoundToInt(5 * k)).Spread(0.25f).Toward(ScreenUp, 25f).Speed(0.6f, 1.2f)
             .PopIn(0.6f).Size(0.12f, 0.18f).Life(0.6f, 0.8f).Upright().Tint(RegenColor).Play(p);
        InkFx.Layer("symbol_01").As(InkFx.Look.Ink).After(delay).Count(3).Spread(0.2f).Toward(ScreenUp, 30f).Speed(0.5f, 1f).PopIn(0.6f)
             .Size(0.12f, 0.16f).Life(0.7f, 0.9f).Upright().Tint(new Color(1f, 0.5f, 0.7f)).Play(p);
        InkFx.Layer("star_04").As(InkFx.Look.Glow).After(delay).Count(6).Spread(0.3f).Speed(0.3f, 0.8f).Size(0.06f, 0.1f).Life(0.5f, 0.7f).Tint(RegenColor).Play(p);
    }

    // ---- Shapes made in code (for things Kenney's pack doesn't have) ----

    // Soft-edged white shapes (bright inside, fading toward the edge) so the ink shader can outline and erode them
    static class Shapes
    {
        static bool made;

        public static void Ensure()
        {
            if (made) return;
            made = true;
            InkFx.Register("gen_chevron", Make(new[] { V(0.1f, 0.3f), V(0.5f, 0.75f), V(0.9f, 0.3f), V(0.9f, 0.12f), V(0.5f, 0.55f), V(0.1f, 0.12f) }, 0.09f));
            InkFx.Register("gen_plus", Make(new[] { V(0.38f, 0.08f), V(0.62f, 0.08f), V(0.62f, 0.38f), V(0.92f, 0.38f), V(0.92f, 0.62f), V(0.62f, 0.62f),
                                                    V(0.62f, 0.92f), V(0.38f, 0.92f), V(0.38f, 0.62f), V(0.08f, 0.62f), V(0.08f, 0.38f), V(0.38f, 0.38f) }, 0.1f));
            InkFx.Register("gen_shield", Make(new[] { V(0.15f, 0.88f), V(0.5f, 0.95f), V(0.85f, 0.88f), V(0.85f, 0.5f), V(0.72f, 0.25f), V(0.5f, 0.06f),
                                                      V(0.28f, 0.25f), V(0.15f, 0.5f) }, 0.14f));
            InkFx.Register("gen_cross", Make(new[] { V(0.2f, 0.08f), V(0.5f, 0.38f), V(0.8f, 0.08f), V(0.92f, 0.2f), V(0.62f, 0.5f), V(0.92f, 0.8f),
                                                     V(0.8f, 0.92f), V(0.5f, 0.62f), V(0.2f, 0.92f), V(0.08f, 0.8f), V(0.38f, 0.5f), V(0.08f, 0.2f) }, 0.09f));
            InkFx.Register("gen_saw", Saw());
        }

        static Vector2 V(float x, float y) => new Vector2(x, y);

        // Polygon in 0-1 coordinates; brightness rises from 0 at the edge to 1 'falloff' inside
        static Texture2D Make(Vector2[] polygon, float falloff, int size = 128)
        {
            return InkArt.Paint(size, size, p =>
            {
                Vector2 uv = p / size;
                float distance = PolygonDistance(uv, polygon);
                float value = Mathf.Clamp01(-distance / falloff);
                return new Color(1f, 1f, 1f, value);
            }, 1, true);
        }

        // Circular saw blade: a toothed ring with a hole in the middle
        static Texture2D Saw()
        {
            const int teeth = 14;
            Vector2[] outline = new Vector2[teeth * 2];
            for (int i = 0; i < teeth * 2; i++)
            {
                float angle = i * Mathf.PI / teeth;
                float radius = i % 2 == 0 ? 0.47f : 0.36f;
                outline[i] = new Vector2(0.5f + Mathf.Cos(angle) * radius, 0.5f + Mathf.Sin(angle) * radius);
            }
            return InkArt.Paint(128, 128, p =>
            {
                Vector2 uv = p / 128f;
                float distance = Mathf.Max(PolygonDistance(uv, outline), 0.12f - (uv - new Vector2(0.5f, 0.5f)).magnitude);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(-distance / 0.08f));
            }, 1, true);
        }

        // Signed distance to a polygon (negative inside)
        static float PolygonDistance(Vector2 p, Vector2[] poly)
        {
            float best = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.000001f, ab.sqrMagnitude));
                best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -best : best;
        }
    }
}

// A big inked comic word ("BONK!") on a jagged burst, that pops in, holds and shrinks away. Uses real time so
// hit-stop doesn't freeze it
public class ComicWord : MonoBehaviour
{
    private static Sprite burst;
    private TextMeshPro text;
    private SpriteRenderer back;
    private float age, size, tilt, delay;
    private Vector3 start;

    public static void Show(string word, Color colour, Vector3 position, float size, float delay = 0f)
    {
        GameObject wordObject = new GameObject("Comic Word " + word);
        ComicWord comic = wordObject.AddComponent<ComicWord>();
        comic.Build(word, colour, position, size, delay);
    }

    void Build(string word, Color colour, Vector3 position, float wordSize, float wait)
    {
        size = wordSize;
        delay = wait;
        tilt = Random.Range(-12f, 12f);
        start = position + (BoardView.CameraRotation * Vector3.right) * Random.Range(-0.15f, 0.15f);
        transform.position = start;
        transform.localScale = Vector3.zero;

        if (burst == null) burst = MakeBurst();
        back = new GameObject("Burst").AddComponent<SpriteRenderer>();
        back.transform.SetParent(transform, false);
        back.sprite = burst;
        back.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        back.color = colour;
        back.sortingOrder = 26;

        text = new GameObject("Word").AddComponent<TextMeshPro>();
        text.transform.SetParent(transform, false);
        text.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        GameFonts.UseInked(text, 0.35f);
        text.text = word;
        text.fontSize = 3.6f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(3f, 1f);
        text.color = Color.white;
        text.sortingOrder = 27;

        // Burst sized to the word
        float width = Mathf.Max(0.8f, text.GetPreferredValues(word).x + 0.35f);
        back.transform.localScale = new Vector3(width / burst.bounds.size.x, 0.85f / burst.bounds.size.y, 1f);
    }

    void LateUpdate()
    {
        if (delay > 0f) { delay -= Time.unscaledDeltaTime; return; }
        age += Time.unscaledDeltaTime;
        Camera cam = Camera.main;
        if (cam != null) transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, tilt);

        // Pop in with an overshoot, hold, then shrink and spin away
        float scale;
        if (age < 0.12f) scale = Mathf.Lerp(0f, 1.3f, age / 0.12f);
        else if (age < 0.24f) scale = Mathf.Lerp(1.3f, 1f, (age - 0.12f) / 0.12f);
        else if (age < 0.75f) scale = 1f;
        else scale = Mathf.Lerp(1f, 0f, (age - 0.75f) / 0.2f);
        transform.localScale = Vector3.one * size * Mathf.Max(0f, scale);
        transform.position = start + (BoardView.CameraRotation * Vector3.up) * (age * 0.25f);
        if (age > 0.75f) tilt += Time.unscaledDeltaTime * 90f;
        if (age > 0.95f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (back != null) Destroy(back.sharedMaterial);
    }

    // Jagged comic burst, white with a thick ink outline (tinted per word)
    static Sprite MakeBurst()
    {
        const int width = 256, height = 128, spikes = 14;
        Vector2 centre = new Vector2(width / 2f, height / 2f);
        Texture2D texture = InkArt.Paint(width, height, p =>
        {
            Vector2 d = new Vector2((p.x - centre.x) / (width * 0.5f), (p.y - centre.y) / (height * 0.5f));
            float angle = Mathf.Atan2(d.y, d.x);
            float spike = Mathf.Abs(Mathf.Repeat(angle / (Mathf.PI * 2f) * spikes, 1f) - 0.5f) * 2f; // 1 at a spike tip, 0 between
            float reach = Mathf.Lerp(0.72f, 0.98f, spike);
            float edge = (d.magnitude - reach) * 60f;
            return InkArt.Outlined(edge, 5f, Color.white);
        }, 2);
        return InkArt.ToSprite(texture, new Vector2(0.5f, 0.5f), height);
    }
}

// Stars circling over a stunned unit's head, always facing the camera
public class OrbitStars : MonoBehaviour
{
    private readonly List<Transform> stars = new List<Transform>();
    private float speed, radius = 0.26f, angle;

    public void Build(int count, Color colour, float size, float degreesPerSecond)
    {
        speed = degreesPerSecond;
        for (int i = 0; i < count; i++)
            stars.Add(InkFx.Sprite("symbol_02", InkFx.Look.Ink, transform, size, colour).transform);
    }

    void LateUpdate()
    {
        angle += speed * Time.deltaTime;
        Camera cam = Camera.main;
        for (int i = 0; i < stars.Count; i++)
        {
            float a = (angle + i * 360f / stars.Count) * Mathf.Deg2Rad;
            // A flat circle around the head, seen from the camera as an ellipse
            stars[i].localPosition = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius * 0.6f);
            if (cam != null) stars[i].rotation = cam.transform.rotation;
        }
    }
}
