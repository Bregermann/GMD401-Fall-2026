using UnityEditor;
using UnityEngine;

// Menu items for the editor UI preview (see UIPreview): Stratafield > UI Preview
public static class UIPreviewMenu
{
    // Adds a "UI Preview" object to the open scene (or selects the one already there). Look in the Game view
    [MenuItem("Stratafield/UI Preview/Show in Scene")]
    static void Show()
    {
        UIPreview existing = Object.FindAnyObjectByType<UIPreview>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.enabled = true;
            Selection.activeObject = existing.gameObject;
            return;
        }

        GameObject preview = new GameObject("UI Preview");
        Undo.RegisterCreatedObjectUndo(preview, "Add UI Preview");
        preview.AddComponent<UIPreview>();
        Selection.activeObject = preview;
    }

    // Rebuilds it, e.g. after changing a card's data
    [MenuItem("Stratafield/UI Preview/Rebuild")]
    static void Rebuild()
    {
        foreach (UIPreview preview in Object.FindObjectsByType<UIPreview>())
            preview.Rebuild();
    }
}
