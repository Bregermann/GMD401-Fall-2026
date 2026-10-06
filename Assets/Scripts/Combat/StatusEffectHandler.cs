using System.Collections.Generic;

// Tracks all active status effects on one card.
// Each CardInstance has one. Stat floors (min 0, min 10) are applied in CardInstance.
public class StatusEffectHandler
{
    // Durations, counted in the affected card's own turns
    public const int DefaultDuration = 2;
    public const int StunDuration = 1;

    // Effect values
    public const int AttackModifier = 100;
    public const int DefenseModifier = 50;
    public const int SpeedModifier = 50;
    public const int MoveModifier = 1;
    public const int RegenerationAmount = 100;
    public const int BurnDamage = 50;
    public const int PoisonDamage = 100;

    private readonly List<StatusEffectInstance> activeEffects = new List<StatusEffectInstance>();

    public IReadOnlyList<StatusEffectInstance> ActiveEffects => activeEffects;

    // Adds an effect, or resets its timer if the card already has it.
    // duringOwnTurn adds an extra turn so it isn't counted down at the end of the current turn.
    public void Apply(StatusEffectType type, bool duringOwnTurn)
    {
        // TODO: Stun should require Stagger first - waiting on rules
        int duration = type == StatusEffectType.Stun ? StunDuration : DefaultDuration;
        if (duringOwnTurn) duration++;

        StatusEffectInstance existing = Find(type);
        if (existing != null)
            existing.turnsRemaining = duration;
        else
            activeEffects.Add(new StatusEffectInstance(type, duration));
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

    // Called at the end of the card's turn. Counts every effect down and removes expired ones.
    public void TickDown()
    {
        foreach (StatusEffectInstance effect in activeEffects)
            effect.turnsRemaining--;

        activeEffects.RemoveAll(e => e.turnsRemaining <= 0);
    }

    // Stat changes from effects. A buff and debuff on the same stat cancel out.
    public int GetPhysicalAttackModifier() => BuffMinusDebuff(StatusEffectType.PhysicalAttackUp, StatusEffectType.PhysicalAttackDown, AttackModifier);
    public int GetMagicAttackModifier() => BuffMinusDebuff(StatusEffectType.MagicAttackUp, StatusEffectType.MagicAttackDown, AttackModifier);
    public int GetPhysicalDefenseModifier() => BuffMinusDebuff(StatusEffectType.PhysicalDefenseUp, StatusEffectType.PhysicalDefenseDown, DefenseModifier);
    public int GetMagicDefenseModifier() => BuffMinusDebuff(StatusEffectType.MagicDefenseUp, StatusEffectType.MagicDefenseDown, DefenseModifier);
    public int GetSpeedModifier() => BuffMinusDebuff(StatusEffectType.SpeedUp, StatusEffectType.SpeedDown, SpeedModifier);
    public int GetMoveModifier() => BuffMinusDebuff(StatusEffectType.Haste, StatusEffectType.Hindered, MoveModifier);

    private int BuffMinusDebuff(StatusEffectType buff, StatusEffectType debuff, int amount)
    {
        int total = 0;
        if (Has(buff)) total += amount;
        if (Has(debuff)) total -= amount;
        return total;
    }

    private StatusEffectInstance Find(StatusEffectType type)
    {
        return activeEffects.Find(e => e.type == type);
    }
}
