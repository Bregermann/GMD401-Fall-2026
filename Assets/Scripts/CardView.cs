using TMPro;
using UnityEngine;

public class CardView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer artRenderer;
    [SerializeField] private Vector2 artSize = new Vector2(1f, 1f); // size of the art window in local units
    [SerializeField] private Sprite cardBack;                         // shown on the underside, seen when the card is flipped face down
    [SerializeField] private TMP_FontAsset textFont;                  // font for the name, stats and attacks on blank art (empty = default)

    private SpriteRenderer backRenderer;
    private GameObject cardText;   // name, stats and attacks written on art that doesn't have them printed
    private CardData textFor;      // which card the text was written for, so it isn't rebuilt for nothing

    void Awake()
    {
        CreateBack();
    }

    public void Setup(CardData data)
    {
        if (artRenderer == null)
            artRenderer = GetComponentInChildren<SpriteRenderer>();

        artRenderer.sprite = data.artwork;
        CreateBack();
        if (data.artwork == null) return;

        artRenderer.transform.localScale = FitScale(data.artwork);
        WriteCardText(data);
    }

    // Blank art (artHasText off) gets the card's name, stats and attacks written on in the printed layout
    void WriteCardText(CardData data)
    {
        if (textFor == data && cardText != null) return;
        if (cardText != null) Destroy(cardText);
        cardText = null;
        textFor = data;

        if (data.artHasText) return;
        cardText = CardFaceText.Build(artRenderer, data, textFont, artRenderer.sortingOrder + 1);
    }

    // Copy of the art renderer flipped onto the underside of the card. Turned so it reads upright when the card is flipped over
    void CreateBack()
    {
        if (backRenderer != null || cardBack == null) return;
        if (artRenderer == null) artRenderer = GetComponentInChildren<SpriteRenderer>();
        if (artRenderer == null) return;

        Quaternion flip = Quaternion.Euler(0f, 0f, 180f); // Same flip the draft uses to turn cards face down
        Transform art = artRenderer.transform;

        GameObject back = new GameObject("Back");
        back.transform.SetParent(art.parent, false);
        back.transform.localPosition = flip * art.localPosition;
        back.transform.localRotation = flip * art.localRotation;
        back.transform.localScale = FitScale(cardBack);

        backRenderer = back.AddComponent<SpriteRenderer>();
        backRenderer.sprite = cardBack;
        backRenderer.sharedMaterial = artRenderer.sharedMaterial; // Same look as the front art
        backRenderer.sortingOrder = artRenderer.sortingOrder + 2;  // Above the card text, so face-down cards hide it
    }

    // Stretches a sprite to fill the art window
    Vector3 FitScale(Sprite sprite)
    {
        Vector2 spriteSize = sprite.bounds.size;
        return new Vector3(artSize.x / spriteSize.x, artSize.y / spriteSize.y, 1f);
    }
}
