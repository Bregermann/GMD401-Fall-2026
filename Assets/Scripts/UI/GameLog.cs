using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// The in-game battle log: short, readable lines for what happened (attacks, status ticks, defeats, rounds, the draft).
// Listens to game events, so detailed debug text stays in Unity's Console instead.
// Builds its own panel in the bottom left. Click the header to hide or show it. Put it on any GameObject in the scene.
public class GameLog : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new Vector2(470, 330);
    [SerializeField] private Vector2 margin = new Vector2(20, 20);  // Gap from the bottom-left corner of the screen
    [SerializeField] private int fontSize = 17;
    [SerializeField] private int maxEntries = 40;                  // Oldest lines drop off after this

    [Header("Debug")]
    [SerializeField] private bool showDebugMessages = false;       // Also show Debug.Log lines in grey (for testing)

    // Colours (hex, for TextMeshPro rich text)
    const string Player1 = "#5A8CFF";
    const string Player2 = "#FF5A5A";
    const string DamageHex = "#FF9A55";
    const string HealHex = "#66E07A";
    const string StatusHex = "#C08CFF";
    const string GoldHex = "#FFD34D";
    const string DimHex = "#8A93A8";

    static readonly Color PanelColour = new Color(0.06f, 0.07f, 0.11f, 0.88f);
    static readonly Color BorderColour = new Color(0.35f, 0.42f, 0.65f, 0.6f);
    static readonly Color RowColour = new Color(1f, 1f, 1f, 0.035f);
    static readonly Color TextColour = new Color(0.9f, 0.92f, 0.96f);
    static readonly Color GoldColour = new Color(1f, 0.83f, 0.3f);
    static readonly Color SystemAccent = new Color(0.55f, 0.6f, 0.75f);

    private RectTransform panel;
    private GameObject body;
    private ScrollRect scroll;
    private RectTransform content;
    private TextMeshProUGUI roundLabel;
    private TextMeshProUGUI toggleLabel;
    private Sprite rounded;
    private bool collapsed;
    private readonly Queue<GameObject> entries = new Queue<GameObject>();

    void Awake()
    {
        BuildPanel();
    }

    void OnEnable()
    {
        if (showDebugMessages) Application.logMessageReceived += OnDebugLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= OnDebugLog;
        Unsubscribe();
    }

    void Start()
    {
        Subscribe();
    }

    // ---- Game events ----

    void Subscribe()
    {
        if (DraftManager.Instance != null)
        {
            DraftManager.Instance.DraftStarted += OnDraftStarted;
            DraftManager.Instance.CardDrafted += OnCardDrafted;
        }
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.PlacementStarted += OnPlacementStarted;
            TurnManager.Instance.BattleStarted += OnBattleStarted;
            TurnManager.Instance.RoundStarted += OnRoundStarted;
            TurnManager.Instance.TurnStarted += OnTurnStarted;
            TurnManager.Instance.CardBurned += OnBurned;
            TurnManager.Instance.CardPoisoned += OnPoisoned;
            TurnManager.Instance.CardRegenerated += OnRegenerated;
            TurnManager.Instance.CardStunned += OnStunned;
            TurnManager.Instance.GameEnded += OnGameEnded;
        }
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.AttackResolved += OnAttack;
            CombatManager.Instance.CardDefeated += OnDefeated;
        }
    }

    void Unsubscribe()
    {
        if (DraftManager.Instance != null)
        {
            DraftManager.Instance.DraftStarted -= OnDraftStarted;
            DraftManager.Instance.CardDrafted -= OnCardDrafted;
        }
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.PlacementStarted -= OnPlacementStarted;
            TurnManager.Instance.BattleStarted -= OnBattleStarted;
            TurnManager.Instance.RoundStarted -= OnRoundStarted;
            TurnManager.Instance.TurnStarted -= OnTurnStarted;
            TurnManager.Instance.CardBurned -= OnBurned;
            TurnManager.Instance.CardPoisoned -= OnPoisoned;
            TurnManager.Instance.CardRegenerated -= OnRegenerated;
            TurnManager.Instance.CardStunned -= OnStunned;
            TurnManager.Instance.GameEnded -= OnGameEnded;
        }
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.AttackResolved -= OnAttack;
            CombatManager.Instance.CardDefeated -= OnDefeated;
        }
    }

    void OnDraftStarted(int firstPicker)
    {
        AddDivider("DRAFT");
        AddEntry($"Coin flip: {PlayerName(firstPicker)} picks first", 0);
    }

    void OnCardDrafted(CardUnit unit, int player) => AddEntry($"{PlayerName(player)} drafts {Name(unit)}", player);

    void OnPlacementStarted(int firstPlayer)
    {
        AddDivider("PLACEMENT");
        AddEntry($"{PlayerName(firstPlayer)} places first", 0);
    }

    void OnBattleStarted() => AddDivider("BATTLE");

    void OnRoundStarted(int round)
    {
        roundLabel.text = $"ROUND {round}";
        AddDivider($"ROUND {round}");
    }

    void OnTurnStarted(CardUnit unit) => AddEntry($"<color={DimHex}>{Name(unit)}'s turn</color>", Owner(unit), true);

    // e.g. "Galvin → Fjora  Wind Blade  -190", or for area attacks "Chandra  Starfall  hits 3"
    void OnAttack(CardUnit attacker, AttackData attack, CardUnit target, AttackResult result)
    {
        if (attacker == null || attack == null || result == null) return;
        string line = $"{Name(attacker)}";
        string attackName = $"<i>{attack.attackName}</i>";

        if (result.hits.Count == 1 && result.hits[0].card != attacker.Instance)
        {
            AttackHit hit = result.hits[0];
            line += $" <color={DimHex}>→</color> {Name(hit.card)}  {attackName}  {HitText(hit, attack)}";
        }
        else if (result.hits.Count > 1)
        {
            int total = 0;
            foreach (AttackHit hit in result.hits) total += hit.damage + hit.healed;
            string amount = total > 0 ? $"  {(attack.damageType == DamageType.Heal ? Colour(HealHex, $"+{total}") : Colour(DamageHex, $"-{total}"))} total" : "";
            line += $"  {attackName}  hits {result.hits.Count}{amount}";
        }
        else line += $"  {attackName}"; // Self buffs and attacks that found nobody

        bool bonus = result.hits.Exists(h => h.bonusTriggered);
        if (bonus) line += $"  {Colour(GoldHex, "bonus")}";
        if (result.wasKnockedBack) line += $"  <color={DimHex}>knocked back</color>";
        if (result.knockbackBlocked) line += $"  <color={DimHex}>anchored</color>";
        if (result.hitObstacle && result.collisionDamage > 0) line += $"  <color={DimHex}>crash</color> {Colour(DamageHex, $"-{result.collisionDamage}")}";
        AddEntry(line, Owner(attacker));

        foreach (AttackHit hit in result.hits)
            if (hit.executed) AddEntry($"{Name(hit.card)} is {Colour(StatusHex, "executed")}", hit.card.owner);
        if (result.reflectedDamage > 0)
            AddEntry($"{Name(attacker)} is hit back  {Colour(DamageHex, $"-{result.reflectedDamage}")}", Owner(attacker));
    }

    // Damage, healing, or the effects it gave when there's no number
    string HitText(AttackHit hit, AttackData attack)
    {
        if (hit.damage > 0) return Colour(DamageHex, $"-{hit.damage}");
        if (hit.healed > 0) return Colour(HealHex, $"+{hit.healed}");
        if (attack.targetStatusEffects == null || attack.targetStatusEffects.Length == 0) return "";

        List<string> names = new List<string>();
        foreach (StatusEffectApplication effect in attack.targetStatusEffects) names.Add(CardText.Name(effect.type));
        return Colour(StatusHex, string.Join(", ", names));
    }

    void OnBurned(CardUnit unit, int amount) => AddEntry($"{Name(unit)} burns  {Colour(DamageHex, $"-{amount}")}", Owner(unit));
    void OnPoisoned(CardUnit unit, int amount) => AddEntry($"{Name(unit)} is poisoned  {Colour(DamageHex, $"-{amount}")}", Owner(unit));
    void OnRegenerated(CardUnit unit, int amount) => AddEntry($"{Name(unit)} regenerates  {Colour(HealHex, $"+{amount}")}", Owner(unit));
    void OnStunned(CardUnit unit) => AddEntry($"{Name(unit)} is {Colour(StatusHex, "stunned")} and loses the turn", Owner(unit));

    void OnDefeated(CardUnit unit)
    {
        if (unit == null || unit.Instance == null) return;
        AddEntry($"{Colour(Player2, "×")} {Name(unit)} is defeated", Owner(unit));
    }

    void OnGameEnded(int winner)
    {
        AddDivider("GAME OVER");
        AddEntry($"<size=130%><b>{PlayerName(winner)} wins!</b></size>", 0, false, GoldColour);
    }

    // Grey debug lines, only with showDebugMessages on
    void OnDebugLog(string message, string stackTrace, LogType type)
    {
        string colour = type == LogType.Log ? DimHex : type == LogType.Warning ? GoldHex : Player2;
        AddEntry($"<size=80%>{Colour(colour, message)}</size>", 0, true);
    }

    // ---- Text helpers ----

    static string Colour(string hex, string text) => $"<color={hex}>{text}</color>";
    static string PlayerHex(int player) => player == 2 ? Player2 : Player1;
    static string PlayerName(int player) => $"<b>{Colour(PlayerHex(player), $"Player {player}")}</b>";
    static string Name(CardInstance card) => card == null ? "?" : $"<b>{Colour(PlayerHex(card.owner), card.data.cardName)}</b>";
    static string Name(CardUnit unit) => unit == null || unit.Instance == null ? "?" : Name(unit.Instance);
    static int Owner(CardUnit unit) => unit != null && unit.Instance != null ? unit.Instance.owner : 0;

    // ---- Entries ----

    // One line. player picks the stripe colour (0 = game event). quiet lines are smaller and dimmer
    public void AddEntry(string text, int player, bool quiet = false, Color? accentOverride = null)
    {
        Color accent = accentOverride ?? (player == 1 ? Hex(Player1) : player == 2 ? Hex(Player2) : SystemAccent);

        GameObject row = new GameObject("Entry", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(CanvasGroup));
        row.transform.SetParent(content, false);
        Image background = row.GetComponent<Image>();
        background.sprite = rounded;
        background.type = Image.Type.Sliced;
        background.color = RowColour;

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(0, 10, 5, 5);
        layout.spacing = 10;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        // Coloured stripe on the left for whose action it was
        GameObject stripe = new GameObject("Stripe", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        stripe.transform.SetParent(row.transform, false);
        stripe.GetComponent<Image>().color = accent;
        stripe.GetComponent<LayoutElement>().preferredWidth = 4;

        TextMeshProUGUI label = MakeText(row.transform, text, quiet ? fontSize - 3 : fontSize, TextAlignmentOptions.MidlineLeft, TextColour);
        label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

        Push(row);
    }

    // Centred heading between sections, e.g. "ROUND 2"
    void AddDivider(string title)
    {
        GameObject row = new GameObject("Divider", typeof(RectTransform), typeof(CanvasGroup), typeof(LayoutElement));
        row.transform.SetParent(content, false);
        row.GetComponent<LayoutElement>().preferredHeight = fontSize + 12;
        TextMeshProUGUI label = MakeText(row.transform, $"<color={DimHex}>—</color>  {title}  <color={DimHex}>—</color>", fontSize - 4, TextAlignmentOptions.Center, Hex(DimHex));
        label.characterSpacing = 8;
        label.fontStyle = FontStyles.Bold;
        Stretch(label.rectTransform);
        Push(row);
    }

    // Adds a row to the bottom, drops the oldest past maxEntries, fades the new one in and scrolls down
    void Push(GameObject row)
    {
        entries.Enqueue(row);
        while (entries.Count > maxEntries) Destroy(entries.Dequeue());

        StartCoroutine(FadeIn(row.GetComponent<CanvasGroup>()));
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 0f;
    }

    IEnumerator FadeIn(CanvasGroup group)
    {
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            if (group == null) yield break;
            group.alpha = t / 0.25f;
            yield return null;
        }
        if (group != null) group.alpha = 1f;
    }

    void ToggleCollapsed()
    {
        collapsed = !collapsed;
        body.SetActive(!collapsed);
        panel.sizeDelta = new Vector2(panelSize.x, collapsed ? 44f : panelSize.y);
        toggleLabel.text = collapsed ? "SHOW" : "HIDE";
        if (!collapsed)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }
    }

    // ---- Building the panel ----

    void BuildPanel()
    {
        rounded = MakeRoundedSprite();

        GameObject canvasObject = new GameObject("GameLogCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Above other UI, below the card inspector
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Panel with a thin border (border is a slightly bigger panel behind)
        GameObject border = MakeImage("GameLogBorder", canvasObject.transform, BorderColour);
        panel = (RectTransform)border.transform;
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
        panel.anchoredPosition = margin;
        panel.sizeDelta = panelSize;

        GameObject fill = MakeImage("GameLogPanel", border.transform, PanelColour);
        RectTransform fillRect = (RectTransform)fill.transform;
        Stretch(fillRect, 1.5f);

        // Header: title, round, and a hide/show toggle. The whole header is clickable
        GameObject header = MakeImage("Header", fill.transform, new Color(1f, 1f, 1f, 0.04f));
        RectTransform headerRect = (RectTransform)header.transform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = Vector2.one;
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 40f);
        header.AddComponent<Button>().onClick.AddListener(ToggleCollapsed);

        TextMeshProUGUI title = MakeText(header.transform, "BATTLE LOG", fontSize - 2, TextAlignmentOptions.MidlineLeft, new Color(0.75f, 0.8f, 1f));
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 10;
        Stretch(title.rectTransform, 0f, 14f);

        roundLabel = MakeText(header.transform, "", fontSize - 4, TextAlignmentOptions.MidlineRight, GoldColour);
        roundLabel.fontStyle = FontStyles.Bold;
        roundLabel.characterSpacing = 6;
        Stretch(roundLabel.rectTransform, 0f, 70f);

        toggleLabel = MakeText(header.transform, "HIDE", fontSize - 6, TextAlignmentOptions.MidlineRight, Hex(DimHex));
        Stretch(toggleLabel.rectTransform, 0f, 14f);

        // Scrolling list under the header
        body = new GameObject("Body", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        body.transform.SetParent(fill.transform, false);
        RectTransform bodyRect = (RectTransform)body.transform;
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(8f, 8f);
        bodyRect.offsetMax = new Vector2(-8f, -46f);

        GameObject list = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        list.transform.SetParent(body.transform, false);
        content = (RectTransform)list.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;

        VerticalLayoutGroup listLayout = list.GetComponent<VerticalLayoutGroup>();
        listLayout.spacing = 4;
        listLayout.childControlWidth = true;
        listLayout.childControlHeight = true;
        listLayout.childForceExpandWidth = true;
        listLayout.childForceExpandHeight = false;
        list.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = body.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = bodyRect;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 25f;

        // Scrolling and the header button need an EventSystem
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    GameObject MakeImage(string name, Transform parent, Color colour)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = rounded;
        image.type = Image.Type.Sliced;
        image.color = colour;
        return imageObject;
    }

    static TextMeshProUGUI MakeText(Transform parent, string text, float size, TextAlignmentOptions alignment, Color colour)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = alignment;
        tmp.color = colour;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    // Fills the parent, with an inset all round, or only left/right
    static void Stretch(RectTransform rect, float inset = 0f, float sides = -1f)
    {
        float x = sides >= 0f ? sides : inset;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(x, inset);
        rect.offsetMax = new Vector2(-x, -inset);
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color colour);
        return colour;
    }

    // White rounded rectangle that stretches without warping its corners (9-sliced)
    static Sprite MakeRoundedSprite()
    {
        const int size = 32, radius = 10;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance outside the rounded corner, 0 inside
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                             new Vector4(radius + 1, radius + 1, radius + 1, radius + 1));
    }
}
