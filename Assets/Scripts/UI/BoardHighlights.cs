using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Makes the board readable. Builds everything itself (CombatManager adds it automatically):
// - player-coloured border on every card that has an owner
// - HP bar with the current HP on cards on the board
// - pulsing glow on the card taking its turn
// - with an attack selected, a glow on every card it can hit. Hovering one previews everything the attack would hit:
//   the predicted damage/heal on each card, the HP it would lose flashing on its HP bar, and KO if it would be defeated
// - while dragging, every slot the card can move to lights up
public class BoardHighlights : MonoBehaviour
{
    [Header("Colours")]
    [SerializeField] private Color player1Color = new Color(0.3f, 0.55f, 1f);
    [SerializeField] private Color player2Color = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private Color activeColor = new Color(1f, 0.95f, 0.5f);
    [SerializeField] private Color targetColor = new Color(0.4f, 1f, 0.5f);
    [SerializeField] private Color previewColor = new Color(1f, 0.6f, 0.2f);
    [SerializeField] private Color moveSlotColor = new Color(0.4f, 1f, 0.5f, 0.35f);
    [SerializeField] private Color damageColor = new Color(1f, 0.35f, 0.3f);
    [SerializeField] private Color healColor = new Color(0.4f, 1f, 0.5f);
    [SerializeField] private Color koColor = new Color(1f, 0.85f, 0.3f);

    [Header("Look")]
    [SerializeField] private float borderPadding = 0.04f; // How far the border sits outside the card
    [SerializeField] private float hpBarHeight = 0.13f;
    [SerializeField] private float hpTextSize = 1.1f;     // Raise if the HP number is too small
    [SerializeField] private float lift = 0.03f;          // Height above the card so overlays aren't hidden
    [SerializeField] private float previewTextSize = 3.2f; // Size of the predicted damage number
    [SerializeField] private float previewHeight = 0.3f;  // How far above the card the damage badge floats
    [SerializeField] private float pixelsPerUnit = 48f;   // Size of the pixels in the HP bar and damage badge
    [SerializeField] private float boardPixelsPerUnit = 24f; // Size of the pixels in borders, glows and move markers (matches BoardStyle)

    // Everything drawn for one card
    private class CardOverlay
    {
        public GameObject root;    // Leans with the card: border, glow, damage badge
        public GameObject barRoot; // Flat on the board in front of the card: HP bar and number
        public SpriteRenderer border, glow, hpBack, hpFill, hpGhost;
        public TextMeshPro hpText, previewText;
        public Vector2 size; // Card width and height on screen
        public Transform previewRoot;               // Badge + text, so they pop and wobble together
        public SpriteRenderer badge, badgeShadow;
        public string previewShown;                 // Text the badge was last sized for
        public float previewSince;                  // When the preview appeared, for its pop
        public Vector3 badgeAnchor;                 // Where the damage badge floats (above the card or token)
        public SpriteRenderer ring;                 // Tokens: turn/target highlight on the ground under them
    }

    // What the hovered attack would do to one card
    private class Prediction
    {
        public int damage, heal;
        public bool defeated, bonus;
        public List<string> tags = new List<string>(); // Small words under the number, e.g. PUSH
    }

    private readonly Dictionary<CardUnit, CardOverlay> overlays = new Dictionary<CardUnit, CardOverlay>();
    private readonly List<GameObject> moveMarkers = new List<GameObject>();
    private readonly HashSet<CardInstance> targets = new HashSet<CardInstance>();
    private readonly HashSet<CardInstance> preview = new HashSet<CardInstance>();
    private readonly Dictionary<CardInstance, Prediction> predictions = new Dictionary<CardInstance, Prediction>();
    private float nextScan;
    private Camera cam;
    private Material spriteMaterial;
    private Sprite borderSprite, glowSprite, moveSprite;      // Pixel art at the board's pixel size, made for the card size
    private Sprite barFrameSprite, barFillSprite, badgeSprite; // Made in Start
    private Sprite ringSprite, burstSprite, dashSprite;        // Ground highlights under tokens
    private static BoardHighlights instance;

    // The HP this card would have after the attack being previewed, if one is aimed at it right now
    public static bool TryGetPredictedHP(CardInstance card, out int hpAfter)
    {
        hpAfter = 0;
        if (instance == null || card == null || !instance.predictions.TryGetValue(card, out Prediction p)) return false;
        hpAfter = p.defeated ? 0 : Mathf.Clamp(card.currentHP - p.damage + p.heal, 0, card.data.maxHP);
        return true;
    }

    void Start()
    {
        instance = this;
        cam = Camera.main;
        spriteMaterial = new Material(Shader.Find("Sprites/Default"));
        barFrameSprite = MakeBarFrame();
        barFillSprite = MakeBarFill();
        badgeSprite = MakeBadge();
        ringSprite = MakeRing(false);
        dashSprite = MakeRing(true);
        burstSprite = MakeBurst();
        CardMover.DragStarted += ShowMoves;
        CardMover.DragEnded += ClearMoves;
    }

    void OnDestroy()
    {
        CardMover.DragStarted -= ShowMoves;
        CardMover.DragEnded -= ClearMoves;
    }

    void LateUpdate()
    {
        if (cam == null) return;
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.25f;
            ScanForCards();
        }

        FindTargets();
        foreach (KeyValuePair<CardUnit, CardOverlay> pair in overlays)
            UpdateOverlay(pair.Key, pair.Value);
    }

    // ---- Tracking cards ----

    // Adds overlays for new cards with an owner, removes ones whose card is gone
    void ScanForCards()
    {
        List<CardUnit> gone = new List<CardUnit>();
        foreach (KeyValuePair<CardUnit, CardOverlay> pair in overlays)
            if (pair.Key == null) gone.Add(pair.Key);
        foreach (CardUnit unit in gone)
        {
            Destroy(overlays[unit].root);
            Destroy(overlays[unit].barRoot);
            Destroy(overlays[unit].ring.gameObject);
            overlays.Remove(unit);
        }

        foreach (CardUnit unit in FindObjectsByType<CardUnit>())
        {
            if (unit.Instance == null || !unit.HasOwner || overlays.ContainsKey(unit)) continue;
            overlays[unit] = CreateOverlay(unit);
        }
    }

    CardOverlay CreateOverlay(CardUnit unit)
    {
        CardOverlay overlay = new CardOverlay { size = CardSize(unit.transform) };
        overlay.root = new GameObject($"Overlay ({unit.name})");
        overlay.barRoot = new GameObject($"HP Bar ({unit.name})");

        if (borderSprite == null)
        {
            // Made once at the card's size in board pixels, so they match the tiles' pixels
            Vector2Int card = InBoardPixels(overlay.size);
            borderSprite = MakeCardBorder(card.x, card.y);
            glowSprite = MakeCardGlow(card.x, card.y);
        }

        // Both at their natural size (one texture pixel = one board pixel), centred on the card
        overlay.glow = MakeRenderer("Glow", overlay.root.transform, glowSprite, 1);
        overlay.border = MakeRenderer("Border", overlay.root.transform, borderSprite, 2);

        // Pixel HP bar just below the card on screen: dark frame, solid fill, number on top
        Vector3 barCentre = new Vector3(0f, -(overlay.size.y / 2f + borderPadding + hpBarHeight * 0.75f), 0f);
        overlay.hpBack = MakeRenderer("HP Back", overlay.barRoot.transform, barFrameSprite, 2);
        overlay.hpBack.drawMode = SpriteDrawMode.Sliced;
        overlay.hpBack.size = new Vector2(overlay.size.x, hpBarHeight);
        overlay.hpBack.transform.localPosition = barCentre;
        overlay.hpFill = MakeRenderer("HP Fill", overlay.barRoot.transform, barFillSprite, 3);
        overlay.hpGhost = MakeRenderer("HP Preview", overlay.barRoot.transform, barFillSprite, 3); // The part an attack would take or give

        GameObject textObject = new GameObject("HP Text");
        textObject.transform.SetParent(overlay.barRoot.transform, false);
        textObject.transform.localPosition = barCentre;
        overlay.hpText = MakeWorldText(textObject, hpTextSize, 4);
        overlay.hpText.rectTransform.sizeDelta = new Vector2(overlay.size.x, hpBarHeight * 2f);
        overlay.hpText.outlineWidth = 0.2f;

        // Predicted damage: an inked badge with the number, floating above the unit. Drawn over the tokens (they're at 3)
        overlay.previewRoot = new GameObject("Preview").transform;
        overlay.previewRoot.SetParent(overlay.root.transform, false);
        overlay.badgeShadow = MakeRenderer("Badge Shadow", overlay.previewRoot, badgeSprite, 20);
        overlay.badgeShadow.drawMode = SpriteDrawMode.Sliced;
        overlay.badgeShadow.color = InkArt.Ink;
        overlay.badge = MakeRenderer("Badge", overlay.previewRoot, badgeSprite, 21);
        overlay.badge.drawMode = SpriteDrawMode.Sliced;

        GameObject previewObject = new GameObject("Preview Text");
        previewObject.transform.SetParent(overlay.previewRoot, false);
        overlay.previewText = MakeWorldText(previewObject, previewTextSize, 22);
        GameFonts.UseInked(overlay.previewText, 0.3f);
        overlay.previewText.rectTransform.sizeDelta = new Vector2(overlay.size.x * 3f, overlay.size.y);
        overlay.previewRoot.gameObject.SetActive(false);

        // Ground highlight under a token (flat on the board, under the token)
        overlay.ring = MakeRenderer($"Ground Ring ({unit.name})", null, ringSprite, -15);
        overlay.ring.gameObject.SetActive(false);

        return overlay;
    }

    void UpdateOverlay(CardUnit unit, CardOverlay overlay)
    {
        CardInstance card = unit.Instance;
        bool alive = card.IsAlive;
        overlay.root.SetActive(alive);
        overlay.barRoot.SetActive(alive);
        if (!alive) { overlay.ring.gameObject.SetActive(false); return; }

        // Units shown as tokens (UnitTokens) carry their own HP and frame: only the ground ring and damage badge here
        if (UnitTokens.IsToken(unit))
        {
            overlay.border.enabled = false;
            overlay.glow.enabled = false;
            overlay.barRoot.SetActive(false);
            UpdateRing(unit, overlay);
            predictions.TryGetValue(card, out Prediction tokenPrediction);
            overlay.badgeAnchor = UnitTokens.Top(unit) + BoardView.CameraRotation * Vector3.up * 0.22f;
            UpdatePreview(overlay, tokenPrediction);
            return;
        }
        overlay.border.enabled = true;
        overlay.glow.enabled = true;
        overlay.ring.gameObject.SetActive(false);

        // Border and glow lean exactly like the card (CardHover), just in front of its face so they hug it
        float leanAngle = CardHover.LeanAngle;
        Quaternion lean = Quaternion.AngleAxis(-leanAngle, BoardView.Right);
        overlay.root.transform.SetPositionAndRotation(unit.transform.position + lean * Vector3.up * lift, lean * BoardView.Flat);

        // HP bar lies flat on the board in front of the card's bottom edge. Leaning raises the card, so take that back off
        float leanLift = leanAngle > 0f ? overlay.size.y * 0.5f * Mathf.Sin(leanAngle * Mathf.Deg2Rad) + 0.01f : 0f;
        overlay.barRoot.transform.SetPositionAndRotation(unit.transform.position + Vector3.up * (lift - leanLift), BoardView.Flat);
        overlay.border.color = card.owner == 1 ? player1Color : player2Color;

        // Glow: the card taking its turn, then the area preview, then valid targets
        TurnManager turns = TurnManager.Instance;
        bool active = turns != null && turns.ActiveUnit == unit && turns.Phase == GamePhase.Battle;
        float pulse = Mathf.Round((0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f)) * 3f) / 3f; // In 4 steps, like a pixel-art animation
        Color glow = Color.clear;
        if (preview.Contains(card)) glow = previewColor;
        else if (targets.Contains(card)) glow = WithAlpha(targetColor, 0.5f + 0.4f * pulse);
        else if (active) glow = WithAlpha(activeColor, 0.4f + 0.5f * pulse);
        overlay.glow.color = glow;

        // HP only once the card is on the board
        bool onBoard = CombatManager.Instance != null && CombatManager.Instance.Board.IsOnBoard(card);
        overlay.hpBack.gameObject.SetActive(onBoard);
        overlay.hpFill.gameObject.SetActive(onBoard);
        overlay.hpText.gameObject.SetActive(onBoard);
        predictions.TryGetValue(card, out Prediction prediction);
        if (!onBoard) prediction = null;
        overlay.hpGhost.gameObject.SetActive(prediction != null);
        overlay.badgeAnchor = overlay.root.transform.position + Vector3.up * previewHeight;
        UpdatePreview(overlay, prediction);
        if (!onBoard) return;

        // HP after the previewed attack (same as now without one)
        int hpAfter = card.currentHP;
        if (prediction != null) hpAfter = prediction.defeated ? 0 : Mathf.Clamp(card.currentHP - prediction.damage + prediction.heal, 0, card.data.maxHP);

        float percent = Mathf.Clamp01((float)card.currentHP / card.data.maxHP);
        float after = Mathf.Clamp01((float)hpAfter / card.data.maxHP);
        Vector3 barCentre = overlay.hpBack.transform.localPosition;
        SetBar(overlay.hpFill, barCentre, overlay.size.x, 0f, Mathf.Min(percent, after));
        overlay.hpFill.color = HPColor(percent);
        overlay.hpText.text = hpAfter.ToString();
        overlay.hpText.color = hpAfter < card.currentHP ? new Color(1f, 0.75f, 0.7f) : hpAfter > card.currentHP ? new Color(0.75f, 1f, 0.8f) : Color.white;

        // The part that would be lost (flashing white/red) or gained (flashing green)
        if (prediction == null) return;
        SetBar(overlay.hpGhost, barCentre, overlay.size.x, Mathf.Min(percent, after), Mathf.Max(percent, after));
        float flash = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
        overlay.hpGhost.color = after < percent ? Color.Lerp(damageColor, Color.white, flash) : Color.Lerp(healColor, Color.white, flash * 0.6f);
    }

    // Pixel badge with the predicted number, centred on the card. Pops in, then wobbles and bobs (Balatro-style)
    void UpdatePreview(CardOverlay overlay, Prediction prediction)
    {
        string text = prediction != null ? PreviewLabel(prediction) : null;
        bool show = !string.IsNullOrEmpty(text);
        if (show && !overlay.previewRoot.gameObject.activeSelf) overlay.previewSince = Time.unscaledTime;
        overlay.previewRoot.gameObject.SetActive(show);
        if (!show) return;

        if (text != overlay.previewShown) FitBadge(overlay, text);
        overlay.badge.color = BadgeColor(prediction);

        float t = Time.unscaledTime;
        float age = t - overlay.previewSince;
        float pop = 1f + 0.4f * Mathf.Exp(-age * 10f) * Mathf.Cos(age * 28f); // Overshoots then settles
        if (prediction.defeated) pop *= 1f + 0.06f * Mathf.Sin(t * 8f);        // KO throbs
        // Stands up facing the camera, just above the card, so it reads clearly from any camera angle
        overlay.previewRoot.localScale = Vector3.one * pop;
        overlay.previewRoot.SetPositionAndRotation(
            overlay.badgeAnchor + Vector3.up * 0.025f * Mathf.Sin(t * 3.1f),
            cam.transform.rotation * Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(t * 2.3f)));
    }

    // Token highlight on the ground: a turning gold starburst for the unit taking its turn, a dashed ring on every
    // unit the selected attack can hit, and a solid orange ring on everything the aimed attack would hit
    void UpdateRing(CardUnit unit, CardOverlay overlay)
    {
        CardInstance card = unit.Instance;
        TurnManager turns = TurnManager.Instance;
        bool active = turns != null && turns.ActiveUnit == unit && turns.Phase == GamePhase.Battle;
        bool aimed = preview.Contains(card), target = targets.Contains(card);

        overlay.ring.gameObject.SetActive(active || aimed || target);
        if (!overlay.ring.gameObject.activeSelf) return;

        float t = Time.unscaledTime;
        Sprite sprite = aimed ? ringSprite : target ? dashSprite : burstSprite;
        Color color = aimed ? previewColor : target ? Color.white : InkArt.GoldLight;
        float size = aimed ? 1.05f : target ? 1.0f + 0.06f * Mathf.Sin(t * 6f) : 1.25f;
        float spin = active && !aimed && !target ? t * 25f : target ? t * 40f : 0f;

        overlay.ring.sprite = sprite;
        overlay.ring.color = color;
        Vector3 feet = UnitTokens.Feet(unit);
        overlay.ring.transform.SetPositionAndRotation(new Vector3(feet.x, UnitTokens.FloorHeight(unit) + 0.015f, feet.z),
                                                      Quaternion.AngleAxis(spin, Vector3.up) * BoardView.Flat);
        SetSize(overlay.ring, Vector2.one * size);
    }

    // Sizes the badge around the text and centres both on the card, snapped to whole pixels so it stays crisp
    void FitBadge(CardOverlay overlay, string text)
    {
        overlay.previewShown = text;
        overlay.previewText.text = text;
        overlay.previewText.ForceMeshUpdate();
        Bounds bounds = overlay.previewText.textBounds;

        Vector2 size = new Vector2(bounds.size.x + 0.3f, bounds.size.y + 0.18f);
        overlay.previewText.transform.localPosition = -bounds.center; // Text's visible middle on the badge's middle
        overlay.badge.size = size;
        overlay.badgeShadow.size = size;
        overlay.badgeShadow.transform.localPosition = new Vector3(0.04f, -0.05f, 0f); // Hard ink drop shadow
    }

    // e.g. "-24", "+15" or "KO!", with small tags like BONUS underneath
    string PreviewLabel(Prediction prediction)
    {
        string main = "";
        if (prediction.defeated)
        {
            main = "KO!";
            if (prediction.damage > 0) main += $"\n<size=50%>-{prediction.damage}</size>";
        }
        else if (prediction.damage > 0) main = $"-{prediction.damage}";
        else if (prediction.heal > 0) main = $"+{prediction.heal}";

        List<string> tags = new List<string>(prediction.tags);
        if (prediction.bonus) tags.Insert(0, "BONUS");
        if (tags.Count == 0) return main;
        string small = $"<size=38%><color=#FFE38A>{string.Join(" ", tags)}</color></size>";
        return main.Length > 0 ? main + "\n" + small : small;
    }

    Color BadgeColor(Prediction prediction)
    {
        if (prediction.defeated) return InkArt.Gold;
        if (prediction.damage > 0) return InkArt.Damage;
        if (prediction.heal > 0) return InkArt.Heal;
        return new Color(0.55f, 0.45f, 0.85f); // Tags only, e.g. PUSH
    }

    // Stretches a fill sprite over part of the inside of the HP bar, between two fractions of its width
    void SetBar(SpriteRenderer bar, Vector3 barCentre, float barWidth, float from, float to)
    {
        float pixel = 1f / pixelsPerUnit;
        float inner = barWidth - pixel * 2f; // Inside the frame's 1-pixel outline
        SetSize(bar, new Vector2(inner * Mathf.Max(0.001f, to - from), hpBarHeight - pixel * 2f));
        bar.transform.localPosition = barCentre + new Vector3(inner * (from + to - 1f) / 2f, 0f, 0f);
    }

    static Color HPColor(float percent)
    {
        return percent > 0.5f ? Color.Lerp(Color.yellow, Color.green, (percent - 0.5f) * 2f)
                              : Color.Lerp(Color.red, Color.yellow, percent * 2f);
    }

    // World-space text in the pixel font with a black outline
    static TextMeshPro MakeWorldText(GameObject textObject, float size, int order)
    {
        TextMeshPro text = textObject.AddComponent<TextMeshPro>();
        if (GameFonts.Body != null) GameFonts.UseBody(text);
        else text.fontStyle = FontStyles.Bold;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.outlineColor = Color.black;
        text.sortingOrder = order;
        return text;
    }

    // ---- Attack targets ----

    // Cards the selected attack can hit, plus everything it would hit on the hovered target
    void FindTargets()
    {
        targets.Clear();
        preview.Clear();
        predictions.Clear();

        TurnManager turns = TurnManager.Instance;
        if (turns == null || !turns.CanAct || turns.SelectedAttack == null || CombatManager.Instance == null) return;

        BoardState board = CombatManager.Instance.Board;
        CardInstance attacker = turns.ActiveUnit.Instance;
        AttackData attack = turns.SelectedAttack;

        foreach (CardInstance card in board.GetAllCards())
            if (CombatResolver.CanTarget(attacker, attack, card, board)) targets.Add(card);

        CardUnit hovered = CardHover.UnderMouse(); // Doesn't flicker while the hovered card lifts
        if (hovered != null && hovered.Instance != null && targets.Contains(hovered.Instance))
        {
            foreach (CardInstance card in CombatResolver.GetAffectedCards(attacker, attack, hovered.Instance, board))
                preview.Add(card);
            if (preview.Count == 0) preview.Add(hovered.Instance); // e.g. Roar with nobody nearby
            Predict(attacker, attack, hovered.Instance, board);
        }
    }

    // Fills predictions with what the attack would do to each card (from CombatResolver.PreviewAttack)
    void Predict(CardInstance attacker, AttackData attack, CardInstance target, BoardState board)
    {
        AttackResult result = CombatResolver.PreviewAttack(attacker, attack, target, board);
        if (result == null) return;

        foreach (AttackHit hit in result.hits)
        {
            Prediction p = PredictionFor(hit.card);
            p.damage += hit.damage;
            p.heal += hit.healed;
            p.bonus |= hit.bonusTriggered;
            if (hit.executed) p.tags.Add("EXECUTE");
        }

        // Knockback into a wall or card hurts the target, and the card it hits if that's an enemy
        if (result.wasKnockedBack) PredictionFor(target).tags.Add("PUSH");
        if (result.knockbackBlocked) PredictionFor(target).tags.Add("ANCHORED");
        if (result.hitObstacle)
        {
            Prediction p = PredictionFor(target);
            p.damage += result.collisionDamage;
            p.tags.Add("SLAM");
            if (result.collidedWith != null && result.collidedWith.owner != attacker.owner)
                PredictionFor(result.collidedWith).damage += result.collisionDamage;
        }

        // Hangry-style passives hit the attacker back
        if (result.reflectedDamage > 0)
        {
            Prediction p = PredictionFor(attacker);
            p.damage += result.reflectedDamage;
            p.tags.Add("RECOIL");
        }

        foreach (KeyValuePair<CardInstance, Prediction> pair in predictions)
            pair.Value.defeated = result.defeatedCards.Contains(pair.Key) || pair.Key.currentHP - pair.Value.damage + pair.Value.heal <= 0;
    }

    Prediction PredictionFor(CardInstance card)
    {
        if (!predictions.TryGetValue(card, out Prediction p)) predictions[card] = p = new Prediction();
        return p;
    }

    // ---- Move highlights ----

    void ShowMoves(CardMover mover)
    {
        ClearMoves(mover);
        if (moveSprite == null) moveSprite = MakeSparkle();

        foreach (Transform slot in mover.GetLegalSlots())
        {
            SpriteRenderer marker = MakeRenderer("Move Marker", null, moveSprite, -14);
            marker.transform.SetPositionAndRotation(slot.position + Vector3.up * lift, BoardView.Flat);
            moveMarkers.Add(marker.gameObject);
        }
    }

    void ClearMoves(CardMover mover)
    {
        foreach (GameObject marker in moveMarkers)
            if (marker != null) Destroy(marker);
        moveMarkers.Clear();
    }

    // ---- Helpers ----

    // Card width and height on screen, from its mesh. Works whatever way the card is turned right now
    Vector2 CardSize(Transform card)
    {
        MeshFilter mesh = card.GetComponent<MeshFilter>();
        Vector3 local = mesh != null && mesh.sharedMesh != null ? mesh.sharedMesh.bounds.size : Vector3.one;
        Vector3 world = Vector3.Scale(local, card.lossyScale);
        // Assumes the card lies flat (unrotated). Project onto the screen axes
        float width = Mathf.Abs(BoardView.Right.x) * world.x + Mathf.Abs(BoardView.Right.z) * world.z;
        float height = Mathf.Abs(BoardView.Up.x) * world.x + Mathf.Abs(BoardView.Up.z) * world.z;
        return new Vector2(width, height);
    }

    SpriteRenderer MakeRenderer(string name, Transform parent, Sprite sprite, int order)
    {
        GameObject child = new GameObject(name);
        if (parent != null) child.transform.SetParent(parent, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = spriteMaterial;
        renderer.sortingOrder = order;
        return renderer;
    }

    // Scales a sprite to a world size
    static void SetSize(SpriteRenderer renderer, Vector2 size)
    {
        Vector2 spriteSize = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    // ---- Pixel art ----

    // HP bar frame: 1-pixel black outline around a dark inside. 9-sliced so it fits any width
    Sprite MakeBarFrame()
    {
        return MakePixelSprite(8, 8, 2, (x, y, ring) =>
            ring < 0 ? Color.clear : ring == 0 ? new Color(0.03f, 0.02f, 0.06f, 1f) : new Color(0.12f, 0.1f, 0.18f, 0.95f));
    }

    // HP fill, tinted by colour: light row on top, shade row on the bottom, so it reads as a solid chunky bar
    Sprite MakeBarFill()
    {
        return MakePixelSprite(1, 4, 0, (x, y, ring) =>
            y == 3 ? new Color(1f, 1f, 1f, 1f) : y == 0 ? new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(0.85f, 0.85f, 0.85f, 1f));
    }

    // Damage badge, tinted by colour: black outline, lit top-left bevel, shaded bottom-right, cut corners
    // Damage badge, tinted by colour: rounded box with a thick ink outline. 9-sliced so it fits any number
    Sprite MakeBadge()
    {
        const int size = 48;
        Texture2D texture = InkArt.Paint(size, size, p =>
            InkArt.Outlined(InkArt.Chamfer(p, new Vector2(size / 2f, size / 2f), new Vector2(size / 2f - 1f, size / 2f - 1f), 13f, 4f), 5f, Color.white));
        return InkArt.ToSprite(texture, new Vector2(0.5f, 0.5f), 140f, 16f);
    }

    // Ground ring under a token, tinted: a white band edged in ink. Dashed = broken into segments
    Sprite MakeRing(bool dashed)
    {
        const int size = 128;
        Vector2 centre = new Vector2(size / 2f, size / 2f);
        Texture2D texture = InkArt.Paint(size, size, p =>
        {
            Vector2 d = p - centre;
            if (dashed && Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 30f) > 18f) return Color.clear;
            float band = Mathf.Abs(d.magnitude - size * 0.42f) - 7f; // Ring 14 px wide
            return InkArt.Outlined(band, 3.5f, Color.white);
        });
        return InkArt.ToSprite(texture, new Vector2(0.5f, 0.5f), size);
    }

    // Starburst for the unit taking its turn: ink-edged rays around a ring, tinted gold
    Sprite MakeBurst()
    {
        const int size = 128;
        Vector2 centre = new Vector2(size / 2f, size / 2f);
        Texture2D texture = InkArt.Paint(size, size, p =>
        {
            Vector2 d = p - centre;
            float radius = d.magnitude;
            float angle = Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 30f) / 30f; // 12 rays
            float rayReach = size * (0.36f + 0.13f * (1f - Mathf.Abs(angle * 2f - 1f) * 2f));
            float ray = Mathf.Max(radius - rayReach, size * 0.33f - radius);
            float ring = Mathf.Abs(radius - size * 0.36f) - 5f;
            return InkArt.Outlined(Mathf.Min(ray, ring), 3f, Color.white);
        });
        return InkArt.ToSprite(texture, new Vector2(0.5f, 0.5f), size);
    }

    // Gold sparkle marking a slot a dragged card can move to
    Sprite MakeSparkle()
    {
        const int size = 64;
        Texture2D texture = InkArt.Paint(size, size, p =>
            InkArt.Outlined(InkArt.Sparkle(p, new Vector2(size / 2f, size / 2f), size - 4f), 4f, InkArt.GoldLight), 4);
        return InkArt.ToSprite(texture, new Vector2(0.5f, 0.5f), size / 0.4f);
    }

    // Point-filtered sprite at pixelsPerUnit. ring = how many pixels in from the edge (corners cut, -1 = cut away)
    Sprite MakePixelSprite(int width, int height, int border, System.Func<int, int, int, Color> colorAt)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int dx = Mathf.Min(x, width - 1 - x);
                int dy = Mathf.Min(y, height - 1 - y);
                int ring = border > 0 && dx + dy < 1 ? -1 : Mathf.Min(dx, dy);
                texture.SetPixel(x, y, colorAt(x, y, ring));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0,
                             SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    // ---- Pixel art at the board's pixel size ----

    // Card border, tinted by the player colour: dark outline, 1 coloured pixel, a faint dark line on the card's edge.
    // Corners are cut like the board tiles
    Sprite MakeCardBorder(int cardWidth, int cardHeight)
    {
        return MakeBoardSprite(cardWidth + 4, cardHeight + 4, (x, y, ring) =>
            ring < 0 ? Color.clear
          : ring == 0 ? new Color(0.03f, 0.02f, 0.06f, 0.9f)
          : ring == 1 ? Color.white
          : ring == 2 ? new Color(0f, 0f, 0f, 0.35f)
          : Color.clear);
    }

    // Stepped pixel glow: rings that get fainter going out, the classic retro glow. Only outside the card, so it never covers the art
    Sprite MakeCardGlow(int cardWidth, int cardHeight)
    {
        float[] steps = { 0.12f, 0.25f, 0.45f, 0.7f, 0.9f, 0.9f }; // The last two sit under the border
        return MakeBoardSprite(cardWidth + 4 + 8, cardHeight + 4 + 8, (x, y, ring) =>
            ring < 0 || ring >= steps.Length ? Color.clear : new Color(1f, 1f, 1f, steps[ring]));
    }

    // Slot a dragged card can move to: bright outline and a checkerboard fill
    Sprite MakeMoveMarker(int width, int height)
    {
        return MakeBoardSprite(width, height, (x, y, ring) =>
            ring < 0 ? Color.clear
          : ring == 0 ? Color.white
          : ring == 1 ? new Color(1f, 1f, 1f, 0.45f)
          : new Color(1f, 1f, 1f, (x + y) % 2 == 0 ? 0.35f : 0.1f));
    }

    // Point-filtered sprite at the board's pixel size, with corners cut 2 pixels deep. ring = pixels in from the edge
    Sprite MakeBoardSprite(int width, int height, System.Func<int, int, int, Color> colorAt)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int dx = Mathf.Min(x, width - 1 - x), dy = Mathf.Min(y, height - 1 - y);
                int ring = dx + dy < 2 ? -1 : Mathf.Min(Mathf.Min(dx, dy), dx + dy - 2);
                texture.SetPixel(x, y, colorAt(x, y, ring));
            }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), boardPixelsPerUnit);
    }

    // Size in board pixels, rounded up so the border never cuts into the card
    Vector2Int InBoardPixels(Vector2 size)
    {
        return new Vector2Int(Mathf.CeilToInt(size.x * boardPixelsPerUnit), Mathf.CeilToInt(size.y * boardPixelsPerUnit));
    }
}
