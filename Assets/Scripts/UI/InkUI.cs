using UnityEngine;
using UnityEngine.UI;

// Screen UI pieces in the Inked Star Chart style, shared by the HUD, game log and card inspector:
// - framed panel: slate fill under an ink-sage-ink rim, with a hard ink drop shadow
// - chip: a cut-corner shape in any colour with a thick ink outline (buttons, tags, key caps)
// Corners are cut diagonally like the UI artist's panels: big cuts top-left and bottom-right, small ones on the others
// Sprites are made once in code and 9-sliced, so they fit any size
public static class InkUI
{
    public static readonly Color PanelColor = new Color(0.275f, 0.369f, 0.412f); // Slate blue-grey #465E69, the menu buttons
    public static readonly Color PanelDark = new Color(0.137f, 0.184f, 0.208f);  // Dark slate #232F35: header strips, inset areas
    public static readonly Color Text = new Color(0.96f, 0.95f, 1f);
    public static readonly Color DimText = new Color(0.78f, 0.84f, 0.85f);
    public static readonly Color GoldText = new Color(1f, 0.89f, 0.6f);

    private static Sprite chip, frame, fill;

    public static Sprite ChipSprite => chip != null ? chip : chip = MakeChip();
    public static Sprite FrameSprite => frame != null ? frame : frame = MakeFrame();
    public static Sprite FillSprite => fill != null ? fill : fill = MakeFill();

    // Big panel. Returns the fill Image, so changing its colour changes the panel colour (the rim stays ink and sage)
    public static Image FramedPanel(Transform parent, string name, Color color, float shadow = 7f)
    {
        Image panel = NewImage(parent, name, color);
        panel.sprite = FillSprite;
        panel.type = Image.Type.Sliced;
        panel.raycastTarget = true;
        if (shadow > 0f) AddShadow(panel, shadow);

        Image rim = NewImage(panel.transform, "Rim", Color.white);
        rim.sprite = FrameSprite;
        rim.type = Image.Type.Sliced;
        rim.raycastTarget = false;
        RectTransform rect = rim.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return panel;
    }

    // Chip, button or small frame in any colour, with an ink outline and a small hard shadow
    public static Image Chip(Transform parent, string name, Color color, float shadow = 3f)
    {
        Image image = NewImage(parent, name, color);
        image.sprite = ChipSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1.6f; // Thinner corners so small chips still fit them
        image.raycastTarget = true;
        if (shadow > 0f) AddShadow(image, shadow);
        return image;
    }

    public static void AddShadow(Graphic graphic, float distance)
    {
        Shadow shadow = graphic.gameObject.AddComponent<Shadow>();
        shadow.effectColor = InkArt.Ink;
        shadow.effectDistance = new Vector2(distance, -distance);
    }

    static Image NewImage(Transform parent, string name, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // ---- Drawing ----

    // Cut-corner shape from the UI art: big diagonal cuts top-left and bottom-right, small ones on the other two
    const float FrameBigCut = 26f, FrameSmallCut = 7f;

    // White cut-corner shape with a thick ink outline, tinted by the Image colour
    static Sprite MakeChip()
    {
        const int size = 48;
        Texture2D texture = InkArt.Paint(size, size, p =>
            InkArt.Outlined(InkArt.Chamfer(p, new Vector2(size / 2f, size / 2f), new Vector2(size / 2f - 1f, size / 2f - 1f), 13f, 4f), 5f, Color.white));
        return InkArt.ToSprite(texture, new Vector2(0.5f, 0.5f), 100f, 18f);
    }

    // Panel rim: ink band, sage line, thin ink line, see-through middle (sits on top of the fill).
    // Plus a short sage accent line inside the top-left cut, like the artist's panels
    static Sprite MakeFrame()
    {
        const int size = 96;
        const float border = 30f;
        Vector2 centre = new Vector2(size / 2f, size / 2f), half = new Vector2(size / 2f - 1f, size / 2f - 1f);
        Texture2D texture = InkArt.Paint(size, size, p =>
        {
            float edge = InkArt.Chamfer(p, centre, half, FrameBigCut, FrameSmallCut);
            if (edge > 0f) return Color.clear;
            float depth = -edge;
            if (depth < 6f) return InkArt.Ink;
            if (depth < 9f) return InkArt.Sage;
            if (depth < 11f) return InkArt.Ink;

            // Accent: a line parallel to the top-left cut, kept inside the corner (the part that doesn't stretch)
            bool inCorner = p.x < border && p.y > size - border;
            float fromCut = ((half.x + half.y - FrameBigCut) - (-(p.x - centre.x) + (p.y - centre.y))) * 0.70710678f;
            if (inCorner && fromCut > 14f && fromCut < 16.5f) return InkArt.Sage;
            return Color.clear;
        });
        return InkArt.ToSprite(texture, new Vector2(0.5f, 0.5f), 100f, border);
    }

    // Panel fill: the same cut-corner shape, solid white, tinted by the Image colour
    static Sprite MakeFill()
    {
        const int size = 96;
        Texture2D texture = InkArt.Paint(size, size, p =>
            InkArt.Chamfer(p, new Vector2(size / 2f, size / 2f), new Vector2(size / 2f - 2f, size / 2f - 2f), FrameBigCut - 1f, FrameSmallCut - 1f) <= 0f ? Color.white : Color.clear);
        return InkArt.ToSprite(texture, new Vector2(0.5f, 0.5f), 100f, 30f);
    }
}
