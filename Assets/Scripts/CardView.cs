using UnityEngine;

public class CardView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer artRenderer;
    [SerializeField] private Vector2 artSize = new Vector2(1f, 1f); // size of the art window in local units
    [SerializeField] private Sprite cardBack;                         // shown on the underside, seen when the card is flipped face down

    private SpriteRenderer backRenderer;

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
        backRenderer.sortingOrder = artRenderer.sortingOrder;
    }

    // Stretches a sprite to fill the art window
    Vector3 FitScale(Sprite sprite)
    {
        Vector2 spriteSize = sprite.bounds.size;
        return new Vector3(artSize.x / spriteSize.x, artSize.y / spriteSize.y, 1f);
    }
}
