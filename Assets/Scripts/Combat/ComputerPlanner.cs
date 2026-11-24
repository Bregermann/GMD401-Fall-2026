using System.Collections.Generic;
using UnityEngine;

// The computer opponent's brain: picks what the active card should do this turn. Pure game data (no scene objects),
// so it can be tested like the rest of combat. ComputerPlayer carries the plan out.
//
// How it picks ("utility scoring"): for every square the card could move to (including staying put), it imagines
// standing there and previews every usable attack on every legal target with CombatResolver.PreviewAttack (the same
// maths as the real attack, nothing changes). Each option gets a score:
//   + damage dealt (more on wounded cards), a big bonus for defeating a card, heals on hurt allies,
//     useful status effects (a Stun that will land is worth a lot), knockback into walls and cards
//   - damage to its own team, damage reflected back at it
//   + where it ends up: close enough to fight, not standing in reach of lots of enemies (fragile cards care more)
// The best score wins, with a little randomness so it isn't predictable
public static class ComputerPlanner
{
    public class Plan
    {
        public Vector2Int? moveTo;     // Null = stay
        public int attackIndex = -1;   // -1 = no attack
        public CardInstance target;
        public float score = float.MinValue;

        public string Describe(CardInstance card)
        {
            string move = moveTo.HasValue ? $"move to {moveTo.Value.x},{moveTo.Value.y}" : "stay";
            string attack = attackIndex >= 0 ? $", {card.GetAttacks()[attackIndex].attackName} on {target.data.cardName}" : ", no attack";
            return $"{move}{attack} (score {score:0})";
        }
    }

    // The best thing to do. moveOptions = squares (row, column) the card could move to; staying is always included.
    // randomness = how much scores wobble (0 = always the same choice, used by tests)
    public static Plan Choose(CardInstance card, BoardState board, IList<Vector2Int> moveOptions, float randomness = 0.03f)
    {
        AttackData[] attacks = card.GetAttacks();
        Plan best = new Plan();

        List<Vector2Int?> spots = new List<Vector2Int?> { null };
        foreach (Vector2Int option in moveOptions) spots.Add(option);

        int homeRow = card.row, homeColumn = card.column;
        foreach (Vector2Int? spot in spots)
        {
            // Imagine standing there (only the combat data moves, and it's always put back)
            bool moved = spot.HasValue && board.MoveCard(card, spot.Value.x, spot.Value.y);
            if (spot.HasValue && !moved) continue;
            try
            {
                float position = PositionScore(card, board);
                Consider(ref best, spot, -1, null, position, randomness);

                for (int i = 0; i < attacks.Length; i++)
                {
                    AttackData attack = attacks[i];
                    if (attack == null || !card.CanUseAttack(attack)) continue;

                    List<CardInstance> targets = CombatResolver.UsedOnSelf(attack) ? new List<CardInstance> { card } : board.GetAllCards();
                    foreach (CardInstance target in targets)
                    {
                        AttackResult preview = CombatResolver.PreviewAttack(card, attack, target, board);
                        if (preview == null) continue;
                        float value = AttackScore(card, attack, preview);
                        if (value <= 0f) continue; // Never do something that only hurts us
                        Consider(ref best, spot, i, target, value + position, randomness);
                    }
                }
            }
            finally
            {
                if (moved) board.MoveCard(card, homeRow, homeColumn);
            }
        }
        return best;
    }

    static void Consider(ref Plan best, Vector2Int? spot, int attackIndex, CardInstance target, float score, float randomness)
    {
        if (randomness > 0f) score += Mathf.Abs(score) * Random.Range(-randomness, randomness);
        if (score <= best.score) return;
        best = new Plan { moveTo = spot, attackIndex = attackIndex, target = target, score = score };
    }

    // After attacking without moving: a square that's clearly safer than staying, or null
    public static Vector2Int? BestRetreat(CardInstance card, BoardState board, IList<Vector2Int> moveOptions)
    {
        int homeRow = card.row, homeColumn = card.column;
        float bestScore = PositionScore(card, board) + 40f;
        Vector2Int? best = null;

        foreach (Vector2Int option in moveOptions)
        {
            if (!board.MoveCard(card, option.x, option.y)) continue;
            try
            {
                float score = PositionScore(card, board);
                if (score > bestScore) { bestScore = score; best = option; }
            }
            finally { board.MoveCard(card, homeRow, homeColumn); }
        }
        return best;
    }

    // ---- Scoring ----

    // How good one attack's previewed result is for the attacker's team
    public static float AttackScore(CardInstance attacker, AttackData attack, AttackResult preview)
    {
        float score = 0f;
        foreach (AttackHit hit in preview.hits)
        {
            CardInstance card = hit.card;
            bool enemy = card.owner != attacker.owner;
            bool defeated = preview.defeatedCards.Contains(card);

            if (enemy)
            {
                float wounded = 1f - (float)card.currentHP / Mathf.Max(1, card.data.maxHP);
                score += hit.damage * (1f + wounded * 0.5f); // Finish off hurt cards
                if (defeated) score += 400f + Power(card);   // Removing a card is huge, more so a dangerous one
            }
            else
            {
                score -= hit.damage * 1.5f;
                if (defeated) score -= 800f;
                score += hit.healed * (card == attacker ? 1f : 1.2f);
            }

            if (!defeated && attack.targetStatusEffects != null)
                foreach (StatusEffectApplication effect in attack.targetStatusEffects)
                    score += EffectValue(effect.type, card, enemy);
        }

        // Knockback: the target hits a wall or a card for extra damage (and so does the card it hits)
        if (preview.wasKnockedBack) score += 30f;
        if (preview.hitObstacle)
        {
            score += preview.collisionDamage;
            if (preview.collidedWith != null) score += preview.collidedWith.owner != attacker.owner ? preview.collisionDamage : -preview.collisionDamage * 1.5f;
        }

        score -= preview.reflectedDamage * 1.2f;
        if (preview.reflectedDamage >= attacker.currentHP) score -= 1000f; // Don't defeat yourself

        if (attack.selfStatusEffects != null)
            foreach (StatusEffectApplication effect in attack.selfStatusEffects)
                score += EffectValue(effect.type, attacker, false);

        return score;
    }

    // Rough worth of giving a card a status effect
    public static float EffectValue(StatusEffectType type, CardInstance card, bool enemy)
    {
        bool buff = type.IsBuff();
        if (enemy == buff) return -50f; // Buffing an enemy or debuffing a friend
        if (card.statusEffects.Has(type) && type != StatusEffectType.Stun) return 15f; // Mostly just refreshes it

        switch (type)
        {
            case StatusEffectType.Stun: return card.statusEffects.Has(StatusEffectType.Stagger) ? 350f : 90f; // Needs Stagger first to land
            case StatusEffectType.Stagger: return 70f;
            case StatusEffectType.Poison: return 160f;
            case StatusEffectType.Burn: return 80f;
            case StatusEffectType.Regeneration: return card.currentHP < card.data.maxHP ? 120f : 30f;
            case StatusEffectType.HealBlock: return 40f;
            default: return 70f; // Attack, defense, speed and move changes
        }
    }

    // How dangerous a card is: its hardest-hitting attack
    public static float Power(CardInstance card)
    {
        float best = 0f;
        foreach (AttackData attack in card.GetAttacks())
            if (attack != null && attack.damageType != DamageType.Heal) best = Mathf.Max(best, card.GetAttackPower(attack));
        return best;
    }

    // Longest reach of its attacks (ranged is capped, the board is only 8 wide)
    static int Reach(CardInstance card, out bool hasDamage)
    {
        int reach = 1;
        hasDamage = false;
        foreach (AttackData attack in card.GetAttacks())
        {
            if (attack == null) continue;
            if (attack.damageType != DamageType.Heal) hasDamage = true;
            reach = Mathf.Max(reach, Mathf.Min(6, CombatResolver.GetRange(card, attack)));
        }
        return reach;
    }

    static int Distance(CardInstance a, CardInstance b) => Mathf.Abs(a.row - b.row) + Mathf.Abs(a.column - b.column);

    // How good the card's current square is: close enough to fight, not surrounded by threats.
    // Healers and Supports also like staying near hurt allies
    public static float PositionScore(CardInstance card, BoardState board)
    {
        List<CardInstance> enemies = board.GetCards(BoardState.OtherPlayer(card.owner));
        if (enemies.Count == 0) return 0f;

        int reach = Reach(card, out bool hasDamage);
        int nearest = int.MaxValue;
        float danger = 0f;
        foreach (CardInstance enemy in enemies)
        {
            if (!enemy.IsAlive) continue;
            int distance = Distance(card, enemy);
            nearest = Mathf.Min(nearest, distance);
            if (distance <= enemy.MoveSpaces + Reach(enemy, out _)) danger += Power(enemy);
        }

        // Fighters want to be just within their reach of the nearest enemy (melee = next to it)
        float score = 0f;
        if (hasDamage && nearest != int.MaxValue)
        {
            int ideal = reach <= 1 ? 1 : Mathf.Clamp(reach - 1, 2, 4);
            score -= Mathf.Abs(nearest - ideal) * 25f;
        }

        // Fragile cards (low HP left, not a Defender) keep out of reach more
        float health = (float)card.currentHP / Mathf.Max(1, card.data.maxHP);
        float fragility = (card.data.characterClass == CharacterClass.Defender ? 0.15f : 0.35f) * (1.5f - health);
        score -= danger * fragility;

        if (card.data.characterClass == CharacterClass.Healer || card.data.characterClass == CharacterClass.Support)
        {
            int closestHurt = int.MaxValue;
            foreach (CardInstance ally in board.GetCards(card.owner))
                if (ally != card && ally.currentHP < ally.data.maxHP) closestHurt = Mathf.Min(closestHurt, Distance(card, ally));
            if (closestHurt != int.MaxValue) score -= closestHurt * 15f;
        }
        return score;
    }
}
