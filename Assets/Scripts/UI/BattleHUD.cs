using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Battle HUD in the Inked Star Chart style: navy panels with an ink-and-gold rim and a hard ink shadow, ink-outlined
// chips and buttons, Lilita One labels. Builds its own canvas. CombatManager adds it automatically.
// - Top left: what to do in the draft/placement, then the turn order during battle (who acts now and next)
// - Right side: the active card (player, HP, move/attack left) and its 4 attacks. Click or press 1-4. Hover for details
// - Bottom right: END TURN (Space or click)
// - Middle: banners for DRAFT, PLACEMENT, BATTLE and each new round
// The board fills the screen's height, so everything sits in the space at the sides
public class BattleHUD : MonoBehaviour
{
    [Header("Colours")]
    [SerializeField] private Color player1Color = new Color(0.30f, 0.61f, 1f);   // Same as the tokens (InkArt)
    [SerializeField] private Color player2Color = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private Color panelColor = new Color(0.275f, 0.369f, 0.412f); // Slate blue-grey #465E69 (menu buttons): panel and button fill
    [SerializeField] private Color goldColor = new Color(1f, 0.89f, 0.6f);

    [Header("Layout")]
    [SerializeField] private int maxPortraits = 7;     // Turn order cards shown before "+N"
    [SerializeField] private float bannerTime = 1.3f;  // How long a banner holds in the middle

    [Header("Logo")]
    [SerializeField] private bool showWatermark = true;       // Faint STRATAFIELD logo at the top middle during a match
    [SerializeField, Range(0f, 1f)] private float watermarkAlpha = 0.35f;
    [SerializeField] private float watermarkWidth = 380f;

    static readonly Color TextColor = new Color(0.96f, 0.95f, 1f);
    static readonly Color DimColor = new Color(0.78f, 0.84f, 0.85f);
    static readonly Color HealColor = new Color(0.45f, 0.9f, 0.5f);
    static readonly Color PhysicalColor = new Color(1f, 0.62f, 0.35f);
    static readonly Color MagicColor = new Color(0.78f, 0.6f, 1f);
    static readonly Color CooldownColor = new Color(1f, 0.42f, 0.42f);
    static readonly Color KeyCapColor = new Color(0.137f, 0.184f, 0.208f); // Dark slate

    private Transform canvasRoot;

    // Top left
    private RectTransform infoPanel;
    private TextMeshProUGUI statusText;
    private RectTransform orderRow;
    private TextMeshProUGUI roundLabel;
    private string orderKey = "";
    private float orderWidth;
    private readonly List<(CardUnit unit, Image hp)> portraits = new List<(CardUnit, Image)>();

    // Right side
    private RectTransform cardPanel;
    private CanvasGroup cardGroup;
    private Image playerChip;
    private TextMeshProUGUI playerChipText, cardName, hpText, hintText;
    private Image hpFill, moveChip, attackChip;
    private TextMeshProUGUI moveChipText, attackChipText;
    private readonly AttackButton[] buttons = new AttackButton[4];
    private int hoveredButton = -1;
    private RectTransform tooltip;
    private TextMeshProUGUI tooltipText;
    private float slideIn;  // Card panel slides in at the start of each turn

    // Bottom right
    private RectTransform endTurnPanel;
    private Image endTurnFrame;
    private TextMeshProUGUI endTurnText;

    // Middle
    private RectTransform banner;
    private CanvasGroup bannerGroup;
    private TextMeshProUGUI bannerTitle, bannerSub;
    private Image bannerLogo;   // STRATAFIELD logo above the match-start banner
    private Image watermark;    // Faint logo at the top of the screen during a match
    private readonly Queue<(string title, string sub, Color color, bool logo)> bannerQueue = new Queue<(string, string, Color, bool)>();
    private bool bannerPlaying;

    private class AttackButton
    {
        public RectTransform root;
        public Image frame;
        public CanvasGroup group;
        public TextMeshProUGUI key, name, damage, tags;
    }

    void Awake()
    {
        _ = GameFonts.Title; // Loads it so <font="Audiowide"> works in the status text
        Build();
    }

    void Start()
    {
        if (DraftManager.Instance != null) DraftManager.Instance.DraftStarted += OnDraftStarted;
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.PlacementStarted += OnPlacementStarted;
            TurnManager.Instance.BattleStarted += OnBattleStarted;
            TurnManager.Instance.RoundStarted += OnRoundStarted;
            TurnManager.Instance.TurnStarted += OnTurnStarted;
        }
    }

    void OnDestroy()
    {
        if (DraftManager.Instance != null) DraftManager.Instance.DraftStarted -= OnDraftStarted;
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.PlacementStarted -= OnPlacementStarted;
            TurnManager.Instance.BattleStarted -= OnBattleStarted;
            TurnManager.Instance.RoundStarted -= OnRoundStarted;
            TurnManager.Instance.TurnStarted -= OnTurnStarted;
        }
        if (canvasRoot != null) Destroy(canvasRoot.gameObject);
    }

    // ---- Events ----

    void OnDraftStarted(int first) => ShowBanner("DRAFT", $"Player {first} picks first", TextColor, true); // The match starts: show the logo
    void OnPlacementStarted(int first) => ShowBanner("PLACEMENT", $"Player {first} places first", TextColor);
    void OnBattleStarted() => ShowBanner("BATTLE!", "Fastest card goes first", goldColor);
    void OnRoundStarted(int round) { if (round > 1) ShowBanner($"ROUND {round}", "", TextColor); }
    void OnTurnStarted(CardUnit unit) => slideIn = 1f;

    // ---- Every frame ----

    void LateUpdate()
    {
        TurnManager turns = TurnManager.Instance;
        bool hidden = turns == null || turns.Phase == GamePhase.GameOver || InputGate.IsPaused;
        canvasRoot.gameObject.SetActive(!hidden || bannerPlaying);
        if (hidden)
        {
            infoPanel.gameObject.SetActive(false);
            cardPanel.gameObject.SetActive(false);
            endTurnPanel.gameObject.SetActive(false);
            return;
        }

        UpdateInfoPanel(turns);
        UpdateCardPanel(turns);
        UpdateEndTurn(turns);
    }

    // Draft/placement instructions, or the turn order in battle
    void UpdateInfoPanel(TurnManager turns)
    {
        string status = null;
        int player = 0;
        DraftManager draft = DraftManager.Instance;
        if (turns.Phase == GamePhase.Draft && draft != null && draft.IsDrafting && !draft.IsDealing)
        {
            player = draft.PickingPlayer;
            status = $"<font=\"Audiowide\">PLAYER {player} PICKS</font>\n<size=72%><color=#C9D8D8>Click a card, SPACE to confirm, right-click to inspect</color></size>";
        }
        else if (turns.Phase == GamePhase.Placement)
        {
            player = turns.PlacingPlayer;
            status = $"<font=\"Audiowide\">PLAYER {player} PLACES</font>\n<size=72%><color=#C9D8D8>Drag a card onto your coloured rows</color></size>";
        }

        bool battle = turns.Phase == GamePhase.Battle;
        infoPanel.gameObject.SetActive(status != null || battle);
        statusText.gameObject.SetActive(status != null);
        orderRow.gameObject.SetActive(battle);
        roundLabel.gameObject.SetActive(battle);

        if (status != null)
        {
            statusText.text = status;
            infoPanel.sizeDelta = new Vector2(470, 78);
            infoPanel.GetComponent<Image>().color = Color.Lerp(panelColor, PlayerColor(player), 0.45f); // Navy washed with the player's colour
            return;
        }
        if (!battle) return;

        infoPanel.GetComponent<Image>().color = panelColor;
        // Working out the turn order sorts lists and builds strings, so check it a few times a second, not every frame
        if (Time.unscaledTime >= nextOrderCheck)
        {
            nextOrderCheck = Time.unscaledTime + 0.2f;
            roundLabel.text = $"ROUND {turns.Round}";
            RebuildTurnOrder(turns);
        }
        infoPanel.sizeDelta = new Vector2(Mathf.Max(470f, 112f + orderWidth + 12f), 132f);

        foreach ((CardUnit unit, Image hp) in portraits)
            if (unit != null && unit.Instance != null) SetFill(hp, (float)unit.Instance.currentHP / unit.Instance.data.maxHP);
    }

    // Active card and its attacks
    void UpdateCardPanel(TurnManager turns)
    {
        CardUnit active = turns.ActiveUnit;
        bool show = turns.Phase == GamePhase.Battle && active != null && active.Instance != null && active.Instance.IsAlive;
        cardPanel.gameObject.SetActive(show);
        if (!show)
        {
            tooltip.gameObject.SetActive(false);
            hoveredButton = -1;
            return;
        }

        // Slide in from the right at the start of a turn
        slideIn = Mathf.MoveTowards(slideIn, 0f, Time.unscaledDeltaTime * 4f);
        float eased = slideIn * slideIn;
        cardPanel.anchoredPosition = new Vector2(-24f + eased * 120f, 40f);
        cardGroup.alpha = 1f - eased;

        // Rewriting the text makes garbage and re-lays out the TMP text, so only do it when something changed
        // (plus twice a second to catch buffs that change attack power)
        CardMover mover = active.GetComponent<CardMover>();
        bool hasMoved = mover != null && mover.hasMoved;
        CardInstance card = active.Instance;
        int key = 17;
        key = key * 31 + card.GetHashCode();
        key = key * 31 + card.currentHP;
        key = key * 31 + card.Speed;
        key = key * 31 + (hasMoved ? 1 : 0) + (turns.HasAttacked ? 2 : 0) + (turns.CanAct ? 4 : 0);
        key = key * 31 + (turns.SelectedAttack != null ? turns.SelectedAttack.GetHashCode() : 0);
        key = key * 31 + hoveredButton;
        key = key * 31 + (card.CanUseAttack(card.data.attack1) ? 1 : 0) + (card.CanUseAttack(card.data.attack2) ? 2 : 0)
                       + (card.CanUseAttack(card.data.attack3) ? 4 : 0) + (card.CanUseAttack(card.data.attack4) ? 8 : 0);
        if (key == cardPanelKey && Time.unscaledTime < cardPanelRefresh) return;
        cardPanelKey = key;
        cardPanelRefresh = Time.unscaledTime + 0.5f;
        FillCardPanel(card, hasMoved, turns.HasAttacked, turns.CanAct, turns.SelectedAttack);
    }

    private int cardPanelKey;
    private float cardPanelRefresh, nextOrderCheck;

    // Writes one card's details into the card panel. Shared by the game and the editor preview
    void FillCardPanel(CardInstance card, bool hasMoved, bool hasAttacked, bool canAct, AttackData selected)
    {
        Color owner = PlayerColor(card.owner);
        playerChip.color = owner;
        playerChipText.text = $"PLAYER {card.owner}";
        cardName.text = card.data.cardName;
        SetFill(hpFill, (float)card.currentHP / card.data.maxHP);
        hpFill.color = Color.Lerp(CooldownColor, HealColor, (float)card.currentHP / card.data.maxHP);
        hpText.text = $"HP {card.currentHP} / {card.data.maxHP}   SPD {card.Speed}";

        bool canMove = !hasMoved && card.MoveSpaces > 0;
        SetChip(moveChip, moveChipText, canMove ? "MOVE" : hasMoved ? "MOVED" : "NO MOVE", canMove);
        SetChip(attackChip, attackChipText, hasAttacked ? "ATTACKED" : "ATTACK", !hasAttacked);

        if (selected != null)
            hintText.text = CombatResolver.UsedOnSelf(selected) ? $"Click <b>{card.data.cardName}</b> to use {selected.attackName}"
                                                                : $"Click a target for <b>{selected.attackName}</b>";
        else if (!canAct) hintText.text = "";
        else if (hasAttacked) hintText.text = canMove ? "Drag to move, or SPACE to end turn" : "Press SPACE to end turn";
        else hintText.text = canMove ? "Drag to move, pick an attack (1-4)" : "Pick an attack (1-4)";

        AttackData[] attacks = card.GetAttacks();
        for (int i = 0; i < 4; i++) UpdateButton(buttons[i], i, attacks[i], card, canAct, hasAttacked, selected);
        UpdateTooltip(attacks, card);
    }

    void UpdateButton(AttackButton button, int index, AttackData attack, CardInstance card, bool canAct, bool hasAttacked, AttackData selected)
    {
        bool isSelected = attack != null && attack == selected;
        bool onCooldown = attack != null && !card.CanUseAttack(attack);
        bool usable = attack != null && !onCooldown && !hasAttacked && canAct;

        button.key.text = (index + 1).ToString();
        button.group.alpha = attack == null ? 0.35f : usable || isSelected ? 1f : 0.5f;
        button.frame.color = isSelected ? goldColor : index == hoveredButton && usable ? Color.Lerp(panelColor, Color.white, 0.3f) : Color.Lerp(panelColor, Color.white, 0.1f);
        button.root.anchoredPosition = new Vector2(isSelected ? -2f : 14f, button.root.anchoredPosition.y); // Selected pops out toward the board

        if (attack == null)
        {
            button.name.text = "Empty";
            button.damage.text = "";
            button.tags.text = "";
            return;
        }

        button.name.text = attack.attackName;
        if (attack.damageType == DamageType.Heal) button.damage.text = Colored($"HEALS {card.GetAttackPower(attack)}", HealColor);
        else if (attack.damage == 0) button.damage.text = Colored("NO DAMAGE", DimColor);
        else button.damage.text = Colored($"{card.GetAttackPower(attack)} {attack.damageType.ToString().ToUpper()}",
                                          attack.damageType == DamageType.Magic ? MagicColor : PhysicalColor);

        button.tags.text = onCooldown ? Colored("ON COOLDOWN - use another attack first", CooldownColor) : ShortTags(attack, card);
    }

    // Full attack text next to the hovered button
    void UpdateTooltip(AttackData[] attacks, CardInstance card)
    {
        AttackData attack = hoveredButton >= 0 ? attacks[hoveredButton] : null;
        tooltip.gameObject.SetActive(attack != null);
        if (attack == null) return;

        string text = CardText.Attack(hoveredButton + 1, attack, card);
        if (!string.IsNullOrEmpty(attack.description)) text += $"\n\n<i><color=#B8BCD6>{attack.description}</color></i>";
        tooltipText.text = text;
        float height = tooltipText.GetPreferredValues(text, 340f, 0f).y + 30f;
        tooltip.sizeDelta = new Vector2(370f, height);

        RectTransform button = buttons[hoveredButton].root;
        Vector3[] corners = new Vector3[4];
        button.GetWorldCorners(corners);
        tooltip.position = (corners[0] + corners[1]) * 0.5f; // Left middle of the button
        tooltip.anchoredPosition += new Vector2(-14f, 0f);
    }

    void UpdateEndTurn(TurnManager turns)
    {
        bool show = turns.CanAct;
        endTurnPanel.gameObject.SetActive(show);
        if (!show) return;

        // Pulses gold once there's nothing left to do
        CardMover mover = turns.ActiveUnit.GetComponent<CardMover>();
        bool done = turns.HasAttacked && (mover == null || mover.hasMoved);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
        endTurnFrame.color = done ? Color.Lerp(new Color(0.85f, 0.62f, 0.2f), goldColor, pulse) : panelColor;
        endTurnText.color = TextColor; // White with an ink outline reads on navy and gold
        endTurnPanel.localScale = Vector3.one * (done ? 1f + pulse * 0.04f : 1f);
    }

    // ---- Turn order ----

    void RebuildTurnOrder(TurnManager turns)
    {
        List<CardUnit> now = new List<CardUnit>();
        if (turns.ActiveUnit != null) now.Add(turns.ActiveUnit);
        now.AddRange(turns.UpcomingTurns());
        List<CardUnit> next = turns.NextRoundOrder();

        // Only rebuild when the order changes (a turn passes, a card dies, speed changes)
        string key = string.Join(",", now.ConvertAll(u => u.Instance.GetHashCode() + ":" + u.Instance.Speed)) + "|" +
                     string.Join(",", next.ConvertAll(u => u.Instance.GetHashCode().ToString()));
        if (key == orderKey) return;
        orderKey = key;

        foreach (Transform child in orderRow) Destroy(child.gameObject);
        portraits.Clear();

        float x = 0f;
        int shown = 0;
        for (int i = 0; i < now.Count && shown < maxPortraits; i++, shown++)
            x = AddPortrait(now[i], x, i == 0 && now[i] == turns.ActiveUnit);

        if (shown < maxPortraits && next.Count > 0)
        {
            x = AddDivider(x, $"R{turns.Round + 1}");
            for (int i = 0; i < next.Count && shown < maxPortraits; i++, shown++)
                x = AddPortrait(next[i], x, false, true);
        }

        int hiddenCount = now.Count + next.Count - shown;
        if (hiddenCount > 0)
        {
            TextMeshProUGUI more = MakeText(orderRow, $"+{hiddenCount}", 18, TextAlignmentOptions.MidlineLeft, DimColor);
            Place(more.rectTransform, new Vector2(x + 4f, 30f), new Vector2(40f, 40f));
            x += 44f;
        }
        orderWidth = x;
    }

    float AddPortrait(CardUnit unit, float x, bool isActive, bool nextRound = false)
    {
        CardInstance card = unit.Instance;
        x = AddPortrait(card.data, card.owner, card.Speed, (float)card.currentHP / card.data.maxHP, x, isActive, nextRound, out Image hpFill);
        portraits.Add((unit, hpFill));
        return x;
    }

    // One small unit in the turn order: its face in a player-coloured frame (gold for the one acting), HP bar and speed under it
    float AddPortrait(CardData data, int ownerPlayer, int speedValue, float hp, float x, bool isActive, bool nextRound, out Image hpFill)
    {
        Vector2 size = isActive ? new Vector2(54f, 72f) : new Vector2(42f, 56f);
        Color owner = PlayerColor(ownerPlayer);

        Image frame = MakePanel(orderRow, isActive ? goldColor : owner, "Portrait");
        Place(frame.rectTransform, new Vector2(x, 22f), size);
        if (nextRound) frame.gameObject.AddComponent<CanvasGroup>().alpha = 0.55f; // Next round is greyed a little

        Sprite art = data.artwork;
        if (art != null)
        {
            // The character's face, cropped from the art window like the board tokens
            RawImage face = new GameObject("Face", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            face.transform.SetParent(frame.transform, false);
            face.raycastTarget = false;
            face.texture = art.texture;
            Stretch(face.rectTransform, 4f);
            face.uvRect = PortraitUV(art, (size.x - 8f) / (size.y - 8f));
        }
        else
        {
            TextMeshProUGUI initial = MakeText(frame.transform, data.cardName.Substring(0, 1), 22, TextAlignmentOptions.Center, TextColor);
            Heading(initial);
            Stretch(initial.rectTransform, 0f);
        }

        Image hpBack = MakeImage(orderRow, InkArt.Ink, "HP");
        Place(hpBack.rectTransform, new Vector2(x, 13f), new Vector2(size.x, 7f));
        hpFill = MakeImage(hpBack.transform, HealColor, "Fill");
        Stretch(hpFill.rectTransform, 1.5f);
        hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        SetFill(hpFill, hp);

        TextMeshProUGUI speed = MakeText(orderRow, speedValue.ToString(), 13, TextAlignmentOptions.Center, isActive ? goldColor : DimColor);
        Place(speed.rectTransform, new Vector2(x, -4f), new Vector2(size.x, 16f));

        if (isActive)
        {
            TextMeshProUGUI now = MakeText(orderRow, "NOW", 12, TextAlignmentOptions.Center, goldColor);
            Heading(now);
            Place(now.rectTransform, new Vector2(x, 22f + size.y), new Vector2(size.x, 14f));
        }
        return x + size.x + 6f;
    }

    float AddDivider(float x, string label)
    {
        Image line = MakeImage(orderRow, new Color(1f, 1f, 1f, 0.25f), "Divider");
        Place(line.rectTransform, new Vector2(x + 8f, 14f), new Vector2(3f, 64f));
        TextMeshProUGUI text = MakeText(orderRow, label, 11, TextAlignmentOptions.Center, DimColor);
        Heading(text);
        Place(text.rectTransform, new Vector2(x - 2f, -4f), new Vector2(24f, 16f));
        return x + 20f;
    }

    // ---- Banners ----

    void ShowBanner(string title, string sub, Color color, bool withLogo = false)
    {
        bannerQueue.Enqueue((title, sub, color, withLogo));
        if (!bannerPlaying) StartCoroutine(PlayBanners());
    }

    // Slides each banner in from the left, holds, then out to the right
    IEnumerator PlayBanners()
    {
        bannerPlaying = true;
        canvasRoot.gameObject.SetActive(true);
        banner.gameObject.SetActive(true);

        while (bannerQueue.Count > 0)
        {
            (string title, string sub, Color color, bool withLogo) = bannerQueue.Dequeue();
            if (bannerLogo != null) bannerLogo.gameObject.SetActive(withLogo);
            bannerTitle.text = title;
            bannerTitle.color = color;
            bannerSub.text = sub;

            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            {
                SetBanner(1f - t, t);
                yield return null;
            }
            SetBanner(0f, 1f);
            yield return new WaitForSecondsRealtime(bannerTime);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f)
            {
                SetBanner(-t, 1f - t);
                yield return null;
            }
        }

        banner.gameObject.SetActive(false);
        bannerPlaying = false;
    }

    // offset 1 = off to the left, 0 = middle, -1 = off to the right
    void SetBanner(float offset, float alpha)
    {
        float eased = Mathf.Sign(offset) * offset * offset;
        bannerTitle.rectTransform.anchoredPosition = new Vector2(-eased * 900f, 18f);
        bannerSub.rectTransform.anchoredPosition = new Vector2(-eased * 600f, -40f);
        if (bannerLogo != null) bannerLogo.rectTransform.anchoredPosition = new Vector2(-eased * 1100f, 150f); // Leads in a little ahead
        bannerGroup.alpha = Mathf.Clamp01(alpha);
    }

    // ---- Building ----

    void Build()
    {
        GameObject canvasObject = new GameObject("BattleHUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot = canvasObject.transform;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // Below the game log and inspector
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        BuildWatermark();
        BuildInfoPanel();
        BuildCardPanel();
        BuildEndTurn();
        BuildBanner();

        // Clicks need an EventSystem (only while playing, so the editor preview never adds one to the scene)
        if (Application.isPlaying && FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // ---- Editor preview ----

    // The HUD's canvas, so the editor preview can tidy it up
    public GameObject Root => canvasRoot != null ? canvasRoot.gameObject : null;

    // Builds the HUD outside Play mode with example data: the turn order, the first card's panel with its attacks
    // (first one hovered, second selected), END TURN, and optionally the BATTLE! banner. Used by UIPreview
    public void BuildPreview(List<CardData> cards, bool showBanner)
    {
        _ = GameFonts.Title;
        Build();
        if (cards == null || cards.Count == 0) return;

        // Turn order: this round, then the start of next round
        infoPanel.gameObject.SetActive(true);
        statusText.gameObject.SetActive(false);
        orderRow.gameObject.SetActive(true);
        roundLabel.gameObject.SetActive(true);
        roundLabel.text = "ROUND 2";
        float x = 0f;
        for (int i = 0; i < cards.Count && i < 4; i++)
            x = AddPortrait(cards[i], i % 2 == 0 ? 1 : 2, cards[i].speed, 1f - 0.2f * i, x, i == 0, false, out _);
        x = AddDivider(x, "R3");
        for (int i = 0; i < cards.Count && i < 2; i++)
            x = AddPortrait(cards[i], i % 2 == 0 ? 1 : 2, cards[i].speed, 1f, x, false, true, out _);
        orderWidth = x;
        infoPanel.sizeDelta = new Vector2(Mathf.Max(470f, 112f + orderWidth + 12f), 132f);

        // The first card's panel, a little hurt
        CardInstance card = new CardInstance(cards[0], 1, -1, -1);
        card.currentHP = Mathf.RoundToInt(card.data.maxHP * 0.7f);
        AttackData[] attacks = card.GetAttacks();
        cardPanel.gameObject.SetActive(true);
        hoveredButton = 0;
        FillCardPanel(card, false, false, true, attacks.Length > 1 ? attacks[1] : null);
        Canvas.ForceUpdateCanvases();
        FillCardPanel(card, false, false, true, attacks.Length > 1 ? attacks[1] : null); // Again, so the tooltip lines up with the laid-out buttons

        endTurnPanel.gameObject.SetActive(true);
        if (showBanner)
        {
            banner.gameObject.SetActive(true);
            if (bannerLogo != null) bannerLogo.gameObject.SetActive(true);
            bannerTitle.text = "BATTLE!";
            bannerTitle.color = goldColor;
            bannerSub.text = "Fastest card goes first";
            SetBanner(0f, 1f);
        }
    }

    void BuildInfoPanel()
    {
        Image panel = MakeFramedPanel(canvasRoot, "Info Panel");
        infoPanel = panel.rectTransform;
        infoPanel.anchorMin = infoPanel.anchorMax = infoPanel.pivot = new Vector2(0f, 1f);
        infoPanel.anchoredPosition = new Vector2(20f, -20f);
        infoPanel.sizeDelta = new Vector2(470f, 78f);

        statusText = MakeText(infoPanel, "", 24, TextAlignmentOptions.Center, TextColor);
        statusText.characterSpacing = 1;
        Stretch(statusText.rectTransform, 10f);

        orderRow = new GameObject("Turn Order", typeof(RectTransform)).GetComponent<RectTransform>();
        orderRow.SetParent(infoPanel, false);
        orderRow.anchorMin = orderRow.anchorMax = orderRow.pivot = Vector2.zero;
        orderRow.anchoredPosition = new Vector2(112f, 14f);

        roundLabel = MakeText(infoPanel, "", 18, TextAlignmentOptions.Center, goldColor);
        Heading(roundLabel);
        roundLabel.characterSpacing = 2;
        Place(roundLabel.rectTransform, new Vector2(12f, 20f), new Vector2(92f, 92f));
    }

    void BuildCardPanel()
    {
        Image panel = MakeFramedPanel(canvasRoot, "Card Panel");
        cardPanel = panel.rectTransform;
        cardPanel.anchorMin = cardPanel.anchorMax = cardPanel.pivot = new Vector2(1f, 0.5f);
        cardPanel.anchoredPosition = new Vector2(-24f, 40f);
        cardPanel.sizeDelta = new Vector2(400f, 580f);
        cardGroup = cardPanel.gameObject.AddComponent<CanvasGroup>();

        // Header: player chip, name, HP, move/attack left
        playerChip = MakePanel(cardPanel, player1Color, "Player Chip");
        PlaceTop(playerChip.rectTransform, new Vector2(18f, -16f), new Vector2(120f, 30f));
        playerChipText = MakeText(playerChip.transform, "PLAYER 1", 15, TextAlignmentOptions.Center, TextColor);
        Heading(playerChipText);
        playerChipText.characterSpacing = 2;
        Stretch(playerChipText.rectTransform, 0f);

        cardName = MakeText(cardPanel, "", 30, TextAlignmentOptions.MidlineLeft, TextColor);
        Heading(cardName);
        cardName.textWrappingMode = TextWrappingModes.NoWrap;
        cardName.overflowMode = TextOverflowModes.Ellipsis;
        PlaceTop(cardName.rectTransform, new Vector2(18f, -50f), new Vector2(364f, 38f));

        Image hpBack = MakeImage(cardPanel, new Color(0f, 0f, 0f, 0.6f), "HP Bar");
        PlaceTop(hpBack.rectTransform, new Vector2(18f, -94f), new Vector2(364f, 12f));
        hpFill = MakeImage(hpBack.transform, HealColor, "Fill");
        Stretch(hpFill.rectTransform, 0f);
        hpFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        hpText = MakeText(cardPanel, "", 15, TextAlignmentOptions.MidlineLeft, DimColor);
        PlaceTop(hpText.rectTransform, new Vector2(18f, -110f), new Vector2(240f, 20f));

        moveChip = MakePanel(cardPanel, HealColor, "Move Chip");
        PlaceTop(moveChip.rectTransform, new Vector2(170f, -16f), new Vector2(100f, 30f));
        moveChipText = MakeText(moveChip.transform, "", 13, TextAlignmentOptions.Center, TextColor);
        Heading(moveChipText);
        Stretch(moveChipText.rectTransform, 0f);

        attackChip = MakePanel(cardPanel, HealColor, "Attack Chip");
        PlaceTop(attackChip.rectTransform, new Vector2(276f, -16f), new Vector2(106f, 30f));
        attackChipText = MakeText(attackChip.transform, "", 13, TextAlignmentOptions.Center, TextColor);
        Heading(attackChipText);
        Stretch(attackChipText.rectTransform, 0f);

        hintText = MakeText(cardPanel, "", 16, TextAlignmentOptions.Center, goldColor);
        PlaceTop(hintText.rectTransform, new Vector2(18f, -136f), new Vector2(364f, 24f));

        // The 4 attacks
        for (int i = 0; i < 4; i++)
            buttons[i] = MakeAttackButton(i, -166f - i * 102f);

        // Hover details, to the left of the panel
        Image tip = MakeFramedPanel(canvasRoot, "Attack Tooltip");
        tooltip = tip.rectTransform;
        tooltip.pivot = new Vector2(1f, 0.5f);
        tip.raycastTarget = false;
        tooltipText = MakeText(tooltip, "", 16, TextAlignmentOptions.TopLeft, TextColor);
        Stretch(tooltipText.rectTransform, 15f);
        tooltip.gameObject.SetActive(false);
    }

    AttackButton MakeAttackButton(int index, float top)
    {
        AttackButton button = new AttackButton();
        button.frame = MakePanel(cardPanel, panelColor, "Attack " + (index + 1));
        button.root = button.frame.rectTransform;
        PlaceTop(button.root, new Vector2(14f, top), new Vector2(372f, 94f));
        button.group = button.root.gameObject.AddComponent<CanvasGroup>();

        Button click = button.root.gameObject.AddComponent<Button>();
        click.transition = Selectable.Transition.None;
        click.navigation = new Navigation { mode = Navigation.Mode.None };
        button.root.gameObject.AddComponent<UISound>().clickCue = ""; // Hover sound only; TurnManager plays the select sound
        click.onClick.AddListener(() =>
        {
            TurnManager.Instance?.SelectAttack(index);
            EventSystem.current?.SetSelectedGameObject(null); // Otherwise Space would also "press" the button
        });

        EventTrigger trigger = button.root.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, () => hoveredButton = index);
        AddTrigger(trigger, EventTriggerType.PointerExit, () => { if (hoveredButton == index) hoveredButton = -1; });

        // Key cap
        Image keyCap = MakePanel(button.root, KeyCapColor, "Key");
        PlaceTop(keyCap.rectTransform, new Vector2(12f, -12f), new Vector2(30f, 30f));
        button.key = MakeText(keyCap.transform, "", 17, TextAlignmentOptions.Center, TextColor);
        Heading(button.key);
        Stretch(button.key.rectTransform, 0f);

        button.name = MakeText(button.root, "", 20, TextAlignmentOptions.MidlineLeft, TextColor);
        Heading(button.name);
        button.name.textWrappingMode = TextWrappingModes.NoWrap;
        button.name.overflowMode = TextOverflowModes.Ellipsis;
        PlaceTop(button.name.rectTransform, new Vector2(52f, -12f), new Vector2(190f, 30f));

        button.damage = MakeText(button.root, "", 16, TextAlignmentOptions.MidlineRight, TextColor);
        Heading(button.damage);
        button.damage.textWrappingMode = TextWrappingModes.NoWrap;
        PlaceTop(button.damage.rectTransform, new Vector2(240f, -12f), new Vector2(118f, 30f));

        button.tags = MakeText(button.root, "", 14, TextAlignmentOptions.TopLeft, DimColor);
        button.tags.overflowMode = TextOverflowModes.Ellipsis;
        PlaceTop(button.tags.rectTransform, new Vector2(14f, -48f), new Vector2(344f, 40f));
        return button;
    }

    void BuildEndTurn()
    {
        endTurnFrame = MakeFramedPanel(canvasRoot, "End Turn");
        endTurnPanel = endTurnFrame.rectTransform;
        endTurnPanel.anchorMin = endTurnPanel.anchorMax = endTurnPanel.pivot = new Vector2(1f, 0f);
        endTurnPanel.anchoredPosition = new Vector2(-24f, 24f);
        endTurnPanel.sizeDelta = new Vector2(270f, 76f);

        Button click = endTurnPanel.gameObject.AddComponent<Button>();
        click.transition = Selectable.Transition.None;
        click.navigation = new Navigation { mode = Navigation.Mode.None };
        endTurnPanel.gameObject.AddComponent<UISound>().clickCue = ""; // Hover sound only; TurnManager plays the end turn sound
        click.onClick.AddListener(() =>
        {
            TurnManager.Instance?.EndTurn();
            EventSystem.current?.SetSelectedGameObject(null);
        });

        Image keyCap = MakePanel(endTurnPanel, KeyCapColor, "Space Key");
        Place(keyCap.rectTransform, new Vector2(14f, 18f), new Vector2(92f, 40f));
        TextMeshProUGUI keyText = MakeText(keyCap.transform, "SPACE", 15, TextAlignmentOptions.Center, TextColor);
        Heading(keyText);
        Stretch(keyText.rectTransform, 0f);

        endTurnText = MakeText(endTurnPanel, "END TURN", 24, TextAlignmentOptions.MidlineLeft, TextColor);
        Heading(endTurnText);
        endTurnText.characterSpacing = 2;
        Place(endTurnText.rectTransform, new Vector2(118f, 0f), new Vector2(150f, 76f));
    }

    // Faint STRATAFIELD logo at the top middle, in the free space above the board, behind every panel
    void BuildWatermark()
    {
        Sprite logo = GameArt.Logo;
        if (logo == null || !showWatermark) return;
        watermark = MakeImage(canvasRoot, new Color(1f, 1f, 1f, watermarkAlpha), "Logo Watermark");
        watermark.sprite = logo;
        watermark.preserveAspect = true;
        RectTransform rect = watermark.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -16f);
        rect.sizeDelta = new Vector2(watermarkWidth, watermarkWidth * logo.rect.height / logo.rect.width);
    }

    void BuildBanner()
    {
        Image band = MakeImage(canvasRoot, new Color(0.137f, 0.184f, 0.208f, 0.9f), "Banner");
        banner = band.rectTransform;
        banner.anchorMin = new Vector2(0f, 0.5f);
        banner.anchorMax = new Vector2(1f, 0.5f);
        banner.sizeDelta = new Vector2(0f, 160f);
        bannerGroup = band.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;

        // The logo, above the band, for the match-start banner
        Sprite logo = GameArt.Logo;
        if (logo != null)
        {
            bannerLogo = MakeImage(banner, Color.white, "Logo");
            bannerLogo.sprite = logo;
            bannerLogo.preserveAspect = true;
            bannerLogo.rectTransform.sizeDelta = new Vector2(760f, 760f * logo.rect.height / logo.rect.width);
            bannerLogo.gameObject.SetActive(false);
        }

        // Ink lines with a gold line inside, above and below the band (like the board's rim)
        for (int side = -1; side <= 1; side += 2)
        {
            float anchor = side < 0 ? 0f : 1f;
            Image ink = MakeImage(banner, InkArt.Ink, "Edge Ink");
            ink.rectTransform.anchorMin = new Vector2(0f, anchor);
            ink.rectTransform.anchorMax = new Vector2(1f, anchor);
            ink.rectTransform.sizeDelta = new Vector2(0f, 10f);
            Image gold = MakeImage(banner, InkArt.Sage, "Edge Line");
            gold.rectTransform.anchorMin = new Vector2(0f, anchor);
            gold.rectTransform.anchorMax = new Vector2(1f, anchor);
            gold.rectTransform.sizeDelta = new Vector2(0f, 3f);
            gold.rectTransform.anchoredPosition = new Vector2(0f, -side * 7f);
        }

        bannerTitle = MakeText(banner, "", 80, TextAlignmentOptions.Center, TextColor);
        Heading(bannerTitle);
        bannerTitle.characterSpacing = 4;
        bannerTitle.outlineWidth = 0.28f;
        bannerTitle.rectTransform.sizeDelta = new Vector2(1400f, 100f);

        bannerSub = MakeText(banner, "", 26, TextAlignmentOptions.Center, DimColor);
        bannerSub.characterSpacing = 1;
        bannerSub.rectTransform.sizeDelta = new Vector2(1000f, 40f);
        banner.gameObject.SetActive(false);
    }

    // ---- Helpers ----

    Color PlayerColor(int player) => player == 2 ? player2Color : player == 1 ? player1Color : panelColor;

    void SetChip(Image chip, TextMeshProUGUI text, string label, bool available)
    {
        chip.color = available ? HealColor : new Color(0.4f, 0.4f, 0.48f);
        text.text = label;
        text.color = available ? TextColor : DimColor;
    }

    // Range, area and effects in one short line, e.g. "Range 3 · 3x3 · Knockback · Burn"
    static string ShortTags(AttackData attack, CardInstance card)
    {
        List<string> tags = new List<string>();
        if (attack.area == AttackArea.Self) tags.Add("Self");
        else tags.Add($"Range {CombatResolver.GetRange(card, attack)}");
        int width = attack.areaSize * 2 + 1;
        if (attack.area == AttackArea.AroundTarget) tags.Add($"{width}x{width} area");
        if (attack.area == AttackArea.AroundSelf) tags.Add($"Around self {attack.areaSize}");
        if (attack.hasKnockback && attack.area == AttackArea.Single) tags.Add("Knockback");
        if (attack.ignoresDefense) tags.Add("Ignores defense");
        if (attack.executeAtOrBelowHP > 0) tags.Add("Execute");
        if (attack.targetStatusEffects != null)
            foreach (StatusEffectApplication effect in attack.targetStatusEffects) tags.Add(CardText.Name(effect.type));
        if (attack.selfStatusEffects != null)
            foreach (StatusEffectApplication effect in attack.selfStatusEffects) tags.Add("Self " + CardText.Name(effect.type));
        if (attack.bonuses != null && attack.bonuses.Length > 0) tags.Add("Bonus");
        return string.Join("  ·  ", tags);
    }

    static string Colored(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    // Labels and numbers: Audiowide (the logo-style font), white with an ink outline (thinner on small text). Shrinks long names to fit
    static void Heading(TextMeshProUGUI text)
    {
        GameFonts.UseTitle(text, text.fontSize >= 22f ? 0.22f : 0.16f);
        text.enableAutoSizing = true;
        text.fontSizeMax = text.fontSize;
        text.fontSizeMin = Mathf.Min(12f, text.fontSize);
    }

    // Part of the card art showing the character (the art window near the top), at the given width/height shape
    public static Rect PortraitUV(Sprite art, float aspect)
    {
        Rect rect = art.textureRect;
        Texture texture = art.texture;
        float cropW = 0.62f, top = 0.06f;
        float cropH = Mathf.Min(1f - top, cropW * (rect.width / rect.height) / Mathf.Max(0.01f, aspect));
        float x = 0.5f - cropW / 2f, y = 1f - top - cropH;
        return new Rect((rect.x + rect.width * x) / texture.width, (rect.y + rect.height * y) / texture.height,
                        rect.width * cropW / texture.width, rect.height * cropH / texture.height);
    }

    static void SetFill(Image fill, float percent)
    {
        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(percent), 1f);
    }

    static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    // Chip, button or small frame (InkUI): the colour fills a rounded shape with a thick ink outline
    Image MakePanel(Transform parent, Color color, string name) => InkUI.Chip(parent, name, color);

    // Big panel (InkUI): navy fill (the returned Image, so its colour can change) under an ink-and-gold rim
    Image MakeFramedPanel(Transform parent, string name) => InkUI.FramedPanel(parent, name, panelColor);

    static Image MakeImage(Transform parent, Color color, string name)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI MakeText(Transform parent, string text, float size, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        GameFonts.UseBody(tmp);
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        return tmp;
    }

    // Positions from the parent's bottom-left corner
    static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // Positions from the parent's top-left corner
    static void PlaceTop(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
