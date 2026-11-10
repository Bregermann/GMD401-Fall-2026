using NUnit.Framework;
using static TestHelpers;

public class TurnOrderTests
{
    static CardInstance Make(int speed, CharacterClass characterClass = CharacterClass.Attacker)
    {
        return new CardInstance(Card(characterClass: characterClass, speed: speed), 1, -1, -1);
    }

    [Test]
    public void FasterCardGoesFirst()
    {
        Assert.IsTrue(TurnOrder.GoesBefore(Make(300), Make(200), 0f, 1f));
        Assert.IsFalse(TurnOrder.GoesBefore(Make(200), Make(300), 1f, 0f));
    }

    [Test]
    public void SpeedChanges_CountStraightAway()
    {
        CardInstance slow = Make(200);
        CardInstance fast = Make(220);
        slow.ApplyStatusEffect(StatusEffectType.SpeedUp, 50);
        Assert.IsTrue(TurnOrder.GoesBefore(slow, fast, 0f, 0f));
    }

    [Test]
    public void SpeedTie_GoesToClassPriority()
    {
        // Assassin > Support > Attacker > Healer > Ranged > Mage > Defender
        Assert.IsTrue(TurnOrder.GoesBefore(Make(300, CharacterClass.Assassin), Make(300, CharacterClass.Support), 0f, 1f));
        Assert.IsTrue(TurnOrder.GoesBefore(Make(300, CharacterClass.Mage), Make(300, CharacterClass.Defender), 0f, 1f));
        Assert.IsFalse(TurnOrder.GoesBefore(Make(300, CharacterClass.Healer), Make(300, CharacterClass.Attacker), 1f, 0f));
    }

    [Test]
    public void FullTie_GoesToCoinFlip()
    {
        Assert.IsTrue(TurnOrder.GoesBefore(Make(300), Make(300), 0.8f, 0.2f));
        Assert.IsFalse(TurnOrder.GoesBefore(Make(300), Make(300), 0.2f, 0.8f));
    }
}
