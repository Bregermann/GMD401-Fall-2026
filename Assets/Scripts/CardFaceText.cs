using TMPro;
using UnityEngine;

// Writes a card's name, HP, stats and attacks onto blank card art, in the same layout as the printed cards (e.g. Chandra).
// Box positions are fractions of the art (0,0 = top left, 1,1 = bottom right), measured from the blank card template,
// so they fit any resolution of that template. Text auto-sizes to fit each box.
public static class CardFaceText
{
    // Boxes on the template: x, y, width, height
    static readonly Rect SpeedBox   = new Rect(0.081f, 0.567f, 0.236f, 0.050f);
    static readonly Rect PhysDefBox = new Rect(0.363f, 0.568f, 0.179f, 0.048f);
    static readonly Rect MagDefBox  = new Rect(0.591f, 0.567f, 0.189f, 0.049f);
    static readonly Rect NameBox    = new Rect(0.045f, 0.620f, 0.593f, 0.071f);
    static readonly Rect HPBox      = new Rect(0.642f, 0.620f, 0.313f, 0.071f);
    static readonly Rect AttackArea = new Rect(0.043f, 0.694f, 0.914f, 0.268f);

    const float Padding = 0.012f;              // Gap inside each box
    static readonly Color Ink = new Color(0.08f, 0.08f, 0.1f);
    static readonly Color HealInk = new Color(0.1f, 0.5f, 0.2f);
    static readonly Color LineColor = new Color(0.35f, 0.35f, 0.38f);

    // Builds the text under the art renderer and returns the parent object (so it can be rebuilt later).
    // sortingOrder should be above the art so the text draws on top of it
    public static GameObject Build(SpriteRenderer art, CardData data, TMP_FontAsset font, int sortingOrder)
    {
        GameObject root = new GameObject("Card Text");
        root.transform.SetParent(art.transform, false);
        Vector2 artSize = art.sprite.bounds.size;

        AddText(root, artSize, SpeedBox, $"{data.speed} SP", TextAlignmentOptions.Center, FontStyles.Bold, Ink, font, sortingOrder);
        AddText(root, artSize, PhysDefBox, $"{data.physicalDefense} PD", TextAlignmentOptions.Center, FontStyles.Bold, Ink, font, sortingOrder);
        AddText(root, artSize, MagDefBox, $"{data.magicDefense} MD", TextAlignmentOptions.Center, FontStyles.Bold, Ink, font, sortingOrder);
        AddText(root, artSize, NameBox, data.cardName, TextAlignmentOptions.Left, FontStyles.Bold, Ink, font, sortingOrder);
        AddText(root, artSize, HPBox, $"{data.maxHP} Hp", TextAlignmentOptions.Center, FontStyles.Bold, Ink, font, sortingOrder);

        // Attacks in a 2x2 grid with lines between them, like the printed cards
        AttackData[] attacks = { data.attack1, data.attack2, data.attack3, data.attack4 };
        float cellWidth = AttackArea.width / 2f;
        float cellHeight = AttackArea.height / 2f;
        for (int i = 0; i < attacks.Length; i++)
        {
            Rect cell = new Rect(AttackArea.x + cellWidth * (i % 2), AttackArea.y + cellHeight * (i / 2), cellWidth, cellHeight);
            AddAttack(root, artSize, cell, attacks[i], font, sortingOrder);
        }
        AddLine(root, art, new Rect(AttackArea.x + cellWidth - 0.002f, AttackArea.y, 0.004f, AttackArea.height), sortingOrder);
        AddLine(root, art, new Rect(AttackArea.x, AttackArea.y + cellHeight - 0.002f, AttackArea.width, 0.004f), sortingOrder);

        return root;
    }

    // Name and damage on the top line, description in smaller text below
    static void AddAttack(GameObject root, Vector2 artSize, Rect cell, AttackData attack, TMP_FontAsset font, int sortingOrder)
    {
        if (attack == null) return;

        float top = cell.height * 0.32f;
        Rect nameBox = new Rect(cell.x, cell.y, cell.width * 0.72f, top);
        Rect damageBox = new Rect(cell.x + cell.width * 0.72f, cell.y, cell.width * 0.28f, top);
        Rect descriptionBox = new Rect(cell.x, cell.y + top, cell.width, cell.height - top);

        AddText(root, artSize, nameBox, attack.attackName, TextAlignmentOptions.Left, FontStyles.Bold, Ink, font, sortingOrder);

        // Damage number, healing in green, nothing for status-only attacks
        if (attack.damage > 0)
        {
            bool heal = attack.damageType == DamageType.Heal;
            AddText(root, artSize, damageBox, heal ? $"+{attack.damage}" : attack.damage.ToString(),
                    TextAlignmentOptions.Right, FontStyles.Bold, heal ? HealInk : Ink, font, sortingOrder);
        }

        if (!string.IsNullOrWhiteSpace(attack.description))
        {
            TextMeshPro description = AddText(root, artSize, descriptionBox, attack.description, TextAlignmentOptions.TopLeft,
                                              FontStyles.Normal, Ink, font, sortingOrder);
            description.fontSizeMax *= 0.45f; // Smaller than the name, like the printed cards
        }
    }

    // One piece of text filling a box on the art
    static TextMeshPro AddText(GameObject root, Vector2 artSize, Rect box, string text, TextAlignmentOptions alignment,
                               FontStyles style, Color color, TMP_FontAsset font, int sortingOrder)
    {
        GameObject textObject = new GameObject(text);
        textObject.transform.SetParent(root.transform, false);
        textObject.transform.localPosition = BoxCentre(artSize, box) + new Vector3(0f, 0f, -0.01f); // Just in front of the art

        TextMeshPro tmp = textObject.AddComponent<TextMeshPro>();
        if (font != null) tmp.font = font;
        tmp.rectTransform.sizeDelta = new Vector2((box.width - Padding * 2f) * artSize.x, (box.height - Padding) * artSize.y);
        tmp.text = text;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.enableAutoSizing = true;                         // Shrinks long names and descriptions to fit
        tmp.fontSizeMin = 0.1f;
        tmp.fontSizeMax = box.height * artSize.y * 7f;       // About as tall as the box
        tmp.sortingOrder = sortingOrder;
        return tmp;
    }

    // Thin divider line, made from a stretched white pixel
    static void AddLine(GameObject root, SpriteRenderer art, Rect box, int sortingOrder)
    {
        Vector2 artSize = art.sprite.bounds.size;
        GameObject lineObject = new GameObject("Line");
        lineObject.transform.SetParent(root.transform, false);
        lineObject.transform.localPosition = BoxCentre(artSize, box) + new Vector3(0f, 0f, -0.005f);

        SpriteRenderer line = lineObject.AddComponent<SpriteRenderer>();
        line.sprite = WhitePixel();
        line.sharedMaterial = art.sharedMaterial;
        line.color = LineColor;
        line.sortingOrder = sortingOrder;
        Vector2 pixelSize = line.sprite.bounds.size;
        lineObject.transform.localScale = new Vector3(box.width * artSize.x / pixelSize.x, box.height * artSize.y / pixelSize.y, 1f);
    }

    // Box on the art (fractions, from top left) to a position in the art sprite's own space (centre is 0,0, up is +y)
    static Vector3 BoxCentre(Vector2 artSize, Rect box)
    {
        return new Vector3((box.center.x - 0.5f) * artSize.x, (0.5f - box.center.y) * artSize.y, 0f);
    }

    static Sprite whitePixel;
    static Sprite WhitePixel()
    {
        if (whitePixel != null) return whitePixel;
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        whitePixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return whitePixel;
    }
}
