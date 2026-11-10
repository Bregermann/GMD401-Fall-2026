using UnityEngine;

// Defines one of a card's four attacks.
// Each attack is its own ScriptableObject so cards reference them individually
[CreateAssetMenu(fileName = "NewAttack", menuName = "Stratafield/Attack")]
public class AttackData : ScriptableObject
{
    [Header("Base")]
    public string attackName;
    [TextArea(2, 4)] public string description; // What it does, shown on the card (e.g. "Inflicts magic defense down on target")
    public int damage;
    public DamageType damageType;

    [Header("Range")]
    // How many spaces away this attack reaches. 0 = default (Ranged class reaches across the board, everyone else 1 space)
    public int range = 0;

    [Header("Area")]
    // Single hits only the target. AroundTarget also hits cards near the target.
    // AroundSelf hits cards near the attacker and Self hits only the attacker. Both are used by clicking the attacker.
    // areaSize and areaAffects are only for AroundTarget/AroundSelf. Area attacks never knock back.
    public AttackArea area = AttackArea.Single;
    public int areaSize = 1;                              // Spaces out from the center, 1 = 3x3
    public AreaAffects areaAffects = AreaAffects.Enemies; // Who else gets hit. Heals always hit allies

    [Header("Positional Bonus")]
    // Extra damage when the attacker is on the specified side of the target.
    // multiplier is ignored when positionalBonus is None. For a flat "+120 from behind" use Bonuses instead.
    public PositionalBonus positionalBonus = PositionalBonus.None;
    public float positionalMultiplier = 1.25f;

    [Header("Class Bonus")]
    // Extra damage when the target belongs to a specific class.
    // classBonusTarget and classMultiplier are ignored when hasClassBonus is false. For a flat bonus use Bonuses instead.
    public bool hasClassBonus;
    public CharacterClass classBonusTarget;
    public float classMultiplier = 1.25f;

    [Header("Knockback")]
    public bool hasKnockback;
    // Damage dealt if the knocked-back card hits a wall or any card (it stays in place).
    // If the card it hits is an enemy of the attacker, that card takes this damage too.
    public int knockbackCollisionDamage = 10;

    [Header("Status Effects")]
    public StatusEffectApplication[] targetStatusEffects; // Applied to the card being attacked
    public StatusEffectApplication[] selfStatusEffects;   // Applied to the card using this attack

    [Header("Bonuses")]
    // Extra damage or effects when a condition is met, e.g. "+120 damage if the target is Hindered"
    public AttackBonus[] bonuses;

    [Header("Special")]
    public bool ignoresDefense;                                  // Skips the target's defense (the damage floor still applies)
    public int executeAtOrBelowHP;                               // Defeats the target instantly if its HP is this or lower after the hit. 0 = off
    public CooldownPassive whileOnCooldown = CooldownPassive.None; // Passive the user has while this is the attack on cooldown
    public int passiveDamage = 100;                              // For DamageAttackers: damage dealt to each enemy that attacks the user

    [Header("Visual Effects")]
    // Optional. Leave empty to use the default effect for the damage type
    public GameObject hitEffectPrefab;  // Spawned on each card hit, removed after 3 seconds
    public GameObject projectilePrefab; // Flies from the attacker to the target when they aren't next to each other
}

// One status effect an attack applies. Amount/duration of 0 use the effect's default (see StatusEffectHandler)
[System.Serializable]
public class StatusEffectApplication
{
    public StatusEffectType type;
    public int amount;          // e.g. 80 for Magic Defense Down -80. 0 = default
    public int duration;        // In the affected card's own turns. 0 = default
    public bool ignoreStagger;  // Stun lands even without Stagger (e.g. a card stunning itself)
}

// Extra damage or effects an attack gets when its condition is met.
// Only fill in the field the condition uses (e.g. status for TargetHasStatus).
[System.Serializable]
public class AttackBonus
{
    public BonusCondition condition;

    [Header("Condition settings")]
    public StatusEffectType status;     // TargetHasStatus, AttackerHasStatus
    public CardData ally;               // AllyInPlay: this card is on the user's team and still alive
    public AttackData otherAttack;      // AttackOnCooldown: the user's last attack was this one
    public PositionalBonus side;        // FromSide
    public CharacterClass targetClass;  // TargetClass
    public int hp;                      // AttackerHPBelow / AttackerHPAbove
    public bool hpIsPercent;            // hp is a percent of max HP (50 = half) instead of a number

    [Header("Reward")]
    public int bonusDamage;             // Added to the base damage (or healing). For TargetDebuffCount it's per debuff
    public StatusEffectApplication[] extraTargetEffects;
    public StatusEffectApplication[] extraSelfEffects;
}
