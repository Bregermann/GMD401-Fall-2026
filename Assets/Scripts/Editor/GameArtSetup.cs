using UnityEditor;
using UnityEngine;

// Makes Resources/UI/GameArt and fills in the logo (the MenuUI_0 slice of Material/MenuUI.png) if it's empty,
// whenever scripts reload in the editor. A logo you've set by hand is left alone
[InitializeOnLoad]
public static class GameArtSetup
{
    const string AssetPath = "Assets/Resources/UI/GameArt.asset";
    const string MenuArt = "Assets/Material/MenuUI.png";
    const string LogoSlice = "MenuUI_0";

    static GameArtSetup()
    {
        EditorApplication.delayCall += Fill;
    }

    static void Fill()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources", "UI");
        GameArt art = AssetDatabase.LoadAssetAtPath<GameArt>(AssetPath);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<GameArt>();
            AssetDatabase.CreateAsset(art, AssetPath);
        }
        FillShowcase(art);
        if (art.logo != null) return;

        foreach (Object item in AssetDatabase.LoadAllAssetsAtPath(MenuArt))
        {
            if (item is Sprite sprite && sprite.name == LogoSlice)
            {
                art.logo = sprite;
                EditorUtility.SetDirty(art);
                AssetDatabase.SaveAssets();
                return;
            }
        }
    }

    // The main menu's showcase: every real card with art (not placeholders or test cards). Kept up to date as cards
    // get their art
    static void FillShowcase(GameArt art)
    {
        System.Collections.Generic.List<CardData> cards = new System.Collections.Generic.List<CardData>();
        foreach (string guid in AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data/Cards" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Placeholder") || System.IO.Path.GetFileName(path).StartsWith("test")) continue;
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card != null && card.artwork != null) cards.Add(card);
        }

        if (art.showcaseCards != null && System.Linq.Enumerable.SequenceEqual(art.showcaseCards, cards)) return;
        art.showcaseCards = cards.ToArray();
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
    }
}
