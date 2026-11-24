using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The main menu's background ("Champions on a Star Chart sky"), all built in code behind the menu UI:
// - the battle's swirling space background and post-processing (SpaceBackground, ScreenLook)
// - astrolabe rings turning behind the menu, and a gold class constellation in each corner that traces itself in,
//   holds, then fades, one after another
// - a champion on each side (blue team left, red team right): a crown token of a real card on a glowing pedestal
//   with its name and class underneath. Every few seconds it shrinks away in an ink splash and the next card pops in.
//   Hovering Play makes both champions lean in toward the menu, ready to fight
// - drifting gold motes and the odd shooting star
// - the STRATAFIELD logo inks in with a splash, then floats with a shimmer passing over it every few seconds
// It's drawn on its own canvas behind the menu's canvas, so the menu's own objects don't change (apart from its
// see-through white panel being made clear). Added automatically when MainMenuScene loads
public class MenuBackdrop : MonoBehaviour
{
    const string MenuScene = "MainMenuScene";
    const float Width = 1600f, Height = 900f; // Layout is in these units (16:9), scaled to the screen

    [Header("Champions")]
    [SerializeField] private float swapSeconds = 4.5f;   // How long each champion stays before the next pops in
    [SerializeField] private float championWidth = 170f; // Crown body width in layout units
    [SerializeField] private float edgeInset = 165f;     // Champion centre from the left/right edge
    [SerializeField] private float feetFromTop = 563f;   // Where champions stand (from the top)

    [Header("Logo")]
    [SerializeField] private float logoRaise = 40f;      // Moves the logo up from where the scene puts it (pixels)
    [SerializeField] private float logoFloat = 5f;       // Pixels up and down
    [SerializeField] private float shimmerEvery = 5f;    // Seconds between shimmers

    // ---- Installing ----

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded += (scene, mode) => Install(scene);
        Install(SceneManager.GetActiveScene());
    }

    static void Install(Scene scene)
    {
        if (scene.name != MenuScene || FindAnyObjectByType<MenuBackdrop>() != null) return;
        new GameObject("Menu Backdrop").AddComponent<MenuBackdrop>();
    }

    // ---- State ----

    private Camera cam;
    private RectTransform layer;
    private readonly List<Texture2D> textures = new List<Texture2D>();
    private readonly List<(RectTransform ring, float speed)> rings = new List<(RectTransform, float)>();
    private Sprite dotSprite, lineSprite, glowSprite;
    private ParticleSystem shootingStars;
    private float nextShootingStar;
    private static bool playHovered;
    private float lean; // 0-1, eases toward 1 while Play is hovered

    class Champion
    {
        public RectTransform root, art;
        public Image token, glow, pedestal, splash;
        public TextMeshProUGUI name, type;
        public int side; // -1 left (blue), 1 right (red)
        public Vector2 home;
        public float phase;
    }
    private readonly Champion[] champions = new Champion[2];

    void Start()
    {
        cam = Camera.main;
        if (cam == null) return;
        cam.fieldOfView = 35f;
        cam.farClipPlane = 200f;

        ClearMenuHaze();
        gameObject.AddComponent<SpaceBackground>(); // The same swirling space as the battle
        gameObject.AddComponent<ScreenLook>();      // Bloom, vignette and colour grading
        HookPlayButton();

        dotSprite = MakeSprite(Paint(32, 32, p => new Color(1f, 1f, 1f, (p - new Vector2(16f, 16f)).magnitude < 14f ? 1f : 0f), 3));
        lineSprite = MakeSprite(Paint(4, 4, p => Color.white, 1));
        glowSprite = MakeSprite(Paint(128, 128, p =>
        {
            float d = (p - new Vector2(64f, 64f)).magnitude / 64f;
            return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f));
        }, 1));

        BuildLayer();
        BuildRings();
        BuildConstellations();
        BuildChampions();
        BuildMotes();
        StartCoroutine(RunChampions());
        StartCoroutine(AnimateLogo());
    }

    void OnDestroy()
    {
        foreach (Texture2D texture in textures) if (texture != null) Destroy(texture);
        playHovered = false;
    }

    void Update()
    {
        foreach ((RectTransform ring, float speed) in rings) ring.Rotate(0f, 0f, speed * Time.deltaTime);

        // Champions bob, and lean in toward the menu while Play is hovered
        lean = Mathf.MoveTowards(lean, playHovered ? 1f : 0f, Time.deltaTime * 4f);
        float eased = Mathf.SmoothStep(0f, 1f, lean);
        foreach (Champion champion in champions)
        {
            if (champion == null) continue;
            float bob = 12f * Mathf.Sin(Time.time * 1.5f + champion.phase);
            champion.root.anchoredPosition = champion.home + new Vector2(-champion.side * 22f * eased, 0f);
            champion.art.anchoredPosition = new Vector2(0f, bob);
            champion.art.localRotation = Quaternion.Euler(0f, 0f, champion.side * 7f * eased);
            float glowBoost = 1f + 0.7f * eased;
            champion.glow.color = WithAlpha(champion.glow.color, 0.3f * glowBoost);
            champion.pedestal.color = WithAlpha(champion.pedestal.color, 0.55f * glowBoost);
        }

        if (shootingStars != null && Time.time >= nextShootingStar)
        {
            nextShootingStar = Time.time + Random.Range(4f, 9f);
            ShootingStar();
        }
    }

    // ---- The layer behind the menu ----

    // A camera canvas: drawn in front of the space background but behind the menu's own (overlay) canvas
    void BuildLayer()
    {
        GameObject canvasObject = new GameObject("Backdrop Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 6f;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(Width, Height);
        scaler.matchWidthOrHeight = 0.5f;
        layer = (RectTransform)canvasObject.transform;
    }

    // A group pinned to a point on the screen (0-1), so corners stay in the corners on any screen shape
    RectTransform Group(string name, Vector2 anchor)
    {
        RectTransform group = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        group.SetParent(layer, false);
        group.anchorMin = group.anchorMax = anchor;
        group.pivot = anchor;
        group.sizeDelta = Vector2.zero;
        return group;
    }

    // Layout point (from the top-left, as in the mockup) relative to a group's anchor
    static Vector2 FromAnchor(Vector2 anchor, float x, float y) => new Vector2(x - anchor.x * Width, (1f - anchor.y) * Height - y);

    Image MakeImage(string name, RectTransform parent, Sprite sprite, Vector2 position, Vector2 size, Color colour)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        image.sprite = sprite;
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    TextMeshProUGUI MakeText(string name, RectTransform parent, string value, Vector2 position, float size, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = new Vector2(320f, 50f);
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    // ---- Astrolabe rings ----

    void BuildRings()
    {
        RectTransform centre = Group("Rings", new Vector2(0.5f, 0.5f));
        centre.anchoredPosition = new Vector2(0f, -20f); // Centred just below the middle, behind the buttons
        (float radius, float speed, Color colour, float width, int dashes, float dashOn)[] specs =
        {
            (345f, 4f, new Color(0.91f, 0.78f, 0.43f, 0.55f), 2.2f, 0, 1f),
            (327f, 4f, new Color(0.91f, 0.78f, 0.43f, 0.4f), 2f, 120, 0.13f),
            (420f, -2.6f, new Color(0.73f, 0.81f, 0.78f, 0.3f), 2.4f, 48, 0.62f),
            (260f, 0f, new Color(0.91f, 0.78f, 0.43f, 0.18f), 1.4f, 0, 1f),
        };
        foreach ((float radius, float speed, Color colour, float width, int dashes, float dashOn) in specs)
        {
            // One sample per pixel with the edge softened by distance (smooth, and cheap enough not to stall WebGL)
            const int size = 512;
            float scale = size / (radius * 2f + 8f);
            float r = radius * scale, half = Mathf.Max(0.6f, width * scale * 0.5f);
            Texture2D texture = Paint(size, size, p =>
            {
                Vector2 d = p - new Vector2(size / 2f, size / 2f);
                float alpha = Mathf.Clamp01(half + 0.5f - Mathf.Abs(d.magnitude - r));
                if (alpha <= 0f) return Color.clear;
                if (dashes > 0)
                {
                    float t = (Mathf.Atan2(d.y, d.x) / (Mathf.PI * 2f) + 1f) * dashes % 1f;
                    if (t > dashOn) return Color.clear;
                }
                return new Color(1f, 1f, 1f, alpha);
            }, 1);
            Image ring = MakeImage($"Ring {radius}", centre, MakeSprite(texture), Vector2.zero, Vector2.one * (size / scale), colour);
            rings.Add((ring.rectTransform, speed));
        }
    }

    // ---- Constellations ----

    void BuildConstellations()
    {
        // Points and labels in layout units (from the top-left), one per corner
        (Vector2 anchor, Vector2[] points, bool closed, string label, Vector2 labelAt, TextAlignmentOptions align)[] specs =
        {
            (new Vector2(0f, 1f), new[] { new Vector2(90, 120), new Vector2(170, 80), new Vector2(240, 140), new Vector2(190, 210) }, true, "ATTACKER", new Vector2(110, 250), TextAlignmentOptions.Left),
            (new Vector2(1f, 1f), new[] { new Vector2(1360, 90), new Vector2(1440, 140), new Vector2(1500, 80), new Vector2(1545, 170) }, false, "MAGE", new Vector2(1400, 210), TextAlignmentOptions.Left),
            (new Vector2(0f, 0f), new[] { new Vector2(70, 800), new Vector2(140, 740), new Vector2(220, 790), new Vector2(170, 860) }, false, "HEALER", new Vector2(100, 720), TextAlignmentOptions.Left),
            (new Vector2(1f, 0f), new[] { new Vector2(1380, 760), new Vector2(1450, 810), new Vector2(1530, 760), new Vector2(1500, 850), new Vector2(1420, 850) }, false, "SUPPORT", new Vector2(1400, 735), TextAlignmentOptions.Left),
        };

        for (int c = 0; c < specs.Length; c++)
        {
            var spec = specs[c];
            RectTransform group = Group("Constellation " + spec.label, spec.anchor);
            CanvasGroup fade = group.gameObject.AddComponent<CanvasGroup>();
            List<Image> lines = new List<Image>();
            int count = spec.closed ? spec.points.Length : spec.points.Length - 1;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = FromAnchor(spec.anchor, spec.points[i].x, spec.points[i].y);
                Vector2 b = FromAnchor(spec.anchor, spec.points[(i + 1) % spec.points.Length].x, spec.points[(i + 1) % spec.points.Length].y);
                Image line = MakeImage("Line", group, lineSprite, a, new Vector2((b - a).magnitude, 2.2f), new Color(0.91f, 0.78f, 0.43f, 0.9f));
                line.rectTransform.pivot = new Vector2(0f, 0.5f);
                line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                line.type = Image.Type.Filled;
                line.fillMethod = Image.FillMethod.Horizontal;
                line.fillAmount = 0f;
                lines.Add(line);
            }

            // Stars stay visible (and twinkle) while the lines draw in and out
            foreach (Vector2 point in spec.points)
            {
                Image star = MakeImage("Star", layer, dotSprite, Vector2.zero, Vector2.one * Random.Range(9f, 13f), new Color(0.96f, 0.96f, 0.94f, 1f));
                star.rectTransform.SetParent(group, false);
                star.rectTransform.anchoredPosition = FromAnchor(spec.anchor, point.x, point.y);
                if (Random.value < 0.4f) StartCoroutine(Twinkle(star));
            }

            TextMeshProUGUI label = MakeText("Label", group, spec.label, Vector2.zero, 16f, spec.align);
            GameFonts.UseTitle(label, 0f);
            label.color = new Color(0.91f, 0.78f, 0.43f, 0.8f);
            label.characterSpacing = 18f;
            label.rectTransform.pivot = new Vector2(0f, 0.5f);
            label.rectTransform.anchoredPosition = FromAnchor(spec.anchor, spec.labelAt.x, spec.labelAt.y - 6f);

            StartCoroutine(TraceLoop(lines, fade, c * 2.5f));
        }
    }

    // Draw each line in turn, hold, fade out, repeat (every 10 seconds)
    IEnumerator TraceLoop(List<Image> lines, CanvasGroup fade, float delay)
    {
        yield return new WaitForSeconds(delay);
        while (true)
        {
            fade.alpha = 1f;
            float perLine = 4f / Mathf.Max(1, lines.Count);
            foreach (Image line in lines)
            {
                for (float t = 0f; t < perLine; t += Time.deltaTime) { line.fillAmount = t / perLine; yield return null; }
                line.fillAmount = 1f;
            }
            yield return new WaitForSeconds(3f);
            for (float t = 0f; t < 1.5f; t += Time.deltaTime) { fade.alpha = 1f - t / 1.5f; yield return null; }
            foreach (Image line in lines) line.fillAmount = 0f;
            fade.alpha = 1f;
            yield return new WaitForSeconds(1.5f);
        }
    }

    IEnumerator Twinkle(Image star)
    {
        float speed = Random.Range(1.5f, 2.5f), offset = Random.Range(0f, 6f);
        while (star != null)
        {
            star.color = WithAlpha(star.color, 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(Time.time * speed + offset)));
            yield return null;
        }
    }

    // ---- Champions ----

    void BuildChampions()
    {
        for (int i = 0; i < 2; i++)
        {
            int side = i == 0 ? -1 : 1;
            Vector2 anchor = new Vector2(i, 0.5f);
            RectTransform group = Group(side < 0 ? "Blue Champion" : "Red Champion", anchor);
            float x = side < 0 ? edgeInset : Width - edgeInset;
            Color team = InkArt.PlayerColor(side < 0 ? 1 : 2);

            Champion champion = new Champion { side = side, phase = i * 2.1f };
            champion.root = new GameObject("Champion", typeof(RectTransform)).GetComponent<RectTransform>();
            champion.root.SetParent(group, false);
            champion.home = FromAnchor(anchor, x, feetFromTop);
            champion.root.anchoredPosition = champion.home;

            // Team glow behind, gold pedestal under the feet
            champion.glow = MakeImage("Team Glow", champion.root, glowSprite, new Vector2(0f, 93f), new Vector2(400f, 380f), WithAlpha(team, 0.3f));
            champion.pedestal = MakeImage("Pedestal", champion.root, glowSprite, new Vector2(0f, -5f), new Vector2(270f, 48f), new Color(0.91f, 0.78f, 0.43f, 0.55f));

            champion.art = new GameObject("Art", typeof(RectTransform)).GetComponent<RectTransform>();
            champion.art.SetParent(champion.root, false);
            float scale = championWidth / ClassFrames.BodyWidth;
            champion.token = MakeImage("Token", champion.art, null, Vector2.zero,
                new Vector2(ClassFrames.CanvasWidth, ClassFrames.CanvasHeight) * scale, Color.white);
            champion.token.rectTransform.pivot = ClassFrames.Pivot; // So the position is where it stands
            champion.token.preserveAspect = true;
            champion.token.enabled = false;

            champion.splash = MakeImage("Ink Splash", champion.root, dotSprite, new Vector2(0f, 113f), Vector2.one * 170f, WithAlpha(InkArt.Ink, 0f));

            champion.name = MakeText("Name", champion.root, "", new Vector2(0f, -51f), 30f, TextAlignmentOptions.Center);
            GameFonts.UseInked(champion.name, 0.3f);
            champion.type = MakeText("Class", champion.root, "", new Vector2(0f, -78f), 15f, TextAlignmentOptions.Center);
            GameFonts.UseTitle(champion.type, 0f);
            champion.type.characterSpacing = 16f;

            champions[i] = champion;
        }
    }

    // Shows the next card on each side every few seconds: shrink + ink splash, then pop in
    IEnumerator RunChampions()
    {
        List<CardData> cards = new List<CardData>();
        GameArt art = GameArt.Asset;
        if (art != null && art.showcaseCards != null) foreach (CardData card in art.showcaseCards) if (card != null) cards.Add(card);
        if (cards.Count == 0) yield break;
        for (int i = 0; i < cards.Count; i++) { int j = Random.Range(i, cards.Count); (cards[i], cards[j]) = (cards[j], cards[i]); }

        // Left takes every other card, right the rest (both sides share if there's only one)
        List<CardData>[] lists = { new List<CardData>(), new List<CardData>() };
        for (int i = 0; i < cards.Count; i++) lists[i % 2].Add(cards[i]);
        if (lists[1].Count == 0) lists[1].AddRange(lists[0]);

        Dictionary<(CardData, int), Sprite> frames = new Dictionary<(CardData, int), Sprite>();
        InkArt.FrameBudget budget = new InkArt.FrameBudget(4f);
        int[] next = { 0, 0 };
        bool first = true;

        while (true)
        {
            // Make the next frames (a few milliseconds per frame, so nothing stutters)
            for (int i = 0; i < 2; i++)
            {
                CardData card = lists[i][next[i] % lists[i].Count];
                int owner = i == 0 ? 1 : 2;
                if (frames.ContainsKey((card, owner))) continue;
                System.Func<Vector2, Color> portrait = UnitTokens.PortraitSampler(card);
                yield return null;
                budget.Restart();
                Texture2D frame = null;
                yield return ClassFrames.PaintFrameRoutine(card.characterClass, InkArt.PlayerColor(owner), portrait, UnitTokens.FramePixels, budget, t => frame = t);
                textures.Add(frame);
                frames[(card, owner)] = Sprite.Create(frame, new Rect(0, 0, frame.width, frame.height), ClassFrames.Pivot, 100f);
            }

            for (int i = 0; i < 2; i++)
            {
                CardData card = lists[i][next[i] % lists[i].Count];
                StartCoroutine(Swap(champions[i], card, frames[(card, i == 0 ? 1 : 2)], !first));
                next[i]++;
            }
            first = false;
            yield return new WaitForSeconds(swapSeconds);
        }
    }

    IEnumerator Swap(Champion champion, CardData card, Sprite frame, bool shrinkFirst)
    {
        RectTransform token = champion.token.rectTransform;
        if (shrinkFirst && champion.token.enabled)
        {
            StartCoroutine(Splash(champion.splash));
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                float p = t / 0.22f;
                token.localScale = Vector3.one * (1f - p * p);
                champion.name.alpha = champion.type.alpha = 1f - p;
                yield return null;
            }
        }

        champion.token.sprite = frame;
        champion.token.enabled = true;
        champion.name.text = card.cardName.ToUpper();
        champion.type.text = card.characterClass.ToString().ToUpper();
        champion.type.color = ClassFrames.ColourOf(card.characterClass);

        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            float p = t / 0.4f;
            float overshoot = 1f + 0.12f * Mathf.Sin(p * Mathf.PI); // Pops a little past full size, then settles
            token.localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, p) * overshoot;
            champion.name.alpha = champion.type.alpha = p;
            yield return null;
        }
        token.localScale = Vector3.one;
        champion.name.alpha = champion.type.alpha = 1f;
    }

    IEnumerator Splash(Image splash)
    {
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float p = t / 0.5f;
            splash.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, 1.4f, p);
            splash.color = WithAlpha(splash.color, 0.55f * (1f - p));
            yield return null;
        }
        splash.color = WithAlpha(splash.color, 0f);
    }

    // ---- Play button reaction ----

    void HookPlayButton()
    {
        foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include))
            if (button.name.Contains("Play") && button.GetComponent<PlayHover>() == null) button.gameObject.AddComponent<PlayHover>();
    }

    // Tells the backdrop when the mouse is over Play
    class PlayHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => playHovered = true;
        public void OnPointerExit(PointerEventData eventData) => playHovered = false;
        void OnDisable() => playHovered = false;
    }

    // ---- Motes and shooting stars ----

    void BuildMotes()
    {
        Material glow = Resources.Load<Material>("VFX/VFX_Glow");
        if (glow == null) return;
        Texture2D dot = Paint(32, 32, p =>
        {
            float d = (p - new Vector2(16f, 16f)).magnitude / 16f;
            return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - d), 2f));
        }, 1);
        Material material = new Material(glow);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", dot);
        material.mainTexture = dot;

        // Gold dust drifting up across the whole screen (well behind the champions)
        ParticleSystem motes = MakeParticles("Motes", material);
        ParticleSystem.MainModule main = motes.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 15f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
        main.startColor = new ParticleSystem.MinMaxGradient(InkArt.GoldLight, new Color(0.85f, 0.95f, 1f));
        main.maxParticles = 160;
        main.prewarm = true;
        ParticleSystem.EmissionModule emission = motes.emission;
        emission.rateOverTime = 12f;
        ParticleSystem.ShapeModule shape = motes.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(24f, 10f, 4f);
        shape.position = cam.transform.position + cam.transform.forward * 14f - Vector3.up * 3f;
        ParticleSystem.VelocityOverLifetimeModule velocity = motes.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        FadeInAndOut(motes);
        motes.Play();

        // Shooting stars high up, fired now and then (Update)
        shootingStars = MakeParticles("Shooting Stars", material);
        ParticleSystem.MainModule starMain = shootingStars.main;
        starMain.startLifetime = 0.9f;
        starMain.startSize = 0.14f;
        starMain.maxParticles = 4;
        ParticleSystem.EmissionModule starEmission = shootingStars.emission;
        starEmission.rateOverTime = 0f;
        ParticleSystem.ShapeModule starShape = shootingStars.shape;
        starShape.enabled = false;
        ParticleSystemRenderer starRenderer = shootingStars.GetComponent<ParticleSystemRenderer>();
        starRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        starRenderer.velocityScale = 0.12f;
        starRenderer.lengthScale = 2f;
        FadeInAndOut(shootingStars);
        shootingStars.Play(); // Emits nothing on its own; running so stars fired with Emit move and fade
        nextShootingStar = Time.time + 2.5f;
    }

    ParticleSystem MakeParticles(string name, Material material)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(transform, false);
        ParticleSystem particles = holder.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        return particles;
    }

    static void FadeInAndOut(ParticleSystem particles)
    {
        ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
        colour.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        colour.color = gradient;
    }

    void ShootingStar()
    {
        // Somewhere high and far back, streaking down and sideways
        Vector3 start = cam.transform.position + cam.transform.forward * 18f + new Vector3(Random.Range(-8f, 8f), Random.Range(2.5f, 4.5f), 0f);
        Vector3 direction = new Vector3(Random.value < 0.5f ? -1f : 1f, -0.45f, 0f).normalized;
        shootingStars.Emit(new ParticleSystem.EmitParams { position = start, velocity = direction * Random.Range(9f, 13f), startColor = new Color(1f, 0.95f, 0.8f) }, 1);
    }

    // ---- The menu's own UI ----

    // The menu's full-screen panel is Unity's default see-through white, which washed everything out. Make it clear
    // (it stays in place, so it still works as the menu's container and catches clicks the same as before)
    static void ClearMenuHaze()
    {
        foreach (Image image in FindObjectsByType<Image>(FindObjectsInactive.Include))
        {
            RectTransform rect = image.rectTransform;
            bool fullScreen = rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one;
            bool hazy = image.color.a < 0.95f && image.color.r > 0.9f && image.color.g > 0.9f && image.color.b > 0.9f;
            if (image.name == "MainMenu" || (fullScreen && hazy && image.sprite == null))
                image.color = WithAlpha(image.color, 0f);
        }
    }

    IEnumerator AnimateLogo()
    {
        yield return null; // Let the menu UI finish setting up
        Image logo = FindLogo();
        if (logo == null) yield break;
        RectTransform rect = logo.rectTransform;
        Vector2 basePosition = rect.anchoredPosition + Vector2.up * logoRaise;
        rect.anchoredPosition = basePosition;
        Vector3 baseScale = rect.localScale;
        CanvasGroup fade = logo.GetComponent<CanvasGroup>();
        if (fade == null) fade = logo.gameObject.AddComponent<CanvasGroup>();

        // Ink splash behind it, then the logo stamps in
        SplashBehind(rect);
        for (float t = 0f; t < 0.7f; t += Time.deltaTime)
        {
            float p = t / 0.7f;
            float back = 1f + 2.2f * Mathf.Pow(p - 1f, 3f) + 1.2f * Mathf.Pow(p - 1f, 2f); // Ease out with a small overshoot
            rect.localScale = baseScale * Mathf.LerpUnclamped(1.35f, 1f, back);
            fade.alpha = Mathf.Clamp01(p * 2f);
            yield return null;
        }
        rect.localScale = baseScale;
        fade.alpha = 1f;

        // Shimmer: a bright band clipped to the logo's shape
        RectTransform shimmer = MakeShimmer(logo);
        float nextShimmer = Time.time + 1.2f;
        while (logo != null)
        {
            rect.anchoredPosition = basePosition + Vector2.up * (logoFloat * Mathf.Sin(Time.time * 1.4f));
            if (shimmer != null && Time.time >= nextShimmer)
            {
                nextShimmer = Time.time + shimmerEvery;
                StartCoroutine(SweepShimmer(shimmer, rect.rect.width));
            }
            yield return null;
        }
    }

    // The menu's own logo (not the copy inside the options panel)
    static Image FindLogo()
    {
        Image best = null;
        foreach (Image image in FindObjectsByType<Image>(FindObjectsInactive.Exclude))
            if (image.name == "Stratafield Logo" && image.isActiveAndEnabled && (best == null || image.rectTransform.rect.width > best.rectTransform.rect.width))
                best = image;
        return best;
    }

    void SplashBehind(RectTransform logo)
    {
        Sprite sprite = MakeSprite(Paint(64, 64, p =>
        {
            Vector2 d = p - new Vector2(32f, 32f);
            float wobble = 26f + 3f * Mathf.Sin(Mathf.Atan2(d.y, d.x) * 5f);
            return d.magnitude < wobble ? Color.white : Color.clear;
        }, 2));
        Color[] colours = { InkArt.Ink, InkArt.Navy, InkArt.Sage, InkArt.GoldLight };

        for (int i = 0; i < 14; i++)
        {
            GameObject drop = new GameObject("Ink Drop", typeof(RectTransform), typeof(Image));
            drop.transform.SetParent(logo.parent, false);
            drop.transform.SetSiblingIndex(logo.GetSiblingIndex()); // Behind the logo
            Image image = drop.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            Color colour = colours[i % colours.Length];
            image.color = colour;
            RectTransform rect = (RectTransform)drop.transform;
            rect.anchorMin = rect.anchorMax = logo.anchorMin;
            rect.anchoredPosition = logo.anchoredPosition + new Vector2(Random.Range(-0.3f, 0.3f) * logo.rect.width, Random.Range(-20f, 20f));
            float size = Random.Range(18f, 54f);
            rect.sizeDelta = new Vector2(size, size);
            StartCoroutine(Splat(rect, image, colour, Random.insideUnitCircle.normalized * Random.Range(90f, 260f)));
        }
    }

    IEnumerator Splat(RectTransform drop, Image image, Color colour, Vector2 fly)
    {
        Vector2 start = drop.anchoredPosition;
        Vector2 size = drop.sizeDelta;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            float p = t / 0.8f;
            drop.anchoredPosition = start + fly * (1f - Mathf.Pow(1f - p, 3f));
            drop.sizeDelta = size * Mathf.Lerp(1.4f, 0.4f, p);
            image.color = WithAlpha(colour, 1f - p * p);
            yield return null;
        }
        Destroy(drop.gameObject);
    }

    RectTransform MakeShimmer(Image logo)
    {
        if (logo.GetComponent<Mask>() == null) logo.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        Texture2D band = Paint(64, 8, p =>
        {
            float x = Mathf.Abs(p.x / 64f - 0.5f) * 2f;
            return new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - x), 2f) * 0.75f);
        }, 1);

        GameObject shine = new GameObject("Shimmer", typeof(RectTransform), typeof(RawImage));
        shine.transform.SetParent(logo.transform, false);
        RawImage image = shine.GetComponent<RawImage>();
        image.texture = band;
        image.raycastTarget = false;
        RectTransform rect = (RectTransform)shine.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(logo.rectTransform.rect.width * 0.18f, logo.rectTransform.rect.height * 0.6f);
        rect.localRotation = Quaternion.Euler(0f, 0f, -18f);
        rect.anchoredPosition = new Vector2(-9999f, 0f);
        return rect;
    }

    IEnumerator SweepShimmer(RectTransform shimmer, float width)
    {
        for (float t = 0f; t < 0.85f; t += Time.deltaTime)
        {
            if (shimmer == null) yield break;
            shimmer.anchoredPosition = new Vector2(Mathf.Lerp(-width * 0.2f, width * 1.2f, Mathf.SmoothStep(0f, 1f, t / 0.85f)), 0f);
            yield return null;
        }
        if (shimmer != null) shimmer.anchoredPosition = new Vector2(-9999f, 0f);
    }

    // ---- Helpers ----

    Texture2D Paint(int width, int height, System.Func<Vector2, Color> colourAt, int samples)
    {
        Texture2D texture = InkArt.Paint(width, height, colourAt, samples);
        textures.Add(texture);
        return texture;
    }

    static Sprite MakeSprite(Texture2D texture) =>
        Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);

    static Color WithAlpha(Color colour, float alpha) { colour.a = alpha; return colour; }
}
