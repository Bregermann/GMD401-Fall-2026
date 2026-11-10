using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static TestHelpers;

public class CardPoolTests
{
    static CardPool PoolWith(int perClass)
    {
        CardPool pool = ScriptableObject.CreateInstance<CardPool>();
        foreach (CharacterClass characterClass in System.Enum.GetValues(typeof(CharacterClass)))
            for (int i = 0; i < perClass; i++)
                pool.cards.Add(Card($"{characterClass} {i}", characterClass));
        return pool;
    }

    [Test]
    public void DraftPool_HasRightSizeNoDuplicatesAndEveryClass()
    {
        CardPool pool = PoolWith(3); // 21 cards

        for (int run = 0; run < 20; run++) // It's random, so try a few times
        {
            List<CardData> draft = pool.GetDraftPool(20, 2);
            Assert.AreEqual(20, draft.Count);
            Assert.AreEqual(20, draft.Distinct().Count(), "No duplicates");

            foreach (CharacterClass characterClass in System.Enum.GetValues(typeof(CharacterClass)))
                Assert.GreaterOrEqual(draft.Count(c => c.characterClass == characterClass), 2, $"{characterClass}");
        }
    }

    [Test]
    public void DraftPool_IgnoresEmptyAndDuplicateEntries()
    {
        CardPool pool = PoolWith(3);
        pool.cards.Add(null);
        pool.cards.Add(pool.cards[0]);

        List<CardData> draft = pool.GetDraftPool(21, 2);
        Assert.AreEqual(21, draft.Count);
        Assert.IsFalse(draft.Contains(null));
        Assert.AreEqual(21, draft.Distinct().Count());
    }

    [Test]
    public void DraftPool_WarnsWhenAClassIsShort()
    {
        CardPool pool = PoolWith(3);
        pool.cards.RemoveAll(c => c.characterClass == CharacterClass.Ranged && c.cardName != "Ranged 0");

        LogAssert.Expect(LogType.Warning, new Regex("only has 1 Ranged"));
        List<CardData> draft = pool.GetDraftPool(18, 2);
        Assert.AreEqual(18, draft.Count, "Gap filled with other cards");
    }

    [Test]
    public void DraftPool_WarnsWhenTooFewCards()
    {
        CardPool pool = PoolWith(2); // 14 cards

        LogAssert.Expect(LogType.Warning, new Regex("only has 14 cards"));
        Assert.AreEqual(14, pool.GetDraftPool(20, 2).Count);
    }
}
