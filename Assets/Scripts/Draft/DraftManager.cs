using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Runs the draft at the start of the match. Put one in the game scene with the CardPool and card prefab assigned.
// A random pool is laid out on the board and a coin flip decides who picks first.
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

    // UI hooks for popups and animations
    public event System.Action<CardUnit> CardSelected;
    public event System.Action<CardUnit, int> CardDrafted; // Card and the player who picked it
    public event System.Action DraftFinished;

    public bool IsDrafting { get; private set; }
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

        StartDraft();
    }

    void Update()
    {
        if (!IsDrafting || isBusy || CardInspector.IsOpen) return;

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

    void StartDraft()
    {
        IsDrafting = true;

        List<CardData> pool = cardPool.GetDraftPool(poolSize, minPerClass);
        for (int i = 0; i < pool.Count; i++)
        {
            Transform slot = grid.GetSlot(firstRow + i / cardsPerRow, firstColumn + i % cardsPerRow);
            if (slot == null)
            {
                Debug.LogWarning($"No slot for draft card {i + 1}, check DraftManager's layout settings");
                continue;
            }
            available.Add(SpawnCard(pool[i], slot));
        }

        firstPicker = Random.value < 0.5f ? 1 : 2;
        PickingPlayer = firstPicker;
        Debug.Log($"Draft started. Coin flip: Player {firstPicker} picks first");
        Debug.Log($"Player {PickingPlayer}'s pick. Click a card, Space to confirm");
    }

    // Spawns a card on a slot. It belongs to nobody until it's picked
    CardUnit SpawnCard(CardData data, Transform slot)
    {
        CardUnit unit = CardUnit.Spawn(cardPrefab, data, 0, slot.position + Vector3.up * HeightOffset, slot.rotation);
        unit.transform.SetParent(slot);
        cardData[unit] = data;
        return unit;
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
