using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Inked Star Chart board: a slate board (#465E69, the menu buttons) with an ink-and-sage rim, ink-lined cells, gold star points where the cells
// meet, each player's starting rows tinted, a thick ink slab edge with its shadow on the void, faint gold astrolabe
// rings around it, and a gold outline on the cell under the mouse.
// Hides the plain slot cubes (their colliders stay). Colours come from InkArt. CombatManager adds it automatically
public class BoardStyle : MonoBehaviour
{
    [SerializeField] private float player1Tint = 0.22f;     // How strongly each side's starting rows are tinted
    [SerializeField] private float player2Tint = 0.22f;
    [SerializeField] private float frameMargin = 0.3f;      // Gap between the outer cells and the rim
    [SerializeField] private bool showHover = true;
    [SerializeField] private bool starPoints = true;
    [SerializeField] private bool astrolabeRings = true;
    [SerializeField] private float boardThickness = 0.35f;  // Depth of the board's solid edge (0 = flat)
    [SerializeField] private bool shadowOnVoid = false;     // Soft shadow under the board on the space below
    [SerializeField] private float voidShadowDepth = 1.5f;  // How far below the board that shadow falls

    private const float TexturePixelsPerUnit = 100f;        // Detail of the cell textures

    private Camera cam;
    private CardGrid grid;
    private Material spriteMaterial;
    private Transform root;
    private Vector2 tileSize;
    private SpriteRenderer hover;
    private Transform hovered;
    private readonly Dictionary<Transform, SpriteRenderer> fills = new Dictionary<Transform, SpriteRenderer>();
    private readonly Dictionary<Transform, Color> fillColors = new Dictionary<Transform, Color>();
    private readonly List<Texture2D> textures = new List<Texture2D>();
    private readonly List<Mesh> meshes = new List<Mesh>();

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
        foreach (Mesh mesh in meshes) Destroy(mesh);
        if (spriteMaterial != null) Destroy(spriteMaterial);
    }

    // ---- Building ----

    void Build()
    {
        root = new GameObject("Board Style").transform; // Not under the slots, or they'd look taken (CardGrid checks childCount)

        Transform first = grid.GetSlot(0, 0);
        Renderer slotRenderer = first.GetComponent<Renderer>();
        tileSize = slotRenderer != null ? BoardView.Size(slotRenderer.bounds.size) : new Vector2(0.6f, 1f);

        Sprite fillSprite = MakeCellFill(tileSize);
        Sprite lineSprite = MakeCellLines(tileSize);
        Bounds area = new Bounds(first.position, Vector3.zero);
        int rows = 0, columns = 0;

        for (int row = 0; grid.GetSlot(row, 0) != null; row++)
        {
            rows = row + 1;
            for (int col = 0; grid.GetSlot(row, col) != null; col++)
            {
                columns = Mathf.Max(columns, col + 1);
                Transform slot = grid.GetSlot(row, col);
                area.Encapsulate(slot.position);

                MeshRenderer cube = slot.GetComponent<MeshRenderer>();
                if (cube != null) cube.enabled = false;

                // Each side's starting rows get a faint team tint, the middle stays clear
                Color color = BoardState.IsInPlacementZone(row, 1) ? WithAlpha(InkArt.Player1, player1Tint)
                            : BoardState.IsInPlacementZone(row, 2) ? WithAlpha(InkArt.Player2, player2Tint)
                            : WithAlpha(Color.white, 0.03f);

                SpriteRenderer fill = MakeRenderer("Cell_" + row + "_" + col, fillSprite, -22);
                fill.transform.SetPositionAndRotation(slot.position + Vector3.up * 0.008f, BoardView.Flat);
                fill.color = color;
                fills[slot] = fill;
                fillColors[slot] = color;

                SpriteRenderer lines = MakeRenderer("Cell Lines_" + row + "_" + col, lineSprite, -21);
                lines.transform.SetPositionAndRotation(slot.position + Vector3.up * 0.01f, BoardView.Flat);
            }
        }

        // Rim around every cell: ink, gold line, ink, then the navy board surface
        Vector2 gridSize = BoardView.Size(area.size) + tileSize + Vector2.one * frameMargin * 2f;
        SpriteRenderer frame = MakeRenderer("Board Frame", MakeFrame(), -30);
        frame.drawMode = SpriteDrawMode.Sliced;
        frame.size = gridSize;
        frame.transform.SetPositionAndRotation(area.center + Vector3.up * 0.005f, BoardView.Flat);

        if (starPoints && rows > 1 && columns > 1) MakeStarPoints(rows, columns);
        if (boardThickness > 0f) MakeSlab(area.center + Vector3.up * 0.004f, gridSize);
        if (astrolabeRings) MakeRings(area.center + Vector3.down * (boardThickness + 0.25f), gridSize);

        // Gold outline on the cell under the mouse
        hover = MakeRenderer("Hover", MakeHoverOutline(tileSize), -18);
        hover.enabled = false;
    }

    // A small gold star where every four cells meet, plus along the edges
    void MakeStarPoints(int rows, int columns)
    {
        Vector3 origin = grid.GetSlot(0, 0).position;
        Vector3 colStep = grid.GetSlot(0, 1).position - origin;
        Vector3 rowStep = grid.GetSlot(1, 0).position - origin;
        Sprite star = MakeStar();

        for (int r = 0; r <= rows; r++)
            for (int c = 0; c <= columns; c++)
            {
                Vector3 point = origin + colStep * (c - 0.5f) + rowStep * (r - 0.5f);
                SpriteRenderer renderer = MakeRenderer("Star Point", star, -20);
                renderer.transform.SetPositionAndRotation(point + Vector3.up * 0.012f, BoardView.Flat);
            }
    }

    // Faint gold rings below the board, like an astrolabe's dials. The middle one has tick marks
    void MakeRings(Vector3 centre, Vector2 boardSize)
    {
        float radius = boardSize.magnitude * 0.5f;
        float[] scales = { 1.08f, 1.3f, 1.55f };
        Sprite plain = MakeAstrolabeRing(false), ticked = MakeAstrolabeRing(true);
        for (int i = 0; i < scales.Length; i++)
        {
            SpriteRenderer ring = MakeRenderer("Astrolabe Ring", i == 1 ? ticked : plain, -45);
            ring.transform.SetPositionAndRotation(centre, BoardView.Flat);
            SetSize(ring, Vector2.one * radius * 2f * scales[i]);
            ring.color = WithAlpha(Color.white, i == 1 ? 0.4f : 0.28f);
        }
    }

    // ---- Board slab ----

    // The four sides of the board: a gold line at the top, a thick ink body and a darker bottom line.
    // The side facing the viewer is lit, the ends are shaded. Plus a soft shadow below it on the void
    void MakeSlab(Vector3 top, Vector2 size)
    {
        Vector3 right = BoardView.Right * size.x * 0.5f, up = BoardView.Up * size.y * 0.5f;
        Vector3[] corners = { top - right - up, top + right - up, top + right + up, top - right + up }; // Near-left, near-right, far-right, far-left

        List<Vector3> vertices = new List<Vector3>();
        List<Color> colors = new List<Color>();
        List<int> triangles = new List<int>();
        float[] shades = { 1f, 0.7f, 0.5f, 0.7f }; // Near side lit, ends shaded, far side (mostly hidden) darkest
        Color body = new Color32(50, 68, 76, 255); // Darker slate

        for (int side = 0; side < 4; side++)
        {
            Vector3 a = corners[side], b = corners[(side + 1) % 4];
            float shade = shades[side];
            AddBand(vertices, colors, triangles, a, b, 0f, 0.035f, InkArt.Ink);
            AddBand(vertices, colors, triangles, a, b, 0.035f, 0.06f, InkArt.Sage * shade);
            AddBand(vertices, colors, triangles, a, b, 0.06f, boardThickness - 0.04f, body * shade);
            AddBand(vertices, colors, triangles, a, b, boardThickness - 0.04f, boardThickness, InkArt.Ink);
        }

        Mesh mesh = new Mesh { name = "Board Slab" };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        meshes.Add(mesh);

        GameObject slab = new GameObject("Board Slab", typeof(MeshFilter), typeof(MeshRenderer));
        slab.transform.SetParent(root, false);
        slab.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer slabRenderer = slab.GetComponent<MeshRenderer>();
        slabRenderer.sharedMaterial = spriteMaterial; // Sprites/Default uses the vertex colours and draws both sides
        slabRenderer.sortingOrder = -31;
        slabRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Shadow on the void: a bigger soft dark shape further down, nudged away from the top-left light.
        // Off by default: from the tilted camera its soft edge reads as a stray ring under the board
        if (!shadowOnVoid) return;
        Vector2 shadowSize = size + Vector2.one * 1.2f;
        SpriteRenderer shadow = MakeRenderer("Board Shadow", MakeVoidShadow(), -40);
        Vector3 away = (BoardView.Right * 0.5f - BoardView.Up * 0.7f) * 0.6f;
        shadow.transform.SetPositionAndRotation(top + Vector3.down * voidShadowDepth + away, BoardView.Flat);
        SetSize(shadow, shadowSize);
    }

    // One horizontal strip of a side face, from depth "from" to depth "to" below the top edge a-b
    static void AddBand(List<Vector3> vertices, List<Color> colors, List<int> triangles, Vector3 a, Vector3 b, float from, float to, Color color)
    {
        color.a = 1f;
        int start = vertices.Count;
        vertices.Add(a + Vector3.down * from);
        vertices.Add(b + Vector3.down * from);
        vertices.Add(b + Vector3.down * to);
        vertices.Add(a + Vector3.down * to);
        for (int i = 0; i < 4; i++) colors.Add(color);
        triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
    }

    // ---- Hover ----

    void UpdateHover()
    {
        // No cell outline while pointing at a unit: the unit itself reacts
        Transform slot = showHover && CardHover.UnderMouse() == null ? SlotUnderMouse() : null;

        if (slot != hovered)
        {
            if (hovered != null && fills.TryGetValue(hovered, out SpriteRenderer old)) old.color = fillColors[hovered];
            if (slot != null) fills[slot].color = Color.Lerp(fillColors[slot], WithAlpha(InkArt.GoldLight, 0.35f), 0.6f);
            hovered = slot;
        }

        hover.enabled = slot != null;
        if (slot == null) return;

        hover.transform.SetPositionAndRotation(slot.position + Vector3.up * 0.02f, BoardView.Flat);
        hover.color = WithAlpha(Color.white, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f)); // Gentle pulse
    }

    Transform SlotUnderMouse()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || InputGate.IsPaused || InputGate.PointerOverUI || CardInspector.IsOpen) return null;

        Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        Plane board = new Plane(Vector3.up, grid.GetSlot(0, 0).position);
        if (!board.Raycast(ray, out float enter)) return null;
        Vector3 point = ray.GetPoint(enter);

        foreach (Transform slot in fills.Keys)
        {
            Vector3 offset = point - slot.position;
            if (Mathf.Abs(Vector3.Dot(offset, BoardView.Right)) <= tileSize.x * 0.5f
                && Mathf.Abs(Vector3.Dot(offset, BoardView.Up)) <= tileSize.y * 0.5f)
                return slot;
        }
        return null;
    }

    // ---- Helpers ----

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

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    Sprite Keep(Texture2D texture, float pixelsPerUnit, float border = 0f)
    {
        textures.Add(texture);
        return InkArt.ToSprite(texture, new Vector2(0.5f, 0.5f), pixelsPerUnit, border);
    }

    // ---- Drawing ----

    // Cell fill: a soft rounded rectangle, tinted per cell
    Sprite MakeCellFill(Vector2 size)
    {
        int w = Mathf.Max(8, Mathf.RoundToInt(size.x * TexturePixelsPerUnit)), h = Mathf.Max(8, Mathf.RoundToInt(size.y * TexturePixelsPerUnit));
        return Keep(InkArt.Paint(w, h, p =>
            InkArt.Chamfer(p, new Vector2(w / 2f, h / 2f), new Vector2(w / 2f - 2f, h / 2f - 2f), 12f, 4f) <= 0f ? Color.white : Color.clear, 2), TexturePixelsPerUnit);
    }

    // Cell lines: a thin ink outline, the board's grid lines
    Sprite MakeCellLines(Vector2 size)
    {
        int w = Mathf.Max(8, Mathf.RoundToInt(size.x * TexturePixelsPerUnit)), h = Mathf.Max(8, Mathf.RoundToInt(size.y * TexturePixelsPerUnit));
        Color line = WithAlpha(InkArt.Ink, 0.7f);
        return Keep(InkArt.Paint(w, h, p =>
            Mathf.Abs(InkArt.Chamfer(p, new Vector2(w / 2f, h / 2f), new Vector2(w / 2f - 3f, h / 2f - 3f), 12f, 4f)) <= 1.6f ? line : Color.clear, 2), TexturePixelsPerUnit);
    }

    // Hover: a gold outline edged in ink, a little outside the cell
    Sprite MakeHoverOutline(Vector2 size)
    {
        int w = Mathf.RoundToInt(size.x * TexturePixelsPerUnit) + 8, h = Mathf.RoundToInt(size.y * TexturePixelsPerUnit) + 8;
        return Keep(InkArt.Paint(w, h, p =>
        {
            float d = Mathf.Abs(InkArt.Chamfer(p, new Vector2(w / 2f, h / 2f), new Vector2(w / 2f - 5f, h / 2f - 5f), 14f, 5f));
            return d <= 2f ? InkArt.GoldLight : d <= 4f ? InkArt.Ink : Color.clear;
        }, 2), TexturePixelsPerUnit);
    }

    // Board rim: ink band, sage line, thin ink line, slate surface. 9-sliced so it fits the whole board
    Sprite MakeFrame()
    {
        const int size = 96;
        return Keep(InkArt.Paint(size, size, p =>
        {
            float edge = InkArt.Chamfer(p, new Vector2(size / 2f, size / 2f), new Vector2(size / 2f - 1f, size / 2f - 1f), 26f, 8f);
            if (edge > 0f) return Color.clear;
            float depth = -edge;
            return depth < 7f ? InkArt.Ink : depth < 10f ? InkArt.Sage : depth < 12f ? InkArt.Ink : InkArt.Navy;
        }), 120f, 32f);
    }

    Sprite MakeStar()
    {
        const int size = 40;
        return Keep(InkArt.Paint(size, size, p =>
            InkArt.Outlined(InkArt.Sparkle(p, new Vector2(size / 2f, size / 2f), size - 4f), 3f, InkArt.GoldLight), 4), size / 0.17f);
    }

    // Thin gold circle, optionally with tick marks every 10 degrees (longer every 30)
    Sprite MakeAstrolabeRing(bool ticks)
    {
        const int size = 512;
        Vector2 centre = new Vector2(size / 2f, size / 2f);
        float radius = size * 0.48f;
        return Keep(InkArt.Paint(size, size, p =>
        {
            Vector2 d = p - centre;
            float r = d.magnitude;
            if (Mathf.Abs(r - radius) <= 1.5f) return InkArt.Gold;
            if (ticks)
            {
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 180f;
                bool major = Mathf.Abs(Mathf.Repeat(angle + 1f, 30f) - 1f) < 1f;
                bool minor = Mathf.Abs(Mathf.Repeat(angle + 0.5f, 10f) - 0.5f) < 0.5f;
                float length = major ? 16f : minor ? 8f : 0f;
                if (length > 0f && r < radius && r > radius - length) return InkArt.Gold;
            }
            return Color.clear;
        }, 2), size);
    }

    // Soft shadow under the board on the void
    Sprite MakeVoidShadow()
    {
        const int size = 64;
        return Keep(InkArt.Paint(size, size, p =>
        {
            float d = InkArt.RoundedRect(p, new Vector2(size / 2f, size / 2f), new Vector2(size / 2f - 12f, size / 2f - 12f), 8f);
            Color c = InkArt.Ink;
            c.a = Mathf.Clamp01(0.5f - d / 24f) * 0.75f;
            return c;
        }, 1), size);
    }
}
