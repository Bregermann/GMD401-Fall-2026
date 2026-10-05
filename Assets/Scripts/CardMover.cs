using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using NUnit.Framework;

public class CardMover : MonoBehaviour
{
    [Header("Snap Settings")]
    [SerializeField] float snapDistance = 1.5f;
    [SerializeField] private float snapSpeed = 15f;
    [SerializeField] private float heightOffset = 0.05f;

    private bool isDragging = false;
    private bool isSnapping = false;          // prevents conflicts
    private Vector3 dragOffset;
    private Camera cam;
    private CardGrid grid;
    private Plane dragPlane;
    private Coroutine snapCoroutine;          // so we can stop it

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
        if (mouse.leftButton.wasPressedThisFrame && !isSnapping)
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
    }

    void Drag()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 newPos = hitPoint + dragOffset;
            newPos.y = transform.position.y;   // keep original height
            transform.position = newPos;
        }
    }

    void EndDrag()
    {
        isDragging = false;
        TrySnap();
    }

    void TrySnap()
{
    if (grid == null || isSnapping) return;

    Transform nearest = grid.GetNearestEmptySlot(transform.position, 999f);

    if (nearest != null)
    {
        snapCoroutine = StartCoroutine(SmoothSnap(nearest));
    }
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

        isSnapping = false;
        snapCoroutine = null;
    }
}