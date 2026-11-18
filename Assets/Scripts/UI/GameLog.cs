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

    // Colours (hex, for TextMeshPro rich text), matching the Inked Star Chart palette
    const string Player1 = "#4D9BFF";
    const string Player2 = "#FF5A5A";
    const string DamageHex = "#FF9A55";
    const string HealHex = "#66E07A";
    const string StatusHex = "#C08CFF";
    const string GoldHex = "#FFE39A";
    const string DimHex = "#A9BDC2";

    static readonly Color RowColour = new Color(1f, 1f, 1f, 0.06f);
    static readonly Color TextColour = new Color(0.96f, 0.95f, 1f);
    static readonly Color GoldColour = new Color(1f, 0.89f, 0.6f);
    static readonly Color SystemAccent = new Color(0.66f, 0.68f, 0.82f);

    private RectTransform panel;
    private GameObject body;
    private ScrollRect scroll;
    private RectTransform content;
    private TextMeshProUGUI roundLabel;
    private TextMeshProUGUI toggleLabel;
    private Sprite rounded;
    private bool collapsed;
    private bool previewMode;      // Built by the editor preview: no fade-ins (coroutines don't run outside Play mode)
    private GameObject canvasRoot;
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
        background.pixelsPerUnitMultiplier = 3f; // Small corners for thin rows
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
        TextMeshProUGUI label = MakeText(row.transform, $"<color={GoldHex}>—</color>  {title}  <color={GoldHex}>—</color>", fontSize - 2, TextAlignmentOptions.Center, GoldColour);
        GameFonts.UseTitle(label, 0.2f);
        label.characterSpacing = 3;
        Stretch(label.rectTransform);
        Push(row);
    }

    // Adds a row to the bottom, drops the oldest past maxEntries, fades the new one in and scrolls down
    void Push(GameObject row)
    {
        entries.Enqueue(row);
        while (entries.Count > maxEntries) Destroy(entries.Dequeue());

        if (previewMode) row.GetComponent<CanvasGroup>().alpha = 1f;
        else StartCoroutine(FadeIn(row.GetComponent<CanvasGroup>()));
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
        panel.sizeDelta = new Vector2(panelSize.x, collapsed ? 64f : panelSize.y); // Header plus the rim
        toggleLabel.text = collapsed ? "SHOW" : "HIDE";
        if (!collapsed)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }
    }

    // ---- Building the panel ----

    // Inked Star Chart panel (InkUI): navy with an ink-and-gold rim and a hard shadow, a darker header strip
    void BuildPanel()
    {
        rounded = InkUI.FillSprite;

        GameObject canvasObject = new GameObject("GameLogCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot = canvasObject;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Above other UI, below the card inspector
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        Image frame = InkUI.FramedPanel(canvasObject.transform, "GameLogPanel", InkUI.PanelColor);
        panel = frame.rectTransform;
        panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero;
        panel.anchoredPosition = margin;
        panel.sizeDelta = panelSize;
        Transform fill = panel;

        // Header inside the rim: title, round, and a hide/show toggle. The whole header is clickable
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(Image));
        header.transform.SetParent(fill, false);
        header.GetComponent<Image>().color = InkUI.PanelDark;
        RectTransform headerRect = (RectTransform)header.transform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = Vector2.one;
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.offsetMin = new Vector2(11f, -53f);
        headerRect.offsetMax = new Vector2(-11f, -11f);
        header.transform.SetSiblingIndex(0); // Under the rim
        header.AddComponent<Button>().onClick.AddListener(ToggleCollapsed);

        TextMeshProUGUI title = MakeText(header.transform, "BATTLE LOG", fontSize + 3, TextAlignmentOptions.MidlineLeft, TextColour);
        GameFonts.UseTitle(title, 0.2f);
        title.characterSpacing = 3;
        Stretch(title.rectTransform, 0f, 14f);

        roundLabel = MakeText(header.transform, "", fontSize, TextAlignmentOptions.MidlineRight, GoldColour);
        GameFonts.UseTitle(roundLabel, 0.2f);
        roundLabel.characterSpacing = 2;
        Stretch(roundLabel.rectTransform, 0f, 70f);

        toggleLabel = MakeText(header.transform, "HIDE", fontSize - 4, TextAlignmentOptions.MidlineRight, Hex(DimHex));
        Stretch(toggleLabel.rectTransform, 0f, 14f);

        // Scrolling list under the header, inside the rim
        body = new GameObject("Body", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        body.transform.SetParent(fill, false);
        body.transform.SetSiblingIndex(1); // Under the rim too
        RectTransform bodyRect = (RectTransform)body.transform;
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(16f, 16f);
        bodyRect.offsetMax = new Vector2(-16f, -60f);

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

        // Scrolling and the header button need an EventSystem (only while playing, so the editor preview never adds one)
        if (Application.isPlaying && FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // ---- Editor preview ----

    // The log's canvas, so the editor preview can tidy it up
    public GameObject Root => canvasRoot;

    // Builds the log outside Play mode with example lines using the given cards. Used by UIPreview
    public void BuildPreview(List<CardData> cards)
    {
        previewMode = true;
        BuildPanel();
        if (cards == null || cards.Count == 0) return;

        string first = PreviewName(cards, 0, 1), second = PreviewName(cards, 1, 2), third = PreviewName(cards, 2, 1);
        AttackData attack = cards[0].attack1 != null ? cards[0].attack1 : cards[0].attack2;
        string attackName = attack != null ? attack.attackName : "Attack";

        AddDivider("DRAFT");
        AddEntry($"Coin flip: {PlayerName(1)} picks first", 0);
        AddEntry($"{PlayerName(1)} drafts {first}", 1);
        AddEntry($"{PlayerName(2)} drafts {second}", 2);
        AddDivider("BATTLE");
        AddEntry($"<color={DimHex}>{first}'s turn</color>", 1, true);
        AddEntry($"{first} <color={DimHex}>→</color> {second}  <i>{attackName}</i>  {Colour(DamageHex, "-340")}  {Colour(GoldHex, "bonus")}", 1);
        AddEntry($"{second} burns  {Colour(DamageHex, "-60")}", 2);
        AddDivider("ROUND 2");
        AddEntry($"{third} regenerates  {Colour(HealHex, "+120")}", 1);
        AddEntry($"{Colour(Player2, "×")} {second} is defeated", 2);
        roundLabel.text = "ROUND 2";
    }

    static string PreviewName(List<CardData> cards, int index, int owner)
    {
        CardData data = cards[Mathf.Min(index, cards.Count - 1)];
        return $"<b>{Colour(PlayerHex(owner), data.cardName)}</b>";
    }

    static TextMeshProUGUI MakeText(Transform parent, string text, float size, TextAlignmentOptions alignment, Color colour)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        GameFonts.UseBody(tmp);
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
}
