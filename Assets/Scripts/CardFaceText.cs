using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Writes a card's name, HP, stats, movement and attacks onto blank card art (artHasText off).
// Look: big Lilita numbers with small grey labels, damage coloured by type, and each attack's effects as small coloured
// chips (range, area, buffs/debuffs with amounts, stun, knockback, class counters) built from the attack's data.
// Box positions are fractions of the art (0,0 = top left, 1,1 = bottom right), measured from the blank card template,
// so they fit any resolution of that template. Sizes are in "cw": percent of the card's width
public static class CardFaceText
{
    // Boxes on the template: x, y, width, height
    static readonly Rect SpeedBox   = new Rect(0.081f, 0.567f, 0.236f, 0.050f);
    static readonly Rect PhysDefBox = new Rect(0.363f, 0.568f, 0.179f, 0.048f);
    static readonly Rect MagDefBox  = new Rect(0.591f, 0.567f, 0.189f, 0.049f);
    static readonly Rect NameBox    = new Rect(0.045f, 0.620f, 0.593f, 0.071f);
    static readonly Rect HPBox      = new Rect(0.642f, 0.620f, 0.313f, 0.071f);
    static readonly Rect MoveBox    = new Rect(0.752f, 0.048f, 0.107f, 0.064f);
    static readonly Rect DiagonalBox = new Rect(0.859f, 0.037f, 0.108f, 0.075f);
    static readonly Rect AttackBox  = new Rect(0.043f, 0.694f, 0.910f, 0.256f); // Stops short of the white box's edges

    // Colours
    static readonly Color Ink = Hex("16191d");
    static readonly Color Label = Hex("6b747c");
    static readonly Color Physical = Hex("c8402f");
    static readonly Color Magic = Hex("2f66c8");
    static readonly Color Heal = Hex("23884a");
    static readonly Color LineColor = Hex("cfd5d8");

    // Chip styles: fill, border, text
    enum ChipKind { Range, Area, Buff, Debuff, Stun, Push, Counter }
    static readonly Dictionary<ChipKind, Color[]> ChipColours = new Dictionary<ChipKind, Color[]>
    {
        { ChipKind.Range,  new[] { Hex("e8eef1"), Hex("b9c6cc"), Hex("3d4b53") } },
        { ChipKind.Area,   new[] { Hex("eae6f4"), Hex("c3b8de"), Hex("4e4370") } },
        { ChipKind.Buff,   new[] { Hex("e0f2e6"), Hex("a9d6b7"), Hex("1d6b3a") } },
        { ChipKind.Debuff, new[] { Hex("fbe3df"), Hex("e7b1a8"), Hex("8a2b1f") } },
        { ChipKind.Stun,   new[] { Hex("fff1c9"), Hex("e5c56e"), Hex("7a5a0c") } },
        { ChipKind.Push,   new[] { Hex("ece8e2"), Hex("cbc0b0"), Hex("5a4a33") } },
    };

    struct Chip
    {
        public string text;
        public ChipKind kind;
        public CharacterClass counters; // For Counter chips: coloured by the class it beats
        public Chip(string text, ChipKind kind, CharacterClass counters = CharacterClass.Attacker)
        {
            this.text = text.ToUpperInvariant().Replace(" VS ", " vs "); // Small "x1.25 vs HEALER" reads better
            if (this.text.Length > 1 && this.text[0] == 'X' && char.IsDigit(this.text[1])) this.text = "x" + this.text.Substring(1);
            this.kind = kind; this.counters = counters;
        }
    }

    // Builds the text under the art renderer and returns the parent object (so it can be rebuilt later).
    // sortingOrder should be above the art so the text draws on top of it. font is only used if the game fonts are missing
    public static GameObject Build(SpriteRenderer art, CardData data, TMP_FontAsset font, int sortingOrder)
    {
        GameObject root = new GameObject("Card Text");
        root.transform.SetParent(art.transform, false);
        var card = new Layout(root, art, font, sortingOrder);

        // Stats: big number, small grey label on the same line
        card.Stat(SpeedBox, data.speed.ToString(), "SPD", 5f, Label);
        card.Stat(PhysDefBox, data.physicalDefense.ToString(), "P.DEF", 5f, Label);
        card.Stat(MagDefBox, data.magicDefense.ToString(), "M.DEF", 5f, Label);
        card.Stat(HPBox, data.maxHP.ToString(), "HP", 6.6f, Physical);

        // Name, left aligned
        Rect name = card.Inset(NameBox, 3.2f, 0f, 1.2f, 0f);
        card.Text(name, data.cardName, card.DisplayFont, 7f, Ink, TextAlignmentOptions.Left, -0.01f);

        // Top right: how far it moves, and whether it can go diagonally
        card.Corner(MoveBox, data.moveSpaces.ToString(), "MOVE");
        card.Corner(DiagonalBox, data.canMoveDiagonally ? "X" : "+", data.canMoveDiagonally ? "ANY WAY" : "STRAIGHT");

        // Attacks in a 2x2 grid with lines between them
        AttackData[] attacks = { data.attack1, data.attack2, data.attack3, data.attack4 };
        float cellWidth = AttackBox.width / 2f;
        float cellHeight = AttackBox.height / 2f;
        for (int i = 0; i < attacks.Length; i++)
        {
            Rect cell = new Rect(AttackBox.x + cellWidth * (i % 2), AttackBox.y + cellHeight * (i / 2), cellWidth, cellHeight);
            if (attacks[i] != null) card.Attack(cell, attacks[i], data, i == 2); // Bottom left sits over the template's corner notch
        }
        float line = 0.3f / 100f; // 0.3cw thick
        card.Line(new Rect(AttackBox.x + cellWidth - line / 2f, AttackBox.y, line, AttackBox.height));
        card.Line(new Rect(AttackBox.x, AttackBox.y + cellHeight - card.HeightOf(0.3f) / 2f, AttackBox.width, card.HeightOf(0.3f)));

        card.Finish();
        return root;
    }

    // ---- Chips from attack data ----

    static List<Chip> ChipsFor(AttackData attack, CardData owner)
    {
        var chips = new List<Chip>();
        bool fromSelf = attack.area == AttackArea.Self || attack.area == AttackArea.AroundSelf;

        // Reach: melee (1 space) needs no chip
        if (!fromSelf)
        {
            if (attack.range > 1) chips.Add(new Chip($"Range {attack.range}", ChipKind.Range));
            else if (attack.range == 0 && owner.characterClass == CharacterClass.Ranged) chips.Add(new Chip("Any range", ChipKind.Range));
        }

        // Area
        int span = attack.areaSize * 2 + 1;
        if (attack.area == AttackArea.AroundTarget) chips.Add(new Chip($"{span}x{span} area", ChipKind.Area));
        if (attack.area == AttackArea.AroundSelf) chips.Add(new Chip("Around self", ChipKind.Area));

        if (attack.hasKnockback) chips.Add(new Chip("Knockback", ChipKind.Push));

        // Effects on the target (or allies around), then on the user
        bool onAllies = fromSelf && attack.area == AttackArea.AroundSelf && attack.areaAffects == AreaAffects.Allies;
        AddEffects(chips, attack.targetStatusEffects, onAllies ? "Allies " : "", false);
        AddEffects(chips, attack.selfStatusEffects, attack.area == AttackArea.Self ? "" : "Self ", true);

        if (attack.ignoresDefense) chips.Add(new Chip("Ignores def", ChipKind.Debuff));
        if (attack.executeAtOrBelowHP > 0) chips.Add(new Chip($"Finishes at {attack.executeAtOrBelowHP} HP", ChipKind.Debuff));

        // Conditional bonuses, kept short
        if (attack.bonuses != null)
            foreach (AttackBonus bonus in attack.bonuses)
            {
                if (bonus == null) continue;
                string extra = bonus.bonusDamage > 0 ? $"+{bonus.bonusDamage}" : "Bonus";
                switch (bonus.condition)
                {
                    case BonusCondition.TargetHasStatus: chips.Add(new Chip($"{extra} if {ShortName(bonus.status)}", ChipKind.Range)); break;
                    case BonusCondition.TargetDebuffCount: chips.Add(new Chip($"{extra} per debuff", ChipKind.Range)); break;
                    case BonusCondition.FromSide: chips.Add(new Chip($"{extra} from {bonus.side}", ChipKind.Range)); break;
                    case BonusCondition.TargetClass: chips.Add(new Chip($"{extra} vs {bonus.targetClass}", ChipKind.Counter, bonus.targetClass)); break;
                    case BonusCondition.AllyInPlay: if (bonus.ally != null) chips.Add(new Chip($"{extra} with {bonus.ally.cardName}", ChipKind.Range)); break;
                    default: chips.Add(new Chip(extra, ChipKind.Range)); break;
                }
            }

        switch (attack.whileOnCooldown)
        {
            case CooldownPassive.DamageAttackers: chips.Add(new Chip($"Thorns {attack.passiveDamage}", ChipKind.Buff)); break;
            case CooldownPassive.KnockbackImmune: chips.Add(new Chip("Can't be pushed", ChipKind.Buff)); break;
        }

        // Positional and class bonuses last
        if (attack.positionalBonus != PositionalBonus.None)
            chips.Add(new Chip($"x{attack.positionalMultiplier:0.##} from {attack.positionalBonus}", ChipKind.Range));
        if (attack.hasClassBonus)
            chips.Add(new Chip($"x{attack.classMultiplier:0.##} vs {attack.classBonusTarget}", ChipKind.Counter, attack.classBonusTarget));

        return chips;
    }

    // Buffs and debuffs with their amounts. Matching physical/magic pairs share one chip ("P.DEF & M.DEF +200")
    static void AddEffects(List<Chip> chips, StatusEffectApplication[] effects, string prefix, bool onSelf)
    {
        if (effects == null) return;
        var list = new List<StatusEffectApplication>();
        foreach (var effect in effects) if (effect != null) list.Add(effect);

        for (int i = 0; i < list.Count; i++)
        {
            StatusEffectType type = list[i].type;
            int amount = AmountOf(list[i]);

            if (type == StatusEffectType.Stun) { chips.Add(new Chip(onSelf ? "Stuns self" : prefix + "Stun", ChipKind.Stun)); continue; }
            if (type == StatusEffectType.Stagger) { chips.Add(new Chip(prefix + "Stagger", ChipKind.Stun)); continue; }

            string label = EffectLabel(type, amount);
            StatusEffectType? partner = PartnerOf(type);
            if (partner.HasValue)
            {
                int match = list.FindIndex(i + 1, e => e.type == partner.Value && AmountOf(e) == amount);
                if (match >= 0)
                {
                    list.RemoveAt(match);
                    label = $"{ShortName(type)} & {ShortName(partner.Value)} {Signed(type, amount)}";
                }
            }
            chips.Add(new Chip(prefix + label, type.IsBuff() ? ChipKind.Buff : ChipKind.Debuff));
        }
    }

    static int AmountOf(StatusEffectApplication effect) =>
        effect.amount != 0 ? effect.amount : StatusEffectHandler.GetDefaultAmount(effect.type);

    static StatusEffectType? PartnerOf(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.PhysicalDefenseUp: return StatusEffectType.MagicDefenseUp;
            case StatusEffectType.MagicDefenseUp: return StatusEffectType.PhysicalDefenseUp;
            case StatusEffectType.PhysicalDefenseDown: return StatusEffectType.MagicDefenseDown;
            case StatusEffectType.MagicDefenseDown: return StatusEffectType.PhysicalDefenseDown;
            case StatusEffectType.PhysicalAttackUp: return StatusEffectType.MagicAttackUp;
            case StatusEffectType.MagicAttackUp: return StatusEffectType.PhysicalAttackUp;
            case StatusEffectType.PhysicalAttackDown: return StatusEffectType.MagicAttackDown;
            case StatusEffectType.MagicAttackDown: return StatusEffectType.PhysicalAttackDown;
            default: return null;
        }
    }

    static string EffectLabel(StatusEffectType type, int amount)
    {
        switch (type)
        {
            case StatusEffectType.Haste: return $"Move +{amount}";
            case StatusEffectType.Hindered: return $"Move -{amount}";
            case StatusEffectType.Regeneration: return $"Regen {amount}";
            case StatusEffectType.Burn: return $"Burn {amount}";
            case StatusEffectType.Poison: return $"Poison {amount}";
            case StatusEffectType.HealBlock: return "No healing";
            default: return $"{ShortName(type)} {Signed(type, amount)}";
        }
    }

    static string Signed(StatusEffectType type, int amount) => (type.IsBuff() ? "+" : "-") + amount;

    static string ShortName(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.PhysicalAttackUp: case StatusEffectType.PhysicalAttackDown: return "P.ATK";
            case StatusEffectType.MagicAttackUp: case StatusEffectType.MagicAttackDown: return "M.ATK";
            case StatusEffectType.PhysicalDefenseUp: case StatusEffectType.PhysicalDefenseDown: return "P.DEF";
            case StatusEffectType.MagicDefenseUp: case StatusEffectType.MagicDefenseDown: return "M.DEF";
            case StatusEffectType.SpeedUp: case StatusEffectType.SpeedDown: return "SPD";
            case StatusEffectType.HealBlock: return "Heal block";
            default: return type.ToString();
        }
    }

    // ---- Placing things on the card ----

    class Layout
    {
        readonly GameObject root;
        readonly SpriteRenderer art;
        readonly Vector2 size;      // Art size in its own units
        readonly int order;
        public readonly TMP_FontAsset DisplayFont, BodyFont;
        TextMeshPro measure;        // Hidden text used to measure chip widths

        public Layout(GameObject root, SpriteRenderer art, TMP_FontAsset fallback, int order)
        {
            this.root = root;
            this.art = art;
            this.order = order;
            size = art.sprite.bounds.size;
            DisplayFont = GameFonts.Display != null ? GameFonts.Display : fallback;
            BodyFont = GameFonts.Body != null ? GameFonts.Body : fallback;
        }

        // cw (percent of card width) to art units, and to a fraction of the art's height
        float Units(float cw) => cw / 100f * size.x;
        public float HeightOf(float cw) => Units(cw) / size.y;

        // Shrinks a box by cw amounts on each side
        public Rect Inset(Rect box, float left, float top, float right, float bottom) =>
            new Rect(box.x + left / 100f, box.y + HeightOf(top), box.width - (left + right) / 100f, box.height - HeightOf(top + bottom));

        // Number and a small coloured label on one line
        public void Stat(Rect box, string number, string label, float numberCw, Color labelColour)
        {
            float labelPercent = (label == "HP" ? 2.6f : 2.15f) / numberCw * 100f;
            string text = $"{number}<space=0.2em><font=\"{BodyFont?.name}\"><size={labelPercent:0}%><cspace=0.06em><color=#{ColorUtility.ToHtmlStringRGB(labelColour)}>{label}</color></cspace></size></font>";
            Text(Inset(box, 0.8f, 0f, 0.8f, 0f), text, DisplayFont, numberCw, Ink, TextAlignmentOptions.Center, -0.01f);
        }

        // Big number or symbol on top, tiny label underneath (the boxes in the top right corner)
        public void Corner(Rect box, string big, string label)
        {
            Rect top = new Rect(box.x, box.y + box.height * 0.08f, box.width, box.height * 0.62f);
            Rect bottom = new Rect(box.x, box.y + box.height * 0.68f, box.width, box.height * 0.24f);
            Text(top, big, DisplayFont, 6f, Ink, TextAlignmentOptions.Center, -0.01f);
            Text(Inset(bottom, 0.4f, 0f, 0.4f, 0f), $"<cspace=0.06em>{label}</cspace>", BodyFont, 1.55f, Label, TextAlignmentOptions.Center, -0.01f);
        }

        // Name and damage on the top line, effect chips underneath
        public void Attack(Rect cell, AttackData attack, CardData owner, bool overNotch)
        {
            Rect inner = Inset(cell, 2.2f, 1.6f, 2.2f, overNotch ? 8f : 1.2f);
            float headHeight = HeightOf(4.4f);
            Rect head = new Rect(inner.x, inner.y, inner.width, headHeight);

            // Damage on the right, coloured by type. Nothing for attacks with no damage
            float damageWidth = 0f;
            if (attack.damage > 0)
            {
                bool heal = attack.damageType == DamageType.Heal;
                Color colour = heal ? Heal : attack.damageType == DamageType.Magic ? Magic : Physical;
                string damage = heal ? $"+{attack.damage}" : attack.damage.ToString();
                damageWidth = Measure(damage, DisplayFont, 3.9f) / size.x + 0.01f;
                Text(new Rect(head.xMax - damageWidth, head.y, damageWidth, head.height), damage, DisplayFont, 3.9f, colour, TextAlignmentOptions.Right, -0.01f);
            }
            Text(new Rect(head.x, head.y, head.width - damageWidth - 0.01f, head.height), attack.attackName, DisplayFont, 3.55f, Ink, TextAlignmentOptions.Left, -0.01f);

            Rect chipArea = new Rect(inner.x, head.yMax + HeightOf(1f), inner.width, inner.yMax - head.yMax - HeightOf(1f));
            List<Chip> chips = ChipsFor(attack, owner);
            if (chips.Count > 0) Chips(chipArea, chips, overNotch);
            else if (!string.IsNullOrWhiteSpace(attack.description)) // Nothing to show as chips: use the written description
                Text(chipArea, attack.description, BodyFont, 2.4f, Ink, TextAlignmentOptions.TopLeft, -0.01f, wrap: true);
        }

        // Lays chips left to right, wrapping to new rows. Shrinks them all a little if they don't fit
        void Chips(Rect area, List<Chip> chips, bool oneRow)
        {
            for (float scale = 1f; ; scale -= 0.08f)
            {
                float font = 1.95f * scale, padX = 1.3f * scale, height = 3.4f * scale, gap = 0.9f * scale;
                var places = new List<Rect>();
                float x = 0f, y = 0f;
                bool fits = true;
                foreach (Chip chip in chips)
                {
                    float width = Measure(chip.text, BodyFont, font) / size.x + padX * 2f / 100f;
                    if (x > 0f && x + width > area.width)
                    {
                        if (oneRow) { fits = false; break; }
                        x = 0f;
                        y += HeightOf(height + gap);
                    }
                    if (width > area.width) fits = false;
                    places.Add(new Rect(area.x + x, area.y + y, width, HeightOf(height)));
                    x += width + gap / 100f;
                }
                if (y + HeightOf(height) > area.height) fits = false;
                if (!fits && scale > 0.6f) continue;

                for (int i = 0; i < places.Count; i++) DrawChip(places[i], chips[i], font, scale);
                return;
            }
        }

        void DrawChip(Rect box, Chip chip, float fontCw, float scale)
        {
            Color fill, border, text;
            if (chip.kind == ChipKind.Counter)
            {
                Color classColour = ClassFrames.ColourOf(chip.counters);
                fill = Color.Lerp(Color.white, classColour, 0.3f);
                border = classColour;
                text = Ink;
            }
            else
            {
                Color[] colours = ChipColours[chip.kind];
                fill = colours[0]; border = colours[1]; text = colours[2];
            }

            float radius = Units(0.9f * scale), edge = Units(0.25f * scale);
            Vector2 chipSize = new Vector2(box.width * size.x, box.height * size.y);
            Rounded(box, chipSize, radius, border, -0.004f);
            Rounded(box, chipSize - Vector2.one * edge * 2f, Mathf.Max(radius - edge, radius * 0.5f), fill, -0.006f);
            Text(box, chip.text, BodyFont, fontCw, text, TextAlignmentOptions.Center, -0.01f);
        }

        // Rounded rectangle from a 9-sliced sprite
        void Rounded(Rect box, Vector2 boxSize, float radius, Color colour, float z)
        {
            GameObject holder = new GameObject("Chip");
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = Centre(box) + new Vector3(0f, 0f, z);
            float scale = radius / RoundedCorner; // Corner size of the sprite is fixed, so scale it to the radius
            holder.transform.localScale = Vector3.one * scale;

            SpriteRenderer renderer = holder.AddComponent<SpriteRenderer>();
            renderer.sprite = RoundedSprite();
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = boxSize / scale;
            renderer.sharedMaterial = art.sharedMaterial;
            renderer.color = colour;
            renderer.sortingOrder = order;
        }

        public void Line(Rect box)
        {
            GameObject line = new GameObject("Line");
            line.transform.SetParent(root.transform, false);
            line.transform.localPosition = Centre(box) + new Vector3(0f, 0f, -0.005f);
            SpriteRenderer renderer = line.AddComponent<SpriteRenderer>();
            renderer.sprite = WhitePixel();
            renderer.sharedMaterial = art.sharedMaterial;
            renderer.color = LineColor;
            renderer.sortingOrder = order;
            Vector2 pixel = renderer.sprite.bounds.size;
            line.transform.localScale = new Vector3(box.width * size.x / pixel.x, box.height * size.y / pixel.y, 1f);
        }

        // One piece of text in a box. sizeCw is its largest size; it shrinks to fit
        public TextMeshPro Text(Rect box, string text, TMP_FontAsset font, float sizeCw, Color colour, TextAlignmentOptions alignment,
                                float z, bool wrap = false)
        {
            GameObject holder = new GameObject("Text");
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = Centre(box) + new Vector3(0f, 0f, z);

            TextMeshPro tmp = holder.AddComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.rectTransform.sizeDelta = new Vector2(box.width * size.x, box.height * size.y);
            tmp.text = text;
            tmp.color = colour;
            tmp.fontStyle = FontStyles.Normal;
            tmp.alignment = alignment;
            tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.margin = Vector4.zero;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = FontSize(sizeCw);
            tmp.fontSizeMin = FontSize(sizeCw) * 0.4f;
            tmp.sortingOrder = order;
            return tmp;
        }

        // Width of a line of text at a size, in art units
        float Measure(string text, TMP_FontAsset font, float sizeCw)
        {
            if (measure == null)
            {
                GameObject holder = new GameObject("Measure");
                holder.transform.SetParent(root.transform, false);
                measure = holder.AddComponent<TextMeshPro>();
                measure.textWrappingMode = TextWrappingModes.NoWrap;
                measure.enableAutoSizing = false;
                measure.margin = Vector4.zero;
            }
            if (font != null) measure.font = font;
            measure.fontSize = FontSize(sizeCw);
            return measure.GetPreferredValues(text, 9999f, 9999f).x;
        }

        public void Finish()
        {
            if (measure == null) return;
            if (Application.isPlaying) Object.Destroy(measure.gameObject);
            else Object.DestroyImmediate(measure.gameObject);
        }

        // World-space TextMeshPro: font size 10 is about 1 unit
        float FontSize(float cw) => Units(cw) * 10f;

        // Box on the art (fractions, from top left) to a position in the art sprite's own space (centre is 0,0, up is +y)
        Vector3 Centre(Rect box) => new Vector3((box.center.x - 0.5f) * size.x, (0.5f - box.center.y) * size.y, 0f);
    }

    // ---- Shared sprites ----

    const int RoundedPixels = 32, RoundedRadius = 12;
    const float RoundedCorner = RoundedRadius / 100f; // Corner size in sprite units (100 pixels per unit)
    static Sprite rounded;
    static Sprite RoundedSprite()
    {
        if (rounded != null) return rounded;
        var texture = new Texture2D(RoundedPixels, RoundedPixels, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < RoundedPixels; y++)
            for (int x = 0; x < RoundedPixels; x++)
            {
                // Distance outside the rounded corner, softened by a pixel for smooth edges
                float cx = Mathf.Clamp(x + 0.5f, RoundedRadius, RoundedPixels - RoundedRadius);
                float cy = Mathf.Clamp(y + 0.5f, RoundedRadius, RoundedPixels - RoundedRadius);
                float outside = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy)) - RoundedRadius;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - outside)));
            }
        texture.Apply();
        rounded = Sprite.Create(texture, new Rect(0, 0, RoundedPixels, RoundedPixels), new Vector2(0.5f, 0.5f), 100f, 0,
                                SpriteMeshType.FullRect, new Vector4(RoundedRadius, RoundedRadius, RoundedRadius, RoundedRadius));
        return rounded;
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

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color colour);
        return colour;
    }
}
