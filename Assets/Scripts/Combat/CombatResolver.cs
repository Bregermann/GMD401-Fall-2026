using System.Collections.Generic;
using UnityEngine;

// What happened during one attack, for UI and animations
public class AttackResult
{
    public int damageDealt;
    public int amountHealed;
    public bool wasKnockedBack;        // Target moved 1 space
    public bool hitObstacle;           // Knockback was blocked by a wall or card
    public int collisionDamage;        // Damage from hitting the obstacle
    public CardInstance collidedWith;  // Card the target was knocked into (null if a wall)
    public List<CardInstance> defeatedCards = new List<CardInstance>();
}

// Handles attacking: targeting, damage, heals, status effects and knockback.
public static class CombatResolver
{
    public const int MeleeRange = 1;
    public const int RangedRange = 100;  // Placeholder
    public const int HealRange = 1;      // Change to make heals ranged
    public const int DamageFloor = 10;   // Damaging attacks always deal at least this

    // ---- Targeting ----

    public static int GetRange(CardInstance attacker, AttackData attack)
    {
        if (attack.damageType == DamageType.Heal) return HealRange;
        if (attacker.data.characterClass == CharacterClass.Ranged) return RangedRange;
        return MeleeRange;
    }

    // True if the attacker can use this attack on this target right now.
    // Physical/Magic hit enemies only. Heals hit self or allies only.
    public static bool CanTarget(CardInstance attacker, AttackData attack, CardInstance target, BoardState board)
    {
        if (attacker == null || attack == null || target == null) return false;
        if (!board.IsOnBoard(attacker) || !board.IsOnBoard(target)) return false;
        if (!attacker.IsAlive || !target.IsAlive || attacker.IsStunned) return false;
        if (!attacker.CanUseAttack(attack)) return false;

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

    // Power x positional x class, rounded, minus defense, then the damage floor
    public static int CalculateDamage(CardInstance attacker, AttackData attack, CardInstance target)
    {
        if (attack.damage == 0) return 0; // Status-only attack

        float damage = attacker.GetAttackPower(attack);

        if (attack.positionalBonus != PositionalBonus.None && GetSideHit(attacker, target) == attack.positionalBonus)
            damage *= attack.positionalMultiplier;

        if (attack.hasClassBonus && target.data.characterClass == attack.classBonusTarget)
            damage *= attack.classMultiplier;

        int rounded = Mathf.FloorToInt(damage + 0.5f); // Rounds .5 up (Mathf.RoundToInt rounds .5 to even)
        int defense = attack.damageType == DamageType.Physical ? target.PhysicalDefense : target.MagicDefense;

        return Mathf.Max(DamageFloor, rounded - defense);
    }

    // ---- Attacking ----

    // Runs the whole attack. Returns null if the attack isn't allowed (see CanTarget).
    public static AttackResult ResolveAttack(CardInstance attacker, AttackData attack, CardInstance target, BoardState board)
    {
        if (!CanTarget(attacker, attack, target, board)) return null;

        AttackResult result = new AttackResult();

        // Heals get no bonuses
        if (attack.damageType == DamageType.Heal)
        {
            int hpBefore = target.currentHP;
            target.Heal(attack.damage);
            result.amountHealed = target.currentHP - hpBefore;
        }
        else
        {
            result.damageDealt = CalculateDamage(attacker, attack, target);
            target.TakeDamage(result.damageDealt);
        }

        if (target.IsAlive && attack.targetStatusEffects != null)
        {
            foreach (StatusEffectType effect in attack.targetStatusEffects)
                target.ApplyStatusEffect(effect);
        }

        if (attack.selfStatusEffects != null)
        {
            foreach (StatusEffectType effect in attack.selfStatusEffects)
                attacker.ApplyStatusEffect(effect);
        }

        if (attack.hasKnockback && target.IsAlive && target != attacker)
            ResolveKnockback(attacker, attack, target, board, result);

        attacker.RecordAttackUsed(attack);

        // Remove anything that died
        RemoveIfDead(target, board, result);
        if (result.collidedWith != null) RemoveIfDead(result.collidedWith, board, result);

        return result;
    }

    // Pushes the target 1 space away from the attacker. If a wall or any card is in the way,
    // the target stays put and takes collision damage. An enemy card it hits takes it too.
    // Collision damage ignores defense.
    private static void ResolveKnockback(CardInstance attacker, AttackData attack, CardInstance target, BoardState board, AttackResult result)
    {
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
