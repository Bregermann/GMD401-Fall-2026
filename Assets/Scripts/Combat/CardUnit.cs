using UnityEngine;
using UnityEngine.InputSystem;

// Connects a card GameObject to its CardInstance (the card's in-match state).
// Goes on the card prefab next to CardMover.
public class CardUnit : MonoBehaviour
{
    [SerializeField] private int owner = 1; // Player 1 or 2. Only used for cards placed by hand in the scene

    public CardInstance Instance { get; private set; }
    public bool HasOwner { get; private set; } // False for cards still in the draft pool

    private bool ownerPending; // Spawned for the draft, gets its owner when picked

    // Spawns a card from the prefab with its data, adding a CardUnit if the prefab doesn't have one.
    // owner 0 = belongs to nobody yet (draft pool), call Setup when it's picked
    public static CardUnit Spawn(GameObject prefab, CardData data, int owner, Vector3 position, Quaternion rotation)
    {
        GameObject card = Instantiate(prefab, position, rotation);
        card.name = data.cardName;

        CardMover mover = card.GetComponent<CardMover>();
        if (mover != null) mover.data = data;

        CardUnit unit = card.GetComponent<CardUnit>();
        if (unit == null) unit = card.AddComponent<CardUnit>();

        unit.ApplyArt(data);

        if (owner > 0) unit.Setup(data, owner);
        else unit.ownerPending = true;
        return unit;
    }

    // The card under the mouse, or null. Also null while paused or when the mouse is over UI
    public static CardUnit UnderMouse()
    {
        Camera cam = Camera.main;
        Mouse mouse = Mouse.current;
        if (cam == null || mouse == null || InputGate.IsPaused || InputGate.PointerOverUI) return null;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        return Physics.Raycast(ray, out RaycastHit hit, 100f) ? hit.collider.GetComponentInParent<CardUnit>() : null;
    }

    // Called by whatever spawns the card (e.g. after the draft) to set its data and owner.
    // Use like: Instantiate(cardPrefab).GetComponent<CardUnit>().Setup(cardData, 2);
    public void Setup(CardData data, int owner)
    {
        CardMover mover = GetComponent<CardMover>();
        if (mover != null) mover.data = data;
        ApplyArt(data);
        // Row/column are set when the card is placed on the board
        Instance = new CardInstance(data, owner, -1, -1);
        HasOwner = true;
        ownerPending = false;
    }

    // Cards placed by hand in the scene use the Inspector owner and CardMover's data
    void Start()
    {
        if (Instance != null) return; // Already set up by Setup()

        CardMover mover = GetComponent<CardMover>();
        if (mover == null || mover.data == null)
        {
            Debug.LogError($"{name} needs a CardMover with CardData assigned, or a Setup() call");
            return;
        }
        ApplyArt(mover.data);
        Instance = new CardInstance(mover.data, owner, -1, -1);
        HasOwner = !ownerPending; // Draft pool cards get stats for the inspector, but no owner yet
    }
    void ApplyArt(CardData data)
{
    CardView view = GetComponent<CardView>();
    if (view != null) view.Setup(data);
}
}
