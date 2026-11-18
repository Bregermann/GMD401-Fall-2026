using UnityEngine;
using UnityEngine.UI;

// Art from the UI artist that code-built UI uses, like the STRATAFIELD logo (the MenuUI_0 slice of Material/MenuUI.png).
// Lives at Resources/UI/GameArt and is filled in automatically in the editor (Scripts/Editor/GameArtSetup)
[CreateAssetMenu(fileName = "GameArt", menuName = "Stratafield/Game Art")]
public class GameArt : ScriptableObject
{
    public Sprite logo;

    private static GameArt loaded;
    private static bool looked;

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
