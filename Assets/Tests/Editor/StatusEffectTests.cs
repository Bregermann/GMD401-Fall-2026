using NUnit.Framework;

public class StatusEffectTests
{
    StatusEffectHandler effects;

    [SetUp]
    public void SetUp()
    {
        effects = new StatusEffectHandler();
    }

    [Test]
    public void Apply_UsesDefaultAmountAndDuration()
    {
        effects.Apply(StatusEffectType.MagicDefenseDown, false);

        Assert.AreEqual(StatusEffectHandler.DefenseModifier, effects.GetAmount(StatusEffectType.MagicDefenseDown));
        Assert.AreEqual(StatusEffectHandler.DefaultDuration, effects.ActiveEffects[0].turnsRemaining);
    }

    [Test]
    public void Apply_UsesCustomAmountAndDuration()
    {
        effects.Apply(StatusEffectType.MagicDefenseDown, false, 80, 3);

        Assert.AreEqual(80, effects.GetAmount(StatusEffectType.MagicDefenseDown));
        Assert.AreEqual(3, effects.ActiveEffects[0].turnsRemaining);
    }

    [Test]
    public void Apply_DuringOwnTurn_AddsOneTurn()
    {
        effects.Apply(StatusEffectType.Haste, true);
        Assert.AreEqual(StatusEffectHandler.DefaultDuration + 1, effects.ActiveEffects[0].turnsRemaining);
    }

    [Test]
    public void Apply_SameEffect_ReplacesAmountAndTimer()
    {
        effects.Apply(StatusEffectType.PhysicalDefenseUp, false, 100, 5);
        effects.Apply(StatusEffectType.PhysicalDefenseUp, false, 30, 1);

        Assert.AreEqual(1, effects.ActiveEffects.Count);
        Assert.AreEqual(30, effects.GetAmount(StatusEffectType.PhysicalDefenseUp));
        Assert.AreEqual(1, effects.ActiveEffects[0].turnsRemaining);
    }

    [Test]
    public void Stun_WithoutStagger_GivesStaggerInstead()
    {
        effects.Apply(StatusEffectType.Stun, false, 0, 5);

        Assert.IsFalse(effects.Has(StatusEffectType.Stun));
        Assert.IsTrue(effects.Has(StatusEffectType.Stagger));
        Assert.AreEqual(StatusEffectHandler.DefaultDuration, effects.ActiveEffects[0].turnsRemaining); // Custom stun duration isn't used for the Stagger
    }

    [Test]
    public void Stun_WithStagger_LandsAndRemovesStagger()
    {
        effects.Apply(StatusEffectType.Stagger, false);
        effects.Apply(StatusEffectType.Stun, false);

        Assert.IsTrue(effects.Has(StatusEffectType.Stun));
        Assert.IsFalse(effects.Has(StatusEffectType.Stagger));
        Assert.AreEqual(StatusEffectHandler.StunDuration, effects.ActiveEffects[0].turnsRemaining);
    }

    [Test]
    public void Stun_IgnoreStagger_LandsWithoutStagger()
    {
        effects.Apply(StatusEffectType.Stun, false, 0, 0, true);
        Assert.IsTrue(effects.Has(StatusEffectType.Stun));
    }

    [Test]
    public void TickDown_RemovesExpiredEffects()
    {
        effects.Apply(StatusEffectType.Burn, false, 0, 1);
        effects.Apply(StatusEffectType.Poison, false, 0, 2);
        effects.TickDown();

        Assert.IsFalse(effects.Has(StatusEffectType.Burn));
        Assert.IsTrue(effects.Has(StatusEffectType.Poison));
        effects.TickDown();
        Assert.IsFalse(effects.Has(StatusEffectType.Poison));
    }

    [Test]
    public void BuffAndDebuff_CancelByAmount()
    {
        effects.Apply(StatusEffectType.PhysicalDefenseUp, false, 50);
        effects.Apply(StatusEffectType.PhysicalDefenseDown, false, 80);

        Assert.AreEqual(-30, effects.GetPhysicalDefenseModifier());
    }

    [Test]
    public void Modifiers_CoverEveryStat()
    {
        effects.Apply(StatusEffectType.PhysicalAttackUp, false);
        effects.Apply(StatusEffectType.MagicAttackDown, false);
        effects.Apply(StatusEffectType.MagicDefenseUp, false);
        effects.Apply(StatusEffectType.SpeedDown, false);
        effects.Apply(StatusEffectType.Haste, false);

        Assert.AreEqual(StatusEffectHandler.AttackModifier, effects.GetPhysicalAttackModifier());
        Assert.AreEqual(-StatusEffectHandler.AttackModifier, effects.GetMagicAttackModifier());
        Assert.AreEqual(StatusEffectHandler.DefenseModifier, effects.GetMagicDefenseModifier());
        Assert.AreEqual(-StatusEffectHandler.SpeedModifier, effects.GetSpeedModifier());
        Assert.AreEqual(StatusEffectHandler.MoveModifier, effects.GetMoveModifier());
    }

    [Test]
    public void DebuffCount_IgnoresBuffs()
    {
        effects.Apply(StatusEffectType.Haste, false);
        effects.Apply(StatusEffectType.Burn, false);
        effects.Apply(StatusEffectType.SpeedDown, false);
        effects.Apply(StatusEffectType.Stagger, false);

        Assert.AreEqual(3, effects.DebuffCount());
    }

    [Test]
    public void IsBuff_MatchesEnumGroups()
    {
        Assert.IsTrue(StatusEffectType.Regeneration.IsBuff());
        Assert.IsTrue(StatusEffectType.Haste.IsBuff());
        Assert.IsFalse(StatusEffectType.HealBlock.IsBuff());
        Assert.IsFalse(StatusEffectType.Stun.IsBuff());
    }

    [Test]
    public void DefaultAmounts_MatchConstants()
    {
        Assert.AreEqual(StatusEffectHandler.BurnDamage, StatusEffectHandler.GetDefaultAmount(StatusEffectType.Burn));
        Assert.AreEqual(StatusEffectHandler.PoisonDamage, StatusEffectHandler.GetDefaultAmount(StatusEffectType.Poison));
        Assert.AreEqual(StatusEffectHandler.RegenerationAmount, StatusEffectHandler.GetDefaultAmount(StatusEffectType.Regeneration));
        Assert.AreEqual(0, StatusEffectHandler.GetDefaultAmount(StatusEffectType.Stun));
    }
}
