using System.Collections;
using UnityEngine;

// The Inked Star Chart look in one place: the palette, and a painter that draws smooth ink-outlined shapes into
// textures made in code (tokens, board, rings, badges). Shapes are tested several times per pixel so edges are smooth.
public static class InkArt
{
    // ---- Palette (everything on the board uses only these) ----
    public static readonly Color Ink = new Color32(16, 20, 24, 255);        // Outlines and shadows (blue-black, like the UI art)
    public static readonly Color Gold = new Color32(232, 198, 110, 255);    // Star chart details, ribbons, speed
    public static readonly Color GoldLight = new Color32(255, 227, 154, 255);
    public static readonly Color Navy = new Color32(70, 94, 105, 255);      // Main slate blue-grey (#465E69, the menu buttons): board surface
    public static readonly Color Sage = new Color32(185, 206, 200, 255);    // Light sage from the UI art: rim lines
    public static readonly Color Paper = new Color32(244, 244, 240, 255);   // Off-white from the UI art
    public static readonly Color Night = new Color32(35, 47, 53, 255);      // Dark slate (#232F35): insets, space behind cards
    public static readonly Color Player1 = new Color32(77, 155, 255, 255);
    public static readonly Color Player2 = new Color32(255, 90, 90, 255);
    public static readonly Color Damage = new Color32(255, 71, 87, 255);    // HP gems and damage badges
    public static readonly Color Heal = new Color32(90, 220, 120, 255);

    public static Color PlayerColor(int owner) => owner == 2 ? Player2 : Player1;

    // ---- Painting ----

    // Builds a texture by asking colourAt for several points inside each pixel and averaging them (alpha-weighted).
    // Points are in pixels, (0,0) = bottom-left corner
    public static Texture2D Paint(int width, int height, System.Func<Vector2, Color> colourAt, int samples = 3, bool mipmaps = false)
    {
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++) PaintRow(y, width, pixels, colourAt, samples);
        return ToTexture(width, height, pixels, mipmaps);
    }

    // Same as Paint, but spread over several frames: it stops whenever this frame's time budget is used up and
    // carries on next frame (WebGL runs everything on one thread, so a long paint would freeze the game). Use in a coroutine
    public static IEnumerator PaintRoutine(int width, int height, System.Func<Vector2, Color> colourAt, int samples, bool mipmaps,
                                           FrameBudget budget, System.Action<Texture2D> done)
    {
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            PaintRow(y, width, pixels, colourAt, samples);
            if (budget.Spent) { yield return null; budget.Restart(); }
        }
        done(ToTexture(width, height, pixels, mipmaps));
    }

    static void PaintRow(int y, int width, Color[] pixels, System.Func<Vector2, Color> colourAt, int samples)
    {
        float step = 1f / samples;
        for (int x = 0; x < width; x++)
        {
            Vector4 sum = Vector4.zero;
            for (int sy = 0; sy < samples; sy++)
                for (int sx = 0; sx < samples; sx++)
                {
                    Color c = colourAt(new Vector2(x + (sx + 0.5f) * step, y + (sy + 0.5f) * step));
                    sum += new Vector4(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
            float alpha = sum.w / (samples * samples);
            pixels[y * width + x] = alpha > 0f ? new Color(sum.x / sum.w, sum.y / sum.w, sum.z / sum.w, alpha) : Color.clear;
        }
    }

    static Texture2D ToTexture(int width, int height, Color[] pixels, bool mipmaps)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, mipmaps);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply(mipmaps);
        return texture;
    }

    // How long a spread-out job may run each frame before it waits for the next one
    public class FrameBudget
    {
        readonly System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        readonly double milliseconds;
        public FrameBudget(float milliseconds) { this.milliseconds = milliseconds; }
        public bool Spent => watch.Elapsed.TotalMilliseconds > milliseconds;
        public void Restart() => watch.Restart();
    }

    public static Sprite ToSprite(Texture2D texture, Vector2 pivot, float pixelsPerUnit, float border = 0f)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, pixelsPerUnit, 0,
                             SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    // ---- Shapes (signed distance in pixels: negative inside) ----

    public static float RoundedRect(Vector2 p, Vector2 centre, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x - centre.x), Mathf.Abs(p.y - centre.y)) - half + Vector2.one * radius;
        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }

    // Signed distance to a polygon (negative inside), in the polygon's own units
    public static float Polygon(Vector2 p, Vector2[] poly)
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

    // Distance to a line segment (always positive)
    public static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.000001f, ab.sqrMagnitude));
        return (p - (a + ab * t)).magnitude;
    }

    // Rectangle with its corners cut off diagonally (the UI art's panel shape). Cuts are in pixels:
    // big top-left and bottom-right, small top-right and bottom-left, like the artist's panels
    public static float Chamfer(Vector2 p, Vector2 centre, Vector2 half, float bigCut, float smallCut)
    {
        Vector2 q = p - centre;
        Vector2 d = new Vector2(Mathf.Abs(q.x), Mathf.Abs(q.y)) - half;
        float box = Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);

        // The corner this point is nearest: top-left (x<0, y>0) and bottom-right get the big cut
        float sx = q.x < 0f ? -1f : 1f, sy = q.y < 0f ? -1f : 1f;
        float cut = sx != sy ? bigCut : smallCut;
        float corner = (q.x * sx + q.y * sy - (half.x + half.y - cut)) * 0.70710678f;
        return Mathf.Max(box, corner);
    }

    // Arch: a rectangle with rounded bottom corners and a half-ellipse on top (the tarot / star chart token shape).
    // Inset shrinks it evenly. Close to a true distance, which is plenty for outline bands
    public static float Arch(Vector2 p, float width, float height, float archHeight, float bottomRadius, float inset)
    {
        float rectTop = height - archHeight;
        float radius = Mathf.Max(0f, bottomRadius - inset);
        // The rectangle runs a little past rectTop so its rounded top corners get cut off, leaving square shoulders under the arch
        float top = rectTop + radius + 1f;
        float rect = RoundedRect(p, new Vector2(width / 2f, (inset + top) / 2f), new Vector2(width / 2f - inset, (top - inset) / 2f), radius);
        if (p.y > rectTop) rect = Mathf.Max(rect, p.y - rectTop);

        Vector2 radii = new Vector2(width / 2f - inset, archHeight - inset);
        Vector2 local = p - new Vector2(width / 2f, rectTop);
        float ellipse = radii.x <= 0f || radii.y <= 0f ? 1f
                      : (new Vector2(local.x / radii.x, local.y / radii.y).magnitude - 1f) * Mathf.Min(radii.x, radii.y);
        if (p.y < rectTop) ellipse = Mathf.Max(ellipse, rectTop - p.y);
        return Mathf.Min(rect, ellipse);
    }

    // Four-point sparkle star (like ✦), centred, size = tip to tip. Not a true distance, scaled to roughly pixels
    public static float Sparkle(Vector2 p, Vector2 centre, float size)
    {
        Vector2 d = (p - centre) / (size * 0.5f);
        float value = Mathf.Sqrt(Mathf.Abs(d.x)) + Mathf.Sqrt(Mathf.Abs(d.y)) - 1f;
        return value * size * 0.25f;
    }

    // Ink outline of a given thickness around a filled shape: ink at the edge, fill inside, clear outside
    public static Color Outlined(float distance, float outline, Color fill)
    {
        if (distance > 0f) return Color.clear;
        return distance > -outline ? Ink : fill;
    }
}
