using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The class "crowns" for board tokens. Every token shares one body (ink outline, team-colour band, gold line, portrait),
// but each class gets its own top silhouette, decorations and a badge in the top-left corner with the class colour
// and icon (the same corner as the big class circle on the cards):
//   Attacker: gothic point + blade fins     Defender: battlements          Healer: round dome + halo
//   Support: soft arch + pennants           Mage: onion spire + star       Assassin: slashed asymmetric cut
//   Ranged: sight notch + side ticks
// Shapes are drawn in the concept sheet's units: the body runs x 12-128 (116 wide), its bottom is y 192, and y grows
// downward. UnitTokens turns them into textures.
public static class ClassFrames
{
    // ---- Layout ----

    const float CanvasLeft = -8f, CanvasRight = 148f, CanvasTop = -8f, CanvasBottom = 200f;
    public const float BodyWidth = 116f;           // Units across the body (x 12 to 128)
    public const float BodyBottom = 192f;
    public static float CanvasWidth => CanvasRight - CanvasLeft;
    public static float CanvasHeight => CanvasBottom - CanvasTop;
    // Sprite pivot: the middle of the body's bottom edge (the token's feet)
    public static Vector2 Pivot => new Vector2((70f - CanvasLeft) / CanvasWidth, (CanvasBottom - BodyBottom) / CanvasHeight);

    // Bands from the outline inward, in units: ink, team colour, gold, ink, then the portrait
    const float OutlineInk = 3.5f, TeamBand = 5f, GoldLine = 2.2f, InnerInk = 1.4f;
    const float PortraitInset = OutlineInk + TeamBand + GoldLine + InnerInk;

    static readonly Vector2 BadgeCentre = new Vector2(25f, 74f);
    static readonly Vector2 BadgeHalf = new Vector2(15f, 16f);

    // ---- Class colours ----

    public static Color ColourOf(CharacterClass characterClass)
    {
        switch (characterClass)
        {
            case CharacterClass.Attacker: return new Color32(255, 138, 61, 255);  // Ember
            case CharacterClass.Defender: return new Color32(159, 183, 200, 255); // Steel
            case CharacterClass.Healer:   return new Color32(110, 224, 154, 255); // Mint
            case CharacterClass.Support:  return new Color32(255, 210, 90, 255);  // Gold
            case CharacterClass.Mage:     return new Color32(180, 140, 255, 255); // Violet
            case CharacterClass.Assassin: return new Color32(208, 112, 192, 255); // Plum
            case CharacterClass.Ranged:   return new Color32(79, 209, 197, 255);  // Teal
            default:                      return InkArt.Gold;
        }
    }

    // ---- Outlines ----

    static readonly Dictionary<CharacterClass, Vector2[]> outlines = new Dictionary<CharacterClass, Vector2[]>();

    public static Vector2[] Outline(CharacterClass characterClass)
    {
        if (outlines.TryGetValue(characterClass, out Vector2[] cached)) return cached;
        List<Vector2> points = new List<Vector2> { V(12, 184) };
        switch (characterClass)
        {
            case CharacterClass.Attacker: // Gothic point
                points.Add(V(12, 86));
                points.AddRange(Quad(V(12, 86), V(14, 52), V(70, 20)));
                points.AddRange(Quad(V(70, 20), V(126, 52), V(128, 86)));
                break;
            case CharacterClass.Defender: // Battlements
                points.AddRange(new[] { V(12, 52), V(26, 52), V(26, 38), V(46, 38), V(46, 52), V(60, 52), V(60, 38), V(80, 38),
                                        V(80, 52), V(94, 52), V(94, 38), V(114, 38), V(114, 52), V(128, 52) });
                break;
            case CharacterClass.Healer: // Round dome
                points.Add(V(12, 92));
                points.AddRange(Arc(70, 92, 58, 58, Mathf.PI, 0f));
                break;
            case CharacterClass.Support: // Soft arch
                points.Add(V(12, 82));
                points.AddRange(Arc(70, 82, 58, 48, Mathf.PI, 0f));
                break;
            case CharacterClass.Mage: // Onion spire
                points.Add(V(12, 96));
                points.AddRange(Cubic(V(12, 96), V(12, 62), V(58, 60), V(70, 12)));
                points.AddRange(Cubic(V(70, 12), V(82, 60), V(128, 62), V(128, 96)));
                break;
            case CharacterClass.Assassin: // Slashed cut with a notch
                points.AddRange(new[] { V(12, 66), V(44, 36), V(60, 44), V(128, 26) });
                break;
            case CharacterClass.Ranged: // Arch with a sight notch
                points.Add(V(12, 84));
                points.AddRange(Arc(70, 84, 58, 50, Mathf.PI, 1.814f));
                points.Add(V(70, 52));
                points.AddRange(Arc(70, 84, 58, 50, 1.327f, 0f));
                break;
            default:
                points.Add(V(12, 86));
                points.AddRange(Arc(70, 86, 58, 50, Mathf.PI, 0f));
                break;
        }
        points.AddRange(new[] { V(128, 184), V(120, 192), V(20, 192) });
        Vector2[] outline = points.ToArray();
        outlines[characterClass] = outline;
        return outline;
    }

    // How tall the crown stands from its feet to its highest point (decorations included), in units
    public static float Height(CharacterClass characterClass)
    {
        float top = float.MaxValue;
        foreach (Vector2 point in Outline(characterClass)) top = Mathf.Min(top, point.y);
        if (characterClass == CharacterClass.Healer) top = 7f;        // Halo
        if (characterClass == CharacterClass.Mage) top = -5.5f;       // Star
        if (characterClass == CharacterClass.Assassin) top = 15f;     // Blade tip
        return BodyBottom - top;
    }

    // Where the portrait goes: the body's inside, in units (y down)
    public static Rect PortraitBox(CharacterClass characterClass)
    {
        float top = float.MaxValue;
        foreach (Vector2 point in Outline(characterClass)) top = Mathf.Min(top, point.y);
        return Rect.MinMaxRect(12f + PortraitInset, top + PortraitInset, 128f - PortraitInset, 184f);
    }

    // ---- Decorations ----

    // One decoration: its shape (distance in units, negative inside), colour, ink outline width, and whether it's
    // drawn over the frame or behind it
    struct Extra
    {
        public System.Func<Vector2, float> shape;
        public Color fill;
        public float ink;
        public bool inFront;
        public Rect bounds; // Nothing outside this box, so most points skip the shape test
        public Extra(System.Func<Vector2, float> shape, Color fill, float ink, bool inFront, Rect bounds) { this.shape = shape; this.fill = fill; this.ink = ink; this.inFront = inFront; this.bounds = bounds; }
    }

    static List<Extra> ExtrasOf(CharacterClass characterClass)
    {
        List<Extra> extras = new List<Extra>();
        switch (characterClass)
        {
            case CharacterClass.Attacker: // Blade fins on the shoulders
                Vector2[] leftFin = { V(12, 92), V(-2, 62), V(20, 74) }, rightFin = { V(128, 92), V(142, 62), V(120, 74) };
                extras.Add(new Extra(p => InkArt.Polygon(p, leftFin), InkArt.Gold, 1.8f, false, Box(leftFin)));
                extras.Add(new Extra(p => InkArt.Polygon(p, rightFin), InkArt.Gold, 1.8f, false, Box(rightFin)));
                break;
            case CharacterClass.Healer: // Floating halo
                extras.Add(new Extra(p => Mathf.Abs(Ellipse(p - V(70, 18), 28f, 7f)) - 3.6f, InkArt.GoldLight, 1.6f, true, Rect.MinMaxRect(38f, 7f, 102f, 29f)));
                break;
            case CharacterClass.Support: // Notched pennants
                Vector2[] leftFlag = { V(14, 70), V(-4, 74), V(4, 86), V(-4, 98), V(14, 96) };
                Vector2[] rightFlag = { V(126, 70), V(144, 74), V(136, 86), V(144, 98), V(126, 96) };
                extras.Add(new Extra(p => InkArt.Polygon(p, leftFlag), InkArt.Sage, 1.8f, false, Box(leftFlag)));
                extras.Add(new Extra(p => InkArt.Polygon(p, rightFlag), InkArt.Sage, 1.8f, false, Box(rightFlag)));
                break;
            case CharacterClass.Mage: // Star on the tip
                Vector2[] star = Star(V(70, 4), 9.5f, 4.2f);
                extras.Add(new Extra(p => InkArt.Polygon(p, star), InkArt.GoldLight, 1.8f, true, Box(star)));
                break;
            case CharacterClass.Assassin: // Blade tip off the high corner
                Vector2[] tip = { V(126, 28), V(141, 15), V(128, 44) };
                extras.Add(new Extra(p => InkArt.Polygon(p, tip), InkArt.Gold, 1.8f, true, Box(tip)));
                break;
            case CharacterClass.Ranged: // Range-finder ticks down the sides
                foreach (float y in new[] { 110f, 124f, 138f })
                {
                    float tickY = y;
                    extras.Add(new Extra(p => InkArt.Segment(p, V(1, tickY), V(11, tickY)) - 2.4f, InkArt.Gold, 1.2f, false, Rect.MinMaxRect(-2f, tickY - 3f, 14f, tickY + 3f)));
                    extras.Add(new Extra(p => InkArt.Segment(p, V(129, tickY), V(139, tickY)) - 2.4f, InkArt.Gold, 1.2f, false, Rect.MinMaxRect(126f, tickY - 3f, 142f, tickY + 3f)));
                }
                break;
        }
        return extras;
    }

    // ---- Badge icons (in the badge's own units, centred, y down, about 10 each way; negative = ink) ----

    static float Icon(CharacterClass characterClass, Vector2 q)
    {
        switch (characterClass)
        {
            case CharacterClass.Attacker: // Crossed swords
                return Mathf.Min(InkArt.Segment(q, V(-7, 7), V(7, -7)), InkArt.Segment(q, V(-7, -7), V(7, 7))) - 1.7f;
            case CharacterClass.Defender: // Shield outline
                return Mathf.Abs(InkArt.Polygon(q, new[] { V(0, -8.5f), V(8, -5.5f), V(7, 3), V(0, 9.5f), V(-7, 3), V(-8, -5.5f) })) - 1.5f;
            case CharacterClass.Healer: // Plus
                return Mathf.Min(InkArt.Segment(q, V(0, -7.5f), V(0, 7.5f)), InkArt.Segment(q, V(-7.5f, 0), V(7.5f, 0))) - 2.6f;
            case CharacterClass.Support: // Rising chevrons
                return Mathf.Min(Mathf.Min(InkArt.Segment(q, V(-7, -1), V(0, -8)), InkArt.Segment(q, V(0, -8), V(7, -1))),
                                 Mathf.Min(InkArt.Segment(q, V(-7, 6), V(0, -1)), InkArt.Segment(q, V(0, -1), V(7, 6)))) - 1.6f;
            case CharacterClass.Mage: // Crescent moon
                return Mathf.Max(q.magnitude - 8.5f, -((q - V(4, -3)).magnitude - 7f));
            case CharacterClass.Assassin: // Dagger
                return Mathf.Min(InkArt.Polygon(q, new[] { V(-5, 6), V(5, -6), V(8, -8.5f), V(6.5f, -5), V(-3, 7.5f) }),
                                 InkArt.Segment(q, V(-8, 3), V(-2, 9)) - 1.5f);
            case CharacterClass.Ranged: // Crosshair
                float ring = Mathf.Abs(q.magnitude - 6.5f) - 1.3f;
                float dot = q.magnitude - 2.2f;
                float ticks = Mathf.Min(Mathf.Min(InkArt.Segment(q, V(0, -10.5f), V(0, -7)), InkArt.Segment(q, V(0, 7), V(0, 10.5f))),
                                        Mathf.Min(InkArt.Segment(q, V(-10.5f, 0), V(-7, 0)), InkArt.Segment(q, V(7, 0), V(10.5f, 0)))) - 1.2f;
                return Mathf.Min(ring, Mathf.Min(dot, ticks));
            default:
                return 1f;
        }
    }

    // ---- Painting ----

    // The whole frame for one card on one team: decorations, outline bands, portrait and class badge.
    // portraitColour gives the art colour at a point inside the portrait box (in units).
    // The shape work (outline, bands, badge, decorations) is the slow part and is the same for every card of a class,
    // so it's worked out once per class (Regions) and each card only fills in its team colour and portrait.
    // Every job here also has a Routine version that spreads the work over several frames (see InkArt.FrameBudget)
    public static Texture2D PaintFrame(CharacterClass characterClass, Color team, System.Func<Vector2, Color> portraitColour, float pixelsPerUnit)
    {
        Texture2D result = null;
        RunNow(PaintFrameRoutine(characterClass, team, portraitColour, pixelsPerUnit, null, t => result = t));
        return result;
    }

    public static IEnumerator PaintFrameRoutine(CharacterClass characterClass, Color team, System.Func<Vector2, Color> portraitColour,
                                                float pixelsPerUnit, InkArt.FrameBudget budget, System.Action<Texture2D> done)
    {
        budget ??= NoBudget;
        int width = Mathf.RoundToInt(CanvasWidth * pixelsPerUnit), height = Mathf.RoundToInt(CanvasHeight * pixelsPerUnit);
        byte[] regions = null;
        yield return RegionsRoutine(characterClass, pixelsPerUnit, width, height, budget, r => regions = r);
        Color badgeColour = ColourOf(characterClass);
        int rowLength = width * FrameSamples;

        yield return InkArt.PaintRoutine(width, height, pixel =>
        {
            // Paint asks for sample points at x + (s + 0.5) / samples, so this lands on exactly one stored sample
            int sx = Mathf.Min(rowLength - 1, (int)(pixel.x * FrameSamples));
            int sy = Mathf.Min(height * FrameSamples - 1, (int)(pixel.y * FrameSamples));
            switch (regions[sy * rowLength + sx])
            {
                case RegionInk: return InkArt.Ink;
                case RegionTeam: return team;
                case RegionGold: return InkArt.Gold;
                case RegionGoldLight: return InkArt.GoldLight;
                case RegionSage: return InkArt.Sage;
                case RegionBadge: return badgeColour;
                case RegionPortrait:
                    Color art = portraitColour(ToUnits(pixel, pixelsPerUnit));
                    art.a = 1f;
                    return art;
                default: return Color.clear;
            }
        }, FrameSamples, true, budget, done);
    }

    // ---- Shape cache ----
    // Working out a frame means asking "which band is this point in?" for ~250k sample points. Measuring the distance
    // to every edge of the outline each time was the slow part (up to 1.4 s per class), so instead:
    // - inside/outside comes from where the outline crosses each row (worked out once per row)
    // - the distance only needs the edges within a band's reach, found through a grid of nearby edges

    const int FrameSamples = 2; // Samples per pixel each way
    const byte RegionClear = 0, RegionInk = 1, RegionTeam = 2, RegionGold = 3, RegionGoldLight = 4, RegionSage = 5, RegionBadge = 6, RegionPortrait = 7;
    static readonly Dictionary<(CharacterClass, float), byte[]> regionCache = new Dictionary<(CharacterClass, float), byte[]>();
    static readonly InkArt.FrameBudget NoBudget = new InkArt.FrameBudget(float.MaxValue); // Never runs out: finish in one go

    // True once this class's shape has been worked out (so its first token won't hitch)
    public static bool IsReady(CharacterClass characterClass, float pixelsPerUnit) => regionCache.ContainsKey((characterClass, pixelsPerUnit));

    static readonly Rect BadgeBox = new Rect(BadgeCentre - BadgeHalf - Vector2.one, (BadgeHalf + Vector2.one) * 2f);

    // Which part of the frame every sample point falls in, for one class
    static IEnumerator RegionsRoutine(CharacterClass characterClass, float pixelsPerUnit, int width, int height,
                                      InkArt.FrameBudget budget, System.Action<byte[]> done)
    {
        if (regionCache.TryGetValue((characterClass, pixelsPerUnit), out byte[] cached)) { done(cached); yield break; }

        Vector2[] outline = Outline(characterClass);
        List<Extra> extras = ExtrasOf(characterClass);
        SegmentGrid edges = new SegmentGrid(Edges(outline), PortraitInset + 0.5f);
        byte[] regions = new byte[width * FrameSamples * height * FrameSamples];

        yield return SamplesRoutine(width, height, pixelsPerUnit, outline, budget, (index, p, inside) =>
        {
            regions[index] = RegionAt(characterClass, p, inside, edges, extras);
        });

        // Another caller may have finished the same class meanwhile: keep the first so everyone shares one array
        if (regionCache.TryGetValue((characterClass, pixelsPerUnit), out cached)) regions = cached;
        else regionCache[(characterClass, pixelsPerUnit)] = regions;
        done(regions);
    }

    static byte RegionAt(CharacterClass characterClass, Vector2 p, bool inside, SegmentGrid edges, List<Extra> extras)
    {
        // Class badge on top of everything
        if (BadgeBox.Contains(p))
        {
            float badge = InkArt.Chamfer(p, BadgeCentre, BadgeHalf, 7f, 2f);
            if (badge <= 0f)
            {
                if (badge > -2.4f) return RegionInk;
                return Icon(characterClass, p - BadgeCentre) <= 0f ? RegionInk : RegionBadge;
            }
        }

        foreach (Extra extra in extras)
            if (extra.inFront && extra.bounds.Contains(p)) { float d = extra.shape(p); if (d <= 0f) return d > -extra.ink ? RegionInk : FillRegion(extra.fill); }

        if (inside)
        {
            float depth = edges.Nearest(p); // MaxValue = deeper than every band
            if (depth < OutlineInk) return RegionInk;
            if (depth < OutlineInk + TeamBand) return RegionTeam;
            if (depth < OutlineInk + TeamBand + GoldLine) return RegionGold;
            if (depth < PortraitInset) return RegionInk;
            return RegionPortrait;
        }

        foreach (Extra extra in extras)
            if (!extra.inFront && extra.bounds.Contains(p)) { float d = extra.shape(p); if (d <= 0f) return d > -extra.ink ? RegionInk : FillRegion(extra.fill); }
        return RegionClear;
    }

    static byte FillRegion(Color fill) => fill == InkArt.GoldLight ? RegionGoldLight : fill == InkArt.Sage ? RegionSage : RegionGold;

    // Visits every sample point (FrameSamples x FrameSamples per pixel) with its index, position in units and whether
    // it's inside the outline. Inside/outside comes from the outline's crossings on that row. Pauses between rows
    // when the frame's budget is used up
    static IEnumerator SamplesRoutine(int width, int height, float pixelsPerUnit, Vector2[] outline, InkArt.FrameBudget budget,
                                      System.Action<int, Vector2, bool> visit)
    {
        int rowLength = width * FrameSamples;
        float step = 1f / FrameSamples;
        List<float> crossings = new List<float>();

        for (int sy = 0; sy < height * FrameSamples; sy++)
        {
            float y = ToUnits(new Vector2(0f, (sy + 0.5f) * step), pixelsPerUnit).y;
            crossings.Clear();
            for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
            {
                Vector2 a = outline[j], b = outline[i];
                if ((a.y > y) != (b.y > y)) crossings.Add(a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y));
            }
            crossings.Sort();

            int passed = 0;
            for (int sx = 0; sx < rowLength; sx++)
            {
                Vector2 p = ToUnits(new Vector2((sx + 0.5f) * step, (sy + 0.5f) * step), pixelsPerUnit);
                while (passed < crossings.Count && crossings[passed] < p.x) passed++;
                visit(sy * rowLength + sx, p, passed % 2 == 1);
            }

            if (budget.Spent) { yield return null; budget.Restart(); }
        }
    }

    // Runs a routine (and any routines it waits on) to the end right now
    static void RunNow(IEnumerator routine)
    {
        Stack<IEnumerator> stack = new Stack<IEnumerator>();
        stack.Push(routine);
        while (stack.Count > 0)
        {
            IEnumerator top = stack.Peek();
            if (!top.MoveNext()) stack.Pop();
            else if (top.Current is IEnumerator nested) stack.Push(nested);
        }
    }

    static List<(Vector2, Vector2)> Edges(Vector2[] outline)
    {
        List<(Vector2, Vector2)> edges = new List<(Vector2, Vector2)>();
        for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++) edges.Add((outline[j], outline[i]));
        return edges;
    }

    // Line segments sorted into grid cells, so "how far is the nearest segment?" only checks the few nearby ones.
    // Only answers up to reach; anything further away comes back as float.MaxValue
    class SegmentGrid
    {
        readonly float cell, reach;
        readonly Dictionary<(int, int), List<(Vector2 a, Vector2 b)>> cells = new Dictionary<(int, int), List<(Vector2, Vector2)>>();

        public SegmentGrid(List<(Vector2, Vector2)> segments, float reach)
        {
            this.reach = reach;
            cell = Mathf.Max(4f, reach);
            foreach ((Vector2 a, Vector2 b) in segments)
            {
                // Every cell within reach of the segment's box
                Vector2 min = Vector2.Min(a, b) - Vector2.one * reach, max = Vector2.Max(a, b) + Vector2.one * reach;
                for (int cx = Mathf.FloorToInt(min.x / cell); cx <= Mathf.FloorToInt(max.x / cell); cx++)
                    for (int cy = Mathf.FloorToInt(min.y / cell); cy <= Mathf.FloorToInt(max.y / cell); cy++)
                    {
                        if (!cells.TryGetValue((cx, cy), out List<(Vector2, Vector2)> list)) cells[(cx, cy)] = list = new List<(Vector2, Vector2)>();
                        list.Add((a, b));
                    }
            }
        }

        public float Nearest(Vector2 p)
        {
            if (!cells.TryGetValue((Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell)), out List<(Vector2 a, Vector2 b)> list)) return float.MaxValue;
            float best = float.MaxValue;
            foreach ((Vector2 a, Vector2 b) in list) best = Mathf.Min(best, InkArt.Segment(p, a, b));
            return best < reach ? best : float.MaxValue;
        }
    }

    // Paints a texture from one byte per sample point (0 = clear)
    static IEnumerator PaintSamplesRoutine(int width, int height, byte[] samples, Color[] palette, InkArt.FrameBudget budget, System.Action<Texture2D> done)
    {
        int rowLength = width * FrameSamples;
        return InkArt.PaintRoutine(width, height, pixel =>
        {
            int sx = Mathf.Min(rowLength - 1, (int)(pixel.x * FrameSamples));
            int sy = Mathf.Min(height * FrameSamples - 1, (int)(pixel.y * FrameSamples));
            return palette[samples[sy * rowLength + sx]];
        }, FrameSamples, false, budget, done);
    }

    // Solid white body, for the hit flash
    public static Texture2D PaintSilhouette(CharacterClass characterClass, float pixelsPerUnit)
    {
        Texture2D result = null;
        RunNow(SilhouetteRoutine(characterClass, pixelsPerUnit, null, t => result = t));
        return result;
    }

    public static IEnumerator SilhouetteRoutine(CharacterClass characterClass, float pixelsPerUnit, InkArt.FrameBudget budget, System.Action<Texture2D> done)
    {
        budget ??= NoBudget;
        int width = Mathf.RoundToInt(CanvasWidth * pixelsPerUnit), height = Mathf.RoundToInt(CanvasHeight * pixelsPerUnit);
        byte[] samples = new byte[width * FrameSamples * height * FrameSamples];
        yield return SamplesRoutine(width, height, pixelsPerUnit, Outline(characterClass), budget, (index, p, inside) => samples[index] = (byte)(inside ? 1 : 0));
        yield return PaintSamplesRoutine(width, height, samples, new[] { Color.clear, Color.white }, budget, done);
    }

    // Ink cracks spreading across the body from near the top, for the defeat
    public static Texture2D PaintCracks(CharacterClass characterClass, float pixelsPerUnit)
    {
        Texture2D result = null;
        RunNow(CracksRoutine(characterClass, pixelsPerUnit, null, t => result = t));
        return result;
    }

    public static IEnumerator CracksRoutine(CharacterClass characterClass, float pixelsPerUnit, InkArt.FrameBudget budget, System.Action<Texture2D> done)
    {
        budget ??= NoBudget;
        Vector2[] outline = Outline(characterClass);
        Vector2 origin = V(78, 80);
        List<(Vector2, Vector2)> segments = new List<(Vector2, Vector2)>();
        foreach (float startAngle in new[] { 160f, 215f, 270f, 330f, 30f, 95f })
        {
            Vector2 point = origin;
            float angle = startAngle * Mathf.Deg2Rad;
            for (int i = 0; i < 4; i++)
            {
                angle += Random.Range(-0.5f, 0.5f);
                Vector2 next = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 18f;
                segments.Add((point, next));
                point = next;
            }
        }

        SegmentGrid edges = new SegmentGrid(Edges(outline), 3.5f);
        SegmentGrid cracks = new SegmentGrid(segments, 2.6f);
        int width = Mathf.RoundToInt(CanvasWidth * pixelsPerUnit), height = Mathf.RoundToInt(CanvasHeight * pixelsPerUnit);
        byte[] samples = new byte[width * FrameSamples * height * FrameSamples];
        yield return SamplesRoutine(width, height, pixelsPerUnit, outline, budget, (index, p, inside) =>
        {
            if (!inside || edges.Nearest(p) < 3f) return; // Keep the cracks off the outline
            float best = cracks.Nearest(p);
            samples[index] = (byte)(best < 1.5f ? 1 : best < 2.6f ? 2 : 0);
        });
        yield return PaintSamplesRoutine(width, height, samples, new[] { Color.clear, InkArt.Ink, new Color(1f, 1f, 1f, 0.8f) }, budget, done); // Ink crack with a light edge
    }

    // ---- Helpers ----

    // Texture pixel (bottom-left origin) to units (y down)
    static Vector2 ToUnits(Vector2 pixel, float pixelsPerUnit) => new Vector2(CanvasLeft + pixel.x / pixelsPerUnit, CanvasBottom - pixel.y / pixelsPerUnit);

    static Vector2 V(float x, float y) => new Vector2(x, y);

    // Box around some points, with a little margin
    static Rect Box(Vector2[] points)
    {
        Vector2 min = points[0], max = points[0];
        foreach (Vector2 point in points) { min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
        return Rect.MinMaxRect(min.x - 1f, min.y - 1f, max.x + 1f, max.y + 1f);
    }

    static IEnumerable<Vector2> Quad(Vector2 a, Vector2 control, Vector2 b, int steps = 14)
    {
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            yield return (1 - t) * (1 - t) * a + 2 * (1 - t) * t * control + t * t * b;
        }
    }

    static IEnumerable<Vector2> Cubic(Vector2 a, Vector2 c1, Vector2 c2, Vector2 b, int steps = 16)
    {
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps, u = 1 - t;
            yield return u * u * u * a + 3 * u * u * t * c1 + 3 * u * t * t * c2 + t * t * t * b;
        }
    }

    // Points along an ellipse from one angle to another (0 = right, pi/2 = top), y down
    static IEnumerable<Vector2> Arc(float cx, float cy, float rx, float ry, float from, float to, int steps = 18)
    {
        for (int i = 1; i <= steps; i++)
        {
            float a = Mathf.Lerp(from, to, i / (float)steps);
            yield return new Vector2(cx + Mathf.Cos(a) * rx, cy - Mathf.Sin(a) * ry);
        }
    }

    // Approximate distance to an ellipse outline's inside (negative inside)
    static float Ellipse(Vector2 q, float rx, float ry) => (new Vector2(q.x / rx, q.y / ry).magnitude - 1f) * Mathf.Min(rx, ry);

    static Vector2[] Star(Vector2 centre, float outer, float inner)
    {
        Vector2[] points = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = -Mathf.PI / 2f + i * Mathf.PI / 5f;
            float r = i % 2 == 0 ? outer : inner;
            points[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return points;
    }
}
