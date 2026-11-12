using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Pixel-art look for the board: a dark bevelled tile under each slot, each player's starting rows tinted,
// a frame around the whole grid and a highlight on the tile under the mouse.
// Hides the plain slot cubes (their colliders stay). CombatManager adds it automatically
public class BoardStyle : MonoBehaviour
{
    [SerializeField] private float pixelsPerUnit = 24f;                           // Size of the art's pixels, about the same as the background's
    [SerializeField] private Color tileColor = new Color(0.62f, 0.58f, 0.88f);    // Middle rows
    [SerializeField] private Color player1Color = new Color(0.45f, 0.65f, 1f);    // Player 1's starting rows
    [SerializeField] private Color player2Color = new Color(1f, 0.5f, 0.55f);     // Player 2's starting rows
    [SerializeField] private Color frameColor = new Color(0.55f, 0.42f, 0.85f);
    [SerializeField] private Color frameGemColor = new Color(1f, 0.55f, 0.85f);
    [SerializeField] private Color hoverColor = new Color(1f, 0.95f, 0.7f);
    [SerializeField] private float tileAlpha = 0.9f;                              // Lower lets more background through the tiles
    [SerializeField] private float frameBacking = 0.45f;                          // How dark the area inside the frame is
    [SerializeField] private float frameMargin = 0.3f;                            // Gap between the outer tiles and the frame
    [SerializeField] private bool showHover = true;

    private Camera cam;
    private CardGrid grid;
    private Material spriteMaterial;
    private Transform root;
    private Vector2 tileSize;
    private SpriteRenderer hover;
    private Transform hovered;
    private readonly Dictionary<Transform, SpriteRenderer> tiles = new Dictionary<Transform, SpriteRenderer>();
    private readonly Dictionary<Transform, Color> tileColors = new Dictionary<Transform, Color>();
    private readonly List<Texture2D> textures = new List<Texture2D>();

    void Start()
    {
        cam = Camera.main;
        grid = FindAnyObjectByType<CardGrid>();
        spriteMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    void LateUpdate()
    {
        if (cam == null || grid == null) return;

        // The grid makes its slots in its own Start, so wait until they exist
        if (root == null)
        {
            if (grid.GetSlot(0, 0) == null) return;
            Build();
        }

        UpdateHover();
    }

    void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
        foreach (Texture2D texture in textures) Destroy(texture);
        if (spriteMaterial != null) Destroy(spriteMaterial);
    }

    // ---- Building ----

    void Build()
    {
        root = new GameObject("Board Style").transform; // Not under the slots, or they'd look taken (CardGrid checks childCount)

        Transform first = grid.GetSlot(0, 0);
        Renderer slotRenderer = first.GetComponent<Renderer>();
        tileSize = slotRenderer != null ? ScreenSize(slotRenderer.bounds.size) : new Vector2(0.6f, 1f);

        int tileWidth = Mathf.Max(8, Mathf.RoundToInt(tileSize.x * pixelsPerUnit));
        int tileHeight = Mathf.Max(8, Mathf.RoundToInt(tileSize.y * pixelsPerUnit));
        Sprite tileSprite = MakeTile(tileWidth, tileHeight);
        Bounds area = new Bounds(first.position, Vector3.zero);

        for (int row = 0; grid.GetSlot(row, 0) != null; row++)
        {
            for (int col = 0; grid.GetSlot(row, col) != null; col++)
            {
                Transform slot = grid.GetSlot(row, col);
                area.Encapsulate(slot.position);

                MeshRenderer cube = slot.GetComponent<MeshRenderer>();
                if (cube != null) cube.enabled = false;

                Color color = BoardState.IsInPlacementZone(row, 1) ? player1Color
                            : BoardState.IsInPlacementZone(row, 2) ? player2Color
                            : tileColor;
                color.a = 1f;

                SpriteRenderer tile = MakeRenderer("Tile_" + row + "_" + col, tileSprite, -20);
                tile.transform.SetPositionAndRotation(slot.position + Vector3.up * 0.01f, cam.transform.rotation);
                tile.color = color;
                SetSize(tile, tileSize);
                tiles[slot] = tile;
                tileColors[slot] = color;
            }
        }

        // Frame around every tile, a little bigger than the grid
        Vector2 gridSize = ScreenSize(area.size) + tileSize + Vector2.one * frameMargin * 2f;
        int frameWidth = Mathf.RoundToInt(gridSize.x * pixelsPerUnit);
        int frameHeight = Mathf.RoundToInt(gridSize.y * pixelsPerUnit);
        SpriteRenderer frame = MakeRenderer("Board Frame", MakeFrame(frameWidth, frameHeight), -30);
        frame.transform.SetPositionAndRotation(area.center + Vector3.up * 0.005f, cam.transform.rotation);
        SetSize(frame, gridSize);

        // Hover outline is a bit bigger than a tile so it still shows around a card sitting on it
        hover = MakeRenderer("Hover", MakeHover(tileWidth + 2, tileHeight + 2), -18);
        SetSize(hover, tileSize * (1f + 2f / tileWidth) * 1.06f);
        hover.enabled = false;
    }

    // ---- Hover ----

    void UpdateHover()
    {
        Transform slot = showHover ? SlotUnderMouse() : null;

        if (slot != hovered)
        {
            if (hovered != null && tiles.TryGetValue(hovered, out SpriteRenderer old)) old.color = tileColors[hovered];
            if (slot != null) tiles[slot].color = Color.Lerp(tileColors[slot], Color.white, 0.3f);
            hovered = slot;
        }

        hover.enabled = slot != null;
        if (slot == null) return;

        hover.transform.SetPositionAndRotation(slot.position + Vector3.up * 0.02f, cam.transform.rotation);
        Color color = hoverColor;
        color.a = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f); // Gentle pulse
        hover.color = color;
    }

    Transform SlotUnderMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || InputGate.IsPaused || InputGate.PointerOverUI || CardInspector.IsOpen) return null;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        Plane board = new Plane(Vector3.up, grid.GetSlot(0, 0).position);
        if (!board.Raycast(ray, out float enter)) return null;
        Vector3 point = ray.GetPoint(enter);

        foreach (Transform slot in tiles.Keys)
        {
            Vector3 offset = point - slot.position;
            if (Mathf.Abs(Vector3.Dot(offset, cam.transform.right)) <= tileSize.x * 0.5f
                && Mathf.Abs(Vector3.Dot(offset, cam.transform.up)) <= tileSize.y * 0.5f)
                return slot;
        }
        return null;
    }

    // ---- Helpers ----

    // Width and height on screen of something lying flat on the board
    Vector2 ScreenSize(Vector3 world)
    {
        Vector3 right = cam.transform.right;
        Vector3 up = cam.transform.up;
        return new Vector2(Mathf.Abs(right.x) * world.x + Mathf.Abs(right.z) * world.z,
                           Mathf.Abs(up.x) * world.x + Mathf.Abs(up.z) * world.z);
    }

    SpriteRenderer MakeRenderer(string name, Sprite sprite, int order)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(root, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = spriteMaterial;
        renderer.sortingOrder = order;
        return renderer;
    }

    static void SetSize(SpriteRenderer renderer, Vector2 size)
    {
        Vector2 spriteSize = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
    }

    // ---- Drawing the pixel art ----

    // Grey tile, tinted by the renderer's colour: light rim, raised bevel (lit top-left, shaded bottom-right), dark middle
    Sprite MakeTile(int width, int height)
    {
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int ring = Ring(x, y, width, height, 2);
                float shade;
                float alpha = 1f;
                if (ring < 0) { shade = 0f; alpha = 0f; }                  // Cut corner
                else if (ring == 0) shade = 0.7f;                          // Rim
                else if (ring == 1) shade = LitSide(x, y, width, height) ? 0.45f : 0.1f; // Bevel
                else if (ring == 2 && IsCorner(x, y, width, height, 2)) shade = 0.38f;   // Little corner studs
                else { shade = 0.22f; alpha = tileAlpha; }                 // Middle

                pixels[y * width + x] = new Color(shade, shade, shade, alpha);
            }
        }
        return MakeSprite(pixels, width, height);
    }

    // Chunky outline for the hovered tile, with a faint fill
    Sprite MakeHover(int width, int height)
    {
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int ring = Ring(x, y, width, height, 2);
                float alpha = ring < 0 ? 0f : ring == 0 ? 1f : ring == 1 ? 0.55f : 0.1f;
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        return MakeSprite(pixels, width, height);
    }

    // Board frame: dark outline, a 3-pixel bevelled band, gems in the middle of each side, dark backing inside
    Sprite MakeFrame(int width, int height)
    {
        Color outline = new Color(0.04f, 0.03f, 0.08f, 1f);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int ring = Ring(x, y, width, height, 3);
                Color color;
                if (ring < 0) color = Color.clear;
                else if (ring == 0 || ring == 4) color = outline;
                else if (ring <= 3)
                {
                    bool gem = Mathf.Abs(x - width / 2) <= 1 || Mathf.Abs(y - height / 2) <= 1;
                    float shade = (ring == 1 ? 1f : ring == 2 ? 0.8f : 0.6f) * (LitSide(x, y, width, height) ? 1.15f : 0.75f);
                    color = (gem ? frameGemColor : frameColor) * shade;
                    color.a = 1f;
                }
                else color = new Color(0.02f, 0.01f, 0.05f, frameBacking);

                pixels[y * width + x] = color;
            }
        }
        return MakeSprite(pixels, width, height);
    }

    // Which ring in from the edge a pixel is on (0 = outermost). Corners are cut diagonally; -1 means cut away
    static int Ring(int x, int y, int width, int height, int cut)
    {
        int dx = Mathf.Min(x, width - 1 - x);
        int dy = Mathf.Min(y, height - 1 - y);
        if (dx + dy < cut) return -1;
        return Mathf.Min(Mathf.Min(dx, dy), dx + dy - cut);
    }

    // True if the nearest edge is the top or left (the lit side of a raised bevel)
    static bool LitSide(int x, int y, int width, int height)
    {
        int left = x, right = width - 1 - x, bottom = y, top = height - 1 - y;
        int nearest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
        return nearest == top || nearest == left;
    }

    static bool IsCorner(int x, int y, int width, int height, int inset)
    {
        return (x == inset || x == width - 1 - inset) && (y == inset || y == height - 1 - inset);
    }

    Sprite MakeSprite(Color[] pixels, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point; // Keeps the pixels crisp
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(pixels);
        texture.Apply();
        textures.Add(texture);
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), width);
    }
}
