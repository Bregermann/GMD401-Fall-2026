// Shared enums used across CardData and AttackData.
// Add new status effects or classes here as the game expands.

// Controls turn-order tiebreaking. Priority descends left to right:
// Assassin > Support > Attacker > Healer > Ranged > Mage > Defender
public enum CharacterClass
{
    Attacker,
    Defender,
    Healer,
    Support,
    Mage,
    Assassin,
    Ranged
}

// Determines which defense stat reduces incoming damage, or whether the move heals instead.
// Shown on cards as red (Physical), blue (Magic), or green (Heal).
public enum DamageType
{
    Physical,
    Magic,
    Heal
}

// Direction of the attacker
// Set to None if the attack has no positional requirement.
public enum PositionalBonus
{
    None,
    Side,
    Back,
    Front
}

// Every status effect in the game that weve made so far
// Applying the same effect to a card that already has it resets the timer rather than stacking.
public enum StatusEffectType
{
    //Buffs 
    PhysicalAttackUp,   // +100 physical damage dealt, 2 turns
    MagicAttackUp,      // +100 magic damage dealt, 2 turns
    PhysicalDefenseUp,  // +50 physical defense, 2 turns
    MagicDefenseUp,     // +50 magic defense, 2 turns
    SpeedUp,            // +50 speed (may shift turn order), 2 turns
    Haste,              // +1 move space, 2 turns
    Regeneration,       // Heal 100 HP at start of turn, 2 turns

    //Debuffs
    PhysicalAttackDown,  // -100 physical damage dealt (min 0), 2 turns
    MagicAttackDown,     // -100 magic damage dealt (min 0), 2 turns
    PhysicalDefenseDown, // -50 physical defense (min 0), 2 turns
    MagicDefenseDown,    // -50 magic defense (min 0), 2 turns
    SpeedDown,           // -50 speed (min 10, may shift turn order), 2 turns
    Stagger,             // Does nothing alone; required before Stun can be applied, 2 turns
    Stun,                // Target loses its next turn; card must already have Stagger, 1 turn
    Burn,                // 50 damage at turn start, ignores defense, 2 turns
    Poison,              // 100 damage at turn end, ignores defense, 2 turns
    Hindered,            // -1 move space, 2 turns
    HealBlock            // Card cannot receive healing, 2 turns
}
