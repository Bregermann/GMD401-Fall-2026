using System.Collections.Generic;

// Tracks all active status effects on one card.
// Each CardInstance has one. Stat floors (min 0, min 10) are applied in CardInstance.
public class StatusEffectHandler
{
    // Default durations, counted in the affected card's own turns. Attacks can set their own
    public const int DefaultDuration = 2;
    public const int StunDuration = 1;

    // Default effect values. Attacks can set their own
    public const int AttackModifier = 100;
    public const int DefenseModifier = 50;
    public const int SpeedModifier = 50;
    public const int MoveModifier = 1;
    public const int RegenerationAmount = 100;
    public const int BurnDamage = 50;
    public const int PoisonDamage = 100;

    private readonly List<StatusEffectInstance> activeEffects = new List<StatusEffectInstance>();

    public IReadOnlyList<StatusEffectInstance> ActiveEffects => activeEffects;

    // Adds an effect. If the card already has it, the new one replaces it (amount and timer).
    // amount/duration of 0 use the defaults above.
    // duringOwnTurn adds an extra turn so it isn't counted down at the end of the current turn.
    public void Apply(StatusEffectType type, bool duringOwnTurn, int amount = 0, int duration = 0)
    {
        // Stun needs Stagger first. Without it, the card gets a default Stagger instead.
        // Landing a Stun removes the Stagger, so it has to be reapplied.
        if (type == StatusEffectType.Stun)
        {
            if (!Has(StatusEffectType.Stagger))
            {
                type = StatusEffectType.Stagger;
                amount = 0;
                duration = 0;
            }
            else
            {
                Remove(StatusEffectType.Stagger);
            }
        }

        if (amount <= 0) amount = GetDefaultAmount(type);
        if (duration <= 0) duration = type == StatusEffectType.Stun ? StunDuration : DefaultDuration;
        if (duringOwnTurn) duration++;

        Remove(type); // New one replaces the old one
        activeEffects.Add(new StatusEffectInstance(type, duration, amount));
    }

    public void Remove(StatusEffectType type)
    {
        activeEffects.RemoveAll(e => e.type == type);
    }

    public void ClearAll()
    {
        activeEffects.Clear();
    }

    public bool Has(StatusEffectType type)
    {
        return Find(type) != null;
    }

    // How strong an active effect is (e.g. Burn damage). 0 if the card doesn't have it
    public int GetAmount(StatusEffectType type)
    {
        StatusEffectInstance effect = Find(type);
        return effect != null ? effect.amount : 0;
    }

    // Called at the end of the card's turn. Counts every effect down and removes expired ones.
    public void TickDown()
    {
        foreach (StatusEffectInstance effect in activeEffects)
            effect.turnsRemaining--;

        activeEffects.RemoveAll(e => e.turnsRemaining <= 0);
    }

    // Stat changes from effects. A buff and debuff on the same stat cancel out (by amount).
    public int GetPhysicalAttackModifier() => BuffMinusDebuff(StatusEffectType.PhysicalAttackUp, StatusEffectType.PhysicalAttackDown);
    public int GetMagicAttackModifier() => BuffMinusDebuff(StatusEffectType.MagicAttackUp, StatusEffectType.MagicAttackDown);
    public int GetPhysicalDefenseModifier() => BuffMinusDebuff(StatusEffectType.PhysicalDefenseUp, StatusEffectType.PhysicalDefenseDown);
    public int GetMagicDefenseModifier() => BuffMinusDebuff(StatusEffectType.MagicDefenseUp, StatusEffectType.MagicDefenseDown);
    public int GetSpeedModifier() => BuffMinusDebuff(StatusEffectType.SpeedUp, StatusEffectType.SpeedDown);
    public int GetMoveModifier() => BuffMinusDebuff(StatusEffectType.Haste, StatusEffectType.Hindered);

    private int BuffMinusDebuff(StatusEffectType buff, StatusEffectType debuff)
    {
        return GetAmount(buff) - GetAmount(debuff);
    }

    // The amount an effect uses when the attack doesn't set one
    public static int GetDefaultAmount(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.PhysicalAttackUp:
            case StatusEffectType.PhysicalAttackDown:
            case StatusEffectType.MagicAttackUp:
            case StatusEffectType.MagicAttackDown:
                return AttackModifier;
            case StatusEffectType.PhysicalDefenseUp:
            case StatusEffectType.PhysicalDefenseDown:
            case StatusEffectType.MagicDefenseUp:
            case StatusEffectType.MagicDefenseDown:
                return DefenseModifier;
            case StatusEffectType.SpeedUp:
            case StatusEffectType.SpeedDown:
                return SpeedModifier;
            case StatusEffectType.Haste:
            case StatusEffectType.Hindered:
                return MoveModifier;
            case StatusEffectType.Regeneration: return RegenerationAmount;
            case StatusEffectType.Burn:         return BurnDamage;
            case StatusEffectType.Poison:       return PoisonDamage;
            default:                            return 0; // Stagger, Stun, HealBlock have no amount
        }
    }

    private StatusEffectInstance Find(StatusEffectType type)
    {
        return activeEffects.Find(e => e.type == type);
    }
}
