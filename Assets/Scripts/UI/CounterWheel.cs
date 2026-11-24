using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The class counter wheel: each class deals x1.25 damage to the next two classes around the ring
//   Assassin > Mage > Healer > Defender > Attacker > Ranged > Support > (Assassin)
// A CLASSES button under the top-left panel (or the C key) opens it as a popup. Hover a class to see its matchups:
// green arrows to the classes it beats, red arrows from the classes that beat it, everything else dims.
// It opens by itself the first time someone drafts. While it's open, gameplay input waits (InputGate.IsPaused).
// CombatManager adds it automatically
public class CounterWheel : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    // Wheel order: each class beats the next two (keep in step with the class bonuses on the attacks)
    public static readonly CharacterClass[] Order =
        { CharacterClass.Assassin, CharacterClass.Mage, CharacterClass.Healer, CharacterClass.Defender, CharacterClass.Attacker, CharacterClass.Ranged, CharacterClass.Support };
    public const float Multiplier = 1.25f;

    public static bool Beats(CharacterClass attacker, CharacterClass target)
    {
        int a = System.Array.IndexOf(Order, attacker), t = System.Array.IndexOf(Order, target);
        return a >= 0 && t >= 0 && (t == (a + 1) % Order.Length || t == (a + 2) % Order.Length);
    }

    const string SeenKey = "Stratafield.SeenCounterWheel";
    const float Radius = 215f;
    static readonly Color Gold = new Color(0.91f, 0.78f, 0.43f), Strong = new Color(0.43f, 0.88f, 0.48f), Weak = new Color(1f, 0.42f, 0.42f);

    private RectTransform canvasRoot, popup, wheel, classesButton;
    private CanvasGroup popupFade;
    private readonly Dictionary<CharacterClass, Image> badges = new Dictionary<CharacterClass, Image>();
    private readonly Dictionary<CharacterClass, TextMeshProUGUI> labels = new Dictionary<CharacterClass, TextMeshProUGUI>();
    private readonly List<(CharacterClass from, CharacterClass to, List<Image> parts)> arrows = new List<(CharacterClass, CharacterClass, List<Image>)>();
    private readonly List<Texture2D> textures = new List<Texture2D>();
    private Sprite lineSprite, headSprite;
    private DraftManager draft;

    void Start()
    {
        lineSprite = MakeSprite(InkArt.Paint(4, 4, p => Color.white, 1));
        headSprite = MakeSprite(InkArt.Paint(32, 32, p => Mathf.Abs(p.y - 16f) < (32f - p.x) * 0.5f ? Color.white : Color.clear, 3));
        Build();
        Show(false, true);

        draft = FindAnyObjectByType<DraftManager>();
        if (draft != null) draft.DraftStarted += OnDraftStarted;
    }

    void OnDestroy()
    {
        if (draft != null) draft.DraftStarted -= OnDraftStarted;
        foreach (Texture2D texture in textures) if (texture != null) Destroy(texture);
        if (canvasRoot != null) Destroy(canvasRoot.gameObject);
        IsOpen = false;
    }

    // The first draft ever: show the wheel once the DRAFT banner has gone
    void OnDraftStarted(int firstPicker)
    {
        if (PlayerPrefs.GetInt(SeenKey, 0) == 1) return;
        PlayerPrefs.SetInt(SeenKey, 1);
        StartCoroutine(OpenAfter(1.6f));
    }

    IEnumerator OpenAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        if (!IsOpen) Show(true);
    }

    void Update()
    {
        // Button under the top-left panel, which is taller in battle (turn order)
        TurnManager turns = TurnManager.Instance;
        bool active = turns != null && turns.Phase != GamePhase.GameOver && Time.timeScale != 0f;
        classesButton.gameObject.SetActive(active && !IsOpen);
        classesButton.anchoredPosition = new Vector2(20f, turns != null && turns.Phase == GamePhase.Battle ? -164f : -110f);

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame && active && !CardInspector.IsOpen) Show(!IsOpen);
    }

    // ---- Opening and closing ----

    void Show(bool open, bool instant = false)
    {
        IsOpen = open;
        StopAllCoroutines();
        if (instant || !open) { popup.gameObject.SetActive(open); popupFade.alpha = open ? 1f : 0f; Focus(null); return; }
        popup.gameObject.SetActive(true);
        StartCoroutine(PopIn());
    }

    IEnumerator PopIn()
    {
        Focus(null);
        for (float t = 0f; t < 0.2f; t += Time.unscaledDeltaTime)
        {
            float p = t / 0.2f;
            popupFade.alpha = p;
            wheel.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, 1f - (1f - p) * (1f - p));
            yield return null;
        }
        popupFade.alpha = 1f;
        wheel.localScale = Vector3.one;
    }

    // Highlights one class's matchups (null = show everything evenly)
    void Focus(CharacterClass? focus)
    {
        foreach ((CharacterClass from, CharacterClass to, List<Image> parts) in arrows)
        {
            bool outgoing = focus.HasValue && from == focus.Value, incoming = focus.HasValue && to == focus.Value;
            Color colour = outgoing ? Strong : incoming ? Weak : Gold;
            colour.a = !focus.HasValue ? 0.75f : outgoing || incoming ? 1f : 0.1f;
            float width = outgoing || incoming ? 5f : 3f;
            for (int i = 0; i < parts.Count; i++)
            {
                parts[i].color = colour;
                if (i < parts.Count - 1) parts[i].rectTransform.sizeDelta = new Vector2(parts[i].rectTransform.sizeDelta.x, width);
            }
        }
        foreach (CharacterClass type in Order)
        {
            bool related = !focus.HasValue || type == focus.Value || Beats(focus.Value, type) || Beats(type, focus.Value);
            badges[type].color = new Color(1f, 1f, 1f, related ? 1f : 0.3f);
            labels[type].alpha = related ? 1f : 0.35f;
        }
    }

    // ---- Building ----

    void Build()
    {
        GameObject canvasObject = new GameObject("Counter Wheel", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70; // Above the HUD and game log
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRoot = (RectTransform)canvasObject.transform;

        BuildButton();

        // Popup: dark cover (click it to close) and a framed panel
        popup = NewRect("Popup", canvasRoot);
        Stretch(popup);
        popupFade = popup.gameObject.AddComponent<CanvasGroup>();
        Image cover = popup.gameObject.AddComponent<Image>();
        cover.color = new Color(0.03f, 0.06f, 0.08f, 0.78f);
        popup.gameObject.AddComponent<Button>().onClick.AddListener(() => Show(false));

        Image panel = InkUI.FramedPanel(popup, "Panel", InkUI.PanelColor);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(860f, 880f);
        panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // Clicks on the panel don't close it

        TextMeshProUGUI title = Text(panelRect, "CLASS COUNTERS", new Vector2(0f, 395f), 34f, InkUI.Text);
        GameFonts.UseTitle(title, 0.2f);
        TextMeshProUGUI sub = Text(panelRect, "Each class deals x1.25 damage to the next two around the wheel", new Vector2(0f, 355f), 19f, InkUI.DimText);
        GameFonts.UseBody(sub);

        wheel = NewRect("Wheel", panelRect);
        wheel.anchoredPosition = new Vector2(0f, -10f);
        BuildRing(wheel);
        Vector2[] positions = new Vector2[Order.Length];
        for (int i = 0; i < Order.Length; i++)
        {
            float angle = Mathf.PI / 2f - i * 2f * Mathf.PI / Order.Length; // Clockwise from the top
            positions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Radius;
        }
        for (int i = 0; i < Order.Length; i++)
            for (int k = 1; k <= 2; k++)
                BuildArrow(wheel, Order[i], Order[(i + k) % Order.Length], positions[i], positions[(i + k) % Order.Length], k == 1 ? 0.22f : 0.12f);
        for (int i = 0; i < Order.Length; i++) BuildBadge(wheel, Order[i], positions[i]);
        BuildCentre(wheel);

        TextMeshProUGUI hint = Text(panelRect, "Hover a class: green = it beats them, red = they beat it", new Vector2(0f, -330f), 17f, InkUI.DimText);
        GameFonts.UseBody(hint);

        Image close = InkUI.Chip(panelRect, "Close", InkUI.PanelDark);
        close.rectTransform.anchorMin = close.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        close.rectTransform.anchoredPosition = new Vector2(0f, -385f);
        close.rectTransform.sizeDelta = new Vector2(180f, 50f);
        close.gameObject.AddComponent<Button>().onClick.AddListener(() => Show(false));
        TextMeshProUGUI closeText = Text(close.rectTransform, "CLOSE", Vector2.zero, 22f, InkUI.Text);
        GameFonts.UseTitle(closeText, 0.15f);
    }

    void BuildButton()
    {
        Image chip = InkUI.Chip(canvasRoot, "Classes Button", InkUI.PanelDark);
        classesButton = chip.rectTransform;
        classesButton.anchorMin = classesButton.anchorMax = classesButton.pivot = new Vector2(0f, 1f);
        classesButton.sizeDelta = new Vector2(196f, 46f);
        chip.gameObject.AddComponent<Button>().onClick.AddListener(() => Show(true));

        Image icon = Image(classesButton, MakeSprite(ClassFrames.PaintBadge(CharacterClass.Mage, 48)), new Vector2(-66f, 0f), new Vector2(30f, 30f));
        icon.raycastTarget = false;
        TextMeshProUGUI label = Text(classesButton, "CLASSES", new Vector2(14f, 0f), 20f, InkUI.Text);
        label.rectTransform.sizeDelta = new Vector2(140f, 40f);
        GameFonts.UseTitle(label, 0.15f);
        TextMeshProUGUI key = Text(classesButton, "C", new Vector2(80f, 0f), 15f, InkUI.GoldText);
        key.rectTransform.sizeDelta = new Vector2(24f, 30f);
        GameFonts.UseTitle(key, 0.1f);
    }

    void BuildRing(RectTransform parent)
    {
        // Thin gold rings (one sample per pixel, edges softened by distance)
        foreach ((float radius, float alpha, int dashes) in new[] { (Radius, 0.35f, 0), (Radius - 18f, 0.2f, 72) })
        {
            const int size = 512;
            float scale = size / (radius * 2f + 8f), r = radius * scale;
            Texture2D texture = InkArt.Paint(size, size, p =>
            {
                Vector2 d = p - new Vector2(size / 2f, size / 2f);
                float a = Mathf.Clamp01(1.4f - Mathf.Abs(d.magnitude - r));
                if (dashes > 0 && (Mathf.Atan2(d.y, d.x) / (Mathf.PI * 2f) + 1f) * dashes % 1f > 0.2f) a = 0f;
                return new Color(1f, 1f, 1f, a);
            }, 1);
            textures.Add(texture);
            Image ring = Image(parent, MakeSprite(texture), Vector2.zero, Vector2.one * (size / scale));
            ring.color = new Color(Gold.r, Gold.g, Gold.b, alpha);
            ring.raycastTarget = false;
        }
    }

    // A gently curved arrow from one badge to another, bowing away from the middle
    void BuildArrow(RectTransform parent, CharacterClass from, CharacterClass to, Vector2 a, Vector2 b, float bend)
    {
        Vector2 direction = (b - a).normalized;
        a += direction * 44f;
        b -= direction * 50f;
        Vector2 middle = (a + b) / 2f, side = new Vector2(-(b - a).y, (b - a).x) * bend;
        Vector2 control = (middle + side).magnitude > (middle - side).magnitude ? middle + side : middle - side;

        List<Image> parts = new List<Image>();
        const int steps = 10;
        Vector2 previous = a;
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 point = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * control + t * t * b;
            Image segment = Image(parent, lineSprite, previous, new Vector2((point - previous).magnitude + 1f, 3f));
            segment.rectTransform.pivot = new Vector2(0f, 0.5f);
            segment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(point.y - previous.y, point.x - previous.x) * Mathf.Rad2Deg);
            segment.raycastTarget = false;
            parts.Add(segment);
            previous = point;
        }
        Vector2 tangent = (b - control).normalized;
        Image head = Image(parent, headSprite, b, new Vector2(18f, 18f));
        head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
        head.raycastTarget = false;
        parts.Add(head);
        arrows.Add((from, to, parts));
    }

    void BuildBadge(RectTransform parent, CharacterClass type, Vector2 position)
    {
        Texture2D texture = ClassFrames.PaintBadge(type, 128);
        textures.Add(texture);
        Image badge = Image(parent, MakeSprite(texture), position, new Vector2(76f, 76f));
        badges[type] = badge;

        // Hover (or tap) focuses this class
        EventTrigger trigger = badge.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, () => Focus(type));
        AddTrigger(trigger, EventTriggerType.PointerExit, () => Focus(null));
        AddTrigger(trigger, EventTriggerType.PointerClick, () => Focus(type));

        // Name just past the badge's edge, pointing away from the wheel so it never sits on the icon:
        // left-aligned on the right side, right-aligned on the left, centred at the top and bottom
        Vector2 outward = position / Radius;
        TextAlignmentOptions alignment = outward.x > 0.35f ? TextAlignmentOptions.MidlineLeft : outward.x < -0.35f ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.Center;
        TextMeshProUGUI label = Text(parent, type.ToString().ToUpper(), Vector2.zero, 20f, ClassFrames.ColourOf(type));
        GameFonts.UseTitle(label, 0.3f);
        label.rectTransform.sizeDelta = new Vector2(180f, 30f);
        if (alignment == TextAlignmentOptions.MidlineLeft) { label.rectTransform.pivot = new Vector2(0f, 0.5f); label.rectTransform.anchoredPosition = position + new Vector2(48f, 0f); }
        else if (alignment == TextAlignmentOptions.MidlineRight) { label.rectTransform.pivot = new Vector2(1f, 0.5f); label.rectTransform.anchoredPosition = position - new Vector2(48f, 0f); }
        else label.rectTransform.anchoredPosition = position + new Vector2(0f, outward.y > 0f ? 56f : -56f);
        label.alignment = alignment;
        labels[type] = label;
    }

    void BuildCentre(RectTransform parent)
    {
        Image disc = Image(parent, MakeSprite(InkArt.Paint(128, 128, p =>
        {
            float d = (p - new Vector2(64f, 64f)).magnitude;
            return d > 63f ? Color.clear : d > 60f ? new Color(Gold.r, Gold.g, Gold.b, 0.6f) : new Color(0.06f, 0.08f, 0.09f, 0.6f);
        }, 2)), Vector2.zero, new Vector2(190f, 190f));
        disc.raycastTarget = false;
        TextMeshProUGUI multiplier = Text(parent, "x1.25", new Vector2(0f, 18f), 54f, Gold);
        GameFonts.UseInked(multiplier, 0.3f);
        TextMeshProUGUI line1 = Text(parent, "DAMAGE VS", new Vector2(0f, -28f), 15f, new Color(0.73f, 0.81f, 0.78f));
        GameFonts.UseTitle(line1, 0f);
        TextMeshProUGUI line2 = Text(parent, "NEXT TWO", new Vector2(0f, -48f), 15f, new Color(0.73f, 0.81f, 0.78f));
        GameFonts.UseTitle(line2, 0f);
    }

    // ---- Helpers ----

    static RectTransform NewRect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static Image Image(Transform parent, Sprite sprite, Vector2 position, Vector2 size)
    {
        Image image = new GameObject("Image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        image.sprite = sprite;
        return image;
    }

    static TextMeshProUGUI Text(Transform parent, string value, Vector2 position, float size, Color colour)
    {
        TextMeshProUGUI text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = new Vector2(760f, 40f);
        text.text = value;
        text.fontSize = size;
        text.color = colour;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    Sprite MakeSprite(Texture2D texture)
    {
        if (!textures.Contains(texture)) textures.Add(texture);
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }
}
