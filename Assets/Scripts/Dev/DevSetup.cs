using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// One card for dev mode to put on the board
[System.Serializable]
public class DevCard
{
    public CardData data;
    public int owner = 1;
    public int row;
    public int column;
    public int startingHP = 0;                   // 0 = full HP
    public StatusEffectType[] startingEffects;   // e.g. Stagger to test Stun
}

// DEV ONLY - for a test scene, don't put this in GameScene.
// Skips the draft and placement: spawns the cards listed below on their slots and starts the battle straight away.
// Hotkeys (hover a card): K kill, D damage, H heal to full. R restarts the scene.
public class DevSetup : MonoBehaviour
{
    public static DevSetup Instance { get; private set; }

    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private List<DevCard> cards = new List<DevCard>();

    [Header("Options")]
    [SerializeField] private bool ignoreCooldowns = false; // Use the same attack every turn
    [SerializeField] private bool fixedTurnOrder = false;  // Turns go in list order instead of by speed
    [SerializeField] private int damageAmount = 100;       // How much D deals

    void Awake()
    {
        Instance = this;
        CardInstance.IgnoreCooldowns = ignoreCooldowns;
    }

    void OnDestroy()
    {
        CardInstance.IgnoreCooldowns = false; // Don't leak into other scenes
    }

    // Waits a frame so CardGrid and CombatManager are set up
    IEnumerator Start()
    {
        yield return null;

        if (cardPrefab == null || CombatManager.Instance == null || TurnManager.Instance == null)
        {
            Debug.LogError("DevSetup needs a card prefab, a CombatManager and a TurnManager in the scene");
            yield break;
        }

        List<CardUnit> spawned = new List<CardUnit>();
        foreach (DevCard devCard in cards)
        {
            CardUnit unit = SpawnCard(devCard);
            if (unit != null) spawned.Add(unit);
        }

        Debug.Log("Dev mode: hover a card and press K to kill, D to damage, H to heal. R restarts");
        TurnManager.Instance.FixedTurnOrder = fixedTurnOrder;
        TurnManager.Instance.StartBattle(spawned);
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || CardInspector.IsOpen) return;

        if (keyboard.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        // Not while a turn is starting/ending, a popup is waiting, or the game is over
        if (TurnManager.Instance == null || TurnManager.Instance.IsBusy || TurnManager.Instance.Phase != GamePhase.Battle) return;

        if (keyboard.kKey.wasPressedThisFrame) ChangeHovered(card => card.TakeDamage(card.currentHP), "killed");
        if (keyboard.dKey.wasPressedThisFrame) ChangeHovered(card => card.TakeDamage(damageAmount), $"took {damageAmount} damage");
        if (keyboard.hKey.wasPressedThisFrame) ChangeHovered(card => card.currentHP = card.data.maxHP, "healed to full");
    }

    // Puts one listed card on its slot with its starting HP and effects
    CardUnit SpawnCard(DevCard devCard)
    {
        if (devCard.data == null)
        {
            Debug.LogWarning("Dev card with no CardData, skipped");
            return null;
        }

        CardUnit unit = CardUnit.Spawn(cardPrefab, devCard.data, devCard.owner, Vector3.zero, Quaternion.identity);
        unit.name = $"{devCard.data.cardName} (P{devCard.owner})";

        if (!CombatManager.Instance.UpdateCardPosition(unit, devCard.row, devCard.column))
        {
            Debug.LogWarning($"{unit.name} couldn't go on {devCard.row}, {devCard.column} (taken or off the board), skipped");
            Destroy(unit.gameObject);
            return null;
        }
        CombatManager.Instance.SnapToSlot(unit);

        CardInstance instance = unit.Instance;
        if (devCard.startingHP > 0) instance.currentHP = Mathf.Min(devCard.startingHP, devCard.data.maxHP);
        if (devCard.startingEffects != null)
            foreach (StatusEffectType effect in devCard.startingEffects)
                instance.ApplyStatusEffect(effect);

        return unit;
    }

    // Runs a change on the card under the mouse, then lets the turn manager react (defeats, game over)
    void ChangeHovered(System.Action<CardInstance> change, string whatHappened)
    {
        CardUnit unit = CardUnit.UnderMouse();
        if (unit == null || unit.Instance == null || !CombatManager.Instance.Board.IsOnBoard(unit.Instance)) return;

        change(unit.Instance);
        Debug.Log($"[Dev] {unit.Instance.data.cardName} {whatHappened} (HP {unit.Instance.currentHP}/{unit.Instance.data.maxHP})");
        TurnManager.Instance.DevRefresh();
    }
}
