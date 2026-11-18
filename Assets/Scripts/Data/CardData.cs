using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Stratafield/Card")]
public class CardData : ScriptableObject
{
    [Header("Identity")]
    public string cardName;
    public CharacterClass characterClass;
    public Sprite artwork;
    public bool artHasText; // Name, stats and attacks are already printed on the art. Off = they're written on in-game

    [Header("Board Token Portrait")]
    // Which part of the art the standing token on the board shows. The inspector shows a box over the art to help
    public Vector2 portraitFocus = new Vector2(0.5f, 0.69f); // Point to centre on: 0-1 across the art, from its bottom-left
    [Range(0.2f, 1f)] public float portraitZoom = 0.42f;     // How much of the art's width fits in the portrait (lower = closer)

    [Header("Stats")]
    public int maxHP;
    public int physicalDefense;
    public int magicDefense;
    public int speed;        // Determines turn order; higher goes first
    public int moveSpaces;   // How many grid spaces this card can move per turn, calculated from speed
    public bool canMoveDiagonally;

    [Header("Attacks")]
    // Cards have 4 attacks. After an attack is used it goes on cooldown
    // until a different attack on this card is used first.
    // Assign AttackData assets from Assets/Data/Attacks/
    public AttackData attack1;
    public AttackData attack2;
    public AttackData attack3;
    public AttackData attack4;
}
