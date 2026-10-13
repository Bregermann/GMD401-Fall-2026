using UnityEngine;

// A card while it's in a match. CardData is the base card and never changes,
// this tracks everything that does (HP, position, owner, status effects, cooldown).
public class CardInstance
{
    public CardData data { get; private set; }
    public int owner;               // Player 1 or 2
    public int currentHP;
    public int row;                 // Board position
    public int column;
    public AttackData lastAttackUsed; // On cooldown until a different attack is used or the attack is skipped
    public StatusEffectHandler statusEffects = new StatusEffectHandler();

    public bool IsAlive => currentHP > 0;
    public bool IsTakingTurn { get; private set; }
    public bool IsStunned => statusEffects.Has(StatusEffectType.Stun); // Can't move or attack this turn

    public CardInstance(CardData data, int owner, int row, int column)
    {
        this.data = data;
        this.owner = owner;
        this.row = row;
        this.column = column;
        currentHP = data.maxHP;
    }

    // Stats with status effects included
    public const int MinSpeed = 10; // SpeedDown can't push speed below this

    // Floor only stops debuffs, a card with base speed under 10 keeps it
    public int Speed => Mathf.Max(Mathf.Min(data.speed, MinSpeed), data.speed + statusEffects.GetSpeedModifier());
    public int MoveSpaces => Mathf.Max(0, data.moveSpaces + statusEffects.GetMoveModifier());
    public int PhysicalDefense => Mathf.Max(0, data.physicalDefense + statusEffects.GetPhysicalDefenseModifier());
    public int MagicDefense => Mathf.Max(0, data.magicDefense + statusEffects.GetMagicDefenseModifier());

    // Attack damage after attack buffs/debuffs, before multipliers and defense. Heals are unchanged.
    public int GetAttackPower(AttackData attack)
    {
        switch (attack.damageType)
        {
            case DamageType.Physical: return Mathf.Max(0, attack.damage + statusEffects.GetPhysicalAttackModifier());
            case DamageType.Magic:    return Mathf.Max(0, attack.damage + statusEffects.GetMagicAttackModifier());
            default:                  return attack.damage;
        }
    }

    // ---- Health ----

    public void TakeDamage(int amount)
    {
        currentHP = Mathf.Max(0, currentHP - amount);
    }

    // Returns false if the card has HealBlock or is already dead
    public bool Heal(int amount)
    {
        if (!IsAlive || statusEffects.Has(StatusEffectType.HealBlock)) return false;
        currentHP = Mathf.Min(data.maxHP, currentHP + amount);
        return true;
    }

    // ---- Status effects ----

    public void ApplyStatusEffect(StatusEffectType type)
    {
        statusEffects.Apply(type, IsTakingTurn);
    }

    // ---- Attacks ----

    public AttackData[] GetAttacks()
    {
        return new AttackData[] { data.attack1, data.attack2, data.attack3, data.attack4 };
    }

    public bool CanUseAttack(AttackData attack)
    {
        return attack != null && attack != lastAttackUsed;
    }

    public void RecordAttackUsed(AttackData attack)
    {
        lastAttackUsed = attack;
    }

    // Skipping the attack clears the cooldown
    public void SkipAttack()
    {
        lastAttackUsed = null;
    }

    // ---- Turn hooks (called by the turn manager) ----

    // Regeneration then Burn happen at the start of the turn.
    // A stunned turn still happens (effects still trigger and count down), it just counts as skipping the attack.
    // If Burn kills the card, the turn manager should skip the rest of its turn (check IsAlive after this).
    public void StartTurn()
    {
        IsTakingTurn = true;
        if (IsStunned) SkipAttack();
        if (statusEffects.Has(StatusEffectType.Regeneration)) Heal(StatusEffectHandler.RegenerationAmount);
        if (statusEffects.Has(StatusEffectType.Burn)) TakeDamage(StatusEffectHandler.BurnDamage);
    }

    // Poison happens at the end of the turn, then effects count down
    public void EndTurn()
    {
        if (statusEffects.Has(StatusEffectType.Poison)) TakeDamage(StatusEffectHandler.PoisonDamage);
        statusEffects.TickDown();
        IsTakingTurn = false;
    }
}
