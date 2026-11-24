using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

// Inked Star Chart units. Once a card is on the board it shows as a standing token instead of a flat card:
// a class "crown" frame (ink outline, team-colour band, gold line, class shape and badge from ClassFrames) around the
// character's portrait (cropped per card with CardData.portraitFocus/Zoom), a sage name ribbon (cut front, notched tail),
// and only two numbers: HP (red gem) and speed (gold gem). The full card is still there for the inspector.
// The real card GameObject keeps doing everything (dragging, combat, effects), its look is just hidden, and the token
// follows it. Also draws a soft shadow under each token and dotted gold "constellation" lines between allies.
// CombatManager adds it automatically
[DefaultExecutionOrder(-500)] // After CardHover lifts the cards, before BoardHighlights reads where the tokens are
public class UnitTokens : MonoBehaviour
{
    [Header("Size (world units)")]
    [SerializeField] private float tokenWidth = 0.66f;       // Body width; the height comes from each class's crown
    [SerializeField] private float hoverScale = 1.1f;

    [Header("Extras")]
    [SerializeField] private bool constellationLines = true;
    [SerializeField] private float shadowStrength = 0.55f;

    public static UnitTokens Instance { get; private set; }

    // Frame texture pixels per crown unit (the body is 116 units wide)
    public const float FramePixels = 1.4f;

    // World units per crown unit
    float UnitScale => tokenWidth / ClassFrames.BodyWidth;

    private class Token
    {
        public GameObject root;
        public SpriteRenderer shadow, hpGem, spdGem;
        public TextMeshPro hpText, spdText;
        public int shownHp = int.MinValue, shownSpd = int.MinValue;
        public float scale = 1f, dying = -1f, noticedDead = -1f;
        public SpriteRenderer flash, crack;
        public float flashUntil, flashLength;
        public Color flashColor = Color.white;
        public List<Renderer> hidden = new List<Renderer>();
        public BoxCollider box;
        public Vector3 boxSize, boxCenter;
    }

    private readonly Dictionary<CardUnit, Token> tokens = new Dictionary<CardUnit, Token>();
    private readonly HashSet<Transform> tokenCards = new HashSet<Transform>();
    private readonly Dictionary<(CardData, int), Sprite> frames = new Dictionary<(CardData, int), Sprite>();
    private readonly List<Texture2D> textures = new List<Texture2D>();
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private readonly Dictionary<CharacterClass, (Sprite, Sprite)> classShapes = new Dictionary<CharacterClass, (Sprite, Sprite)>(); // Flash + cracks
    private Sprite pillSprite, ribbonSprite, shadowSprite;
    private Material spriteMaterial, lineMaterial;
    private float nextScan;
    private CardGrid grid;

    // ---- Queries for other scripts ----

    // True while this card is shown as a token (its own visuals are hidden)
    public static bool IsToken(Component card) => Instance != null && card != null && Instance.tokenCards.Contains(card.transform);

    // Where the token stands on the board
    public static Vector3 Feet(CardUnit unit)
    {
        float offset = CombatManager.Instance != null ? CombatManager.Instance.CardHeightOffset : 0.05f;
        return unit.transform.position - Vector3.up * offset;
    }

    // Height of the board surface under this unit
    public static float FloorHeight(CardUnit unit) => Instance != null ? Instance.BoardFloor(unit) : unit.transform.position.y - 0.05f;

    // The top of the token's frame (for badges above it)
    public static Vector3 Top(CardUnit unit)
    {
        float height = Instance != null ? Instance.HeightOf(unit) : 0.92f;
        return Feet(unit) + BoardView.CameraRotation * Vector3.up * height;
    }

    // How tall this unit's crown stands, in world units
    float HeightOf(CardUnit unit)
    {
        CardData data = unit.Instance != null ? unit.Instance.data : null;
        return ClassFrames.Height(data != null ? data.characterClass : CharacterClass.Attacker) * UnitScale;
    }

    // Flashes a token (a hit lands on it)
    public static void Flash(CardUnit unit, Color colour, float duration)
    {
        if (Instance == null || unit == null || !Instance.tokens.TryGetValue(unit, out Token token)) return;
        token.flashColor = colour;
        token.flashLength = Mathf.Max(0.05f, duration);
        token.flashUntil = Time.time + token.flashLength;
    }

    // The killing blow has landed: crack the frame and shrink away (ThemedEffects draws the shatter)
    public static void StartDefeat(CardUnit unit)
    {
        if (Instance == null || unit == null || !Instance.tokens.TryGetValue(unit, out Token token)) return;
        if (token.dying < 0f) BeginDying(token);
    }

    static void BeginDying(Token token)
    {
        token.dying = Time.time;
        if (token.crack != null) token.crack.enabled = true;
    }

    // True if the ray points at the token's upright frame (grown by margin), for hover and clicks
    public static bool RayHits(CardUnit unit, Ray ray, float margin)
    {
        if (Instance == null) return false;
        Quaternion facing = BoardView.CameraRotation;
        Vector3 feet = Feet(unit);
        Plane plane = new Plane(facing * Vector3.forward, feet);
        if (!plane.Raycast(ray, out float enter)) return false;
        Vector3 local = Quaternion.Inverse(facing) * (ray.GetPoint(enter) - feet);
        float halfWidth = Instance.tokenWidth * 0.5f * margin;
        return Mathf.Abs(local.x) <= halfWidth && local.y >= -0.08f && local.y <= Instance.HeightOf(unit) * margin;
    }

    // ---- Lifetime ----

    void Awake() => Instance = this;

    void Start()
    {
        spriteMaterial = new Material(Shader.Find("Sprites/Default"));
        lineMaterial = new Material(spriteMaterial);
        pillSprite = MakePill();
        ribbonSprite = MakeRibbon();
        shadowSprite = MakeShadow();
        _ = GameFonts.Display;

        // Make each drafted card's token frame ahead of time, a little each frame, so the draft and placing don't hitch
        draft = FindAnyObjectByType<DraftManager>();
        if (draft != null) draft.CardDrafted += OnDrafted;
        StartCoroutine(PrewarmLoop());
    }

    [Header("Performance")]
    [SerializeField] private float prewarmMsPerFrame = 3f; // Time per frame spent making upcoming token frames

    private DraftManager draft;
    private readonly Queue<(CardData, int)> prewarm = new Queue<(CardData, int)>();

    void OnDrafted(CardUnit unit, int player)
    {
        if (unit != null && unit.Instance != null) prewarm.Enqueue((unit.Instance.data, player));
    }

    void OnDestroy()
    {
        if (draft != null) draft.CardDrafted -= OnDrafted;
        foreach (KeyValuePair<CardUnit, Token> pair in tokens) ExitTokenMode(pair.Key, pair.Value);
        foreach (Texture2D texture in textures) Destroy(texture);
        foreach (LineRenderer line in lines) if (line != null) Destroy(line.gameObject);
        Destroy(spriteMaterial);
        Destroy(lineMaterial);
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        if (CombatManager.Instance == null) return;
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.2f;
            Scan();
        }

        foreach (KeyValuePair<CardUnit, Token> pair in tokens)
            if (pair.Key != null) UpdateToken(pair.Key, pair.Value);

        UpdateConstellations();
    }

    // Cards become tokens when they land on the board. Dead ones shrink away. Destroyed ones are cleaned up
    void Scan()
    {
        List<CardUnit> gone = new List<CardUnit>();
        foreach (KeyValuePair<CardUnit, Token> pair in tokens)
            if (pair.Key == null) gone.Add(pair.Key);
        foreach (CardUnit unit in gone)
        {
            if (tokens[unit].root != null) Destroy(tokens[unit].root);
            tokens.Remove(unit);
        }
        tokenCards.RemoveWhere(card => card == null);

        BoardState board = CombatManager.Instance.Board;
        foreach (CardUnit unit in FindObjectsByType<CardUnit>())
        {
            if (unit.Instance == null || !unit.HasOwner) continue;
            bool onBoard = board.IsOnBoard(unit.Instance);
            bool isToken = tokens.TryGetValue(unit, out Token token);

            if (!isToken && onBoard && unit.Instance.IsAlive) tokens[unit] = EnterTokenMode(unit);
            else if (isToken && !onBoard && unit.Instance.IsAlive) // Taken off the board (dev restart etc): back to a card
            {
                ExitTokenMode(unit, token);
                tokens.Remove(unit);
            }
        }
    }

    // ---- Switching a card to a token and back ----

    Token EnterTokenMode(CardUnit unit)
    {
        Token token = new Token();

        // Hide the card's own look: its mesh, art, back and printed text. Effects (particles) are left alone
        foreach (Renderer renderer in unit.GetComponentsInChildren<Renderer>(true))
        {
            bool cardLook = renderer is SpriteRenderer || renderer.GetComponent<TMP_Text>() != null || renderer.transform == unit.transform;
            if (!cardLook || !renderer.enabled) continue;
            renderer.enabled = false;
            token.hidden.Add(renderer);
        }

        // Make the clickable box stand up like the token, so pointing at the token's top still picks this card
        token.box = unit.GetComponent<BoxCollider>();
        if (token.box != null)
        {
            token.boxSize = token.box.size;
            token.boxCenter = token.box.center;
            float scaleY = Mathf.Max(0.0001f, Mathf.Abs(unit.transform.lossyScale.y));
            float height = (HeightOf(unit) + 0.1f) / scaleY;
            token.box.size = new Vector3(token.box.size.x, height, token.box.size.z);
            token.box.center = new Vector3(token.box.center.x, height * 0.5f, token.box.center.z);
        }

        BuildVisuals(unit, token);
        tokenCards.Add(unit.transform);
        return token;
    }

    void ExitTokenMode(CardUnit unit, Token token)
    {
        if (unit != null) tokenCards.Remove(unit.transform);
        foreach (Renderer renderer in token.hidden)
            if (renderer != null) renderer.enabled = true;
        if (token.box != null)
        {
            token.box.size = token.boxSize;
            token.box.center = token.boxCenter;
        }
        if (token.root != null) Destroy(token.root);
        if (token.shadow != null) Destroy(token.shadow.gameObject);
    }

    // ---- Building one token ----

    void BuildVisuals(CardUnit unit, Token token)
    {
        CardData data = unit.Instance.data;
        int owner = unit.Instance.owner;

        token.root = new GameObject($"Token ({unit.name})");
        SortingGroup group = token.root.AddComponent<SortingGroup>(); // Draws each token as one piece, sorted by distance
        group.sortingOrder = 3;

        // Frame + portrait, pivot at the bottom middle so the token stands on its feet
        SpriteRenderer frame = MakeRenderer("Frame", token.root.transform, FrameFor(data, owner), 0);
        frame.transform.localPosition = Vector3.zero;

        if (data.artwork == null && !string.IsNullOrEmpty(data.cardName))
        {
            // Placeholder cards have no art: show their first letter in the portrait
            TextMeshPro initial = MakeText("Initial", token.root.transform, data.cardName.Substring(0, 1), 5f, 1);
            Rect box = ClassFrames.PortraitBox(data.characterClass);
            initial.transform.localPosition = new Vector3(0f, (ClassFrames.BodyBottom - box.center.y) * UnitScale, 0f);
            initial.color = InkArt.GoldLight;
        }

        // Hit flash silhouette and defeat cracks in this class's shape, both hidden until needed
        (Sprite silhouette, Sprite cracks) = ShapesFor(data.characterClass);
        token.flash = MakeRenderer("Flash", token.root.transform, silhouette, 1);
        token.flash.color = Color.clear;
        token.crack = MakeRenderer("Cracks", token.root.transform, cracks, 1);
        token.crack.enabled = false;

        // Sage name ribbon across the bottom, cut front and notched tail like the UI art
        SpriteRenderer ribbon = MakeRenderer("Ribbon", token.root.transform, ribbonSprite, 2);
        ribbon.drawMode = SpriteDrawMode.Sliced;
        ribbon.size = new Vector2(tokenWidth * 1.16f, 0.17f);
        ribbon.transform.localPosition = new Vector3(0f, 0.156f, 0f);
        TextMeshPro name = MakeText("Name", token.root.transform, data.cardName, 1.15f, 3);
        name.transform.localPosition = ribbon.transform.localPosition + Vector3.up * 0.005f;
        name.rectTransform.sizeDelta = new Vector2(tokenWidth * 1.0f, 0.15f);
        name.enableAutoSizing = true;
        name.fontSizeMin = 0.5f;
        name.fontSizeMax = 1.15f;
        name.color = InkArt.Ink;
        name.outlineWidth = 0f;

        // Stat gems on the bottom corners: speed (gold) left, HP (red) right
        token.spdGem = MakeGem(token.root.transform, "Speed", InkArt.Gold, -tokenWidth * 0.42f, out token.spdText);
        token.hpGem = MakeGem(token.root.transform, "HP", InkArt.Damage, tokenWidth * 0.42f, out token.hpText);

        // Soft shadow on the board under the token (not part of the group, it lies flat)
        token.shadow = MakeRenderer($"Token Shadow ({unit.name})", null, shadowSprite, -19);
    }

    SpriteRenderer MakeGem(Transform parent, string label, Color color, float x, out TextMeshPro text)
    {
        SpriteRenderer gem = MakeRenderer(label + " Gem", parent, pillSprite, 4);
        gem.drawMode = SpriteDrawMode.Sliced;
        gem.color = color;
        gem.transform.localPosition = new Vector3(x, 0.02f, 0f);
        text = MakeText(label, parent, "", 1.3f, 5);
        text.transform.localPosition = gem.transform.localPosition + Vector3.up * 0.008f;
        return gem;
    }

    // ---- Every frame ----

    void UpdateToken(CardUnit unit, Token token)
    {
        if (token.root == null) return;
        CardInstance card = unit.Instance;
        Transform root = token.root.transform;

        // Defeated: once the killing blow lands (StartDefeat), the frame cracks and shakes, then shrinks away.
        // Without a StartDefeat call it starts on its own a moment later
        float jitter = 0f;
        if (!card.IsAlive)
        {
            if (token.noticedDead < 0f) token.noticedDead = Time.time;
            if (token.dying < 0f && Time.time > token.noticedDead + 1.2f) BeginDying(token);
            if (token.dying >= 0f)
            {
                float t = (Time.time - token.dying) / 0.55f;
                if (t < 0.35f) jitter = Mathf.Sin(Time.time * 70f) * 4f; // Cracked and trembling
                else token.scale = Mathf.Max(0f, 1f - Mathf.Pow((t - 0.35f) / 0.65f, 2f));
                if (token.scale <= 0f) { token.root.SetActive(false); token.shadow.gameObject.SetActive(false); return; }
            }
        }
        else
        {
            float target = CardHover.UnderMouse() == unit ? hoverScale : 1f;
            token.scale = Mathf.Lerp(token.scale, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }

        // Stands on the card's spot facing the camera; lifts with the card when hovered, dragged or bobbing
        Vector3 feet = Feet(unit);
        root.SetPositionAndRotation(feet, BoardView.CameraRotation * Quaternion.Euler(0f, 0f, jitter));
        root.localScale = Vector3.one * token.scale;

        // Hit flash: a white (or tinted) silhouette over the token that fades out
        if (token.flash != null)
        {
            float left = token.flashLength > 0f ? Mathf.Clamp01((token.flashUntil - Time.time) / token.flashLength) : 0f;
            Color colour = Color.Lerp(Color.white, token.flashColor, 0.35f);
            colour.a = left * 0.85f;
            token.flash.color = colour;
        }

        // HP: the previewed value while an attack is aimed at this unit, flashing
        int hp = card.currentHP;
        bool previewing = BoardHighlights.TryGetPredictedHP(card, out int predicted) && predicted != hp;
        if (previewing) hp = predicted;
        SetGemValue(token.hpGem, token.hpText, hp, ref token.shownHp);
        SetGemValue(token.spdGem, token.spdText, card.Speed, ref token.shownSpd);
        float flash = previewing ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f) : 0f;
        token.hpGem.color = Color.Lerp(previewing && predicted > card.currentHP ? InkArt.Heal : InkArt.Damage, Color.white, flash * 0.6f);

        // Shadow on the board: smaller and fainter the higher the token is lifted
        float floor = BoardFloor(unit);
        float lift = Mathf.Max(0f, feet.y - floor);
        Transform shadow = token.shadow.transform;
        shadow.SetPositionAndRotation(new Vector3(feet.x, floor + 0.013f, feet.z) + (BoardView.Right * 0.06f - BoardView.Up * 0.05f), BoardView.Flat);
        float size = tokenWidth * 1.25f * token.scale * (1f + lift * 0.15f);
        Vector2 spriteSize = shadowSprite.bounds.size;
        shadow.localScale = new Vector3(size / spriteSize.x, size * 0.7f / spriteSize.y, 1f);
        token.shadow.color = new Color(1f, 1f, 1f, shadowStrength * Mathf.Lerp(1f, 0.4f, lift));
    }

    void SetGemValue(SpriteRenderer gem, TextMeshPro text, int value, ref int shown)
    {
        if (value == shown) return;
        shown = value;
        text.text = value.ToString();
        float width = text.GetPreferredValues(text.text).x;
        gem.size = new Vector2(Mathf.Max(0.26f, width + 0.12f), 0.19f);
    }

    // Height of the board's surface (slots are all at the same height). Falls back to just under the card
    float BoardFloor(CardUnit unit)
    {
        if (grid == null) grid = FindAnyObjectByType<CardGrid>();
        Transform slot = grid != null ? grid.GetSlot(0, 0) : null;
        if (slot != null) return slot.position.y;
        float offset = CombatManager.Instance != null ? CombatManager.Instance.CardHeightOffset : 0.05f;
        return unit.transform.position.y - offset;
    }

    // ---- Constellation lines ----

    // Joins each team's units with the shortest set of lines that connects them all (a minimum spanning tree)
    void UpdateConstellations()
    {
        int used = 0;
        if (constellationLines && TurnManager.Instance != null && TurnManager.Instance.Phase != GamePhase.GameOver)
        {
            for (int owner = 1; owner <= 2; owner++)
            {
                List<Vector3> points = new List<Vector3>();
                foreach (KeyValuePair<CardUnit, Token> pair in tokens)
                {
                    if (pair.Key == null || !pair.Key.Instance.IsAlive || pair.Key.Instance.owner != owner) continue;
                    Vector3 feet = Feet(pair.Key);
                    points.Add(new Vector3(feet.x, BoardFloor(pair.Key) + 0.02f, feet.z));
                }

                // Prim's algorithm: grow the tree one nearest point at a time
                List<int> inTree = new List<int>();
                if (points.Count > 0) inTree.Add(0);
                while (inTree.Count < points.Count)
                {
                    float best = float.MaxValue;
                    int from = -1, to = -1;
                    for (int i = 0; i < points.Count; i++)
                    {
                        if (inTree.Contains(i)) continue;
                        foreach (int j in inTree)
                        {
                            float d = (points[i] - points[j]).sqrMagnitude;
                            if (d < best) { best = d; from = j; to = i; }
                        }
                    }
                    inTree.Add(to);
                    DrawLine(used++, points[from], points[to]);
                }
            }
        }

        for (int i = used; i < lines.Count; i++) lines[i].gameObject.SetActive(false);
    }

    // Each line is a row of gold dashes, each dash edged in ink (the colours are in the dash texture)
    void DrawLine(int index, Vector3 from, Vector3 to)
    {
        while (lines.Count <= index)
        {
            GameObject lineObject = new GameObject("Constellation");
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.textureMode = LineTextureMode.Tile;
            line.textureScale = new Vector2(6f, 1f);
            line.widthMultiplier = 0.07f;
            line.sortingOrder = -16;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.startColor = line.endColor = Color.white;
            if (lineMaterial.mainTexture == null) lineMaterial.mainTexture = MakeDash();
            lines.Add(line);
        }

        LineRenderer target = lines[index];
        target.gameObject.SetActive(true);
        // Stop short of the units so the line runs between them, not under them
        Vector3 gap = (to - from).normalized * tokenWidth * 0.45f;
        target.SetPosition(0, from + gap);
        target.SetPosition(1, to - gap);
    }

    // ---- Helpers ----

    SpriteRenderer MakeRenderer(string name, Transform parent, Sprite sprite, int order)
    {
        GameObject child = new GameObject(name);
        if (parent != null) child.transform.SetParent(parent, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = spriteMaterial;
        renderer.sortingOrder = order;
        return renderer;
    }

    TextMeshPro MakeText(string name, Transform parent, string value, float size, int order)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        TextMeshPro text = child.AddComponent<TextMeshPro>();
        GameFonts.UseInked(text, 0.32f);
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(tokenWidth, 0.3f);
        text.sortingOrder = order;
        return text;
    }

    // ---- Drawing ----

    // The class crown frame with the character's portrait inside, made once per card per team
    Sprite FrameFor(CardData data, int owner)
    {
        if (frames.TryGetValue((data, owner), out Sprite cached)) return cached;
        using PerfLog.Scope timer = PerfLog.Time($"Token frame {data.cardName}");
        return KeepFrame(data, owner, ClassFrames.PaintFrame(data.characterClass, InkArt.PlayerColor(owner), PortraitSampler(data), FramePixels));
    }

    Sprite KeepFrame(CardData data, int owner, Texture2D texture)
    {
        if (frames.TryGetValue((data, owner), out Sprite existing)) { Destroy(texture); return existing; } // Made another way meanwhile
        textures.Add(texture);
        Sprite sprite = InkArt.ToSprite(texture, ClassFrames.Pivot, FramePixels / UnitScale);
        frames[(data, owner)] = sprite;
        return sprite;
    }

    // Colour of the card's portrait at a point in crown units. The portrait has as many pixels as the frame has
    // inside the crown's portrait box
    public static System.Func<Vector2, Color> PortraitSampler(CardData data)
    {
        Rect box = ClassFrames.PortraitBox(data.characterClass);
        int portraitW = Mathf.CeilToInt(box.width * FramePixels), portraitH = Mathf.CeilToInt(box.height * FramePixels);
        Color[] portrait = ReadPortrait(data, portraitW, portraitH);
        return p =>
        {
            // Crown units (y down) to portrait pixels (y up)
            int px = Mathf.Clamp((int)((p.x - box.xMin) * FramePixels), 0, portraitW - 1);
            int py = Mathf.Clamp((int)((box.yMax - p.y) * FramePixels), 0, portraitH - 1);
            return portrait[py * portraitW + px];
        };
    }

    // Hit flash silhouette and defeat cracks for one class, made once
    (Sprite, Sprite) ShapesFor(CharacterClass characterClass)
    {
        if (classShapes.TryGetValue(characterClass, out (Sprite, Sprite) cached)) return cached;
        return KeepShapes(characterClass, ClassFrames.PaintSilhouette(characterClass, FramePixels), ClassFrames.PaintCracks(characterClass, FramePixels));
    }

    (Sprite, Sprite) KeepShapes(CharacterClass characterClass, Texture2D silhouette, Texture2D cracks)
    {
        if (classShapes.TryGetValue(characterClass, out (Sprite, Sprite) existing)) { Destroy(silhouette); Destroy(cracks); return existing; }
        textures.Add(silhouette);
        textures.Add(cracks);
        (Sprite, Sprite) shapes = (InkArt.ToSprite(silhouette, ClassFrames.Pivot, FramePixels / UnitScale),
                                   InkArt.ToSprite(cracks, ClassFrames.Pivot, FramePixels / UnitScale));
        classShapes[characterClass] = shapes;
        return shapes;
    }

    // Makes drafted cards' frames and their class shapes in the background, a few milliseconds per frame, so the
    // draft never stutters (WebGL has one thread: a whole frame at once froze the game for a moment)
    IEnumerator PrewarmLoop()
    {
        InkArt.FrameBudget budget = new InkArt.FrameBudget(prewarmMsPerFrame);
        while (true)
        {
            if (prewarm.Count == 0) { yield return null; continue; }
            (CardData data, int owner) = prewarm.Dequeue();
            if (data == null) continue;
            budget.Restart();

            if (!frames.ContainsKey((data, owner)))
            {
                System.Func<Vector2, Color> portrait = PortraitSampler(data); // One small GPU read, then wait a frame
                yield return null;
                budget.Restart();
                Texture2D frame = null;
                yield return ClassFrames.PaintFrameRoutine(data.characterClass, InkArt.PlayerColor(owner), portrait, FramePixels, budget, t => frame = t);
                KeepFrame(data, owner, frame);
            }

            if (!classShapes.ContainsKey(data.characterClass))
            {
                Texture2D silhouette = null, cracks = null;
                yield return ClassFrames.SilhouetteRoutine(data.characterClass, FramePixels, budget, t => silhouette = t);
                yield return ClassFrames.CracksRoutine(data.characterClass, FramePixels, budget, t => cracks = t);
                KeepShapes(data.characterClass, silhouette, cracks);
            }
            yield return null;
        }
    }

    // Crops this card's portrait out of its art (see PortraitCrop) and shrinks it to the portrait size on the GPU,
    // then reads it back. Works on art that isn't marked readable. No art = a night-sky gradient
    static Color[] ReadPortrait(CardData data, int width, int height)
    {
        Color[] result = new Color[width * height];
        Sprite art = data.artwork;
        if (art == null)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    result[y * width + x] = Color.Lerp(InkArt.Night, InkArt.Navy, y / (float)height);
            return result;
        }

        Rect rect = art.textureRect;
        Texture source = art.texture;
        Rect crop = PortraitCrop(data, rect.width / rect.height, width / (float)height);
        Vector2 scale = new Vector2(rect.width / source.width * crop.width, rect.height / source.height * crop.height);
        Vector2 offset = new Vector2((rect.x + rect.width * crop.x) / source.width, (rect.y + rect.height * crop.y) / source.height);

        RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, target, scale, offset);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D read = new Texture2D(width, height, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        read.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);

        result = read.GetPixels();
        Destroy(read);
        return result;
    }

    // The part of the art the portrait shows (0-1 from the art's bottom-left): portraitZoom of the art's width,
    // centred on portraitFocus, kept inside the art
    public static Rect PortraitCrop(CardData data, float artAspect, float portraitAspect)
    {
        float cropW = Mathf.Clamp(data.portraitZoom, 0.05f, 1f);
        float cropH = cropW * artAspect / portraitAspect;
        if (cropH > 1f) { cropW /= cropH; cropH = 1f; } // Taller than the art: zoom in until it fits
        float x = Mathf.Clamp(data.portraitFocus.x - cropW / 2f, 0f, 1f - cropW);
        float y = Mathf.Clamp(data.portraitFocus.y - cropH / 2f, 0f, 1f - cropH);
        return new Rect(x, y, cropW, cropH);
    }

    // White pill with an ink outline, tinted per gem. 9-sliced so it stretches to fit the number
    Sprite MakePill()
    {
        const int w = 64, h = 40;
        Texture2D texture = InkArt.Paint(w, h, p =>
            InkArt.Outlined(InkArt.Chamfer(p, new Vector2(w / 2f, h / 2f), new Vector2(w / 2f - 1f, h / 2f - 1f), 13f, 5f), 5f, Color.white));
        textures.Add(texture);
        return InkArt.ToSprite(texture, new Vector2(0.5f, 0.5f), h / 0.19f, 20f);
    }

    // Sage ribbon with a cut front corner, a notched tail and an ink outline
    Sprite MakeRibbon()
    {
        const int w = 128, h = 36;
        Texture2D texture = InkArt.Paint(w, h, p =>
        {
            // Like the artist's ribbons: the front end has a cut top corner, the tail has a V notch
            float box = InkArt.Chamfer(p, new Vector2(w / 2f, h / 2f), new Vector2(w / 2f - 1f, h / 2f - 1f), 10f, 0f);
            float notch = 9f * (1f - Mathf.Abs(p.y - h / 2f) / (h / 2f)) - (w - p.x);
            float edge = Mathf.Max(box, notch);
            return InkArt.Outlined(edge, 4f, InkArt.Sage);
        });
        textures.Add(texture);
        return InkArt.ToSprite(texture, new Vector2(0.5f, 0.5f), h / 0.17f, 16f);
    }

    // Soft round shadow, darker in the middle
    Sprite MakeShadow()
    {
        const int size = 64;
        Texture2D texture = InkArt.Paint(size, size, p =>
        {
            float d = (p - new Vector2(size / 2f, size / 2f)).magnitude / (size / 2f);
            Color c = InkArt.Ink;
            c.a = Mathf.Clamp01(1f - d) * 0.9f;
            return c;
        }, 1);
        textures.Add(texture);
        return InkArt.ToSprite(texture, new Vector2(0.5f, 0.5f), size);
    }

    // Repeating dash for the constellation lines: a gold dash edged in ink, then a gap
    Texture2D MakeDash()
    {
        const int w = 16, h = 8;
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                bool dash = x >= 1 && x < 10;
                bool edge = y < 2 || y >= h - 2 || x == 1 || x == 9;
                texture.SetPixel(x, y, !dash ? Color.clear : edge ? InkArt.Ink : InkArt.GoldLight);
            }
        texture.Apply();
        textures.Add(texture);
        return texture;
    }
}
