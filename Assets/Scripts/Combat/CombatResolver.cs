using System.Collections.Generic;
using UnityEngine;

// One card hit by an attack. Area attacks have one per card
public class AttackHit
{
    public CardInstance card;
    public int damage;
    public int healed;
    public bool executed;              // Defeated instantly by executeAtOrBelowHP
}

// What happened during one attack, for UI and animations
public class AttackResult
{
    public int damageDealt;            // On the main target
    public int amountHealed;           // On the main target
    public List<AttackHit> hits = new List<AttackHit>(); // Every card hit, main target first
    public int reflectedDamage;        // Damage the attacker took back from passives like Hangry
    public bool knockbackBlocked;      // Target ignored the knockback (e.g. Anchored Stance)
    public bool wasKnockedBack;        // Target moved 1 space
    public bool hitObstacle;           // Knockback was blocked by a wall or card
    public int collisionDamage;        // Damage from hitting the obstacle
    public CardInstance collidedWith;  // Card the target was knocked into (null if a wall)
    public List<CardInstance> defeatedCards = new List<CardInstance>();
}

// Handles attacking: targeting, area attacks, bonuses, damage, heals, status effects, knockback and passives.
public static class CombatResolver
{
    public const int MeleeRange = 1;
    public const int RangedRange = 100;  // Placeholder
    public const int HealRange = 1;      // Change to make heals ranged
    public const int DamageFloor = 10;   // Damaging attacks always deal at least this

    // ---- Targeting ----

    public static int GetRange(CardInstance attacker, AttackData attack)
    {
        if (attack.range > 0) return attack.range; // Set on the attack itself
        if (attack.damageType == DamageType.Heal) return HealRange;
        if (attacker.data.characterClass == CharacterClass.Ranged) return RangedRange;
        return MeleeRange;
    }

    // True if the attacker can use this attack on this target right now.
    // Physical/Magic hit enemies only. Heals hit self or allies only.
    // AroundSelf and Self attacks are used on the attacker itself.
    public static bool CanTarget(CardInstance attacker, AttackData attack, CardInstance target, BoardState board)
    {
        if (attacker == null || attack == null || target == null) return false;
        if (!board.IsOnBoard(attacker) || !board.IsOnBoard(target)) return false;
        if (!attacker.IsAlive || !target.IsAlive || attacker.IsStunned) return false;
        if (!attacker.CanUseAttack(attack)) return false;

        if (UsedOnSelf(attack)) return target == attacker;

        if (attack.damageType == DamageType.Heal)
        {
            if (target.owner != attacker.owner) return false;
            if (target == attacker) return true; // Can heal self
        }
        else if (target.owner == attacker.owner)
        {
            return false;
        }

        return HasLineOfSight(attacker, target, GetRange(attacker, attack), board);
    }

    // Attacks used by clicking the attacker itself
    public static bool UsedOnSelf(AttackData attack)
    {
        return attack.area == AttackArea.AroundSelf || attack.area == AttackArea.Self;
    }

    // Straight lines only (same row or column). Any card in between blocks it.
    public static bool HasLineOfSight(CardInstance from, CardInstance to, int range, BoardState board)
    {
        bool sameRow = from.row == to.row;
        bool sameColumn = from.column == to.column;
        if (sameRow == sameColumn) return false; // Diagonal, or the same slot

        int distance = Mathf.Abs(to.row - from.row) + Mathf.Abs(to.column - from.column);
        if (distance > range) return false;

        int rowStep = System.Math.Sign(to.row - from.row);
        int colStep = System.Math.Sign(to.column - from.column);
        for (int i = 1; i < distance; i++)
        {
            if (board.GetCard(from.row + rowStep * i, from.column + colStep * i) != null)
                return false;
        }
        return true;
    }

    // Every card the attack hits, main target first. Area attacks add cards within areaSize
    // of the center (diagonals count) that are on the side areaAffects picks. Heals always hit allies.
    // The attacker is only included for Self attacks.
    public static List<CardInstance> GetAffectedCards(CardInstance attacker, AttackData attack, CardInstance target, BoardState board)
    {
        List<CardInstance> affected = new List<CardInstance>();
        if (attack.area == AttackArea.Self)
        {
            affected.Add(attacker);
            return affected;
        }

        if (attack.area != AttackArea.AroundSelf) affected.Add(target);
        if (attack.area == AttackArea.Single) return affected;

        CardInstance center = attack.area == AttackArea.AroundSelf ? attacker : target;
        bool wantAllies = attack.damageType == DamageType.Heal || attack.areaAffects == AreaAffects.Allies;

        foreach (CardInstance card in board.GetAllCards())
        {
            if (card == attacker || affected.Contains(card) || !card.IsAlive) continue;

            int distance = Mathf.Max(Mathf.Abs(card.row - center.row), Mathf.Abs(card.column - center.column));
            if (distance > attack.areaSize) continue;

            bool isAlly = card.owner == attacker.owner;
            if (isAlly == wantAllies) affected.Add(card);
        }
        return affected;
    }

    // Which side of the target the attacker is on.
    // Player 1 faces toward row 7, Player 2 faces toward row 0.
    public static PositionalBonus GetSideHit(CardInstance attacker, CardInstance target)
    {
        if (attacker.row == target.row) return PositionalBonus.Side;

        int facing = target.owner == 1 ? 1 : -1;
        bool inFront = (attacker.row - target.row) * facing > 0;
        return inFront ? PositionalBonus.Front : PositionalBonus.Back;
    }

    // ---- Damage ----

    // (Power + bonus damage) x positional x class, rounded, minus defense, then the damage floor
    public static int CalculateDamage(CardInstance attacker, AttackData attack, CardInstance target, int bonusDamage = 0)
    {
        if (attack.damage == 0 && bonusDamage == 0) return 0; // Status-only attack

        float damage = attacker.GetAttackPower(attack) + bonusDamage;

        if (attack.positionalBonus != PositionalBonus.None && GetSideHit(attacker, target) == attack.positionalBonus)
            damage *= attack.positionalMultiplier;

        if (attack.hasClassBonus && target.data.characterClass == attack.classBonusTarget)
            damage *= attack.classMultiplier;

        int rounded = Mathf.FloorToInt(damage + 0.5f); // Rounds .5 up (Mathf.RoundToInt rounds .5 to even)
        int defense = attack.damageType == DamageType.Physical ? target.PhysicalDefense : target.MagicDefense;
        if (attack.ignoresDefense) defense = 0;

        return Mathf.Max(DamageFloor, rounded - defense);
    }

    // ---- Bonuses ----

    // Adds up every bonus whose condition is met against this card, and collects their extra effects
    private static int CollectBonuses(CardInstance attacker, AttackData attack, CardInstance card, BoardState board,
                                      List<StatusEffectApplication> extraTargetEffects, List<StatusEffectApplication> extraSelfEffects)
    {
        if (attack.bonuses == null) return 0;

        int bonusDamage = 0;
        foreach (AttackBonus bonus in attack.bonuses)
        {
            int times = TimesMet(bonus, attacker, card, board);
            if (times <= 0) continue;

            bonusDamage += bonus.bonusDamage * times;
            if (bonus.extraTargetEffects != null) extraTargetEffects.AddRange(bonus.extraTargetEffects);
            if (bonus.extraSelfEffects != null) extraSelfEffects.AddRange(bonus.extraSelfEffects);
        }
        return bonusDamage;
    }

    // How many times a bonus applies: 0 or 1, or the number of debuffs for TargetDebuffCount.
    // Checked before this attack's own effects land
    public static int TimesMet(AttackBonus bonus, CardInstance attacker, CardInstance target, BoardState board)
    {
        switch (bonus.condition)
        {
            case BonusCondition.TargetHasStatus:   return target.statusEffects.Has(bonus.status) ? 1 : 0;
            case BonusCondition.TargetDebuffCount: return target.statusEffects.DebuffCount();
            case BonusCondition.AttackerHasStatus: return attacker.statusEffects.Has(bonus.status) ? 1 : 0;
            case BonusCondition.AllyInPlay:        return board.GetCards(attacker.owner).Exists(c => c != attacker && c.IsAlive && c.data == bonus.ally) ? 1 : 0;
            case BonusCondition.AttackOnCooldown:  return bonus.otherAttack != null && attacker.lastAttackUsed == bonus.otherAttack ? 1 : 0;
            case BonusCondition.FromSide:          return target != attacker && GetSideHit(attacker, target) == bonus.side ? 1 : 0;
            case BonusCondition.TargetClass:       return target.data.characterClass == bonus.targetClass ? 1 : 0;
            case BonusCondition.AttackerHPBelow:   return attacker.currentHP < HPThreshold(bonus, attacker) ? 1 : 0;
            case BonusCondition.AttackerHPAbove:   return attacker.currentHP > HPThreshold(bonus, attacker) ? 1 : 0;
            default:                               return 0;
        }
    }

    // hp as a number, or as a percent of max HP
    private static int HPThreshold(AttackBonus bonus, CardInstance attacker)
    {
        return bonus.hpIsPercent ? Mathf.RoundToInt(attacker.data.maxHP * bonus.hp / 100f) : bonus.hp;
    }

    // ---- Attacking ----

    // Runs the whole attack. Returns null if the attack isn't allowed (see CanTarget).
    public static AttackResult ResolveAttack(CardInstance attacker, AttackData attack, CardInstance target, BoardState board)
    {
        if (!CanTarget(attacker, attack, target, board)) return null;

        AttackResult result = new AttackResult();
        List<StatusEffectApplication> extraSelfEffects = new List<StatusEffectApplication>();
        List<CardInstance> affected = GetAffectedCards(attacker, attack, target, board);

        // Damage/heal, bonuses and status effects on every card hit. Bonuses are checked per card
        foreach (CardInstance card in affected)
        {
            AttackHit hit = new AttackHit { card = card };
            List<StatusEffectApplication> extraTargetEffects = new List<StatusEffectApplication>();
            int bonus = CollectBonuses(attacker, attack, card, board, extraTargetEffects, extraSelfEffects);

            // Heals get no multipliers or defense
            if (attack.damageType == DamageType.Heal)
            {
                int hpBefore = card.currentHP;
                card.Heal(attack.damage + bonus);
                hit.healed = card.currentHP - hpBefore;
            }
            else
            {
                hit.damage = CalculateDamage(attacker, attack, card, bonus);
                card.TakeDamage(hit.damage);
            }

            // Instant defeat at low HP (enemies only)
            if (attack.executeAtOrBelowHP > 0 && card.IsAlive && card.owner != attacker.owner && card.currentHP <= attack.executeAtOrBelowHP)
            {
                card.TakeDamage(card.currentHP);
                hit.executed = true;
            }

            // Base effects first so a bonus version of the same effect replaces it
            if (card.IsAlive)
            {
                ApplyEffects(card, attack.targetStatusEffects);
                ApplyEffects(card, extraTargetEffects);
            }

            // Hangry-style passive: enemies that attack this card take damage back
            if (card.owner != attacker.owner && card.ActivePassive == CooldownPassive.DamageAttackers)
            {
                attacker.TakeDamage(card.lastAttackUsed.passiveDamage);
                result.reflectedDamage += card.lastAttackUsed.passiveDamage;
            }

            result.hits.Add(hit);
        }

        // Self bonuses still count when an area attack found nobody
        if (affected.Count == 0)
            CollectBonuses(attacker, attack, attacker, board, new List<StatusEffectApplication>(), extraSelfEffects);

        // Main target's numbers (AroundSelf attacks have no main target)
        if (result.hits.Count > 0 && result.hits[0].card == target)
        {
            result.damageDealt = result.hits[0].damage;
            result.amountHealed = result.hits[0].healed;
        }

        if (attacker.IsAlive)
        {
            ApplyEffects(attacker, attack.selfStatusEffects);
            ApplyEffects(attacker, extraSelfEffects);
        }

        // Area attacks never knock back
        if (attack.hasKnockback && attack.area == AttackArea.Single && target.IsAlive && target != attacker)
            ResolveKnockback(attacker, attack, target, board, result);

        attacker.RecordAttackUsed(attack);

        // Remove anything that died, including the attacker if a passive got it
        foreach (AttackHit hit in result.hits)
            RemoveIfDead(hit.card, board, result);
        if (result.collidedWith != null) RemoveIfDead(result.collidedWith, board, result);
        RemoveIfDead(attacker, board, result);

        return result;
    }

    private static void ApplyEffects(CardInstance card, IEnumerable<StatusEffectApplication> effects)
    {
        if (effects == null) return;
        foreach (StatusEffectApplication effect in effects)
            card.ApplyStatusEffect(effect);
    }

    // Pushes the target 1 space away from the attacker. If a wall or any card is in the way,
    // the target stays put and takes collision damage. An enemy card it hits takes it too.
    // Collision damage ignores defense. Cards with a KnockbackImmune passive aren't moved and take nothing.
    private static void ResolveKnockback(CardInstance attacker, AttackData attack, CardInstance target, BoardState board, AttackResult result)
    {
        if (target.ActivePassive == CooldownPassive.KnockbackImmune)
        {
            result.knockbackBlocked = true;
            return;
        }

        int newRow = target.row + System.Math.Sign(target.row - attacker.row);
        int newColumn = target.column + System.Math.Sign(target.column - attacker.column);

        if (board.IsEmpty(newRow, newColumn))
        {
            board.MoveCard(target, newRow, newColumn);
            result.wasKnockedBack = true;
            return;
        }

        result.hitObstacle = true;
        result.collisionDamage = attack.knockbackCollisionDamage;
        target.TakeDamage(attack.knockbackCollisionDamage);

        CardInstance hitCard = board.GetCard(newRow, newColumn); // Null if it's a wall
        if (hitCard != null)
        {
            result.collidedWith = hitCard;
            if (hitCard.owner != attacker.owner)
                hitCard.TakeDamage(attack.knockbackCollisionDamage);
        }
    }

    private static void RemoveIfDead(CardInstance card, BoardState board, AttackResult result)
    {
        if (card.IsAlive || result.defeatedCards.Contains(card)) return;

        board.Remove(card);
        result.defeatedCards.Add(card);
    }
}
