using System.Collections.Generic;
using UnityEngine;

// Connects the combat system to the scene. Put one on an empty GameObject in the scene.
// Holds the BoardState and updates card GameObjects after attacks.
// Other scripts can reach it with CombatManager.Instance
public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    [SerializeField] private float cardHeightOffset = 0.05f; // Matches CardMover's heightOffset
    [SerializeField] private float defeatDelay = 1.5f;       // How long a defeated card stays for the CARD DEFEATED popup
    public float DefeatDelay => defeatDelay;
    public float CardHeightOffset => cardHeightOffset;       // How far cards sit above their slot

    // UI hook for the CARD DEFEATED popup. The card is already off the board when this fires
    public event System.Action<CardUnit> CardDefeated;

    // Hook for attack visuals: attacker, attack, target, result. Fires before defeated cards are removed
    public event System.Action<CardUnit, AttackData, CardUnit, AttackResult> AttackResolved;

    public BoardState Board { get; private set; } = new BoardState();

    private CardGrid grid;
    private readonly Dictionary<CardInstance, CardUnit> units = new Dictionary<CardInstance, CardUnit>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        grid = FindAnyObjectByType<CardGrid>();
        if (grid == null)
            Debug.LogError("No CardGrid found in the scene!");
    }

    // ---- Keeping the board in sync ----

    // Call when a card is placed or moved on the grid so combat knows where it is.
    // Returns false if the slot is taken or off the board.
    public bool UpdateCardPosition(CardUnit unit, int row, int column)
    {
        if (unit == null || unit.Instance == null) return false;

        CardInstance card = unit.Instance;
        units[card] = unit;

        if (Board.GetCard(row, column) == card) return true; // Already there

        bool placed = Board.Place(card, row, column);
        if (!placed)
            Debug.LogWarning($"{unit.name} couldn't be placed at {row}, {column} - slot taken or off the board");
        return placed;
    }

    // Same as above but takes the slot itself (named "Slot_row_col" by CardGrid).
    // For CardMover after a card snaps: CombatManager.Instance.UpdateCardPosition(GetComponent<CardUnit>(), targetSlot);
    public bool UpdateCardPosition(CardUnit unit, Transform slot)
    {
        if (!TryGetSlotPosition(slot, out int row, out int column))
        {
            Debug.LogWarning($"{(slot != null ? slot.name : "null")} isn't a grid slot");
            return false;
        }
        return UpdateCardPosition(unit, row, column);
    }

    // Reads the row/column from a slot's name, e.g. "Slot_3_5" -> row 3, column 5
    public static bool TryGetSlotPosition(Transform slot, out int row, out int column)
    {
        row = -1;
        column = -1;
        if (slot == null) return false;

        string[] parts = slot.name.Split('_');
        return parts.Length == 3 && parts[0] == "Slot"
            && int.TryParse(parts[1], out row) && int.TryParse(parts[2], out column);
    }

    // ---- Combat ----

    // Runs an attack and updates the scene. Returns null if the attack isn't allowed.
    public AttackResult Attack(CardUnit attacker, AttackData attack, CardUnit target)
    {
        if (attacker == null || target == null) return null;

        AttackResult result = CombatResolver.ResolveAttack(attacker.Instance, attack, target.Instance, Board);
        if (result == null) return null;

        if (result.wasKnockedBack)
            SnapToSlot(target);

        AttackResolved?.Invoke(attacker, attack, target, result);

        foreach (CardInstance dead in result.defeatedCards)
            DestroyCard(dead);

        return result;
    }

    // Removes any card that died outside of an attack (Burn, Poison).
    // The turn manager should call this after StartTurn() and EndTurn().
    public List<CardInstance> RemoveDefeatedCards()
    {
        List<CardInstance> defeated = new List<CardInstance>();
        foreach (CardInstance card in Board.GetAllCards())
        {
            if (card.IsAlive) continue;
            Board.Remove(card);
            defeated.Add(card);
        }

        foreach (CardInstance dead in defeated)
            DestroyCard(dead);

        return defeated;
    }

    // ---- Scene ----

    // The GameObject side of a card on the board. Null if it isn't known
    public CardUnit GetUnit(CardInstance card)
    {
        return card != null && units.TryGetValue(card, out CardUnit unit) ? unit : null;
    }

    // Moves a card's GameObject onto the slot matching its CardInstance row/column
    public void SnapToSlot(CardUnit unit)
    {
        if (grid == null || unit == null || unit.Instance == null) return;

        Transform slot = grid.GetSlot(unit.Instance.row, unit.Instance.column);
        if (slot == null) return;

        unit.transform.SetParent(slot);
        unit.transform.position = slot.position + Vector3.up * cardHeightOffset;
        unit.transform.rotation = slot.rotation;
    }

    private void DestroyCard(CardInstance card)
    {
        if (!units.TryGetValue(card, out CardUnit unit)) return;

        units.Remove(card);
        if (unit == null) return;

        unit.transform.SetParent(null); // Frees the slot straight away
        CardDefeated?.Invoke(unit);
        Destroy(unit.gameObject, defeatDelay);
    }
}
