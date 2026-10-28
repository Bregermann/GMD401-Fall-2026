using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Shows the Console log in the game as a scrolling history of what happened.
// Put it on any GameObject in the scene. If logText and scrollRect are empty it builds its own box in the bottom left.
public class GameLog : MonoBehaviour
{
    [Header("Optional - leave empty to build the box automatically")]
    [SerializeField] private TMP_Text logText;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Settings")]
    [SerializeField] private Vector2 boxSize = new Vector2(550, 260);
    [SerializeField] private int fontSize = 18;
    [SerializeField] private int maxLines = 100;                 // Oldest lines drop off after this
    [SerializeField] private bool showWarningsAndErrors = false; // Yellow/red, handy while testing

    // Lines containing any of these are left out (CardMover's debug lines)
    [SerializeField] private string[] ignoreIfContains =
    {
        "started drag",
        "has used its move",
        "placed on board for the first time",
        "illegal drop"
    };

    private readonly Queue<string> lines = new Queue<string>();

    void Awake()
    {
        if (logText == null || scrollRect == null) BuildBox();
    }

    void OnEnable()
    {
        Application.logMessageReceived += OnLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= OnLog;
    }

    void OnLog(string message, string stackTrace, LogType type)
    {
        if (type != LogType.Log && !showWarningsAndErrors) return;
        foreach (string ignore in ignoreIfContains)
            if (message.Contains(ignore)) return;

        if (type == LogType.Warning) message = $"<color=yellow>{message}</color>";
        else if (type != LogType.Log) message = $"<color=red>{message}</color>";

        lines.Enqueue(message);
        while (lines.Count > maxLines) lines.Dequeue();

        logText.text = string.Join("\n", lines);
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f; // Scroll to the newest line
    }

    // Makes a canvas with a dark scrolling box in the bottom left corner
    void BuildBox()
    {
        GameObject canvasObject = new GameObject("GameLogCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Above other UI
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Box (also the scroll view's mask)
        GameObject box = new GameObject("GameLogBox", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        box.transform.SetParent(canvasObject.transform, false);
        RectTransform boxRect = (RectTransform)box.transform;
        boxRect.anchorMin = Vector2.zero;
        boxRect.anchorMax = Vector2.zero;
        boxRect.pivot = Vector2.zero;
        boxRect.anchoredPosition = new Vector2(20, 20);
        boxRect.sizeDelta = boxSize;
        box.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        // Text grows downward as lines are added
        GameObject textObject = new GameObject("GameLogText", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(ContentSizeFitter));
        textObject.transform.SetParent(box.transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.sizeDelta = Vector2.zero;
        textObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.margin = new Vector4(10, 8, 10, 8);
        text.raycastTarget = false;

        ScrollRect scroll = box.GetComponent<ScrollRect>();
        scroll.content = textRect;
        scroll.viewport = boxRect;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;

        // Scrolling with the mouse wheel needs an EventSystem
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        logText = text;
        scrollRect = scroll;
    }
}
