using NUnit.Framework;
using static TestHelpers;

public class CombatTargetingTests
{
    BoardState board;

    [SetUp]
    public void SetUp()
    {
        board = new BoardState();
    }

    bool Can(CardInstance attacker, AttackData attack, CardInstance target) => CombatResolver.CanTarget(attacker, attack, target, board);

    [Test]
    public void Melee_HitsAdjacentEnemyInLine()
    {
        CardInstance attacker = Put(board, 1, 3, 3);
        Assert.IsTrue(Can(attacker, Attack(100), Put(board, 2, 4, 3)));
        Assert.IsTrue(Can(attacker, Attack(100), Put(board, 2, 3, 4)));
    }

    [Test]
    public void Melee_CantHitDiagonalOrTwoAway()
    {
        CardInstance attacker = Put(board, 1, 3, 3);
        Assert.IsFalse(Can(attacker, Attack(100), Put(board, 2, 4, 4)));
        Assert.IsFalse(Can(attacker, Attack(100), Put(board, 2, 5, 3)));
    }

    [Test]
    public void DamageAttacks_CantHitAllies()
    {
        CardInstance attacker = Put(board, 1, 3, 3);
        Assert.IsFalse(Can(attacker, Attack(100), Put(board, 1, 4, 3)));
    }

    [Test]
    public void Heals_HitSelfAndAlliesOnly()
    {
        CardInstance healer = Put(board, 1, 3, 3);
        AttackData heal = Attack(100, DamageType.Heal);

        Assert.IsTrue(Can(healer, heal, healer));
        Assert.IsTrue(Can(healer, heal, Put(board, 1, 4, 3)));
        Assert.IsFalse(Can(healer, heal, Put(board, 2, 2, 3)));
    }

    [Test]
    public void RangedClass_ReachesAcrossBoardWithLineOfSight()
    {
        CardInstance archer = Put(board, Card("Archer", CharacterClass.Ranged), 1, 0, 3);
        CardInstance far = Put(board, 2, 7, 3);
        Assert.IsTrue(Can(archer, Attack(100), far));

        Put(board, 2, 4, 3); // In the way
        Assert.IsFalse(Can(archer, Attack(100), far));
    }

    [Test]
    public void AttackRange_OverridesClassRange()
    {
        CardInstance mage = Put(board, 1, 0, 0);
        AttackData beam = Attack(100, DamageType.Magic);
        beam.range = 3;

        Assert.IsTrue(Can(mage, beam, Put(board, 2, 3, 0)));
        Assert.IsFalse(Can(mage, beam, Put(board, 2, 0, 4)));
    }

    [Test]
    public void SelfAttacks_OnlyTargetTheUser()
    {
        CardInstance user = Put(board, 1, 3, 3);
        CardInstance enemy = Put(board, 2, 4, 3);

        AttackData self = Attack(0);
        self.area = AttackArea.Self;
        Assert.IsTrue(Can(user, self, user));
        Assert.IsFalse(Can(user, self, enemy));

        AttackData aroundSelf = Attack(0);
        aroundSelf.area = AttackArea.AroundSelf;
        Assert.IsTrue(Can(user, aroundSelf, user));
        Assert.IsFalse(Can(user, aroundSelf, enemy));
    }

    [Test]
    public void StunnedOrOnCooldown_CantAttack()
    {
        CardInstance attacker = Put(board, 1, 3, 3);
        CardInstance enemy = Put(board, 2, 4, 3);
        AttackData attack = Attack(100);

        attacker.RecordAttackUsed(attack);
        Assert.IsFalse(Can(attacker, attack, enemy), "Cooldown");

        attacker.SkipAttack();
        attacker.ApplyStatusEffect(StatusEffectType.Stun, 0, 0, true);
        Assert.IsFalse(Can(attacker, attack, enemy), "Stunned");
    }

    [Test]
    public void CardsOffTheBoard_CantBeTargeted()
    {
        CardInstance attacker = Put(board, 1, 3, 3);
        CardInstance enemy = Put(board, 2, 4, 3);
        board.Remove(enemy);
        Assert.IsFalse(Can(attacker, Attack(100), enemy));
    }

    [Test]
    public void SideHit_DependsOnTargetFacing()
    {
        // Player 1 faces toward row 7
        CardInstance target = Put(board, 1, 3, 3);
        Assert.AreEqual(PositionalBonus.Front, CombatResolver.GetSideHit(Put(board, 2, 4, 3), target));
        Assert.AreEqual(PositionalBonus.Back, CombatResolver.GetSideHit(Put(board, 2, 2, 3), target));
        Assert.AreEqual(PositionalBonus.Side, CombatResolver.GetSideHit(Put(board, 2, 3, 4), target));

        // Player 2 faces toward row 0
        CardInstance p2 = Put(board, 2, 6, 6);
        Assert.AreEqual(PositionalBonus.Front, CombatResolver.GetSideHit(Put(board, 1, 5, 6), p2));
        Assert.AreEqual(PositionalBonus.Back, CombatResolver.GetSideHit(Put(board, 1, 7, 6), p2));
    }
}
