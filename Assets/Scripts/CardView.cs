using UnityEngine;

public class CardView : MonoBehaviour
{
    [SerializeField] private SpriteRenderer artRenderer;
    [SerializeField] private Vector2 artSize = new Vector2(1f, 1f); // size of the art window in local units

    public void Setup(CardData data)
    {
        if (artRenderer == null)
            artRenderer = GetComponentInChildren<SpriteRenderer>();

        artRenderer.sprite = data.artwork;
        if (data.artwork == null) return;

        Vector2 spriteSize = data.artwork.bounds.size;
        artRenderer.transform.localScale = new Vector3(
            artSize.x / spriteSize.x,
            artSize.y / spriteSize.y,
            1f);
    }
}
