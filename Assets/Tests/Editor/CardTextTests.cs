using NUnit.Framework;
using static TestHelpers;

public class CardTextTests
{
    [Test]
    public void Name_AddsSpaces()
    {
        Assert.AreEqual("Magic Defense Down", CardText.Name(StatusEffectType.MagicDefenseDown));
        Assert.AreEqual("Stun", CardText.Name(StatusEffectType.Stun));
    }

    [Test]
    public void Stat_ShowsChangedValue()
    {
        Assert.AreEqual("50", CardText.Stat(50, 50));
        StringAssert.Contains("(100)", CardText.Stat(50, 100));
    }

    [Test]
    public void Applications_ShowRealAmountAndDuration()
    {
        string text = CardText.Applications(new[] { Effect(StatusEffectType.MagicDefenseDown, 80, 3), Effect(StatusEffectType.Stun) });
        StringAssert.Contains("Magic Defense Down 80 for 3 turns", text);
        StringAssert.Contains("Stun for 1 turn", text);
    }

    [Test]
    public void Bonus_DescribesConditionAndReward()
    {
        AttackBonus hindered = new AttackBonus { condition = BonusCondition.TargetHasStatus, status = StatusEffectType.Hindered, bonusDamage = 120 };
        Assert.AreEqual("+120 if target has Hindered", CardText.Bonus(hindered, false));

        AttackBonus perDebuff = new AttackBonus { condition = BonusCondition.TargetDebuffCount, bonusDamage = 30 };
        Assert.AreEqual("+30 per debuff on target", CardText.Bonus(perDebuff, false));

        AttackBonus lowHP = new AttackBonus { condition = BonusCondition.AttackerHPBelow, hp = 50, hpIsPercent = true,
                                              extraSelfEffects = new[] { Effect(StatusEffectType.PhysicalDefenseUp, 100) } };
        StringAssert.Contains("if below 50% HP", CardText.Bonus(lowHP, false));
    }

    [Test]
    public void Attack_ListsSpecialOptions()
    {
        AttackData attack = Attack(200, DamageType.Magic, "Piercing Glow");
        attack.ignoresDefense = true;
        attack.range = 3;
        string text = CardText.Attack(1, attack, null);

        StringAssert.Contains("Piercing Glow", text);
        StringAssert.Contains("200 Magic", text);
        StringAssert.Contains("Ignores defense", text);
        StringAssert.Contains("Range 3", text);
        Assert.AreEqual("2. (empty)", CardText.Attack(2, null, null));
    }

    [Test]
    public void Attack_ShowsCooldownOnLiveCard()
    {
        AttackData attack = Attack(100);
        CardInstance card = new CardInstance(Card(), 1, -1, -1);
        card.RecordAttackUsed(attack);
        StringAssert.Contains("[cooldown]", CardText.Attack(1, attack, card));
    }
}
