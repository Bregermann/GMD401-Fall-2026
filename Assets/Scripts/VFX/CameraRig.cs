using UnityEngine;
using UnityEngine.InputSystem;

// Tabletop camera: tilts the main camera so you look across the board like a board game on a table,
// with a narrow lens so the far side doesn't stretch. Frames the board automatically, keeping the room
// at the screen sides for the HUD. Then sways gently toward the mouse so the board feels like a real object.
// Runs before CombatEffects so screen shake still goes on top. CombatManager adds it automatically
[DefaultExecutionOrder(-50)]
public class CameraRig : MonoBehaviour
{
    [SerializeField] private bool tiltCamera = true;              // Off = keep the camera exactly as the scene has it
    [SerializeField, Range(35f, 90f)] private float pitch = 56f;  // Angle above the board. 90 = straight down
    [SerializeField, Range(15f, 60f)] private float fieldOfView = 30f; // Lower = flatter, less stretched
    [SerializeField] private float edgeMargin = 0.03f;            // Gap above and below the board on screen (0-0.2)
    [SerializeField] private float sideRoom = 1.2f;               // Extra board space at each end for cards waiting after the draft
    [SerializeField] private float swayAngle = 1.5f;              // Degrees the camera drifts toward the mouse
    [SerializeField] private float swaySpeed = 2.5f;

    public static bool HasRestingRotation { get; private set; }
    public static Quaternion RestingRotation { get; private set; } // Without sway, for things drawn flat on the board

    private Camera cam;
    private CardGrid grid;
    private bool framed;
    private Vector3 restPosition, pivot;
    private Quaternion restRotation;
    private Vector2 sway;

    void Start()
    {
        cam = Camera.main;
        grid = FindAnyObjectByType<CardGrid>();
    }

    void OnDestroy()
    {
        HasRestingRotation = false;
    }

    void LateUpdate()
    {
        if (cam == null || grid == null) return;

        // The grid makes its slots in its own Start, which has always run by now
        if (!framed)
        {
            if (grid.GetSlot(0, 0) == null) return;
            Frame();
        }

        Sway();
    }

    // ---- Framing ----

    void Frame()
    {
        framed = true;
        Vector3[] corners = BoardCorners(out Vector3 centre);
        pivot = centre;

        if (tiltCamera)
        {
            // Keep the board as wide on screen as it was, so the HUD at the sides still has its room
            Vector2 widthOnScreen = ViewportRangeX(corners);
            widthOnScreen.x = Mathf.Clamp(widthOnScreen.x, 0.02f, 0.45f);
            widthOnScreen.y = Mathf.Clamp(widthOnScreen.y, 0.55f, 0.98f);

            Vector3 boardUp = Flat(cam.transform.up, Vector3.forward);
            float radians = pitch * Mathf.Deg2Rad;
            Vector3 look = (boardUp * Mathf.Cos(radians) + Vector3.down * Mathf.Sin(radians)).normalized;
            cam.fieldOfView = fieldOfView;
            cam.transform.rotation = Quaternion.LookRotation(look, boardUp);

            // Back off until the board fits, then centre it up and down. Twice, since centring changes the fit a little
            Vector3 aim = centre;
            for (int pass = 0; pass < 3; pass++)
            {
                float distance = FitDistance(corners, aim, look, widthOnScreen);
                cam.transform.position = aim - look * distance;
                Vector2 heightOnScreen = ViewportRangeY(corners);
                float off = (heightOnScreen.x + heightOnScreen.y) * 0.5f - 0.5f;
                float viewHeight = 2f * distance * Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
                aim += boardUp * off * viewHeight / Mathf.Sin(radians);
            }
            cam.transform.position = aim - look * FitDistance(corners, aim, look, widthOnScreen);
        }

        restPosition = cam.transform.position;
        restRotation = cam.transform.rotation;
        RestingRotation = restRotation;
        HasRestingRotation = true;

        SpaceBackground background = FindAnyObjectByType<SpaceBackground>();
        if (background != null) background.Refit();
    }

    // The closest the camera can be (looking along look at aim) with every corner inside the margins
    float FitDistance(Vector3[] corners, Vector3 aim, Vector3 look, Vector2 widthOnScreen)
    {
        float near = 0.5f, far = 300f;
        for (int i = 0; i < 40; i++)
        {
            float middle = (near + far) * 0.5f;
            cam.transform.position = aim - look * middle;
            if (Fits(corners, widthOnScreen)) far = middle;
            else near = middle;
        }
        return far;
    }

    bool Fits(Vector3[] corners, Vector2 widthOnScreen)
    {
        foreach (Vector3 corner in corners)
        {
            Vector3 point = cam.WorldToViewportPoint(corner);
            if (point.z <= 0f) return false;
            if (point.x < widthOnScreen.x || point.x > widthOnScreen.y) return false;
            if (point.y < edgeMargin || point.y > 1f - edgeMargin) return false;
        }
        return true;
    }

    // Corners of a box around every slot, the waiting spots at both ends, and room for lifted cards above
    Vector3[] BoardCorners(out Vector3 centre)
    {
        Bounds area = new Bounds(grid.GetSlot(0, 0).position, Vector3.zero);
        for (int row = 0; grid.GetSlot(row, 0) != null; row++)
            for (int col = 0; grid.GetSlot(row, col) != null; col++)
            {
                Transform slot = grid.GetSlot(row, col);
                Renderer cube = slot.GetComponent<Renderer>();
                area.Encapsulate(cube != null ? cube.bounds : new Bounds(slot.position, Vector3.one));
            }

        // Picked cards wait just past the first and last rows
        Vector3 along = grid.transform.forward;
        area.Encapsulate(area.center + along * (area.extents.z + sideRoom));
        area.Encapsulate(area.center - along * (area.extents.z + sideRoom));
        area.Encapsulate(area.center + Vector3.up * 0.6f);
        area.Encapsulate(area.center - Vector3.up * 0.4f);
        centre = area.center;

        Vector3 min = area.min, max = area.max;
        return new[]
        {
            new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z)
        };
    }

    Vector2 ViewportRangeX(Vector3[] corners)
    {
        Vector2 range = new Vector2(1f, 0f);
        foreach (Vector3 corner in corners)
        {
            float x = cam.WorldToViewportPoint(corner).x;
            range = new Vector2(Mathf.Min(range.x, x), Mathf.Max(range.y, x));
        }
        return range;
    }

    Vector2 ViewportRangeY(Vector3[] corners)
    {
        Vector2 range = new Vector2(1f, 0f);
        foreach (Vector3 corner in corners)
        {
            float y = cam.WorldToViewportPoint(corner).y;
            range = new Vector2(Mathf.Min(range.x, y), Mathf.Max(range.y, y));
        }
        return range;
    }

    // ---- Sway ----

    // Orbits a little around the board toward the mouse. Eases back to rest while paused or inspecting a card
    void Sway()
    {
        Vector2 target = Vector2.zero;
        Mouse mouse = Mouse.current;
        if (mouse != null && !InputGate.IsPaused && !CardInspector.IsOpen && Screen.width > 0 && Screen.height > 0)
        {
            Vector2 position = mouse.position.ReadValue();
            target = new Vector2(Mathf.Clamp(position.x / Screen.width * 2f - 1f, -1f, 1f),
                                 Mathf.Clamp(position.y / Screen.height * 2f - 1f, -1f, 1f));
        }
        sway = Vector2.Lerp(sway, target, 1f - Mathf.Exp(-swaySpeed * Time.unscaledDeltaTime));

        Quaternion turn = Quaternion.AngleAxis(-sway.x * swayAngle, Vector3.up)
                        * Quaternion.AngleAxis(sway.y * swayAngle * 0.6f, restRotation * Vector3.right);
        cam.transform.SetPositionAndRotation(pivot + turn * (restPosition - pivot), turn * restRotation);
    }

    static Vector3 Flat(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : fallback;
    }
}
