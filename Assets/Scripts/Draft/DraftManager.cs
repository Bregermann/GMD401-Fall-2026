using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Runs the draft at the start of the match. Put one in the game scene with the CardPool and card prefab assigned.
// A random pool is shuffled in the middle of the board and dealt out to the slots (Space skips), then a coin flip decides who picks first.
// Players take turns: click a card to select it (it gets bigger), Space to confirm. Picked cards slide to that player's side.
// When both players have their cards, they go to TurnManager for placement. Whoever picked second places first.
public class DraftManager : MonoBehaviour
{
    public static DraftManager Instance { get; private set; }

    [Header("Cards")]
    [SerializeField] private CardPool cardPool;
    [SerializeField] private GameObject cardPrefab;

    [Header("Rules")]
    [SerializeField] private int poolSize = 20;
    [SerializeField] private int picksPerPlayer = 6;
    [SerializeField] private int minPerClass = 2; // So both players can get one of each class

    [Header("Layout")]
    [SerializeField] private int firstRow = 2;          // Draft cards fill rows 2-5...
    [SerializeField] private int firstColumn = 1;       // ...in columns 1-5
    [SerializeField] private int cardsPerRow = 5;
    [SerializeField] private float sideOffset = 1f;      // How far off the board picked cards wait
    [SerializeField] private int firstSideColumn = 1;    // Picked cards line up starting at this column

    [Header("Feel")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float slideSpeed = 10f;

    [Header("Opening Animation")]
    [SerializeField] private bool playOpeningAnimation = true;   // Off = cards appear on their slots straight away
    [SerializeField] private bool showBacksWhileShuffling = false; // Turn on once the card prefab has a card back
    [SerializeField] private int shuffleCount = 3;
    [SerializeField] private float gatherTime = 0.4f;            // Cards sliding into a pile
    [SerializeField] private float splitTime = 0.2f;             // Deck splitting into two halves
    [SerializeField] private float splitDistance = 0.9f;         // How far apart the halves go
    [SerializeField] private float riffleInterval = 0.025f;      // Time between cards dropping back in
    [SerializeField] private float cardMoveTime = 0.12f;         // One card moving during the shuffle
    [SerializeField] private float dealInterval = 0.06f;         // Time between cards being dealt
    [SerializeField] private float dealFlightTime = 0.35f;
    [SerializeField] private float dealArcHeight = 1.5f;         // How high cards lift toward the camera while flying
    [SerializeField] private float pileStep = 0.004f;            // Height between cards in the deck so they don't flicker

    // UI hooks for popups and animations
    public event System.Action<CardUnit> CardSelected;
    public event System.Action<CardUnit, int> CardDrafted; // Card and the player who picked it
    public event System.Action DraftFinished;
    public event System.Action<int> DraftStarted; // Player who won the coin flip and picks first

    public bool IsDrafting { get; private set; }
    public bool IsDealing { get; private set; } // Opening shuffle and deal is playing
    public int PickingPlayer { get; private set; }

    private readonly List<CardUnit> available = new List<CardUnit>();
    private readonly Dictionary<CardUnit, CardData> cardData = new Dictionary<CardUnit, CardData>();
    private readonly Dictionary<int, List<CardUnit>> picked = new Dictionary<int, List<CardUnit>>
    {
        { 1, new List<CardUnit>() },
        { 2, new List<CardUnit>() }
    };

    private CardUnit selected;
    private Vector3 selectedBaseScale;
    private bool isBusy; // True while a picked card slides, blocks input
    private int firstPicker;
    private CardGrid grid;
    private bool skipDeal;   // Space pressed during the opening animation
    private int movingCards; // Cards still moving in the opening animation

    // Same height cards sit at on the board
    private float HeightOffset => CombatManager.Instance != null ? CombatManager.Instance.CardHeightOffset : 0.05f;

    void Awake()
    {
        Instance = this;
    }

    // Waits a frame so CardGrid has built its slots
    IEnumerator Start()
    {
        grid = FindAnyObjectByType<CardGrid>();
        yield return null;

        if (cardPool == null || cardPrefab == null || grid == null)
        {
            Debug.LogError("DraftManager needs a CardPool, a card prefab and a CardGrid in the scene");
            yield break;
        }

        yield return StartDraft();
    }

    void Update()
    {
        // Space skips the opening shuffle and deal
        Keyboard skipKeyboard = Keyboard.current;
        if (IsDealing && !InputGate.IsPaused && skipKeyboard != null && skipKeyboard.spaceKey.wasPressedThisFrame)
            skipDeal = true;

        if (!IsDrafting || isBusy || CardInspector.IsOpen || InputGate.IsPaused) return;

        // Click a card to select it, click it again to deselect
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            CardUnit clicked = CardUnit.UnderMouse();
            if (clicked != null && available.Contains(clicked))
                Select(clicked == selected ? null : clicked);
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame && selected != null)
            StartCoroutine(ConfirmPick());
    }

    // ---- Draft ----

    IEnumerator StartDraft()
    {
        IsDealing = true;

        // Spawn the pool in the middle of the board. Cards belong to nobody until they're picked
        List<CardUnit> cards = new List<CardUnit>();
        List<Transform> slots = new List<Transform>();
        List<CardData> pool = cardPool.GetDraftPool(poolSize, minPerClass);
        for (int i = 0; i < pool.Count; i++)
        {
            Transform slot = grid.GetSlot(firstRow + i / cardsPerRow, firstColumn + i % cardsPerRow);
            if (slot == null)
            {
                Debug.LogWarning($"No slot for draft card {i + 1}, check DraftManager's layout settings");
                continue;
            }

            CardUnit unit = CardUnit.Spawn(cardPrefab, pool[i], 0, DeckCenter(), slot.rotation);
            cardData[unit] = pool[i];
            cards.Add(unit);
            slots.Add(slot);
        }

        // The animation decides which card lands on which slot (top of the deck is dealt first)
        if (playOpeningAnimation && cards.Count > 0)
            yield return OpeningAnimation(cards, slots);

        // Lock every card onto its slot (also finishes the animation if it was skipped)
        for (int i = 0; i < cards.Count; i++)
        {
            Transform card = cards[i].transform;
            card.SetParent(slots[i], true);
            card.position = slots[i].position + Vector3.up * HeightOffset;
            card.rotation = slots[i].rotation;
        }
        available.AddRange(cards);

        IsDealing = false;
        IsDrafting = true;
        firstPicker = Random.value < 0.5f ? 1 : 2;
        PickingPlayer = firstPicker;
        Debug.Log($"Draft started. Coin flip: Player {firstPicker} picks first");
        DraftStarted?.Invoke(firstPicker);
        Debug.Log($"Player {PickingPlayer}'s pick. Click a card, Space to confirm");
    }

    // ---- Opening animation ----

    // Gather into a pile, riffle shuffle a few times, then deal from the top onto the slots.
    // Reorders cards to match the deal, so cards[i] ends up on slots[i]
    IEnumerator OpeningAnimation(List<CardUnit> cards, List<Transform> slots)
    {
        skipDeal = false;
        movingCards = 0;
        Vector3 right = ScreenRight();

        // Start spread out in a loose ring, then slide into a messy pile (index 0 is the bottom)
        List<CardUnit> pile = new List<CardUnit>(cards);
        for (int i = 0; i < pile.Count; i++)
        {
            float angle = i * Mathf.PI * 2f / pile.Count;
            pile[i].transform.position = DeckCenter() + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2f;
            pile[i].transform.rotation = FaceRotation(slots[0].rotation) * Quaternion.Euler(0f, Random.Range(-90f, 90f), 0f);
        }
        for (int i = 0; i < pile.Count; i++)
            StartCoroutine(MoveCard(pile[i].transform, PilePosition(i, Vector3.zero), PileRotation(slots[0].rotation), gatherTime, 0f));
        yield return WaitForCards();

        for (int shuffle = 0; shuffle < shuffleCount && !skipDeal; shuffle++)
        {
            // Split: bottom half slides left, top half slides right
            int half = pile.Count / 2;
            List<CardUnit> left = pile.GetRange(0, half);
            List<CardUnit> rightHalf = pile.GetRange(half, pile.Count - half);
            for (int i = 0; i < left.Count; i++)
                StartCoroutine(MoveCard(left[i].transform, PilePosition(i, -right * splitDistance), PileRotation(slots[0].rotation), splitTime, 0f));
            for (int i = 0; i < rightHalf.Count; i++)
                StartCoroutine(MoveCard(rightHalf[i].transform, PilePosition(i, right * splitDistance), PileRotation(slots[0].rotation), splitTime, 0f));
            yield return WaitForCards();

            // Riffle: cards drop back into the middle from the bottom of either half, randomly
            pile.Clear();
            int l = 0, r = 0;
            while ((l < left.Count || r < rightHalf.Count) && !skipDeal)
            {
                int leftLeft = left.Count - l, rightLeft = rightHalf.Count - r;
                bool fromLeft = Random.Range(0, leftLeft + rightLeft) < leftLeft;
                CardUnit card = fromLeft ? left[l++] : rightHalf[r++];

                StartCoroutine(MoveCard(card.transform, PilePosition(pile.Count, Vector3.zero), PileRotation(slots[0].rotation), cardMoveTime, 0.2f));
                pile.Add(card);
                yield return new WaitForSeconds(riffleInterval);
            }
            if (skipDeal) break;
            yield return WaitForCards();
        }

        // If skipped mid-riffle some cards aren't back in the pile yet
        foreach (CardUnit card in cards)
            if (!pile.Contains(card)) pile.Add(card);

        // Deal from the top of the pile onto the slots in order
        List<CardUnit> dealt = new List<CardUnit>();
        for (int i = 0; i < slots.Count; i++)
        {
            CardUnit card = pile[pile.Count - 1 - i];
            dealt.Add(card);
            if (skipDeal) continue;

            Vector3 target = slots[i].position + Vector3.up * HeightOffset;
            StartCoroutine(MoveCard(card.transform, target, slots[i].rotation, dealFlightTime, dealArcHeight, 180f));
            yield return new WaitForSeconds(dealInterval);
        }
        yield return WaitForCards();

        cards.Clear();
        cards.AddRange(dealt);
    }

    // Moves a card with an ease in/out. arc lifts it toward the camera mid-way, spin adds a turn as it lands.
    // Stops straight away if the animation is skipped (StartDraft snaps everything into place)
    IEnumerator MoveCard(Transform card, Vector3 target, Quaternion targetRotation, float time, float arc, float spin = 0f)
    {
        movingCards++;
        Vector3 start = card.position;
        Quaternion startRotation = card.rotation;

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            if (skipDeal || card == null) { movingCards--; yield break; }

            float p = Mathf.SmoothStep(0f, 1f, t / time);
            card.position = Vector3.Lerp(start, target, p) + Vector3.up * arc * 4f * p * (1f - p);
            card.rotation = Quaternion.Slerp(startRotation, targetRotation, p) * Quaternion.Euler(0f, spin * (1f - p), 0f);
            yield return null;
        }

        if (!skipDeal && card != null)
        {
            card.position = target;
            card.rotation = targetRotation;
        }
        movingCards--;
    }

    IEnumerator WaitForCards()
    {
        while (movingCards > 0 && !skipDeal) yield return null;
    }

    Vector3 DeckCenter() => grid.transform.position + Vector3.up * HeightOffset;

    // Spot in a pile: stacked upward so cards don't flicker, with a little mess
    Vector3 PilePosition(int index, Vector3 offset)
    {
        Vector3 jitter = new Vector3(Random.Range(-0.03f, 0.03f), 0f, Random.Range(-0.03f, 0.03f));
        return DeckCenter() + offset + jitter + Vector3.up * (pileStep * (index + 1));
    }

    // Slightly crooked, and face-down if there's a card back
    Quaternion PileRotation(Quaternion slotRotation)
    {
        return FaceRotation(slotRotation) * Quaternion.Euler(0f, Random.Range(-8f, 8f), 0f);
    }

    // Flipped over along the card's length while shuffling, if the prefab has a back
    Quaternion FaceRotation(Quaternion slotRotation)
    {
        return showBacksWhileShuffling ? slotRotation * Quaternion.Euler(0f, 0f, 180f) : slotRotation;
    }

    // Screen right, flattened onto the board
    Vector3 ScreenRight()
    {
        Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        right.y = 0f;
        return right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
    }

    // Selected card is shown bigger. null clears the selection
    void Select(CardUnit unit)
    {
        if (selected != null) selected.transform.localScale = selectedBaseScale;

        selected = unit;
        if (selected == null)
        {
            Debug.Log("Selection cleared");
            return;
        }

        selectedBaseScale = selected.transform.localScale;
        selected.transform.localScale = selectedBaseScale * selectedScale;

        CardData data = cardData[selected];
        Debug.Log($"Player {PickingPlayer} selected {data.cardName} ({data.characterClass}): HP {data.maxHP}, " +
                  $"Phys Def {data.physicalDefense}, Mag Def {data.magicDefense}, Speed {data.speed}, Move {data.moveSpaces}. Space to confirm");
        CardSelected?.Invoke(selected);
    }

    IEnumerator ConfirmPick()
    {
        isBusy = true;
        CardUnit unit = selected;
        CardData data = cardData[unit];
        int player = PickingPlayer;

        unit.transform.localScale = selectedBaseScale;
        selected = null;
        available.Remove(unit);
        picked[player].Add(unit);

        // Now it belongs to the player
        unit.Setup(data, player);
        unit.name = $"{data.cardName} (P{player})";
        Debug.Log($"Player {player} drafted {data.cardName} ({data.characterClass})");
        CardDrafted?.Invoke(unit, player);

        yield return Slide(unit.transform, SidePosition(player, picked[player].Count - 1));

        bool bothDone = picked[1].Count >= picksPerPlayer && picked[2].Count >= picksPerPlayer;
        if (bothDone || available.Count == 0)
        {
            FinishDraft();
            yield break;
        }

        // Alternate, unless the other player already has all their cards
        int other = BoardState.OtherPlayer(player);
        PickingPlayer = picked[other].Count < picksPerPlayer ? other : player;
        isBusy = false;
        Debug.Log($"Player {PickingPlayer}'s pick. Click a card, Space to confirm");
    }

    void FinishDraft()
    {
        IsDrafting = false;
        isBusy = false;

        // Cards nobody picked are removed
        foreach (CardUnit unit in available)
            Destroy(unit.gameObject);
        available.Clear();

        Debug.Log("Draft finished");
        DraftFinished?.Invoke();

        List<CardUnit> drafted = new List<CardUnit>(picked[1]);
        drafted.AddRange(picked[2]);

        // Whoever picked second places first, to balance the coin flip
        int placesFirst = BoardState.OtherPlayer(firstPicker);
        if (TurnManager.Instance != null)
            TurnManager.Instance.StartPlacement(drafted, placesFirst);
        else
            Debug.LogWarning("No TurnManager in the scene, placement won't start");
    }

    // ---- Helpers ----

    // True if the card is still in the draft pool (for the card inspector)
    public bool IsUndrafted(CardUnit unit) => available.Contains(unit);

    // Spot just off the board on the player's side. Player 1 is past row 0, Player 2 past the last row
    Vector3 SidePosition(int player, int index)
    {
        int edgeRow = player == 1 ? 0 : BoardState.Rows - 1;
        int column = Mathf.Clamp(firstSideColumn + index, 0, BoardState.Columns - 1);
        Transform slot = grid.GetSlot(edgeRow, column);

        Vector3 awayFromBoard = grid.transform.forward * (player == 1 ? -sideOffset : sideOffset);
        return slot.position + awayFromBoard + Vector3.up * HeightOffset;
    }

    // Smooth slide like CardMover's snap
    IEnumerator Slide(Transform card, Vector3 target)
    {
        card.SetParent(null); // Frees the draft slot
        while (Vector3.Distance(card.position, target) > 0.01f)
        {
            card.position = Vector3.Lerp(card.position, target, Time.deltaTime * slideSpeed);
            yield return null;
        }
        card.position = target;
    }
}
