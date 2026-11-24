using UnityEngine;
using UnityEngine.UI;

// Art from the UI artist that code-built UI uses, like the STRATAFIELD logo (the MenuUI_0 slice of Material/MenuUI.png).
// Lives at Resources/UI/GameArt and is filled in automatically in the editor (Scripts/Editor/GameArtSetup)
[CreateAssetMenu(fileName = "GameArt", menuName = "Stratafield/Game Art")]
public class GameArt : ScriptableObject
{
    public Sprite logo;
    public CardData[] showcaseCards; // Cards the main menu shows standing on its board (real cards with art, filled in automatically)

    private static GameArt loaded;
    private static bool looked;

    // The GameArt asset itself (null if it doesn't exist)
    public static GameArt Asset
    {
        get
        {
            if (!looked) { looked = true; loaded = Resources.Load<GameArt>("UI/GameArt"); }
            return loaded;
        }
    }

    // The logo sprite: from the GameArt asset, or else borrowed from any "Stratafield Logo" image in the scene
    // (the pause menu has one). Null if neither exists
    public static Sprite Logo
    {
        get
        {
            if (!looked)
            {
                looked = true;
                loaded = Resources.Load<GameArt>("UI/GameArt");
            }
            if (loaded != null && loaded.logo != null) return loaded.logo;

            foreach (Image image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include))
                if (image.name == "Stratafield Logo" && image.sprite != null) return image.sprite;
            return null;
        }
    }
}
