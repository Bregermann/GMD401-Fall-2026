using UnityEngine;

// Defines one of a card's four attacks.
// Each attack is its own ScriptableObject so cards reference them individually
[CreateAssetMenu(fileName = "NewAttack", menuName = "Stratafield/Attack")]
public class AttackData : ScriptableObject
{
    [Header("Base")]
    public string attackName;
    public int damage;
    public DamageType damageType;

    [Header("Range")]
    // How many spaces away this attack reaches. 0 = default (Ranged class reaches across the board, everyone else 1 space)
    public int range = 0;

    [Header("Area")]
    // Single hits only the target. AroundTarget also hits cards near the target.
    // AroundSelf hits cards near the attacker and is used by clicking the attacker.
    // areaSize and areaAffects are ignored for Single. Area attacks never knock back.
    public AttackArea area = AttackArea.Single;
    public int areaSize = 1;                              // Spaces out from the center, 1 = 3x3
    public AreaAffects areaAffects = AreaAffects.Enemies; // Who else gets hit

    [Header("Positional Bonus")]
    // Extra damage when the attacker is on the specified side of the target.
    // multiplier is ignored when positionalBonus is None.
    public PositionalBonus positionalBonus = PositionalBonus.None;
    public float positionalMultiplier = 1.25f;

    [Header("Class Bonus")]
    // Extra damage when the target belongs to a specific class.
    // classBonusTarget and classMultiplier are ignored when hasClassBonus is false.
    public bool hasClassBonus;
    public CharacterClass classBonusTarget;
    public float classMultiplier = 1.25f;

    [Header("Knockback")]
    public bool hasKnockback;
    // Damage dealt if the knocked-back card hits a wall or any card (it stays in place).
    // If the card it hits is an enemy of the attacker, that card takes this damage too.
    public int knockbackCollisionDamage = 10;

    [Header("Status Effects")]
    public StatusEffectType[] targetStatusEffects; // Applied to the card being attacked
    public StatusEffectType[] selfStatusEffects;   // Applied to the card using this attack
}
