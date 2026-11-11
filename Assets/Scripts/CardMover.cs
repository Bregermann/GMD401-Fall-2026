using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class CardMover : MonoBehaviour
{
    [Header("Snap Settings")]
    [SerializeField] private float snapSpeed = 15f;
    [SerializeField] private float heightOffset = 0.05f;

    [Header("CardData Data")]
    public CardData data;

    private bool isDragging = false;
    private bool isSnapping = false;          
    private Vector3 dragOffset;
    private Camera cam;
    private CardGrid grid;
    private Plane dragPlane;
    private Coroutine snapCoroutine;  
    private Transform currentSlot;
    private Vector3 dragStartPosition;
    private Quaternion dragStartRotation;
    public bool hasMoved = false;
    public bool IsBusy => isDragging || isSnapping; // Being dragged or still snapping

    // Fired when any card is picked up or let go (used to light up legal slots)
    public static event System.Action<CardMover> DragStarted;
    public static event System.Action<CardMover> DragEnded;

    void Start()
    {
        cam = Camera.main;
        grid = FindAnyObjectByType<CardGrid>();

        if (grid == null)
            Debug.LogError("No CardGrid found in the scene!");
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // Start drag
        if (mouse.leftButton.wasPressedThisFrame && !isSnapping && !hasMoved && CanDrag())
        {
            if (IsMouseOverThisCard())
            {
                StartDrag();
            }
        }

        // While dragging
        if (isDragging && mouse.leftButton.isPressed)
        {
            Drag();
        }

        // Release mouse → try to snap
        if (isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            EndDrag();
        }
    }

    // TurnManager decides which cards can move right now. Without one any card can be dragged
    bool CanDrag()
    {
        if (CardInspector.IsOpen) return false; // No dragging while inspecting a card
        if (InputGate.IsPaused || InputGate.PointerOverUI) return false; // Or through the options menu
        if (TurnManager.Instance == null) return true;
        return TurnManager.Instance.CanDrag(GetComponent<CardUnit>());
    }

    bool IsMouseOverThisCard()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            return hit.collider != null && 
                  (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform));
        }
        return false;
    }

    void StartDrag()
    {
        if (snapCoroutine != null)
    {
        StopCoroutine(snapCoroutine);
        snapCoroutine = null;
    }
        currentSlot = transform.parent;
        dragStartPosition = transform.position; // Where an unplaced card goes back to after an illegal drop
        dragStartRotation = transform.rotation;

        isSnapping = false;
        isDragging = true;

        // Unparent immediately so it is free
        transform.SetParent(null);

        // Create horizontal drag plane at current height
        dragPlane = new Plane(Vector3.up, transform.position);

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            dragOffset = transform.position - hitPoint;
        }
        Debug.Log($"{name} started drag. currentSlot = {(currentSlot != null ? currentSlot.name : "NULL")} | data = {(data != null ? data.name : "NULL")}");
        DragStarted?.Invoke(this);
    }

    void Drag()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 newPos = hitPoint + dragOffset;
            newPos.y = transform.position.y;   
            transform.position = newPos;
        }
    }

    void EndDrag()
    {
        isDragging = false;
        DragEnded?.Invoke(this);
        TrySnap();
    }

    // Slots this card can move to right now (only meaningful while it's being dragged)
    public List<Transform> GetLegalSlots()
    {
        if (grid == null || data == null) return new List<Transform>();
        GetMoveRules(out int moveSpaces, out int owner);
        return grid.GetValidSlots(data, moveSpaces, owner, currentSlot);
    }

    // Move spaces with status effects (or the base stat without a CardUnit), and the owner for placement rows (0 = anywhere)
    void GetMoveRules(out int moveSpaces, out int owner)
    {
        CardUnit unit = GetComponent<CardUnit>();
        bool hasInstance = unit != null && unit.Instance != null;
        moveSpaces = hasInstance ? unit.Instance.MoveSpaces : data.moveSpaces;
        owner = hasInstance ? unit.Instance.owner : 0;
    }

    void TrySnap()
{
    if (grid == null || isSnapping || data == null) return;

    GetMoveRules(out int moveSpaces, out int owner);

    // Look for a legal slot near where the player released the mouse
    Transform nearestValid = grid.GetNearestValidSlot(
        transform.position,
        data,
        moveSpaces,
        owner,
        currentSlot,
        1f
    );

    if (nearestValid != null)
    {
        // Player dropped near a legal slot go there
        snapCoroutine = StartCoroutine(SmoothSnap(nearestValid));
    }
    else if (currentSlot != null)
    {
        // No legal slot nearby return to original slot
        Debug.Log($"{name} – illegal drop, returning to original slot");
        snapCoroutine = StartCoroutine(SmoothSnap(currentSlot));
    }
    else
    {
        // Not placed yet (e.g. waiting at the side after the draft), slide back to where it was picked up
        Debug.Log($"{name} – illegal drop, returning to where it was picked up");
        snapCoroutine = StartCoroutine(SlideBack());
    }
}

    // Like SmoothSnap but back to a spot off the board, so nothing changes on the board or the turn
    IEnumerator SlideBack()
    {
        isSnapping = true;

        while (Vector3.Distance(transform.position, dragStartPosition) > 0.01f)
        {
            transform.position = Vector3.Lerp(transform.position, dragStartPosition, Time.deltaTime * snapSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, dragStartRotation, Time.deltaTime * snapSpeed);
            yield return null;
        }

        transform.position = dragStartPosition;
        transform.rotation = dragStartRotation;
        isSnapping = false;
        snapCoroutine = null;
    }
    IEnumerator SmoothSnap(Transform targetSlot)
    {
        isSnapping = true;

        Vector3 targetPos = targetSlot.position + Vector3.up * heightOffset;
        Quaternion targetRot = targetSlot.rotation;

        while (Vector3.Distance(transform.position, targetPos) > 0.01f)
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * snapSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * snapSpeed);
            yield return null;
        }

        // Final lock
        transform.position = targetPos;
        transform.rotation = targetRot;
        transform.SetParent(targetSlot);

        // Tells combat where the card is now
        CardUnit unit = GetComponent<CardUnit>();
        if (unit != null && CombatManager.Instance != null)
            CombatManager.Instance.UpdateCardPosition(unit, targetSlot);

        // Only lock the card if it was ALREADY on a slot and moved to a different one
        if (currentSlot != null && currentSlot != targetSlot)
        {
            hasMoved = true;
            Debug.Log($"{name} has used its move and is now locked");
        }
        else
        {
            Debug.Log($"{name} placed on board for the first time – can still move later");
        }

        isSnapping = false;
        snapCoroutine = null;

        // Lets the turn manager know (next player places, or a move was used)
        if (TurnManager.Instance != null)
            TurnManager.Instance.OnCardSnapped(unit);
    }
    
}