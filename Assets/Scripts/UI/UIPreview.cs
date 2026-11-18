using System.Collections.Generic;
using UnityEngine;

// Shows the battle HUD, game log and card inspector in the Game view without pressing Play, filled with example
// data, so their look and layout can be checked in the editor. It uses the same building code as the game, so it
// always matches. Everything it builds is temporary: it isn't saved in the scene, and it's removed when you press
// Play or turn this off. Add it from the menu: Stratafield > UI Preview > Show in Scene. Does nothing in a build
[ExecuteAlways]
public class UIPreview : MonoBehaviour
{
    [SerializeField] private bool showHUD = true;
    [SerializeField] private bool showLog = true;
    [SerializeField] private bool showBanner = false;
    [SerializeField] private bool showInspector = false;  // Covers the whole screen, so off by default
    [SerializeField] private List<CardData> exampleCards = new List<CardData>(); // Empty = cards with art from the project

#if UNITY_EDITOR
    private const string Prefix = "[UI Preview] ";
    private readonly List<GameObject> built = new List<GameObject>();

    void OnEnable()
    {
        if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += Rebuild;
    }

    // Also runs before Play mode starts and before scripts reload, so nothing is left behind
    void OnDisable() => Clear();

    // Any setting changed in the Inspector: rebuild (after the change is applied)
    void OnValidate()
    {
        if (!Application.isPlaying) UnityEditor.EditorApplication.delayCall += Rebuild;
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (this == null || Application.isPlaying || !isActiveAndEnabled) return;
        Clear();
        List<CardData> cards = ExampleCards();

        if (showHUD)
        {
            BattleHUD hud = Temporary<BattleHUD>("HUD");
            hud.BuildPreview(cards, showBanner);
            Keep(hud.Root);
        }
        if (showLog)
        {
            GameLog log = Temporary<GameLog>("Game Log");
            log.BuildPreview(cards);
            Keep(log.Root);
        }
        if (showInspector)
        {
            CardInspector inspector = Temporary<CardInspector>("Card Inspector");
            inspector.BuildPreview(cards.Count > 0 ? cards[0] : null);
            Keep(inspector.Root);
        }
    }

    // Removes everything this built, plus anything left over from before a script reload
    void Clear()
    {
        foreach (GameObject item in built)
            if (item != null) DestroyImmediate(item);
        built.Clear();

        foreach (GameObject item in Resources.FindObjectsOfTypeAll<GameObject>())
            if (item != null && item.name.StartsWith(Prefix) && item.transform.parent == null && !UnityEditor.EditorUtility.IsPersistent(item))
                DestroyImmediate(item);
    }

    // A hidden holder for one UI script, so its preview can be built without touching the scene's own copy
    T Temporary<T>(string name) where T : Component
    {
        GameObject holder = new GameObject(Prefix + name);
        holder.hideFlags = HideFlags.DontSave;
        built.Add(holder);
        return holder.AddComponent<T>();
    }

    // Marks a built canvas (and everything under it) as temporary so it's never saved into the scene
    void Keep(GameObject root)
    {
        if (root == null) return;
        root.name = Prefix + root.name;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.hideFlags = HideFlags.DontSave;
        built.Add(root);
    }

    // The cards set in the Inspector, or up to 6 cards from the project (ones with art first)
    List<CardData> ExampleCards()
    {
        List<CardData> cards = new List<CardData>();
        foreach (CardData card in exampleCards)
            if (card != null) cards.Add(card);
        if (cards.Count > 0) return cards;

        List<CardData> withoutArt = new List<CardData>();
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:CardData"))
        {
            CardData card = UnityEditor.AssetDatabase.LoadAssetAtPath<CardData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (card == null) continue;
            if (card.artwork != null) cards.Add(card);
            else withoutArt.Add(card);
        }
        cards.AddRange(withoutArt);
        if (cards.Count > 6) cards.RemoveRange(6, cards.Count - 6);
        return cards;
    }
#endif
}
