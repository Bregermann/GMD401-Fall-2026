using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// After PLAY on the main menu: the three menu buttons swap for VS CPU, VS PLAYER and BACK (copies of the real
// buttons, same art and font, only the words change). Picking a mode sets who controls Player 2 and starts the black
// hole into the game. MainMenu.PlayGame opens it
public static class MenuModeSelect
{
    static readonly string[] Originals = { "PlayButton", "OptionsButton", "ExitButton" };
    static readonly List<GameObject> hidden = new List<GameObject>();
    static readonly List<GameObject> copies = new List<GameObject>();

    public static void Open()
    {
        copies.RemoveAll(copy => copy == null); // Left over from an earlier visit to the menu
        if (copies.Count > 0 || BlackHoleTransition.IsPlaying) return;
        hidden.Clear();
        var labels = new List<TMP_Text>();

        string[] words = { "VS CPU", "VS PLAYER", "BACK" }; // No full stops: ROG only has letters
        System.Action[] actions =
        {
            () => Start(ComputerPlayer.Mode.Player2),
            () => Start(ComputerPlayer.Mode.Off),
            Close,
        };

        for (int i = 0; i < Originals.Length; i++)
        {
            GameObject original = GameObject.Find(Originals[i]);
            if (original == null) { Debug.LogWarning($"MenuModeSelect: no {Originals[i]} in the scene"); continue; }

            GameObject copy = Object.Instantiate(original, original.transform.parent);
            copy.name = words[i] + " Button";
            copy.transform.SetSiblingIndex(original.transform.GetSiblingIndex() + 1);

            Button button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent(); // Drop the copied menu actions (Play/Options/Exit)
            System.Action action = actions[i];
            button.onClick.AddListener(() => action());

            TMP_Text text = copy.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = words[i]; // Same ROG font as the menu buttons

                // The originals are left aligned in a box that fits their own word, so centre across the whole button
                RectTransform box = text.rectTransform;
                box.SetParent(copy.transform, false);
                box.anchorMin = Vector2.zero;
                box.anchorMax = Vector2.one;
                box.pivot = new Vector2(0.5f, 0.5f);
                box.offsetMin = new Vector2(10f, 0f);
                box.offsetMax = new Vector2(-10f, 0f);
                text.horizontalAlignment = HorizontalAlignmentOptions.Center;
                text.verticalAlignment = VerticalAlignmentOptions.Middle;
                text.textWrappingMode = TextWrappingModes.NoWrap; // One line, shrunk to fit instead of wrapping
                text.enableAutoSizing = true;
                text.fontSizeMax = text.fontSize;
                text.fontSizeMin = text.fontSize * 0.3f;
                labels.Add(text);
            }

            original.SetActive(false);
            hidden.Add(original);
            copies.Add(copy);
            copy.GetComponent<MonoBehaviour>().StartCoroutine(PopIn(copy.transform, i * 0.06f));
        }

        // All three use the size the longest label fits at, so they match
        float size = float.MaxValue;
        foreach (TMP_Text label in labels) { label.ForceMeshUpdate(); size = Mathf.Min(size, label.fontSize); }
        foreach (TMP_Text label in labels) { label.enableAutoSizing = false; label.fontSize = size; }
    }

    static void Start(ComputerPlayer.Mode mode)
    {
        ComputerPlayer.CurrentMode = mode;
        BlackHoleTransition.Play("GameScene");
    }

    public static void Close()
    {
        foreach (GameObject copy in copies) if (copy != null) Object.Destroy(copy);
        foreach (GameObject original in hidden) if (original != null) original.SetActive(true);
        copies.Clear();
        hidden.Clear();
    }

    // Each button pops in a moment after the one above it
    static IEnumerator PopIn(Transform button, float delay)
    {
        Vector3 size = button.localScale;
        button.localScale = Vector3.zero;
        yield return new WaitForSecondsRealtime(delay);
        for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            if (button == null) yield break;
            float p = t / 0.22f;
            button.localScale = size * (Mathf.SmoothStep(0f, 1f, p) * (1f + 0.12f * Mathf.Sin(p * Mathf.PI)));
            yield return null;
        }
        if (button != null) button.localScale = size;
    }
}
