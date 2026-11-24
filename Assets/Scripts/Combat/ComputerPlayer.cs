using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// The computer opponent: drafts, places and battles.
// ComputerDrafter decides draft picks and placement, ComputerPlanner decides battle turns; this carries them out
// through the same commands a person uses (DraftManager.SelectCard / ConfirmSelection, CardMover.MoveTo,
// TurnManager.SelectAttack / UseAttack / EndTurn), so every game rule applies to it too, with short pauses so
// people can follow along.
//
// Testing: press F2 to switch between Off -> Player 2 is the computer -> Auto-battle (computer plays both sides
// from draft to the end, restarts the scene after each game and keeps a win count).
// The choice is remembered. CombatManager adds this automatically
public class ComputerPlayer : MonoBehaviour
{
    public enum Mode { Off, Player2, Both }

    [Header("Pacing (seconds)")]
    [SerializeField] private float thinkTime = 1.2f;      // Pause before acting, so people can follow
    [SerializeField] private float showAttackTime = 0.9f; // How long the chosen attack stays selected before it's used
    [SerializeField] private float afterActionTime = 0.6f;  // Pause after moving or attacking

    [Header("Auto-battle")]
    [SerializeField] private float restartDelay = 4f;     // After a game ends in auto-battle, reload the scene

    // ---- Who's in control ----

    const string ModeKey = "Stratafield.ComputerMode";
    static Mode? mode;

    public static Mode CurrentMode
    {
        get { if (mode == null) mode = (Mode)PlayerPrefs.GetInt(ModeKey, 0); return mode.Value; }
        set { mode = value; PlayerPrefs.SetInt(ModeKey, (int)value); }
    }

    // True if the computer plays this player's cards
    public static bool Controls(int player)
    {
        Mode current = CurrentMode;
        return current == Mode.Both || (current == Mode.Player2 && player == 2);
    }

    static int gamesPlayed;
    static readonly int[] wins = new int[3];

    private bool playing, restarting;
    private CardGrid grid;
    private GUIStyle labelStyle;

    void Start() => grid = FindAnyObjectByType<CardGrid>();

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
        {
            CurrentMode = (Mode)(((int)CurrentMode + 1) % 3);
            Debug.Log($"[Computer] Mode: {CurrentMode}");
        }

        TurnManager turns = TurnManager.Instance;
        if (turns == null) return;

        if (turns.Phase == GamePhase.GameOver && CurrentMode == Mode.Both && !restarting) StartCoroutine(RestartAfterGame());
        if (playing) return;

        DraftManager draft = DraftManager.Instance;
        if (turns.Phase == GamePhase.Draft && draft != null && draft.CanPick && Controls(draft.PickingPlayer))
            StartCoroutine(PickCard(draft));
        else if (turns.CanPlace && Controls(turns.PlacingPlayer))
            StartCoroutine(PlaceCard(turns.PlacingPlayer));
        else if (turns.CanAct && turns.ActiveUnit != null && Controls(turns.ActiveUnit.Instance.owner))
            StartCoroutine(PlayTurn(turns.ActiveUnit));
    }

    // ---- Draft and placement ----

    IEnumerator PickCard(DraftManager draft)
    {
        playing = true;
        int player = draft.PickingPlayer;
        yield return new WaitForSeconds(thinkTime);

        if (draft.CanPick && draft.PickingPlayer == player && draft.Available.Count > 0)
        {
            List<CardData> pool = new List<CardData>(), mine = new List<CardData>();
            foreach (CardUnit unit in draft.Available) pool.Add(draft.DataOf(unit));
            foreach (CardUnit unit in draft.PickedBy(player)) mine.Add(unit.Instance != null ? unit.Instance.data : draft.DataOf(unit));

            int choice = ComputerDrafter.ChoosePick(pool, mine);
            CardUnit card = draft.Available[choice];
            Debug.Log($"[Computer] P{player} drafts {pool[choice].cardName} ({pool[choice].characterClass})");
            if (draft.SelectCard(card))
            {
                yield return new WaitForSeconds(showAttackTime); // Let people see which card it chose
                draft.ConfirmSelection();
                yield return WaitUntilOrTimeout(() => !draft.CanPick || draft.PickingPlayer != player, 5f);
            }
        }
        playing = false;
    }

    IEnumerator PlaceCard(int player)
    {
        playing = true;
        TurnManager turns = TurnManager.Instance;
        yield return new WaitForSeconds(thinkTime * 0.8f);

        List<CardUnit> unplaced = turns.UnplacedCards(player);
        if (turns.CanPlace && turns.PlacingPlayer == player && unplaced.Count > 0 && grid != null)
        {
            List<CardData> datas = unplaced.ConvertAll(u => u.Instance.data);
            CardUnit card = unplaced[ComputerDrafter.ChooseCardToPlace(datas)];
            CardMover mover = card.GetComponent<CardMover>();
            List<Vector2Int> squares = MoveOptions(mover);

            if (mover != null && squares.Count > 0)
            {
                Vector2Int square = ComputerDrafter.ChooseSquare(card.Instance.data, player, CombatManager.Instance.Board, squares);
                Debug.Log($"[Computer] P{player} places {card.Instance.data.cardName} on {square.x},{square.y}");
                if (mover.MoveTo(grid.GetSlot(square.x, square.y)))
                    yield return WaitUntilOrTimeout(() => !mover.IsBusy, 3f);
                yield return new WaitForSeconds(afterActionTime);
            }
        }
        playing = false;
    }

    // ---- Taking a turn ----

    IEnumerator PlayTurn(CardUnit unit)
    {
        playing = true;
        TurnManager turns = TurnManager.Instance;
        CardMover mover = unit.GetComponent<CardMover>();
        float startedAt = Time.time;

        yield return new WaitForSeconds(thinkTime);
        if (!StillTurnOf(unit)) { playing = false; yield break; }

        ComputerPlanner.Plan plan = ComputerPlanner.Choose(unit.Instance, CombatManager.Instance.Board, MoveOptions(mover));
        Debug.Log($"[Computer] P{unit.Instance.owner} {unit.Instance.data.cardName}: {plan.Describe(unit.Instance)}");

        // Move first (unless it's attacking from where it stands)
        if (plan.moveTo.HasValue && mover != null && grid != null && mover.MoveTo(grid.GetSlot(plan.moveTo.Value.x, plan.moveTo.Value.y)))
        {
            yield return WaitUntilOrTimeout(() => !mover.IsBusy, 3f);
            yield return new WaitForSeconds(afterActionTime);
        }

        // Attack: select it (so the target highlights and damage preview show), pause, then use it
        CardUnit target = plan.target != null ? CombatManager.Instance.GetUnit(plan.target) : null;
        if (plan.attackIndex >= 0 && target != null && StillTurnOf(unit) && turns.CanAct)
        {
            if (turns.SelectedAttack != null) turns.SelectAttack(SelectedIndex(unit)); // Clear any old selection
            if (turns.SelectAttack(plan.attackIndex))
            {
                yield return new WaitForSeconds(showAttackTime);
                bool used = StillTurnOf(unit) && target != null && turns.UseAttack(target);
                if (!used && StillTurnOf(unit) && turns.SelectedAttack != null) turns.SelectAttack(plan.attackIndex); // Cancel
                yield return WaitUntilOrTimeout(() => !StillTurnOf(unit) || turns.CanAct, 10f);
            }

            // Attacked without moving: step somewhere safer if that's clearly better
            if (StillTurnOf(unit) && turns.CanAct && mover != null && !mover.hasMoved && grid != null)
            {
                Vector2Int? retreat = ComputerPlanner.BestRetreat(unit.Instance, CombatManager.Instance.Board, MoveOptions(mover));
                if (retreat.HasValue && mover.MoveTo(grid.GetSlot(retreat.Value.x, retreat.Value.y)))
                    yield return WaitUntilOrTimeout(() => !mover.IsBusy, 3f);
            }
        }

        yield return new WaitForSeconds(afterActionTime);
        yield return WaitUntilOrTimeout(() => !StillTurnOf(unit) || turns.CanAct, Mathf.Max(1f, 15f - (Time.time - startedAt)));
        if (StillTurnOf(unit) && turns.CanAct) turns.EndTurn();
        playing = false;
    }

    // Squares (row, column) the card could move to right now
    List<Vector2Int> MoveOptions(CardMover mover)
    {
        List<Vector2Int> options = new List<Vector2Int>();
        if (mover == null || grid == null) return options;
        foreach (Transform slot in mover.GetMoveOptions())
            if (grid.TryGetCoordinates(slot, out int row, out int column)) options.Add(new Vector2Int(row, column));
        return options;
    }

    static bool StillTurnOf(CardUnit unit) =>
        unit != null && TurnManager.Instance != null && TurnManager.Instance.Phase == GamePhase.Battle && TurnManager.Instance.ActiveUnit == unit;

    static int SelectedIndex(CardUnit unit)
    {
        AttackData[] attacks = unit.Instance.GetAttacks();
        for (int i = 0; i < attacks.Length; i++) if (attacks[i] == TurnManager.Instance.SelectedAttack) return i;
        return 0;
    }

    static IEnumerator WaitUntilOrTimeout(System.Func<bool> done, float timeout)
    {
        float until = Time.time + timeout;
        while (!done() && Time.time < until) yield return null;
    }

    // ---- Auto-battle ----

    IEnumerator RestartAfterGame()
    {
        restarting = true;
        gamesPlayed++;
        if (TurnManager.Winner >= 1 && TurnManager.Winner <= 2) wins[TurnManager.Winner]++;
        Debug.Log($"[Computer] Auto-battle game {gamesPlayed}: Player {TurnManager.Winner} won. Total: P1 {wins[1]}, P2 {wins[2]}");
        yield return new WaitForSecondsRealtime(restartDelay);
        if (CurrentMode == Mode.Both) SceneManager.LoadScene(SceneManager.GetActiveScene().name); // Same as DevSetup's R restart
        restarting = false;
    }

    // Small label so it's obvious the computer is on (F2 to change)
    void OnGUI()
    {
        if (CurrentMode == Mode.Off) return;
        if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        string text = CurrentMode == Mode.Both ? $"AUTO-BATTLE (F2)   games {gamesPlayed}   P1 {wins[1]} : {wins[2]} P2" : "PLAYER 2 = COMPUTER (F2)";
        GUI.Label(new Rect(Screen.width / 2f - 180f, Screen.height - 26f, 360f, 22f), text, labelStyle);
    }
}
