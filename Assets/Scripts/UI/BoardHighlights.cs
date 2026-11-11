using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Makes the board readable. Builds everything itself (CombatManager adds it automatically):
// - player-coloured border on every card that has an owner
// - HP bar with the current HP on cards on the board
// - pulsing glow on the card taking its turn
// - with an attack selected, a glow on every card it can hit. Hovering one previews everything the attack would hit
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

    [Header("Look")]
    [SerializeField] private float borderPadding = 0.04f; // How far the border sits outside the card
    [SerializeField] private float hpBarHeight = 0.09f;
    [SerializeField] private float hpTextSize = 1.4f;     // Raise if the HP number is too small
    [SerializeField] private float lift = 0.03f;          // Height above the card so overlays aren't hidden

    // Everything drawn for one card
    private class CardOverlay
    {
        public GameObject root;
        public SpriteRenderer border, glow, hpBack, hpFill;
        public TextMeshPro hpText;
        public Vector2 size; // Card width and height on screen
    }

    private readonly Dictionary<CardUnit, CardOverlay> overlays = new Dictionary<CardUnit, CardOverlay>();
    private readonly List<GameObject> moveMarkers = new List<GameObject>();
    private readonly HashSet<CardInstance> targets = new HashSet<CardInstance>();
    private readonly HashSet<CardInstance> preview = new HashSet<CardInstance>();
    private float nextScan;
    private Camera cam;
    private Material spriteMaterial;
    private Sprite borderSprite, glowSprite, solidSprite;
    private static Vector2 glowShapeFraction = Vector2.one; // How much of the glow texture the shape itself fills

    void Start()
    {
        cam = Camera.main;
        spriteMaterial = new Material(Shader.Find("Sprites/Default"));
        solidSprite = MakeSprite(64, 64, 10, RoundedRectMode.Fill);
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

        if (borderSprite == null)
        {
            // Made once at the card's shape so the border is the same thickness all round
            int width = 128, height = Mathf.RoundToInt(128 * overlay.size.y / Mathf.Max(0.01f, overlay.size.x));
            borderSprite = MakeSprite(width, height, 14, RoundedRectMode.Outline);
            glowSprite = MakeSprite(width, height, 18, RoundedRectMode.Glow);
        }

        // The glow texture has room around its shape to fade into, so size it so the shape lines up with the card
        overlay.glow = MakeRenderer("Glow", overlay.root.transform, glowSprite, 1);
        Vector2 cardWithBorder = overlay.size + Vector2.one * borderPadding * 2f;
        SetSize(overlay.glow, new Vector2(cardWithBorder.x / glowShapeFraction.x, cardWithBorder.y / glowShapeFraction.y));
        overlay.border = MakeRenderer("Border", overlay.root.transform, borderSprite, 2);
        SetSize(overlay.border, overlay.size + Vector2.one * borderPadding * 2f);

        // HP bar just below the card on screen
        Vector3 barCentre = new Vector3(0f, -(overlay.size.y / 2f + borderPadding + hpBarHeight), 0f);
        overlay.hpBack = MakeRenderer("HP Back", overlay.root.transform, solidSprite, 2);
        overlay.hpBack.color = new Color(0f, 0f, 0f, 0.75f);
        overlay.hpBack.transform.localPosition = barCentre;
        SetSize(overlay.hpBack, new Vector2(overlay.size.x, hpBarHeight));
        overlay.hpFill = MakeRenderer("HP Fill", overlay.root.transform, solidSprite, 3);

        GameObject textObject = new GameObject("HP Text");
        textObject.transform.SetParent(overlay.root.transform, false);
        textObject.transform.localPosition = barCentre;
        overlay.hpText = textObject.AddComponent<TextMeshPro>();
        overlay.hpText.fontSize = hpTextSize;
        overlay.hpText.fontStyle = FontStyles.Bold;
        overlay.hpText.alignment = TextAlignmentOptions.Center;
        overlay.hpText.rectTransform.sizeDelta = new Vector2(overlay.size.x, hpBarHeight * 2f);
        overlay.hpText.outlineWidth = 0.3f;
        overlay.hpText.outlineColor = Color.black;
        overlay.hpText.sortingOrder = 4;

        return overlay;
    }

    void UpdateOverlay(CardUnit unit, CardOverlay overlay)
    {
        CardInstance card = unit.Instance;
        bool alive = card.IsAlive;
        overlay.root.SetActive(alive);
        if (!alive) return;

        // Follow the card, facing the camera
        overlay.root.transform.SetPositionAndRotation(unit.transform.position + Vector3.up * lift, cam.transform.rotation);
        overlay.border.color = card.owner == 1 ? player1Color : player2Color;

        // Glow: the card taking its turn, then the area preview, then valid targets
        TurnManager turns = TurnManager.Instance;
        bool active = turns != null && turns.ActiveUnit == unit && turns.Phase == GamePhase.Battle;
        float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
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
        if (!onBoard) return;

        float percent = Mathf.Clamp01((float)card.currentHP / card.data.maxHP);
        float barWidth = overlay.size.x;
        Vector3 barCentre = overlay.hpBack.transform.localPosition;
        SetSize(overlay.hpFill, new Vector2(Mathf.Max(0.001f, barWidth * percent), hpBarHeight));
        overlay.hpFill.transform.localPosition = barCentre + new Vector3((barWidth * percent - barWidth) / 2f, 0f, 0f);
        overlay.hpFill.color = percent > 0.5f ? Color.Lerp(Color.yellow, Color.green, (percent - 0.5f) * 2f)
                                              : Color.Lerp(Color.red, Color.yellow, percent * 2f);
        overlay.hpText.text = card.currentHP.ToString();
    }

    // ---- Attack targets ----

    // Cards the selected attack can hit, plus everything it would hit on the hovered target
    void FindTargets()
    {
        targets.Clear();
        preview.Clear();

        TurnManager turns = TurnManager.Instance;
        if (turns == null || !turns.CanAct || turns.SelectedAttack == null || CombatManager.Instance == null) return;

        BoardState board = CombatManager.Instance.Board;
        CardInstance attacker = turns.ActiveUnit.Instance;
        AttackData attack = turns.SelectedAttack;

        foreach (CardInstance card in board.GetAllCards())
            if (CombatResolver.CanTarget(attacker, attack, card, board)) targets.Add(card);

        CardUnit hovered = CardUnit.UnderMouse();
        if (hovered != null && hovered.Instance != null && targets.Contains(hovered.Instance))
        {
            foreach (CardInstance card in CombatResolver.GetAffectedCards(attacker, attack, hovered.Instance, board))
                preview.Add(card);
            if (preview.Count == 0) preview.Add(hovered.Instance); // e.g. Roar with nobody nearby
        }
    }

    // ---- Move highlights ----

    void ShowMoves(CardMover mover)
    {
        ClearMoves(mover);
        Vector2 size = CardSize(mover.transform) * 0.9f;
        foreach (Transform slot in mover.GetLegalSlots())
        {
            SpriteRenderer marker = MakeRenderer("Move Marker", null, solidSprite, 0);
            marker.color = moveSlotColor;
            marker.transform.SetPositionAndRotation(slot.position + Vector3.up * lift, cam.transform.rotation);
            SetSize(marker, size);
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
        float width = Mathf.Abs(cam.transform.right.x) * world.x + Mathf.Abs(cam.transform.right.z) * world.z;
        float height = Mathf.Abs(cam.transform.up.x) * world.x + Mathf.Abs(cam.transform.up.z) * world.z;
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

    // ---- Drawing the shapes ----

    private enum RoundedRectMode { Fill, Outline, Glow }

    // White rounded rectangle, filled, as an outline, or as a soft glow fading out from the edge.
    // 1 unit wide, so SetSize can scale it to any card
    static Sprite MakeSprite(int width, int height, int radius, RoundedRectMode mode)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        float glowWidth = Mathf.Min(width, height) * 0.22f; // Glow sprite keeps room around the shape to fade into
        float inset = mode == RoundedRectMode.Glow ? glowWidth : 1f;
        if (mode == RoundedRectMode.Glow)
            glowShapeFraction = new Vector2((width - 2f * glowWidth) / width, (height - 2f * glowWidth) / height);
        Vector2 half = new Vector2(width / 2f - inset, height / 2f - inset);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - width / 2f, y + 0.5f - height / 2f);
                float distance = RoundedRectDistance(p, half, radius); // Negative inside, positive outside
                float alpha;
                switch (mode)
                {
                    case RoundedRectMode.Fill:    alpha = Mathf.Clamp01(0.5f - distance); break;
                    case RoundedRectMode.Outline: alpha = Mathf.Clamp01(3.5f - Mathf.Abs(distance + 3f)); break; // ~6px line on the edge
                    default:                      alpha = distance < 0f ? Mathf.Clamp01(1f + distance / 6f) : Mathf.Pow(Mathf.Clamp01(1f - distance / glowWidth), 2f); break;
                }
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width);
    }

    // Signed distance from a point to a rounded rectangle centred on zero
    static float RoundedRectDistance(Vector2 p, Vector2 half, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + Vector2.one * radius;
        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
    }
}
