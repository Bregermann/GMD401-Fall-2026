using UnityEngine;
using UnityEngine.InputSystem;

// TEMPORARY TEST - delete once there's real attack input or a turn manager.
// Places two cards on the board, then:
//   1-4 = attacker uses that attack on the target
//   T   = both cards take a turn (start + end), to test Burn/Poison/Regen/Stun and effect countdowns
//   L   = log both cards' HP, position and status effects
public class CombatKeyTest : MonoBehaviour
{
    [SerializeField] private CardUnit attacker;
    [SerializeField] private CardUnit target;

    [Header("Starting Slots")]
    [SerializeField] private int attackerRow = 3;
    [SerializeField] private int attackerColumn = 3;
    [SerializeField] private int targetRow = 4;
    [SerializeField] private int targetColumn = 3;

    private bool placed;

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.digit1Key.wasPressedThisFrame) UseAttack(1);
        if (keyboard.digit2Key.wasPressedThisFrame) UseAttack(2);
        if (keyboard.digit3Key.wasPressedThisFrame) UseAttack(3);
        if (keyboard.digit4Key.wasPressedThisFrame) UseAttack(4);
        if (keyboard.tKey.wasPressedThisFrame) TakeTurns();
        if (keyboard.lKey.wasPressedThisFrame) LogCards();
    }

    // Checks everything is set up, and places the cards the first time (the grid is built by then)
    bool Ready()
    {
        if (CombatManager.Instance == null) { Debug.LogError("No CombatManager in the scene"); return false; }
        if (attacker == null || target == null) { Debug.LogWarning("Attacker or target is missing (or was defeated)"); return false; }
        if (attacker.Instance == null || target.Instance == null) { Debug.LogWarning("A card has no CardInstance - check CardMover has CardData"); return false; }

        if (!placed)
        {
            placed = true;
            Place(attacker, attackerRow, attackerColumn);
            Place(target, targetRow, targetColumn);
        }
        return true;
    }

    void Place(CardUnit unit, int row, int column)
    {
        if (CombatManager.Instance.UpdateCardPosition(unit, row, column))
            CombatManager.Instance.SnapToSlot(unit);
    }

    void UseAttack(int number)
    {
        if (!Ready()) return;

        AttackData attack = attacker.Instance.GetAttacks()[number - 1];
        CardInstance targetCard = target.Instance;
        int hpBefore = targetCard.currentHP;

        AttackResult result = CombatManager.Instance.Attack(attacker, attack, target);
        if (result == null)
        {
            Debug.LogWarning($"Attack {number} not allowed. Check: attack is assigned, both cards have HP, " +
                             "owners are different (or same for heals), in range/line of sight, not on cooldown, attacker not stunned");
            return;
        }

        Debug.Log($"{attack.attackName}: damage {result.damageDealt}, healed {result.amountHealed}, " +
                  $"target HP {hpBefore} -> {targetCard.currentHP}, knocked back: {result.wasKnockedBack}, " +
                  $"collision damage: {result.collisionDamage}, defeated: {result.defeatedCards.Count}");
    }

    void TakeTurns()
    {
        if (!Ready()) return;

        foreach (CardUnit unit in new[] { attacker, target })
        {
            if (unit == null) continue;
            CardInstance card = unit.Instance;
            card.StartTurn();
            if (!card.IsAlive) continue; // Died at turn start, skip the rest of the turn
            if (card.IsStunned) Debug.Log($"{card.data.cardName} is stunned and skips this turn");
            card.EndTurn();
        }

        foreach (CardInstance dead in CombatManager.Instance.RemoveDefeatedCards())
            Debug.Log($"{dead.data.cardName} was defeated by a status effect");

        LogCards();
    }

    void LogCards()
    {
        LogCard("Attacker", attacker);
        LogCard("Target", target);
    }

    void LogCard(string label, CardUnit unit)
    {
        if (unit == null || unit.Instance == null) { Debug.Log($"{label}: gone"); return; }

        CardInstance card = unit.Instance;
        string effects = "";
        foreach (StatusEffectInstance effect in card.statusEffects.ActiveEffects)
            effects += $"{effect.type}({effect.turnsRemaining}) ";

        Debug.Log($"{label} {card.data.cardName}: HP {card.currentHP}/{card.data.maxHP}, " +
                  $"slot {card.row}, {card.column}, owner {card.owner}, cooldown: {(card.lastAttackUsed != null ? card.lastAttackUsed.attackName : "none")}, " +
                  $"effects: {effects}");
    }
}
