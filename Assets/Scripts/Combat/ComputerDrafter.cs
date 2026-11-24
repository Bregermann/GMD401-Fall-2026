using System.Collections.Generic;
using UnityEngine;

// The computer opponent's draft and placement choices. Pure card data (no scene objects), so it can be tested.
// ComputerPlayer carries the choices out.
//
// Drafting: every card still in the pool gets a value from its stats compared with the rest of the pool (so real
// cards and placeholders are judged fairly): HP, defenses, speed, movement, hardest attack, plus extras for heals,
// stuns and area attacks. Then the team matters: a class it already has is worth less, and it really wants a
// Defender and a Healer or Support by the middle of the draft.
//
// Placing: Defenders and Attackers go first and want the front row (nearest the enemy) in the middle columns;
// Mages, Ranged, Healers and Supports want the back row; Assassins like the edges. Cards spread out a little so one
// area attack can't hit everyone, front-liners line up across from enemy cards, and Healers/Supports stay near allies
public static class ComputerDrafter
{
    // ---- Drafting ----

    // Index of the card to pick from available
    public static int ChoosePick(IList<CardData> available, IList<CardData> mine, float randomness = 0.25f)
    {
        int best = -1;
        float bestScore = float.MinValue;
        for (int i = 0; i < available.Count; i++)
        {
            float score = CardValue(available[i], available) + TeamValue(available[i], mine);
            if (randomness > 0f) score += Random.Range(-randomness, randomness);
            if (score > bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    // How strong a card is next to the others in the pool (0 = average)
    public static float CardValue(CardData card, IList<CardData> pool)
    {
        float value = 1.0f * Standard(card, pool, c => c.maxHP)
                    + 0.5f * Standard(card, pool, c => c.physicalDefense)
                    + 0.5f * Standard(card, pool, c => c.magicDefense)
                    + 0.7f * Standard(card, pool, c => c.speed)
                    + 0.4f * Standard(card, pool, c => c.moveSpaces)
                    + 1.0f * Standard(card, pool, BestDamage);

        foreach (AttackData attack in Attacks(card))
        {
            if (attack.damageType == DamageType.Heal) value += 0.6f;
            if (attack.area != AttackArea.Single && attack.area != AttackArea.Self) value += 0.3f;
            if (attack.targetStatusEffects != null)
                foreach (StatusEffectApplication effect in attack.targetStatusEffects)
                    if (effect.type == StatusEffectType.Stun) value += 0.4f;
        }
        return value;
    }

    // How much this card helps the team it would join
    public static float TeamValue(CardData card, IList<CardData> mine)
    {
        int sameClass = 0;
        bool hasDefender = false, hasSupport = false;
        foreach (CardData pick in mine)
        {
            if (pick.characterClass == card.characterClass) sameClass++;
            if (pick.characterClass == CharacterClass.Defender) hasDefender = true;
            if (pick.characterClass == CharacterClass.Healer || pick.characterClass == CharacterClass.Support) hasSupport = true;
        }

        float value = -0.9f * sameClass; // Variety: a third Mage is worth much less than a first Defender
        bool middleOfDraft = mine.Count >= 2;
        if (middleOfDraft && !hasDefender && card.characterClass == CharacterClass.Defender) value += 1.2f;
        if (middleOfDraft && !hasSupport && (card.characterClass == CharacterClass.Healer || card.characterClass == CharacterClass.Support)) value += 1.2f;
        return value;
    }

    // ---- Placing ----

    // Which of the unplaced cards to put down next: front-liners first
    public static int ChooseCardToPlace(IList<CardData> unplaced)
    {
        int best = 0;
        for (int i = 1; i < unplaced.Count; i++)
            if (WantsFront(unplaced[i].characterClass) > WantsFront(unplaced[best].characterClass)) best = i;
        return best;
    }

    // The best of the open placement squares (row, column) for this card
    public static Vector2Int ChooseSquare(CardData card, int owner, BoardState board, IList<Vector2Int> options, float randomness = 0.2f)
    {
        Vector2Int best = options.Count > 0 ? options[0] : default;
        float bestScore = float.MinValue;
        foreach (Vector2Int option in options)
        {
            float score = SquareScore(card, owner, option, board);
            if (randomness > 0f) score += Random.Range(-randomness, randomness);
            if (score > bestScore) { bestScore = score; best = option; }
        }
        return best;
    }

    public static float SquareScore(CardData card, int owner, Vector2Int square, BoardState board)
    {
        CharacterClass type = card.characterClass;
        int frontRow = owner == 1 ? 1 : BoardState.Rows - 2; // The placement row nearest the enemy
        bool front = square.x == frontRow;
        float wantFront = WantsFront(type);
        float score = (front ? wantFront : 1f - wantFront) * 3f;

        // Middle columns for the line, edges for Assassins (to slip round the side)
        float fromCentre = Mathf.Abs(square.y - (BoardState.Columns - 1) / 2f);
        score += type == CharacterClass.Assassin ? fromCentre * 0.4f : -fromCentre * (type == CharacterClass.Defender ? 0.6f : 0.3f);

        // Neighbours: spread out against area attacks, except Healers/Supports who want allies close
        bool helper = type == CharacterClass.Healer || type == CharacterClass.Support;
        for (int r = -1; r <= 1; r++)
            for (int c = -1; c <= 1; c++)
            {
                if (r == 0 && c == 0) continue;
                CardInstance next = board.GetCard(square.x + r, square.y + c);
                if (next != null && next.owner == owner) score += helper ? 0.5f : -0.4f;
            }

        // Front-liners line up across from enemy cards already placed
        if (front && wantFront >= 0.5f)
            for (int row = 0; row < BoardState.Rows; row++)
            {
                CardInstance across = board.GetCard(row, square.y);
                if (across != null && across.owner != owner) score += 0.4f;
            }
        return score;
    }

    // 1 = always front row, 0 = always back row
    static float WantsFront(CharacterClass type)
    {
        switch (type)
        {
            case CharacterClass.Defender: return 1f;
            case CharacterClass.Attacker: return 0.8f;
            case CharacterClass.Assassin: return 0.6f;
            case CharacterClass.Support: return 0.3f;
            case CharacterClass.Mage: return 0.2f;
            case CharacterClass.Healer: return 0.1f;
            default: return 0f; // Ranged
        }
    }

    // ---- Helpers ----

    static IEnumerable<AttackData> Attacks(CardData card)
    {
        foreach (AttackData attack in new[] { card.attack1, card.attack2, card.attack3, card.attack4 })
            if (attack != null) yield return attack;
    }

    static float BestDamage(CardData card)
    {
        float best = 0f;
        foreach (AttackData attack in Attacks(card))
            if (attack.damageType != DamageType.Heal) best = Mathf.Max(best, attack.damage);
        return best;
    }

    // How many standard deviations this card's stat is above the pool's average
    static float Standard(CardData card, IList<CardData> pool, System.Func<CardData, float> stat)
    {
        if (pool.Count < 2) return 0f;
        float sum = 0f, sumSquares = 0f;
        foreach (CardData other in pool) { float v = stat(other); sum += v; sumSquares += v * v; }
        float mean = sum / pool.Count;
        float deviation = Mathf.Sqrt(Mathf.Max(0f, sumSquares / pool.Count - mean * mean));
        return deviation < 0.0001f ? 0f : (stat(card) - mean) / deviation;
    }
}
