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
        public Extra(System.Func<Vector2, float> shape, Color fill, float ink, bool inFront) { this.shape = shape; this.fill = fill; this.ink = ink; this.inFront = inFront; }
    }

    static List<Extra> ExtrasOf(CharacterClass characterClass)
    {
        List<Extra> extras = new List<Extra>();
        switch (characterClass)
        {
            case CharacterClass.Attacker: // Blade fins on the shoulders
                Vector2[] leftFin = { V(12, 92), V(-2, 62), V(20, 74) }, rightFin = { V(128, 92), V(142, 62), V(120, 74) };
                extras.Add(new Extra(p => InkArt.Polygon(p, leftFin), InkArt.Gold, 1.8f, false));
                extras.Add(new Extra(p => InkArt.Polygon(p, rightFin), InkArt.Gold, 1.8f, false));
                break;
            case CharacterClass.Healer: // Floating halo
                extras.Add(new Extra(p => Mathf.Abs(Ellipse(p - V(70, 18), 28f, 7f)) - 3.6f, InkArt.GoldLight, 1.6f, true));
                break;
            case CharacterClass.Support: // Notched pennants
                Vector2[] leftFlag = { V(14, 70), V(-4, 74), V(4, 86), V(-4, 98), V(14, 96) };
                Vector2[] rightFlag = { V(126, 70), V(144, 74), V(136, 86), V(144, 98), V(126, 96) };
                extras.Add(new Extra(p => InkArt.Polygon(p, leftFlag), InkArt.Sage, 1.8f, false));
                extras.Add(new Extra(p => InkArt.Polygon(p, rightFlag), InkArt.Sage, 1.8f, false));
                break;
            case CharacterClass.Mage: // Star on the tip
                Vector2[] star = Star(V(70, 4), 9.5f, 4.2f);
                extras.Add(new Extra(p => InkArt.Polygon(p, star), InkArt.GoldLight, 1.8f, true));
                break;
            case CharacterClass.Assassin: // Blade tip off the high corner
                Vector2[] tip = { V(126, 28), V(141, 15), V(128, 44) };
                extras.Add(new Extra(p => InkArt.Polygon(p, tip), InkArt.Gold, 1.8f, true));
                break;
            case CharacterClass.Ranged: // Range-finder ticks down the sides
                foreach (float y in new[] { 110f, 124f, 138f })
                {
                    float tickY = y;
                    extras.Add(new Extra(p => InkArt.Segment(p, V(1, tickY), V(11, tickY)) - 2.4f, InkArt.Gold, 1.2f, false));
                    extras.Add(new Extra(p => InkArt.Segment(p, V(129, tickY), V(139, tickY)) - 2.4f, InkArt.Gold, 1.2f, false));
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
    // portraitColour gives the art colour at a point inside the portrait box (in units)
    public static Texture2D PaintFrame(CharacterClass characterClass, Color team, System.Func<Vector2, Color> portraitColour, float pixelsPerUnit)
    {
        Vector2[] outline = Outline(characterClass);
        List<Extra> extras = ExtrasOf(characterClass);
        Color badgeColour = ColourOf(characterClass);
        int width = Mathf.RoundToInt(CanvasWidth * pixelsPerUnit), height = Mathf.RoundToInt(CanvasHeight * pixelsPerUnit);

        return InkArt.Paint(width, height, pixel =>
        {
            Vector2 p = ToUnits(pixel, pixelsPerUnit);

            // Class badge on top of everything
            float badge = InkArt.Chamfer(p, BadgeCentre, BadgeHalf, 7f, 2f);
            if (badge <= 0f)
            {
                if (badge > -2.4f) return InkArt.Ink;
                return Icon(characterClass, p - BadgeCentre) <= 0f ? InkArt.Ink : badgeColour;
            }

            foreach (Extra extra in extras)
                if (extra.inFront && extra.shape(p) <= 0f) return extra.shape(p) > -extra.ink ? InkArt.Ink : extra.fill;

            float edge = InkArt.Polygon(p, outline);
            if (edge <= 0f)
            {
                float depth = -edge;
                if (depth < OutlineInk) return InkArt.Ink;
                if (depth < OutlineInk + TeamBand) return team;
                if (depth < OutlineInk + TeamBand + GoldLine) return InkArt.Gold;
                if (depth < PortraitInset) return InkArt.Ink;
                Color art = portraitColour(p);
                art.a = 1f;
                return art;
            }

            foreach (Extra extra in extras)
                if (!extra.inFront && extra.shape(p) <= 0f) return extra.shape(p) > -extra.ink ? InkArt.Ink : extra.fill;
            return Color.clear;
        }, 2, true);
    }

    // Solid white body, for the hit flash
    public static Texture2D PaintSilhouette(CharacterClass characterClass, float pixelsPerUnit)
    {
        Vector2[] outline = Outline(characterClass);
        return InkArt.Paint(Mathf.RoundToInt(CanvasWidth * pixelsPerUnit), Mathf.RoundToInt(CanvasHeight * pixelsPerUnit), pixel =>
            InkArt.Polygon(ToUnits(pixel, pixelsPerUnit), outline) <= 0f ? Color.white : Color.clear, 2);
    }

    // Ink cracks spreading across the body from near the top, for the defeat
    public static Texture2D PaintCracks(CharacterClass characterClass, float pixelsPerUnit)
    {
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

        return InkArt.Paint(Mathf.RoundToInt(CanvasWidth * pixelsPerUnit), Mathf.RoundToInt(CanvasHeight * pixelsPerUnit), pixel =>
        {
            Vector2 p = ToUnits(pixel, pixelsPerUnit);
            if (InkArt.Polygon(p, outline) > -3f) return Color.clear;
            float best = float.MaxValue;
            foreach ((Vector2 a, Vector2 b) in segments) best = Mathf.Min(best, InkArt.Segment(p, a, b));
            return best < 1.5f ? InkArt.Ink : best < 2.6f ? new Color(1f, 1f, 1f, 0.8f) : Color.clear;
        }, 2);
    }

    // ---- Helpers ----

    // Texture pixel (bottom-left origin) to units (y down)
    static Vector2 ToUnits(Vector2 pixel, float pixelsPerUnit) => new Vector2(CanvasLeft + pixel.x / pixelsPerUnit, CanvasBottom - pixel.y / pixelsPerUnit);

    static Vector2 V(float x, float y) => new Vector2(x, y);

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
