using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Makes cards feel alive (Balatro-style):
// - the card under the mouse lifts off the board, tilts toward the mouse and wobbles when you first point at it
// - a dragged card lifts higher and leans the way it's moving
// - the card taking its turn bobs gently
// It only adds a small offset on top of where the card really is: the offset comes off at the start of every frame
// (before CardMover, CombatEffects etc. move the card) and goes back on at the end, so nothing else has to know about it.
// Also draws a pixel drop shadow under every card. CombatManager adds it automatically
[DefaultExecutionOrder(-1000)]
public class CardHover : MonoBehaviour
{
    [SerializeField] private bool effectsOn = true;
    [SerializeField] private float hoverLift = 0.45f;   // Up off the board, so it looks closer and its shadow slides away
    [SerializeField] private float hoverTilt = 12f;     // Degrees the card tilts toward the mouse
    [SerializeField] private float hoverWobble = 150f;  // Spin kick when the mouse first goes over a card
    [SerializeField] private float dragLift = 0.8f;
    [SerializeField] private float dragLean = 4f;       // Degrees of lean per unit of drag speed
    [SerializeField] private float maxLean = 18f;
    [SerializeField] private float activeBob = 0.12f;   // How far the card taking its turn bobs
    [SerializeField] private float stiffness = 220f;    // Spring: higher snaps to the target faster
    [SerializeField] private float damping = 16f;       // Spring: lower wobbles more
    [SerializeField] private float hoverMargin = 0.1f;  // A hovered card stays hovered this far past its edge, so it doesn't flicker
    [SerializeField] private bool shadows = true;
    [SerializeField] private float shadowDistance = 0.07f; // How far the shadow sits from a card lying on the board
    [SerializeField] private float shadowStrength = 0.55f;
    [SerializeField, Range(0f, 40f)] private float cardLean = 22f; // Cards tip back this many degrees to face the tilted camera (0 = flat)
    [SerializeField] private bool pixelCardArt = false;  // Test: draws card art as chunky pixels (can be switched while playing)
    [SerializeField] private int pixelArtWidth = 64;     // Pixels across the card art when pixelCardArt is on

    private readonly Dictionary<SpriteRenderer, Sprite> originalArt = new Dictionary<SpriteRenderer, Sprite>();
    private readonly Dictionary<Sprite, Sprite> pixelCache = new Dictionary<Sprite, Sprite>();

    // How far cards lean back, so overlays drawn on the card (borders, HP bar) can lean with them
    public static float LeanAngle => instance != null && instance.isActiveAndEnabled ? instance.cardLean : 0f;

    // Spring state and the offset currently on one card
    private class Motion
    {
        public float lift, liftSpeed;
        public Vector2 tilt, tiltSpeed;   // x = tip left/right, y = tip up/down (degrees)
        public float roll, rollSpeed;     // Spin in the screen plane (degrees)
        public Vector3 lastPosition;      // For drag speed
        public Bounds shape;              // The card mesh's local bounds, for hover
        public bool applied;
        public Vector3 offset;
        public Quaternion turn = Quaternion.identity;
        public Vector3 appliedPosition;
        public Quaternion appliedRotation;
        public SpriteRenderer shadow;
    }

    private readonly Dictionary<CardUnit, Motion> cards = new Dictionary<CardUnit, Motion>();
    private Camera cam;
    private CardMover dragged;
    private CardUnit hovered;
    private static CardHover instance;

    // The card under the mouse. Steadier than CardUnit.UnderMouse while cards lift (falls back to it without a CardHover)
    public static CardUnit UnderMouse() => instance != null && instance.isActiveAndEnabled ? instance.hovered : CardUnit.UnderMouse();

    void Awake() => instance = this;
    private float nextScan;
    private CardGrid grid;
    private float? boardHeight;
    private Sprite shadowSprite;
    private Material shadowMaterial;

    void Start()
    {
        cam = Camera.main;
        grid = FindAnyObjectByType<CardGrid>();
        shadowSprite = MakeShadowSprite();
        shadowMaterial = new Material(Shader.Find("Sprites/Default"));
        CardMover.DragStarted += OnDragStarted;
        CardMover.DragEnded += OnDragEnded;
    }

    void OnDestroy()
    {
        CardMover.DragStarted -= OnDragStarted;
        CardMover.DragEnded -= OnDragEnded;
        RemoveOffsets();
        foreach (Motion motion in cards.Values)
            if (motion.shadow != null) Destroy(motion.shadow.gameObject);
        if (shadowSprite != null) Destroy(shadowSprite.texture);
        foreach (Sprite pixel in pixelCache.Values)
            if (pixel != null) Destroy(pixel.texture);
        if (shadowMaterial != null) Destroy(shadowMaterial);
    }

    void OnDragStarted(CardMover mover) => dragged = mover;
    void OnDragEnded(CardMover mover) { if (dragged == mover) dragged = null; }

    // Runs before every other script: takes last frame's offset off so they see the card's real position
    void Update()
    {
        RemoveOffsets();
    }

    // Runs before the other LateUpdates (so the board highlights follow the lifted card): works out and adds the offset
    void LateUpdate()
    {
        if (cam == null) return;
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.25f;
            ScanForCards();
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        CardUnit nowHovered = FindHovered();
        bool attackPlaying = CombatEffects.IsPlaying; // Lunges and knockback slides look wrong with extra motion

        foreach (KeyValuePair<CardUnit, Motion> pair in cards)
        {
            CardUnit unit = pair.Key;
            Motion motion = pair.Value;
            if (unit == null) continue;

            Vector3 position = unit.transform.position;
            Vector3 velocity = dt > 0f ? (position - motion.lastPosition) / dt : Vector3.zero;
            motion.lastPosition = position;

            // What this card should be doing
            float liftTarget = 0f;
            Vector2 tiltTarget = Vector2.zero;
            float rollTarget = 0f;
            bool alive = unit.Instance.IsAlive;

            if (effectsOn && alive && !attackPlaying)
            {
                bool isDragged = dragged != null && dragged.gameObject == unit.gameObject;
                if (isDragged)
                {
                    liftTarget = dragLift;
                    rollTarget = Mathf.Clamp(Vector3.Dot(velocity, BoardView.Right) * dragLean, -maxLean, maxLean);
                    tiltTarget = new Vector2(0f, Mathf.Clamp(Vector3.Dot(velocity, BoardView.Up) * dragLean * 0.5f, -maxLean, maxLean));
                }
                else if (unit == nowHovered && dragged == null)
                {
                    liftTarget = hoverLift;
                    tiltTarget = MouseTilt(unit) * hoverTilt;
                    if (hovered != unit) motion.rollSpeed += hoverWobble * (Random.value < 0.5f ? -1f : 1f); // Wobble on first hover
                }

                // The card taking its turn bobs and sways a little
                TurnManager turns = TurnManager.Instance;
                if (!isDragged && turns != null && turns.Phase == GamePhase.Battle && turns.ActiveUnit == unit)
                {
                    liftTarget += activeBob * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.5f));
                    rollTarget += 1.5f * Mathf.Sin(Time.unscaledTime * 1.7f);
                }
            }

            Spring(ref motion.lift, ref motion.liftSpeed, liftTarget, dt);
            Spring(ref motion.tilt.x, ref motion.tiltSpeed.x, tiltTarget.x, dt);
            Spring(ref motion.tilt.y, ref motion.tiltSpeed.y, tiltTarget.y, dt);
            Spring(ref motion.roll, ref motion.rollSpeed, rollTarget, dt);

            Apply(unit.transform, motion); // Defeated cards keep their lean, so the defeat flip doesn't snap flat first
            UpdateShadow(unit, motion, alive);
        }

        hovered = nowHovered;
    }

    // ---- Shadows ----

    // Pixel drop shadow on the board under each card. Slides away and fades as the card lifts, so cards sit on
    // the board instead of floating over it
    void UpdateShadow(CardUnit unit, Motion motion, bool alive)
    {
        if (motion.shadow == null) return;
        bool show = shadows && alive && unit.gameObject.activeInHierarchy && !UnitTokens.IsToken(unit); // Tokens draw their own
        motion.shadow.gameObject.SetActive(show);
        if (!show) return;

        if (boardHeight == null && grid != null && grid.GetSlot(0, 0) != null) boardHeight = grid.GetSlot(0, 0).position.y;
        Transform card = unit.transform;
        float floor = boardHeight ?? card.position.y - 0.05f;
        float height = Mathf.Max(0f, card.position.y - floor - LeanLift(motion, card)); // Leaning isn't lifting

        // Footprint: the card's two long sides (skips the thin one)
        Vector3 size = Vector3.Scale(motion.shape.size, card.lossyScale);
        Vector3 along = size.y <= size.z && size.y <= size.x ? card.forward : card.up;
        Vector2 footprint = size.y <= size.z && size.y <= size.x ? new Vector2(size.x, size.z) : new Vector2(size.x, size.y);
        along.y = 0f;
        if (along.sqrMagnitude < 0.0001f) along = BoardView.Up;

        // Light comes from the top-left of the screen
        Vector3 away = (BoardView.Right * 0.6f - BoardView.Up * 0.8f) * (shadowDistance + height * 0.45f);
        Vector3 centre = card.position + card.rotation * motion.shape.center;
        Transform shadow = motion.shadow.transform;
        shadow.SetPositionAndRotation(new Vector3(centre.x, floor + 0.012f, centre.z) + away, Quaternion.LookRotation(Vector3.down, along.normalized));
        Vector2 spriteSize = motion.shadow.sprite.bounds.size;
        float grow = 1f + height * 0.12f;
        shadow.localScale = new Vector3(footprint.x * grow / spriteSize.x, footprint.y * grow / spriteSize.y, 1f);
        motion.shadow.color = new Color(0f, 0f, 0.04f, Mathf.Lerp(shadowStrength, shadowStrength * 0.45f, height / 1f));
    }

    // Dark rectangle with cut corners and a lighter 1-pixel rim, point filtered so it matches the pixel board
    Sprite MakeShadowSprite()
    {
        const int width = 14, height = 22;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int dx = Mathf.Min(x, width - 1 - x), dy = Mathf.Min(y, height - 1 - y);
                float alpha = dx + dy < 2 ? 0f : Mathf.Min(dx, dy) == 0 ? 0.5f : 1f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 24f);
    }

    // ---- Offsets ----

    void Apply(Transform card, Motion motion)
    {
        // Nothing to add once the card has settled (and isn't leaning)
        bool settled = Mathf.Abs(motion.lift) < 0.001f && motion.tilt.sqrMagnitude < 0.01f && Mathf.Abs(motion.roll) < 0.1f;
        if (settled && cardLean <= 0f) return;

        // Straight up off the board, so its shadow slides away. Leaning cards also rise so their near edge stays on the board
        motion.offset = Vector3.up * (motion.lift + LeanLift(motion, card));
        motion.turn = LeanRotation(card)
                    * Quaternion.AngleAxis(motion.roll, Vector3.down)
                    * Quaternion.AngleAxis(motion.tilt.y, BoardView.Right)
                    * Quaternion.AngleAxis(-motion.tilt.x, BoardView.Up); // The side under the mouse dips away

        card.SetPositionAndRotation(card.position + motion.offset, motion.turn * card.rotation);
        motion.appliedPosition = card.position;
        motion.appliedRotation = card.rotation;
        motion.applied = true;
    }

    // Takes each card's offset back off. Skipped if something else moved the card since (it set a fresh position)
    void RemoveOffsets()
    {
        foreach (KeyValuePair<CardUnit, Motion> pair in cards)
        {
            Motion motion = pair.Value;
            if (pair.Key == null || !motion.applied) continue;
            motion.applied = false;

            Transform card = pair.Key.transform;
            if (card.position == motion.appliedPosition) card.position -= motion.offset;
            if (card.rotation == motion.appliedRotation) card.rotation = Quaternion.Inverse(motion.turn) * card.rotation;
            motion.lastPosition = card.position;
        }
    }

    // ---- Helpers ----

    // Adds new cards and forgets destroyed ones
    void ScanForCards()
    {
        List<CardUnit> gone = new List<CardUnit>();
        foreach (CardUnit unit in cards.Keys)
            if (unit == null) gone.Add(unit);
        foreach (CardUnit unit in gone)
        {
            if (cards[unit].shadow != null) Destroy(cards[unit].shadow.gameObject);
            cards.Remove(unit);
        }

        foreach (CardUnit unit in FindObjectsByType<CardUnit>())
        {
            // No Instance = the card inspector's copy, which has no scripts
            if (unit.Instance == null || cards.ContainsKey(unit)) continue;
            MeshFilter mesh = unit.GetComponent<MeshFilter>();
            BoxCollider box = unit.GetComponent<BoxCollider>();
            Bounds shape = mesh != null && mesh.sharedMesh != null ? mesh.sharedMesh.bounds
                         : box != null ? new Bounds(box.center, box.size)
                         : new Bounds(Vector3.zero, Vector3.one);
            // Shadow drawn over the board tiles (-20) but under the hover outline (-18)
            GameObject shadowObject = new GameObject($"Shadow ({unit.name})");
            SpriteRenderer shadow = shadowObject.AddComponent<SpriteRenderer>();
            shadow.sprite = shadowSprite;
            shadow.sharedMaterial = shadowMaterial;
            shadow.sortingOrder = -19;
            cards[unit] = new Motion { lastPosition = unit.transform.position, shape = shape, shadow = shadow };
        }

        List<SpriteRenderer> destroyedArt = new List<SpriteRenderer>();
        foreach (SpriteRenderer art in originalArt.Keys)
            if (art == null) destroyedArt.Add(art);
        foreach (SpriteRenderer art in destroyedArt) originalArt.Remove(art);

        foreach (CardUnit unit in cards.Keys)
            if (unit != null) UpdatePixelArt(unit);
    }

    // The card under the mouse, checked against where the card really is (not lifted or tilted).
    // Using its collider instead flickers at the edges: lifting moves the collider off the mouse, the card drops, and so on
    CardUnit FindHovered()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || CardInspector.IsOpen || InputGate.IsPaused || InputGate.PointerOverUI) return null;
        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());

        CardUnit best = null;
        float bestHeight = float.MinValue;
        foreach (KeyValuePair<CardUnit, Motion> pair in cards)
        {
            CardUnit unit = pair.Key;
            if (unit == null || !unit.Instance.IsAlive) continue;
            float margin = unit == hovered ? 1f + hoverMargin : 1f;
            if (!MouseOnCard(unit, pair.Value, ray, margin, out _)) continue;

            // Cards in a pile: the one on top wins. Same height (standing tokens overlapping): the one nearer the camera
            float height = unit.transform.position.y - (unit.transform.position - cam.transform.position).magnitude * 0.001f;
            if (height > bestHeight) { best = unit; bestHeight = height; }
        }
        return best;
    }

    // True if the ray hits the card's face where it's drawn leaning (but not lifted or tilted), grown by margin.
    // where = the hit point, -1 to 1 across each side
    bool MouseOnCard(CardUnit unit, Motion motion, Ray ray, float margin, out Vector2 where)
    {
        where = Vector2.zero;
        if (UnitTokens.IsToken(unit)) return UnitTokens.RayHits(unit, ray, margin); // Standing token: test its upright frame
        Transform card = unit.transform;
        Quaternion rotation = LeanRotation(card) * card.rotation;
        Vector3 scale = card.lossyScale;
        Vector3 centre = card.position + Vector3.up * LeanLift(motion, card) + rotation * Vector3.Scale(motion.shape.center, scale);

        // The card's thin side is its face direction
        Vector3 half = motion.shape.extents * margin;
        int thin = half.x <= half.y && half.x <= half.z ? 0 : half.y <= half.z ? 1 : 2;
        Vector3 face = rotation * (thin == 0 ? Vector3.right : thin == 1 ? Vector3.up : Vector3.forward);
        Plane plane = new Plane(face, centre);
        if (!plane.Raycast(ray, out float enter)) return false;
        Vector3 hit = ray.GetPoint(enter);

        // In the card's own space, so turned cards (like the draft pile) work too
        Vector3 local = Quaternion.Inverse(rotation) * (hit - centre);
        local = new Vector3(local.x / scale.x, local.y / scale.y, local.z / scale.z);
        for (int axis = 0; axis < 3; axis++)
            if (axis != thin && Mathf.Abs(local[axis]) > Mathf.Max(0.0001f, half[axis])) return false;

        // Back to screen directions for the tilt
        Vector3 offset = hit - centre;
        Vector3 cardUp = LeanRotation(card) * BoardView.Up;
        Vector3 world = Vector3.Scale(motion.shape.extents, scale);
        float halfWidth = Mathf.Max(0.01f, Mathf.Abs(BoardView.Right.x) * world.x + Mathf.Abs(BoardView.Right.z) * world.z);
        float halfHeight = Mathf.Max(0.01f, Mathf.Abs(BoardView.Up.x) * world.x + Mathf.Abs(BoardView.Up.z) * world.z);
        where = new Vector2(Mathf.Clamp(Vector3.Dot(offset, BoardView.Right) / halfWidth, -1f, 1f),
                            Mathf.Clamp(Vector3.Dot(offset, cardUp) / halfHeight, -1f, 1f));
        return true;
    }

    // ---- Lean ----

    // Tips the far edge up, so the card's face turns toward the tilted camera
    Quaternion LeanRotation(Transform card) => UnitTokens.IsToken(card) ? Quaternion.identity : Quaternion.AngleAxis(-cardLean, BoardView.Right);

    // How far a leaning card rises so its near edge stays on the board instead of sinking into it
    float LeanLift(Motion motion, Transform card)
    {
        if (cardLean <= 0f || UnitTokens.IsToken(card)) return 0f;
        Vector3 size = Vector3.Scale(motion.shape.size, card.lossyScale);
        float length = Mathf.Max(size.x, Mathf.Max(size.y, size.z)); // The card's long side
        return length * 0.5f * Mathf.Sin(cardLean * Mathf.Deg2Rad) + 0.01f;
    }

    // ---- Pixel card art (test toggle) ----

    // Swaps each card's art for a chunky pixel copy while pixelCardArt is on, and puts the original back when it's off
    void UpdatePixelArt(CardUnit unit)
    {
        foreach (SpriteRenderer art in unit.GetComponentsInChildren<SpriteRenderer>(true))
        {
            bool swapped = originalArt.TryGetValue(art, out Sprite original);
            if (swapped && !pixelCache.ContainsValue(art.sprite)) { original = art.sprite; originalArt[art] = original; } // Card got new art

            if (pixelCardArt)
            {
                Sprite source = swapped ? original : art.sprite;
                if (source == null || source.rect.width < pixelArtWidth * 1.5f) continue; // Small icons stay as they are
                originalArt[art] = source;
                art.sprite = Pixelated(source);
            }
            else if (swapped)
            {
                art.sprite = original;
                originalArt.Remove(art);
            }
        }
    }

    // Shrinks the art on the GPU (halving each step for a clean average), then reads it back with point filtering.
    // Same size in the world as the original, so the card's layout doesn't change
    Sprite Pixelated(Sprite source)
    {
        if (pixelCache.TryGetValue(source, out Sprite cached)) return cached;

        Rect rect = source.textureRect;
        Texture texture = source.texture;
        int width = pixelArtWidth, height = Mathf.Max(1, Mathf.RoundToInt(pixelArtWidth * rect.height / rect.width));
        Vector2 scale = new Vector2(rect.width / texture.width, rect.height / texture.height);
        Vector2 offset = new Vector2(rect.x / texture.width, rect.y / texture.height);

        int stepWidth = Mathf.RoundToInt(rect.width), stepHeight = Mathf.RoundToInt(rect.height);
        RenderTexture current = RenderTexture.GetTemporary(stepWidth, stepHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(texture, current, scale, offset);
        while (stepWidth / 2 >= width)
        {
            stepWidth /= 2;
            stepHeight = Mathf.Max(1, stepHeight / 2);
            RenderTexture smaller = RenderTexture.GetTemporary(stepWidth, stepHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(current, smaller);
            RenderTexture.ReleaseTemporary(current);
            current = smaller;
        }
        RenderTexture final = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(current, final);
        RenderTexture.ReleaseTemporary(current);

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = final;
        Texture2D pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
        pixels.filterMode = FilterMode.Point;
        pixels.wrapMode = TextureWrapMode.Clamp;
        pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        pixels.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(final);

        Vector2 pivot = new Vector2(source.pivot.x / rect.width, source.pivot.y / rect.height);
        Sprite sprite = Sprite.Create(pixels, new Rect(0, 0, width, height), pivot, source.pixelsPerUnit * width / rect.width);
        sprite.name = source.name + " (pixel)";
        pixelCache[source] = sprite;
        return sprite;
    }

    // Where the mouse is on the card, from -1 to 1 each way
    Vector2 MouseTilt(CardUnit unit)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return Vector2.zero;
        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        MouseOnCard(unit, cards[unit], ray, 10f, out Vector2 where);
        return where;
    }

    // Damped spring toward a target
    void Spring(ref float value, ref float speed, float target, float dt)
    {
        speed += (stiffness * (target - value) - damping * speed) * dt;
        value += speed * dt;
    }
}
