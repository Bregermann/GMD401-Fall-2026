using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GamePhase
{
    Draft,
    Placement,
    Battle,
    GameOver
}

// Runs the match: waits for the draft (DraftManager) or dev mode (DevSetup) to hand over the cards,
// then the placement phase, then card turns by speed until one player has no cards left.
// Put one on the same GameObject as CombatManager.
// Controls for now: drag to move, 1-4 picks an attack (press again to cancel), click a card to attack it, Space ends the turn.
// UI can listen to the events below for popups and animations, e.g. TurnManager.Instance.CardStunned += unit => ShowPopup("STUNNED");
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }
    public OptionsManager optionsManager;
    [Header("Timing")]
    [SerializeField] private float stunnedDelay = 1.5f;    // How long the STUNNED popup shows before the turn ends
    [SerializeField] private float burnDelay = 1f;         // Time for the burn animation
    [SerializeField] private float poisonDelay = 1f;       // Time for the poison animation
    [SerializeField] private float regenerationDelay = 1f; // Time for the regeneration animation
    [SerializeField] private float attackPause = 0.5f;     // Minimum pause after an attack so the hit can play

    // UI hooks for popups and animations. The int is the damage or healing amount
    public event System.Action<CardUnit> CardStunned;
    public event System.Action<CardUnit, int> CardBurned;
    public event System.Action<CardUnit, int> CardPoisoned;
    public event System.Action<CardUnit, int> CardRegenerated;

    // Match flow, for the game log and UI
    public event System.Action<int> PlacementStarted; // Player who places first
    public event System.Action<CardUnit> CardPlaced;
    public event System.Action BattleStarted;
    public event System.Action<int> RoundStarted;     // Round number
    public event System.Action<CardUnit> TurnStarted; // Only for cards that get to act (not stunned or defeated)
    public event System.Action<int> GameEnded;        // Winner

    public GamePhase Phase { get; private set; } = GamePhase.Draft;
    public int PlacingPlayer { get; private set; } = 1;
    public CardUnit ActiveUnit { get; private set; } // Card taking its turn
    public int Round { get; private set; }
    public static int Winner { get; private set; }          // 0 until the game ends
    public bool IsBusy => isBusy;                    // A popup or animation is playing
    public bool FixedTurnOrder { get; set; }         // Dev only: turns go in card list order instead of by speed

    private readonly List<CardUnit> units = new List<CardUnit>();
    private readonly HashSet<CardInstance> actedThisRound = new HashSet<CardInstance>();
    private readonly Dictionary<CardInstance, float> coinFlips = new Dictionary<CardInstance, float>();

    private AttackData selectedAttack;
    private bool hasAttacked;
    private bool isBusy;          // True while a popup or animation plays, blocks input
    private int attackFrame = -1; // Stops the click that attacked from also starting a drag

    private BoardState Board => CombatManager.Instance.Board;

    void Awake()
    {
        Instance = this;
    }

    // Waits a frame so every CardUnit has made its CardInstance
    IEnumerator Start()
    {
        yield return null;

        if (CombatManager.Instance == null)
        {
            Debug.LogError("TurnManager needs a CombatManager in the scene");
            enabled = false;
            yield break;
        }

        // The draft hands the cards over with StartPlacement when it's done
        if (DraftManager.Instance != null) yield break;

        // Dev mode spawns its own cards and calls StartBattle
        if (DevSetup.Instance != null) yield break;

        // No draft in the scene (testing), so use the cards already in the scene
        StartPlacement(new List<CardUnit>(FindObjectsByType<CardUnit>()), 1);
    }

    // Called by the draft when it's done, or by Start when there's no draft
    public void StartPlacement(List<CardUnit> cards, int firstPlayer)
    {
        SetUnits(cards);

        Phase = GamePhase.Placement;
        PlacingPlayer = firstPlayer;
        if (!HasUnplacedCards(PlacingPlayer)) PlacingPlayer = BoardState.OtherPlayer(PlacingPlayer);
        if (!HasUnplacedCards(PlacingPlayer))
        {
            Debug.LogWarning("No cards to place");
            return;
        }
        Debug.Log($"Placement phase. Player {PlacingPlayer} places first");
        PlacementStarted?.Invoke(PlacingPlayer);
    }

    // Turns keyboard and mouse input into commands
    void Update()
    {
        if (!CanAct || CardInspector.IsOpen || InputGate.IsPaused) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) SelectAttack(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectAttack(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SelectAttack(2);
            if (keyboard.digit4Key.wasPressedThisFrame) SelectAttack(3);
            if (keyboard.spaceKey.wasPressedThisFrame && EndTurn()) return;
        }

        Mouse mouse = Mouse.current;
        if (selectedAttack != null && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            CardUnit target = CardUnit.UnderMouse();
            if (target != null) UseAttack(target);
        }
    }

    // ---- Commands (player input now, a computer opponent could use these too) ----

    // The active card is waiting for its player: move, attack or end turn
    public bool CanAct => Phase == GamePhase.Battle && ActiveUnit != null && !isBusy;
    public AttackData SelectedAttack => selectedAttack;

    // Ends the active card's turn. False if it can't right now (e.g. mid-drag)
    public bool EndTurn()
    {
        if (!CanAct) return false;

        CardMover mover = ActiveUnit.GetComponent<CardMover>();
        if (mover != null && mover.IsBusy) return false; // Can't end the turn mid-drag

        StartCoroutine(EndCardTurn());
        return true;
    }

    // ---- Called by CardMover ----

    // Whether this card can be dragged right now
    public bool CanDrag(CardUnit unit)
    {
        if (unit == null || unit.Instance == null) return false;

        // Nothing can be dragged during the draft (falls through to false)
        if (Phase == GamePhase.Placement)
            return units.Contains(unit) && unit.Instance.owner == PlacingPlayer && !IsPlaced(unit);

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
            CardPlaced?.Invoke(unit);
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
        int other = BoardState.OtherPlayer(PlacingPlayer);
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
        BattleStarted?.Invoke();
        StartRound();
        NextTurn();
    }

    // Skips placement, for cards already on the board (used by dev mode)
    public void StartBattle(List<CardUnit> cards)
    {
        SetUnits(cards);
        StartBattle();
    }

    // The cards in this match, skipping any without card data
    void SetUnits(List<CardUnit> cards)
    {
        units.Clear();
        units.AddRange(cards);
        units.RemoveAll(u => u == null || u.Instance == null);
    }

    // Dev tools changed HP outside of an attack. Clears dead cards, checks for a winner,
    // and moves on if the card taking its turn died
    public void DevRefresh()
    {
        CombatManager.Instance.RemoveDefeatedCards();
        if (Phase != GamePhase.Battle || CheckForWinner()) return;

        if (ActiveUnit != null && !ActiveUnit.Instance.IsAlive)
        {
            ActiveUnit = null;
            selectedAttack = null;
            NextTurn();
        }
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
        RoundStarted?.Invoke(Round);
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

    // TurnOrder rules, or list order in dev mode
    bool GoesBefore(CardInstance a, CardInstance b)
    {
        if (FixedTurnOrder)
            return units.FindIndex(u => u.Instance == a) < units.FindIndex(u => u.Instance == b);

        coinFlips.TryGetValue(a, out float aFlip);
        coinFlips.TryGetValue(b, out float bFlip);
        return TurnOrder.GoesBefore(a, b, aFlip, bFlip);
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
        // Regeneration only plays if it actually heals (not at full HP or with HealBlock)
        int regenAmount = Mathf.Min(card.statusEffects.GetAmount(StatusEffectType.Regeneration), card.data.maxHP - card.currentHP);
        bool regenerating = regenAmount > 0 && !card.statusEffects.Has(StatusEffectType.HealBlock);
        bool burning = card.statusEffects.Has(StatusEffectType.Burn);
        int burnAmount = card.statusEffects.GetAmount(StatusEffectType.Burn);
        card.StartTurn();

        if (regenerating)
        {
            CardRegenerated?.Invoke(unit, regenAmount);
            yield return new WaitForSeconds(regenerationDelay);
        }
        if (burning)
        {
            CardBurned?.Invoke(unit, burnAmount);
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
        TurnStarted?.Invoke(unit);
    }

    IEnumerator EndCardTurn()
    {
        isBusy = true;
        CardInstance card = ActiveUnit.Instance;
        if (!hasAttacked) card.SkipAttack(); // Skipping clears the cooldown

        bool poisoned = card.statusEffects.Has(StatusEffectType.Poison);
        int poisonAmount = card.statusEffects.GetAmount(StatusEffectType.Poison);
        card.EndTurn();

        if (poisoned)
        {
            CardPoisoned?.Invoke(ActiveUnit, poisonAmount);
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
        GameEnded?.Invoke(Winner);

        // Finds it if the slot is empty. Scenes without one (TestScene) just skip the win panel
        if (optionsManager == null) optionsManager = FindAnyObjectByType<OptionsManager>();
        if (optionsManager != null) optionsManager.ShowWinPanel(Winner);
        return true;
    }

    // ---- Attacking ----

    // Picks attack 0-3 for the active card. Picking the selected one again cancels it. False if it can't be used
    public bool SelectAttack(int index)
    {
        if (!CanAct || index < 0 || index > 3) return false;
        if (hasAttacked)
        {
            Debug.Log("Already attacked this turn");
            return false;
        }

        CardInstance card = ActiveUnit.Instance;
        AttackData attack = card.GetAttacks()[index];
        if (attack == null)
        {
            Debug.Log($"Attack {index + 1} isn't assigned");
            return false;
        }
        if (!card.CanUseAttack(attack))
        {
            Debug.Log($"{attack.attackName} is on cooldown");
            return false;
        }

        selectedAttack = selectedAttack == attack ? null : attack;
        // AroundSelf and Self attacks are used by clicking the card itself
        string howToUse = CombatResolver.UsedOnSelf(attack) ? $"click {card.data.cardName} to use it" : "click a target";
        Debug.Log(selectedAttack != null ? $"{attack.attackName} selected, {howToUse}" : "Attack cancelled");
        return true;
    }

    // Uses the selected attack on a card. False if nothing is selected or the attack isn't allowed
    public bool UseAttack(CardUnit target)
    {
        if (!CanAct || selectedAttack == null || target == null || target.Instance == null) return false;

        AttackData attack = selectedAttack;
        CardInstance targetCard = target.Instance;
        string targetName = targetCard.data.cardName;

        AttackResult result = CombatManager.Instance.Attack(ActiveUnit, attack, target);
        if (result == null)
        {
            Debug.Log($"Can't use {attack.attackName} on {targetName}. Check team, range and line of sight");
            return false;
        }

        hasAttacked = true;
        selectedAttack = null;
        attackFrame = Time.frameCount;
        if (attack.area == AttackArea.Single)
            Debug.Log($"{attack.attackName} on {targetName}: damage {result.damageDealt}, healed {result.amountHealed}, " +
                      $"HP now {targetCard.currentHP}, knocked back: {result.wasKnockedBack}, defeated: {result.defeatedCards.Count}");
        else
            Debug.Log($"{attack.attackName} hit {result.hits.Count} card(s): {HitsText(result)}, defeated: {result.defeatedCards.Count}");

        if (result.reflectedDamage > 0)
            Debug.Log($"{ActiveUnit.Instance.data.cardName} took {result.reflectedDamage} damage back");
        if (result.knockbackBlocked)
            Debug.Log($"{targetName} couldn't be knocked back");

        if (!CheckForWinner()) StartCoroutine(WaitForAttackEffects(ActiveUnit.Instance));
        return true;
    }

    // Blocks input until the hit has played (at least attackPause, longer if a projectile is still flying).
    // Takes the attacker's CardInstance up front, because its GameObject is destroyed if a passive like Hangry kills it
    IEnumerator WaitForAttackEffects(CardInstance attacker)
    {
        isBusy = true;
        float started = Time.time;
        yield return new WaitForSeconds(attackPause);
        while (CombatEffects.IsPlaying) yield return null;

        // Attacker was defeated during its own attack: let the defeat play out, then the turn ends
        if (!attacker.IsAlive)
        {
            Debug.Log($"{attacker.data.cardName} was defeated by a passive");
            float defeatLeft = CombatManager.Instance.DefeatDelay - (Time.time - started);
            if (defeatLeft > 0f) yield return new WaitForSeconds(defeatLeft);

            ActiveUnit = null;
            isBusy = false;
            NextTurn();
            yield break;
        }

        isBusy = false;
        if (ActiveUnit != null && HasMoved(ActiveUnit)) Debug.Log("Moved and attacked. Press Space to end turn");
    }

    // ---- Helpers ----

    // Each card an area attack hit and what happened to it, e.g. "Mage 1 -320, Defender 2 -150"
    string HitsText(AttackResult result)
    {
        if (result.hits.Count == 0) return "nobody in range";

        List<string> parts = new List<string>();
        foreach (AttackHit hit in result.hits)
        {
            string change = hit.executed ? " executed" : hit.damage > 0 ? $" -{hit.damage}" : hit.healed > 0 ? $" +{hit.healed}" : "";
            parts.Add(hit.card.data.cardName + change);
        }
        return string.Join(", ", parts);
    }

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
}
