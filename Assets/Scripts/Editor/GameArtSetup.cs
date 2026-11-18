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
}
