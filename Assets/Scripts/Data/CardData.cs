using UnityEngine;

[CreateAssetMenu(fileName = "NewCard", menuName = "Stratafield/Card")]
public class CardData : ScriptableObject
{
    [Header("Identity")]
    public string cardName;
    public CharacterClass characterClass;
    public Sprite artwork;

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
