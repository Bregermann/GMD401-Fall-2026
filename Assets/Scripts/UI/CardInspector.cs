using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Right-click any card to see a bigger copy of it and all its stats. Right-click again to close.
// Put it on any GameObject in the game scene. It builds its own panel and camera.
// While it's open other scripts ignore input (they check CardInspector.IsOpen).
public class CardInspector : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [SerializeField] private Vector2 cardImageSize = new Vector2(360, 560);
    [SerializeField] private Vector2 statsSize = new Vector2(620, 560);
    [SerializeField] private int fontSize = 22;
    [SerializeField] private Vector3 copySpot = new Vector3(1000f, 0f, 1000f); // Far off the board so the main camera can't see the copy

    private GameObject panel;
    private RawImage cardImage;
    private TMP_Text statsText;
    private Camera inspectCamera;
    private RenderTexture renderTexture;
    private GameObject copy;
    private CardUnit inspected;
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
        BuildPanel();
        BuildCamera();
        Close();
    }

    void OnDestroy()
    {
        IsOpen = false;
        if (renderTexture != null) renderTexture.Release();
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (IsOpen)
            {
                Close();
            }
            else if (!mouse.leftButton.isPressed) // Not while dragging a card
            {
                CardUnit card = GetClickedCard();
                if (card != null) Open(card);
            }
        }

        // Keeps HP and status effects up to date while it's open
        if (IsOpen) RefreshStats();
    }

    void Open(CardUnit card)
    {
        inspected = card;
        MakeCopy(card);
        RefreshStats();
        panel.SetActive(true);
        inspectCamera.enabled = true;
        IsOpen = true;
    }

    void Close()
    {
        if (copy != null) Destroy(copy);
        copy = null;
        inspected = null;
        panel.SetActive(false);
        inspectCamera.enabled = false;
        IsOpen = false;
    }

    // ---- Card copy ----

    // Copy of the card with only its looks (no scripts or colliders), framed by the inspect camera
    void MakeCopy(CardUnit card)
    {
        copy = Instantiate(card.gameObject, copySpot, card.transform.rotation);
        copy.transform.localScale = card.transform.lossyScale;
        foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>())
            Destroy(script);
        foreach (Collider collider in copy.GetComponentsInChildren<Collider>())
            Destroy(collider);

        // Same view angle as the main camera, zoomed to fit the card
        Bounds bounds = GetBounds(copy);
        inspectCamera.transform.rotation = cam.transform.rotation;
        inspectCamera.transform.position = bounds.center - inspectCamera.transform.forward * 5f;

        float halfHeight = Extent(bounds, inspectCamera.transform.up);
        float halfWidth = Extent(bounds, inspectCamera.transform.right);
        inspectCamera.orthographicSize = Mathf.Max(halfHeight, halfWidth / inspectCamera.aspect) * 1.1f;
    }

    Bounds GetBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(target.transform.position, Vector3.one);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    // How far the bounds reach along a direction from their center
    float Extent(Bounds bounds, Vector3 axis)
    {
        Vector3 e = bounds.extents;
        return Mathf.Abs(axis.x) * e.x + Mathf.Abs(axis.y) * e.y + Mathf.Abs(axis.z) * e.z;
    }

    // ---- Stats ----

    void RefreshStats()
    {
        if (inspected == null)
        {
            statsText.text = "This card is gone\n\n<size=70%>Right-click to close</size>";
            return;
        }

        CardInstance card = inspected.Instance;
        CardMover mover = inspected.GetComponent<CardMover>();
        CardData data = card != null ? card.data : (mover != null ? mover.data : null);
        if (data == null)
        {
            statsText.text = "No card data\n\n<size=70%>Right-click to close</size>";
            return;
        }

        // Live stats only once the card is on the board
        bool onBoard = card != null && CombatManager.Instance != null && CombatManager.Instance.Board.IsOnBoard(card);

        StringBuilder text = new StringBuilder();
        text.AppendLine($"<size=140%><b>{data.cardName}</b></size>");
        text.AppendLine($"{data.characterClass}  -  {OwnerText(card)}");
        text.AppendLine();
        text.AppendLine(onBoard ? $"HP {card.currentHP}/{data.maxHP}" : $"HP {data.maxHP}");
        text.AppendLine($"Phys Def {Stat(data.physicalDefense, onBoard ? card.PhysicalDefense : data.physicalDefense)}    " +
                        $"Mag Def {Stat(data.magicDefense, onBoard ? card.MagicDefense : data.magicDefense)}");
        text.AppendLine($"Speed {Stat(data.speed, onBoard ? card.Speed : data.speed)}    " +
                        $"Move {Stat(data.moveSpaces, onBoard ? card.MoveSpaces : data.moveSpaces)}{(data.canMoveDiagonally ? ", diagonal" : "")}");

        if (onBoard)
        {
            text.AppendLine($"Status: {EffectsText(card)}");
            text.AppendLine($"Cooldown: {(card.lastAttackUsed != null ? card.lastAttackUsed.attackName : "none")}");
        }

        text.AppendLine();
        text.AppendLine("<b>Attacks</b>");
        AttackData[] attacks = { data.attack1, data.attack2, data.attack3, data.attack4 };
        for (int i = 0; i < attacks.Length; i++)
            text.AppendLine(AttackText(i + 1, attacks[i], card, onBoard));

        text.AppendLine();
        text.Append("<size=70%>Right-click to close</size>");
        statsText.text = text.ToString();
    }

    string OwnerText(CardInstance card)
    {
        if (DraftManager.Instance != null && DraftManager.Instance.IsUndrafted(inspected)) return "Not drafted";
        return card != null ? $"Player {card.owner}" : "No owner";
    }

    // Base stat, plus the changed value in green (buffed) or red (debuffed)
    string Stat(int baseValue, int current)
    {
        if (current == baseValue) return baseValue.ToString();
        string color = current > baseValue ? "#66FF66" : "#FF6666";
        return $"{baseValue} <color={color}>({current})</color>";
    }

    // Active effects with amount and turns left, e.g. "MagicDefenseDown 80 (2)"
    string EffectsText(CardInstance card)
    {
        List<string> effects = new List<string>();
        foreach (StatusEffectInstance effect in card.statusEffects.ActiveEffects)
        {
            string amount = effect.amount > 0 ? $" {effect.amount}" : "";
            effects.Add($"{effect.type}{amount} ({effect.turnsRemaining})");
        }
        return effects.Count > 0 ? string.Join(", ", effects) : "none";
    }

    // What an attack applies, with the real amount and duration, e.g. "MagicDefenseDown 80 for 3 turns"
    string ApplicationsText(StatusEffectApplication[] applications)
    {
        List<string> parts = new List<string>();
        foreach (StatusEffectApplication effect in applications)
        {
            int amount = effect.amount > 0 ? effect.amount : StatusEffectHandler.GetDefaultAmount(effect.type);
            int duration = effect.duration > 0 ? effect.duration
                : effect.type == StatusEffectType.Stun ? StatusEffectHandler.StunDuration : StatusEffectHandler.DefaultDuration;
            string amountText = amount > 0 ? $" {amount}" : "";
            parts.Add($"{effect.type}{amountText} for {duration} turn{(duration == 1 ? "" : "s")}");
        }
        return string.Join(", ", parts);
    }

    string AttackText(int number, AttackData attack, CardInstance card, bool onBoard)
    {
        if (attack == null) return $"{number}. (empty)";

        string damage;
        if (attack.damageType == DamageType.Heal) damage = $"Heals {attack.damage}";
        else if (attack.damage == 0) damage = "No damage";
        else damage = $"{Stat(attack.damage, onBoard ? card.GetAttackPower(attack) : attack.damage)} {attack.damageType}";

        string cooldown = onBoard && card.lastAttackUsed == attack ? "  <color=#FF6666>[cooldown]</color>" : "";
        string line = $"{number}. <b>{attack.attackName}</b> - {damage}{cooldown}";

        // Extra effects on a second, smaller line
        List<string> extras = new List<string>();
        if (attack.range > 0) extras.Add($"Range {attack.range}");
        int width = attack.areaSize * 2 + 1;
        if (attack.area == AttackArea.AroundTarget) extras.Add($"Hits {attack.areaAffects.ToString().ToLower()} in a {width}x{width} around the target");
        if (attack.area == AttackArea.AroundSelf) extras.Add($"Hits {attack.areaAffects.ToString().ToLower()} within {attack.areaSize} space(s)");
        if (attack.positionalBonus != PositionalBonus.None) extras.Add($"x{attack.positionalMultiplier} from {attack.positionalBonus}");
        if (attack.hasClassBonus) extras.Add($"x{attack.classMultiplier} vs {attack.classBonusTarget}");
        if (attack.hasKnockback && attack.area == AttackArea.Single) extras.Add($"Knockback ({attack.knockbackCollisionDamage} if it hits something)");
        if (attack.targetStatusEffects != null && attack.targetStatusEffects.Length > 0) extras.Add("Applies " + ApplicationsText(attack.targetStatusEffects));
        if (attack.selfStatusEffects != null && attack.selfStatusEffects.Length > 0) extras.Add("Self " + ApplicationsText(attack.selfStatusEffects));

        return extras.Count == 0 ? line : $"{line}\n    <size=85%>{string.Join(", ", extras)}</size>";
    }

    // ---- Building the UI ----

    // Dark screen with the card copy on the left and the stats on the right
    void BuildPanel()
    {
        GameObject canvasObject = new GameObject("CardInspectorCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // Above the game log
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        panel = new GameObject("CardInspectorPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = (RectTransform)panel.transform;
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        float gap = 40f;
        float left = -(cardImageSize.x + gap + statsSize.x) / 2f; // Centers both side by side

        GameObject imageObject = new GameObject("CardImage", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(panel.transform, false);
        SetRect((RectTransform)imageObject.transform, left, cardImageSize);
        cardImage = imageObject.GetComponent<RawImage>();

        GameObject textObject = new GameObject("CardStats", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panel.transform, false);
        SetRect((RectTransform)textObject.transform, left + cardImageSize.x + gap, statsSize);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableAutoSizing = true; // Shrinks if a card has a lot of text
        text.fontSizeMin = 12;
        text.fontSizeMax = fontSize;
        text.raycastTarget = false;
        statsText = text;
    }

    // Places a box in the middle of the screen, x from the center
    void SetRect(RectTransform rect, float x, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = size;
    }

    // Camera that only films the card copy and draws it into the panel
    void BuildCamera()
    {
        renderTexture = new RenderTexture((int)cardImageSize.x, (int)cardImageSize.y, 16);

        GameObject cameraObject = new GameObject("CardInspectorCamera");
        inspectCamera = cameraObject.AddComponent<Camera>();
        inspectCamera.orthographic = true;
        inspectCamera.clearFlags = CameraClearFlags.SolidColor;
        inspectCamera.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 1f);
        inspectCamera.nearClipPlane = 0.1f;
        inspectCamera.farClipPlane = 20f;
        inspectCamera.targetTexture = renderTexture;

        cardImage.texture = renderTexture;
    }

    CardUnit GetClickedCard()
    {
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            return hit.collider.GetComponentInParent<CardUnit>();
        return null;
    }
}
