using System.Collections.Generic;
using System.Text;

// Readable descriptions of cards, attacks and effects (rich text for TextMeshPro).
// Used by the card inspector and floating effect names. Card visuals can use it too.
public static class CardText
{
    // "MagicDefenseDown" -> "Magic Defense Down"
    public static string Name(StatusEffectType type)
    {
        string name = type.ToString();
        StringBuilder spaced = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i])) spaced.Append(' ');
            spaced.Append(name[i]);
        }
        return spaced.ToString();
    }

    // Base stat, plus the changed value in green (buffed) or red (debuffed)
    public static string Stat(int baseValue, int current)
    {
        if (current == baseValue) return baseValue.ToString();
        string color = current > baseValue ? "#66FF66" : "#FF6666";
        return $"{baseValue} <color={color}>({current})</color>";
    }

    // Active effects with amount and turns left, e.g. "Magic Defense Down 80 (2)"
    public static string ActiveEffects(CardInstance card)
    {
        List<string> effects = new List<string>();
        foreach (StatusEffectInstance effect in card.statusEffects.ActiveEffects)
        {
            string amount = effect.amount > 0 ? $" {effect.amount}" : "";
            effects.Add($"{Name(effect.type)}{amount} ({effect.turnsRemaining})");
        }
        return effects.Count > 0 ? string.Join(", ", effects) : "none";
    }

    // What an attack applies, with the real amount and duration, e.g. "Magic Defense Down 80 for 3 turns"
    public static string Applications(StatusEffectApplication[] applications)
    {
        List<string> parts = new List<string>();
        foreach (StatusEffectApplication effect in applications)
        {
            int amount = effect.amount > 0 ? effect.amount : StatusEffectHandler.GetDefaultAmount(effect.type);
            int duration = effect.duration > 0 ? effect.duration
                : effect.type == StatusEffectType.Stun ? StatusEffectHandler.StunDuration : StatusEffectHandler.DefaultDuration;
            string amountText = amount > 0 ? $" {amount}" : "";
            parts.Add($"{Name(effect.type)}{amountText} for {duration} turn{(duration == 1 ? "" : "s")}");
        }
        return string.Join(", ", parts);
    }

    // One attack: name and damage, then a smaller line with everything else it does.
    // card is the live card on the board (null if not on the board yet) so buffs and cooldown show
    public static string Attack(int number, AttackData attack, CardInstance card)
    {
        if (attack == null) return $"{number}. (empty)";

        string damage;
        if (attack.damageType == DamageType.Heal) damage = $"Heals {attack.damage}";
        else if (attack.damage == 0) damage = "No damage";
        else damage = $"{Stat(attack.damage, card != null ? card.GetAttackPower(attack) : attack.damage)} {attack.damageType}";

        string cooldown = card != null && card.lastAttackUsed == attack ? "  <color=#FF6666>[cooldown]</color>" : "";
        string line = $"{number}. <b>{attack.attackName}</b> - {damage}{cooldown}";

        List<string> extras = new List<string>();
        if (attack.range > 0) extras.Add($"Range {attack.range}");
        int width = attack.areaSize * 2 + 1;
        if (attack.area == AttackArea.AroundTarget) extras.Add($"Hits {attack.areaAffects.ToString().ToLower()} in a {width}x{width} around the target");
        if (attack.area == AttackArea.AroundSelf) extras.Add($"Hits {attack.areaAffects.ToString().ToLower()} within {attack.areaSize} space(s)");
        if (attack.area == AttackArea.Self) extras.Add("Self only");
        if (attack.positionalBonus != PositionalBonus.None) extras.Add($"x{attack.positionalMultiplier} from {attack.positionalBonus}");
        if (attack.hasClassBonus) extras.Add($"x{attack.classMultiplier} vs {attack.classBonusTarget}");
        if (attack.hasKnockback && attack.area == AttackArea.Single) extras.Add($"Knockback ({attack.knockbackCollisionDamage} if it hits something)");
        if (attack.ignoresDefense) extras.Add("Ignores defense");
        if (attack.executeAtOrBelowHP > 0) extras.Add($"Defeats enemies at {attack.executeAtOrBelowHP} HP or less");
        if (attack.targetStatusEffects != null && attack.targetStatusEffects.Length > 0) extras.Add("Applies " + Applications(attack.targetStatusEffects));
        if (attack.selfStatusEffects != null && attack.selfStatusEffects.Length > 0) extras.Add("Self " + Applications(attack.selfStatusEffects));
        if (attack.whileOnCooldown == CooldownPassive.DamageAttackers) extras.Add($"On cooldown: attackers take {attack.passiveDamage}");
        if (attack.whileOnCooldown == CooldownPassive.KnockbackImmune) extras.Add("On cooldown: can't be knocked back");
        if (attack.bonuses != null)
            foreach (AttackBonus bonus in attack.bonuses)
                if (bonus != null) extras.Add(Bonus(bonus, attack.damageType == DamageType.Heal));

        return extras.Count == 0 ? line : $"{line}\n    <size=85%>{string.Join(", ", extras)}</size>";
    }

    // e.g. "+120 if target has Hindered", "applies Speed Down 50 for 2 turns if Fjora is in play"
    public static string Bonus(AttackBonus bonus, bool isHeal)
    {
        List<string> rewards = new List<string>();
        if (bonus.bonusDamage > 0)
        {
            string each = bonus.condition == BonusCondition.TargetDebuffCount ? " per debuff on target" : "";
            rewards.Add($"+{bonus.bonusDamage}{(isHeal ? " healing" : "")}{each}");
        }
        if (bonus.extraTargetEffects != null && bonus.extraTargetEffects.Length > 0) rewards.Add("applies " + Applications(bonus.extraTargetEffects));
        if (bonus.extraSelfEffects != null && bonus.extraSelfEffects.Length > 0) rewards.Add("self " + Applications(bonus.extraSelfEffects));

        string hp = bonus.hpIsPercent ? $"{bonus.hp}% HP" : $"{bonus.hp} HP";
        string condition;
        switch (bonus.condition)
        {
            case BonusCondition.TargetHasStatus:   condition = $"if target has {Name(bonus.status)}"; break;
            case BonusCondition.TargetDebuffCount: condition = ""; break;
            case BonusCondition.AttackerHasStatus: condition = $"if this card has {Name(bonus.status)}"; break;
            case BonusCondition.AllyInPlay:        condition = $"if {(bonus.ally != null ? bonus.ally.cardName : "?")} is in play"; break;
            case BonusCondition.AttackOnCooldown:  condition = $"if {(bonus.otherAttack != null ? bonus.otherAttack.attackName : "?")} is on cooldown"; break;
            case BonusCondition.FromSide:          condition = $"from the {bonus.side.ToString().ToLower()}"; break;
            case BonusCondition.TargetClass:       condition = $"vs {bonus.targetClass}"; break;
            case BonusCondition.AttackerHPBelow:   condition = $"if below {hp}"; break;
            case BonusCondition.AttackerHPAbove:   condition = $"if above {hp}"; break;
            default:                               condition = ""; break;
        }
        return $"{string.Join(" and ", rewards)} {condition}".Trim();
    }
}
