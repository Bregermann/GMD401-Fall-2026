using UnityEngine;

// Shortcuts for building cards, attacks and boards in tests
public static class TestHelpers
{
    public static CardData Card(string name = "Card", CharacterClass characterClass = CharacterClass.Attacker,
                                int hp = 1000, int physicalDefense = 0, int magicDefense = 0, int speed = 100, int moveSpaces = 2)
    {
        CardData card = ScriptableObject.CreateInstance<CardData>();
        card.name = name;
        card.cardName = name;
        card.characterClass = characterClass;
        card.maxHP = hp;
        card.physicalDefense = physicalDefense;
        card.magicDefense = magicDefense;
        card.speed = speed;
        card.moveSpaces = moveSpaces;
        return card;
    }

    public static AttackData Attack(int damage, DamageType type = DamageType.Physical, string name = "Attack")
    {
        AttackData attack = ScriptableObject.CreateInstance<AttackData>();
        attack.name = name;
        attack.attackName = name;
        attack.damage = damage;
        attack.damageType = type;
        return attack;
    }

    public static StatusEffectApplication Effect(StatusEffectType type, int amount = 0, int duration = 0, bool ignoreStagger = false)
    {
        return new StatusEffectApplication { type = type, amount = amount, duration = duration, ignoreStagger = ignoreStagger };
    }

    // A card for a player, placed on the board
    public static CardInstance Put(BoardState board, CardData data, int owner, int row, int column)
    {
        CardInstance card = new CardInstance(data, owner, -1, -1);
        board.Place(card, row, column);
        return card;
    }

    // Quick card on the board with the given stats
    public static CardInstance Put(BoardState board, int owner, int row, int column, int hp = 1000, int physicalDefense = 0,
                                   int magicDefense = 0, CharacterClass characterClass = CharacterClass.Attacker)
    {
        return Put(board, Card($"P{owner} {row},{column}", characterClass, hp, physicalDefense, magicDefense), owner, row, column);
    }
}
