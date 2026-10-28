using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GamePhase
{
    Placement,
    Battle,
    GameOver
}

// Runs the match: placement phase, then card turns by speed until one player has no cards left.
// Put one on the same GameObject as CombatManager.
// Controls for now: drag to move, 1-4 picks an attack (press again to cancel), click a card to attack it, Space ends the turn.
// UI can listen to the events below for popups and animations, e.g. TurnManager.Instance.CardStunned += unit => ShowPopup("STUNNED");
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Timing")]
    [SerializeField] private float stunnedDelay = 1.5f;    // How long the STUNNED popup shows before the turn ends
    [SerializeField] private float burnDelay = 1f;         // Time for the burn animation
    [SerializeField] private float poisonDelay = 1f;       // Time for the poison animation
    [SerializeField] private float regenerationDelay = 1f; // Time for the regeneration animation

    // UI hooks for popups and animations
    public event System.Action<CardUnit> CardStunned;
    public event System.Action<CardUnit> CardBurned;
    public event System.Action<CardUnit> CardPoisoned;
    public event System.Action<CardUnit> CardRegenerated;

    public GamePhase Phase { get; private set; } = GamePhase.Placement;
    public int PlacingPlayer { get; private set; } = 1;
    public CardUnit ActiveUnit { get; private set; } // Card taking its turn
    public int Round { get; private set; }
    public int Winner { get; private set; }          // 0 until the game ends

    private readonly List<CardUnit> units = new List<CardUnit>();
    private readonly HashSet<CardInstance> actedThisRound = new HashSet<CardInstance>();
    private readonly Dictionary<CardInstance, float> coinFlips = new Dictionary<CardInstance, float>();

    private AttackData selectedAttack;
    private bool hasAttacked;
    private bool isBusy;          // True while a popup or animation plays, blocks input
    private int attackFrame = -1; // Stops the click that attacked from also starting a drag
    private Camera cam;

    private BoardState Board => CombatManager.Instance.Board;

    void Awake()
    {
        Instance = this;
    }

    // Waits a frame so every CardUnit has made its CardInstance
    IEnumerator Start()
    {
        cam = Camera.main;
        yield return null;

        if (CombatManager.Instance == null)
        {
            Debug.LogError("TurnManager needs a CombatManager in the scene");
            enabled = false;
            yield break;
        }

        // For now uses the cards already in the scene. The draft will hand these over later.
        units.AddRange(FindObjectsByType<CardUnit>(FindObjectsSortMode.None));
        units.RemoveAll(u => u.Instance == null);

        if (!HasUnplacedCards(1)) PlacingPlayer = 2;
        if (!HasUnplacedCards(PlacingPlayer))
        {
            Debug.LogWarning("No cards to place");
            yield break;
        }
        Debug.Log($"Placement phase. Player {PlacingPlayer} places first");
    }

    void Update()
    {
        if (Phase != GamePhase.Battle || ActiveUnit == null || isBusy) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) SelectAttack(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectAttack(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SelectAttack(2);
            if (keyboard.digit4Key.wasPressedThisFrame) SelectAttack(3);

            // Can't end the turn mid-drag
            CardMover mover = ActiveUnit.GetComponent<CardMover>();
            if (keyboard.spaceKey.wasPressedThisFrame && (mover == null || !mover.IsBusy))
            {
                StartCoroutine(EndCardTurn());
                return;
            }
        }

        Mouse mouse = Mouse.current;
        if (selectedAttack != null && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            CardUnit target = GetClickedCard();
            if (target != null) UseAttack(target);
        }
    }

    // ---- Called by CardMover ----

    // Whether this card can be dragged right now
    public bool CanDrag(CardUnit unit)
    {
        if (unit == null || unit.Instance == null) return false;

        if (Phase == GamePhase.Placement)
            return unit.Instance.owner == PlacingPlayer && !IsPlaced(unit);

        if (Phase == GamePhase.Battle)
            return unit == ActiveUnit && !isBusy && selectedAttack == null && Time.frameCount != attackFrame;

        return false;
    }

    // Called after a card snaps to a slot
    public void OnCardSnapped(CardUnit unit)
    {
        if (unit == null) return;

        if (Phase == GamePhase.Placement && IsPlaced(unit))
        {
            Debug.Log($"Player {unit.Instance.owner} placed {unit.Instance.data.cardName}");
            NextPlacingPlayer();
        }
        else if (Phase == GamePhase.Battle && unit == ActiveUnit && hasAttacked && HasMoved(unit))
        {
            Debug.Log("Moved and attacked. Press Space to end turn");
        }
    }

    // ---- Placement ----

    // Zones (rows 0-1 and 6-7) are checked in CardGrid
    bool IsPlaced(CardUnit unit) => Board.IsOnBoard(unit.Instance);

    bool HasUnplacedCards(int owner)
    {
        return units.Exists(u => u != null && u.Instance.owner == owner && !IsPlaced(u));
    }

    // Players take turns placing. If one runs out, the other keeps going
    void NextPlacingPlayer()
    {
        int other = PlacingPlayer == 1 ? 2 : 1;
        if (HasUnplacedCards(other))
        {
            PlacingPlayer = other;
        }
        else if (!HasUnplacedCards(PlacingPlayer))
        {
            StartBattle();
            return;
        }
        Debug.Log($"Player {PlacingPlayer}'s turn to place");
    }

    // ---- Battle ----

    void StartBattle()
    {
        Phase = GamePhase.Battle;
        Debug.Log("All cards placed. Battle starts");
        StartRound();
        NextTurn();
    }

    // A round is every card taking one turn
    void StartRound()
    {
        Round++;
        actedThisRound.Clear();
        coinFlips.Clear();
        foreach (CardUnit unit in LivingUnits())
            coinFlips[unit.Instance] = Random.value; // Coin flip for full ties
        Debug.Log($"Round {Round}");
    }

    void NextTurn()
    {
        if (CheckForWinner()) return;

        CardUnit next = PickNextCard();
        if (next == null)
        {
            StartRound(); // Everyone has gone
            next = PickNextCard();
        }
        if (next != null) StartCoroutine(StartCardTurn(next));
    }

    // Fastest card that hasn't gone yet. Checked fresh every turn so speed changes reshuffle the order
    CardUnit PickNextCard()
    {
        CardUnit best = null;
        foreach (CardUnit unit in LivingUnits())
        {
            if (actedThisRound.Contains(unit.Instance)) continue;
            if (best == null || GoesBefore(unit.Instance, best.Instance)) best = unit;
        }
        return best;
    }

    // Speed, then class priority, then the coin flip
    bool GoesBefore(CardInstance a, CardInstance b)
    {
        if (a.Speed != b.Speed) return a.Speed > b.Speed;

        int aPriority = a.data.characterClass.TurnPriority();
        int bPriority = b.data.characterClass.TurnPriority();
        if (aPriority != bPriority) return aPriority < bPriority;

        coinFlips.TryGetValue(a, out float aFlip);
        coinFlips.TryGetValue(b, out float bFlip);
        return aFlip > bFlip;
    }

    IEnumerator StartCardTurn(CardUnit unit)
    {
        isBusy = true;
        ActiveUnit = unit;
        actedThisRound.Add(unit.Instance);
        selectedAttack = null;
        hasAttacked = false;

        CardMover mover = unit.GetComponent<CardMover>();
        if (mover != null) mover.hasMoved = false; // Gets its move back

        // Regeneration then Burn both happen in StartTurn, so the animations play after the HP has changed
        CardInstance card = unit.Instance;
        bool regenerating = card.statusEffects.Has(StatusEffectType.Regeneration) && !card.statusEffects.Has(StatusEffectType.HealBlock);
        bool burning = card.statusEffects.Has(StatusEffectType.Burn);
        card.StartTurn();

        if (regenerating)
        {
            CardRegenerated?.Invoke(unit);
            yield return new WaitForSeconds(regenerationDelay);
        }
        if (burning)
        {
            CardBurned?.Invoke(unit);
            yield return new WaitForSeconds(burnDelay);
        }
        yield return RemoveDefeated();

        if (!card.IsAlive)
        {
            Debug.Log($"{card.data.cardName} was defeated at the start of its turn");
            ActiveUnit = null;
            isBusy = false;
            NextTurn();
            yield break;
        }

        if (card.IsStunned)
        {
            Debug.Log($"{card.data.cardName} is stunned and loses its turn");
            CardStunned?.Invoke(unit);
            yield return new WaitForSeconds(stunnedDelay);
            yield return EndCardTurn();
            yield break;
        }

        isBusy = false;
        Debug.Log($"Player {card.owner}: {card.data.cardName}'s turn (speed {card.Speed}, HP {card.currentHP}/{card.data.maxHP}). " +
                  "Drag to move, 1-4 to pick an attack, Space to end turn");
    }

    IEnumerator EndCardTurn()
    {
        isBusy = true;
        CardInstance card = ActiveUnit.Instance;
        if (!hasAttacked) card.SkipAttack(); // Skipping clears the cooldown

        bool poisoned = card.statusEffects.Has(StatusEffectType.Poison);
        card.EndTurn();

        if (poisoned)
        {
            CardPoisoned?.Invoke(ActiveUnit);
            yield return new WaitForSeconds(poisonDelay);
        }
        yield return RemoveDefeated();
        if (!card.IsAlive) Debug.Log($"{card.data.cardName} was defeated at the end of its turn");

        ActiveUnit = null;
        isBusy = false;
        NextTurn();
    }

    // Removes dead cards and waits for the CARD DEFEATED popup if any died
    IEnumerator RemoveDefeated()
    {
        if (CombatManager.Instance.RemoveDefeatedCards().Count > 0)
            yield return new WaitForSeconds(CombatManager.Instance.DefeatDelay);
    }

    // A player loses when all their cards are defeated
    bool CheckForWinner()
    {
        bool player1Alive = Board.GetCards(1).Count > 0;
        bool player2Alive = Board.GetCards(2).Count > 0;
        if (player1Alive && player2Alive) return false;

        Phase = GamePhase.GameOver;
        ActiveUnit = null;
        Winner = player1Alive ? 1 : 2;
        Debug.Log($"Player {Winner} wins!");
        return true;
    }

    // ---- Attacking ----

    void SelectAttack(int index)
    {
        if (hasAttacked)
        {
            Debug.Log("Already attacked this turn");
            return;
        }

        CardInstance card = ActiveUnit.Instance;
        AttackData attack = card.GetAttacks()[index];
        if (attack == null)
        {
            Debug.Log($"Attack {index + 1} isn't assigned");
            return;
        }
        if (!card.CanUseAttack(attack))
        {
            Debug.Log($"{attack.attackName} is on cooldown");
            return;
        }

        // Pressing the same attack again cancels it
        selectedAttack = selectedAttack == attack ? null : attack;
        Debug.Log(selectedAttack != null ? $"{attack.attackName} selected, click a target" : "Attack cancelled");
    }

    void UseAttack(CardUnit target)
    {
        AttackData attack = selectedAttack;
        CardInstance targetCard = target.Instance;
        string targetName = targetCard.data.cardName;

        AttackResult result = CombatManager.Instance.Attack(ActiveUnit, attack, target);
        if (result == null)
        {
            Debug.Log($"Can't use {attack.attackName} on {targetName}. Check team, range and line of sight");
            return;
        }

        hasAttacked = true;
        selectedAttack = null;
        attackFrame = Time.frameCount;
        Debug.Log($"{attack.attackName} on {targetName}: damage {result.damageDealt}, healed {result.amountHealed}, " +
                  $"HP now {targetCard.currentHP}, knocked back: {result.wasKnockedBack}, defeated: {result.defeatedCards.Count}");

        if (CheckForWinner()) return;
        if (HasMoved(ActiveUnit)) Debug.Log("Moved and attacked. Press Space to end turn");
    }

    // ---- Helpers ----

    // Cards still on the board. Defeated cards are taken off the board straight away
    List<CardUnit> LivingUnits()
    {
        return units.FindAll(u => u != null && Board.IsOnBoard(u.Instance) && u.Instance.IsAlive);
    }

    bool HasMoved(CardUnit unit)
    {
        CardMover mover = unit.GetComponent<CardMover>();
        return mover != null && mover.hasMoved;
    }

    CardUnit GetClickedCard()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            return hit.collider.GetComponentInParent<CardUnit>();
        return null;
    }
}
