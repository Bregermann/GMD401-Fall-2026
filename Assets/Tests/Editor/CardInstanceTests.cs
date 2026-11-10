using NUnit.Framework;
using static TestHelpers;

public class CardInstanceTests
{
    [Test]
    public void StartsAtFullHP()
    {
        CardInstance card = new CardInstance(Card(hp: 500), 1, -1, -1);
        Assert.AreEqual(500, card.currentHP);
        Assert.IsTrue(card.IsAlive);
    }

    [Test]
    public void TakeDamage_StopsAtZero()
    {
        CardInstance card = new CardInstance(Card(hp: 100), 1, -1, -1);
        card.TakeDamage(250);
        Assert.AreEqual(0, card.currentHP);
        Assert.IsFalse(card.IsAlive);
    }

    [Test]
    public void Heal_CapsAtMaxAndIsBlocked()
    {
        CardInstance card = new CardInstance(Card(hp: 100), 1, -1, -1);
        card.TakeDamage(50);
        Assert.IsTrue(card.Heal(500));
        Assert.AreEqual(100, card.currentHP);

        card.TakeDamage(50);
        card.ApplyStatusEffect(StatusEffectType.HealBlock);
        Assert.IsFalse(card.Heal(30));
        Assert.AreEqual(50, card.currentHP);
    }

    [Test]
    public void Heal_DoesNothingWhenDead()
    {
        CardInstance card = new CardInstance(Card(hp: 100), 1, -1, -1);
        card.TakeDamage(100);
        Assert.IsFalse(card.Heal(50));
        Assert.AreEqual(0, card.currentHP);
    }

    [Test]
    public void SpeedDown_CantGoBelowFloor()
    {
        CardInstance card = new CardInstance(Card(speed: 40), 1, -1, -1);
        card.ApplyStatusEffect(StatusEffectType.SpeedDown);
        Assert.AreEqual(CardInstance.MinSpeed, card.Speed);
    }

    [Test]
    public void SpeedFloor_DoesntRaiseSlowCards()
    {
        CardInstance card = new CardInstance(Card(speed: 5), 1, -1, -1);
        Assert.AreEqual(5, card.Speed);
    }

    [Test]
    public void DefenseAndMove_DontGoBelowZero()
    {
        CardInstance card = new CardInstance(Card(physicalDefense: 20, moveSpaces: 1), 1, -1, -1);
        card.ApplyStatusEffect(StatusEffectType.PhysicalDefenseDown, 100);
        card.ApplyStatusEffect(StatusEffectType.Hindered, 5);

        Assert.AreEqual(0, card.PhysicalDefense);
        Assert.AreEqual(0, card.MoveSpaces);
    }

    [Test]
    public void AttackPower_UsesBuffsForMatchingType()
    {
        CardInstance card = new CardInstance(Card(), 1, -1, -1);
        card.ApplyStatusEffect(StatusEffectType.PhysicalAttackUp, 40);

        Assert.AreEqual(140, card.GetAttackPower(Attack(100)));
        Assert.AreEqual(100, card.GetAttackPower(Attack(100, DamageType.Magic)));
        Assert.AreEqual(100, card.GetAttackPower(Attack(100, DamageType.Heal)));
    }

    [Test]
    public void Cooldown_BlocksSameAttackUntilSkipped()
    {
        CardInstance card = new CardInstance(Card(), 1, -1, -1);
        AttackData a = Attack(10, name: "A");
        AttackData b = Attack(10, name: "B");

        card.RecordAttackUsed(a);
        Assert.IsFalse(card.CanUseAttack(a));
        Assert.IsTrue(card.CanUseAttack(b));

        card.SkipAttack();
        Assert.IsTrue(card.CanUseAttack(a));
        Assert.IsFalse(card.CanUseAttack(null));
    }

    [Test]
    public void StartTurn_RegenerationThenBurn()
    {
        CardInstance card = new CardInstance(Card(hp: 1000), 1, -1, -1);
        card.TakeDamage(500);
        card.ApplyStatusEffect(StatusEffectType.Regeneration, 200);
        card.ApplyStatusEffect(StatusEffectType.Burn, 50);

        card.StartTurn();
        Assert.AreEqual(650, card.currentHP);
        Assert.IsTrue(card.IsTakingTurn);
    }

    [Test]
    public void EndTurn_PoisonThenEffectsCountDown()
    {
        CardInstance card = new CardInstance(Card(hp: 1000), 1, -1, -1);
        card.ApplyStatusEffect(StatusEffectType.Poison, 100, 1);

        card.StartTurn();
        card.EndTurn();
        Assert.AreEqual(900, card.currentHP);
        Assert.IsFalse(card.statusEffects.Has(StatusEffectType.Poison));
        Assert.IsFalse(card.IsTakingTurn);
    }

    [Test]
    public void StunnedTurn_ClearsCooldownAndLastsOneTurn()
    {
        CardInstance card = new CardInstance(Card(), 1, -1, -1);
        card.RecordAttackUsed(Attack(10));
        card.ApplyStatusEffect(StatusEffectType.Stagger);
        card.ApplyStatusEffect(StatusEffectType.Stun);

        card.StartTurn();
        Assert.IsTrue(card.IsStunned);
        Assert.IsNull(card.lastAttackUsed);
        card.EndTurn();
        Assert.IsFalse(card.IsStunned);
    }

    [Test]
    public void SelfStunDuringOwnTurn_SkipsTheNextTurn()
    {
        CardInstance card = new CardInstance(Card(), 1, -1, -1);
        card.StartTurn();
        card.ApplyStatusEffect(StatusEffectType.Stun, 0, 0, true); // Like Hibernate
        card.EndTurn();

        card.StartTurn();
        Assert.IsTrue(card.IsStunned, "Should still be stunned on its next turn");
        card.EndTurn();
        Assert.IsFalse(card.IsStunned);
    }

    [Test]
    public void ActivePassive_ComesFromAttackOnCooldown()
    {
        CardInstance card = new CardInstance(Card(), 1, -1, -1);
        AttackData hangry = Attack(0);
        hangry.whileOnCooldown = CooldownPassive.DamageAttackers;

        Assert.AreEqual(CooldownPassive.None, card.ActivePassive);
        card.RecordAttackUsed(hangry);
        Assert.AreEqual(CooldownPassive.DamageAttackers, card.ActivePassive);
        card.SkipAttack();
        Assert.AreEqual(CooldownPassive.None, card.ActivePassive);
    }
}
