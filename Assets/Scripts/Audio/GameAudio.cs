using UnityEngine;

// Game-flow sounds: draft picks, placing cards, turn and round starts, banners, the winner, and hovering cards.
// Attack, status and defeat sounds are timed in CombatEffects; the shuffle in DraftManager; the inspector in
// CardInspector. CombatManager adds this automatically
public class GameAudio : MonoBehaviour
{
    private CardUnit lastHovered;
    private DraftManager draft;

    void Start()
    {
        SoundManager.EnsureExists();
        GameSfx.StartAmbience(); // Quiet space room tone under the whole match

        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.PlacementStarted += OnBanner;
            TurnManager.Instance.CardPlaced += OnPlaced;
            TurnManager.Instance.BattleStarted += OnBattleStarted;
            TurnManager.Instance.RoundStarted += OnRoundStarted;
            TurnManager.Instance.TurnStarted += OnTurnStarted;
            TurnManager.Instance.GameEnded += OnGameEnded;
        }

        draft = FindAnyObjectByType<DraftManager>();
        if (draft != null)
        {
            draft.DraftStarted += OnBanner;
            draft.CardSelected += OnSelected;
            draft.CardDrafted += OnDrafted;
        }
    }

    void OnDestroy()
    {
        GameSfx.StopAmbience();
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.PlacementStarted -= OnBanner;
            TurnManager.Instance.CardPlaced -= OnPlaced;
            TurnManager.Instance.BattleStarted -= OnBattleStarted;
            TurnManager.Instance.RoundStarted -= OnRoundStarted;
            TurnManager.Instance.TurnStarted -= OnTurnStarted;
            TurnManager.Instance.GameEnded -= OnGameEnded;
        }
        if (draft != null)
        {
            draft.DraftStarted -= OnBanner;
            draft.CardSelected -= OnSelected;
            draft.CardDrafted -= OnDrafted;
        }
    }

    // Soft tick when the mouse moves onto a different card
    void Update()
    {
        CardUnit hovered = CardHover.UnderMouse();
        if (hovered != null && hovered != lastHovered && !CardInspector.IsOpen) GameSfx.Play("card.hover", 1f, GameSfx.PanAt(hovered.transform.position));
        lastHovered = hovered;
    }

    void OnBanner(int player) => GameSfx.Play("banner");
    void OnBattleStarted() => GameSfx.PlayFirst(1f, "banner.battle", "banner");
    void OnRoundStarted(int round) { if (round > 1) GameSfx.Play("banner"); }
    void OnTurnStarted(CardUnit unit) => GameSfx.Play("turn.start");
    void OnPlaced(CardUnit unit) => GameSfx.Play("card.place", 1f, Pan(unit));
    void OnSelected(CardUnit unit) => GameSfx.Play("card.pick", 1f, Pan(unit));
    void OnDrafted(CardUnit unit, int player) => GameSfx.Play("card.place", 1f, Pan(unit));
    float Pan(CardUnit unit) => unit != null ? GameSfx.PanAt(unit.transform.position) : 0f;
    void OnGameEnded(int winner) => GameSfx.Play("victory");
}
