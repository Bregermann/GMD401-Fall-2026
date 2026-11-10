using NUnit.Framework;
using static TestHelpers;

public class CombatAreaAndKnockbackTests
{
    BoardState board;

    [SetUp]
    public void SetUp()
    {
        board = new BoardState();
    }

    // ---- Area ----

    [Test]
    public void AroundTarget_HitsEnemiesNearTarget()
    {
        CardInstance mage = Put(board, 1, 3, 3);
        CardInstance target = Put(board, 2, 4, 3);
        CardInstance diagonal = Put(board, 2, 5, 4);   // Within 1 of the target
        CardInstance farEnemy = Put(board, 2, 7, 7);
        CardInstance allyNearby = Put(board, 1, 4, 4); // Within 1, but an ally

        AttackData starfall = Attack(300, DamageType.Magic);
        starfall.area = AttackArea.AroundTarget;
        starfall.areaSize = 1;
        AttackResult result = CombatResolver.ResolveAttack(mage, starfall, target, board);

        Assert.AreEqual(2, result.hits.Count);
        Assert.AreEqual(target, result.hits[0].card, "Main target first");
        Assert.AreEqual(700, diagonal.currentHP);
        Assert.AreEqual(1000, farEnemy.currentHP);
        Assert.AreEqual(1000, allyNearby.currentHP);
        Assert.AreEqual(1000, mage.currentHP, "Attacker never hits itself");
    }

    [Test]
    public void AroundSelf_BuffsNearbyAlliesNotUser()
    {
        CardInstance arcturus = Put(board, 1, 3, 3);
        CardInstance near = Put(board, 1, 4, 4);
        CardInstance far = Put(board, 1, 7, 7);
        Put(board, 2, 2, 3); // Enemy nearby, not buffed

        AttackData roar = Attack(0);
        roar.area = AttackArea.AroundSelf;
        roar.areaSize = 1;
        roar.areaAffects = AreaAffects.Allies;
        roar.targetStatusEffects = new[] { Effect(StatusEffectType.PhysicalAttackUp) };
        AttackResult result = CombatResolver.ResolveAttack(arcturus, roar, arcturus, board);

        Assert.AreEqual(1, result.hits.Count);
        Assert.IsTrue(near.statusEffects.Has(StatusEffectType.PhysicalAttackUp));
        Assert.IsFalse(far.statusEffects.Has(StatusEffectType.PhysicalAttackUp));
        Assert.IsFalse(arcturus.statusEffects.Has(StatusEffectType.PhysicalAttackUp));
    }

    [Test]
    public void AreaHeals_AlwaysHitAllies()
    {
        CardInstance lupine = Put(board, 1, 3, 3);
        CardInstance ally = Put(board, 1, 3, 5);
        CardInstance enemy = Put(board, 2, 3, 6);
        ally.TakeDamage(500);
        enemy.TakeDamage(500);

        AttackData plumeria = Attack(200, DamageType.Heal);
        plumeria.area = AttackArea.AroundSelf;
        plumeria.areaSize = 7;
        plumeria.areaAffects = AreaAffects.Enemies; // Wrongly set, heals should still only hit allies
        CombatResolver.ResolveAttack(lupine, plumeria, lupine, board);

        Assert.AreEqual(700, ally.currentHP);
        Assert.AreEqual(500, enemy.currentHP);
    }

    [Test]
    public void AroundSelf_WithNobodyNearby_StillWorks()
    {
        CardInstance user = Put(board, 1, 0, 0);
        AttackData roar = Attack(0);
        roar.area = AttackArea.AroundSelf;
        roar.areaAffects = AreaAffects.Allies;
        roar.selfStatusEffects = new[] { Effect(StatusEffectType.PhysicalAttackUp) };

        AttackResult result = CombatResolver.ResolveAttack(user, roar, user, board);
        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.hits.Count);
        Assert.IsTrue(user.statusEffects.Has(StatusEffectType.PhysicalAttackUp));
        Assert.AreEqual(roar, user.lastAttackUsed);
    }

    [Test]
    public void SelfAttack_OnlyAffectsUser()
    {
        CardInstance gourmand = Put(board, 1, 3, 3);
        CardInstance ally = Put(board, 1, 4, 3);
        gourmand.TakeDamage(500);
        ally.TakeDamage(500);

        AttackData selfHeal = Attack(270, DamageType.Heal);
        selfHeal.area = AttackArea.Self;
        CombatResolver.ResolveAttack(gourmand, selfHeal, gourmand, board);

        Assert.AreEqual(770, gourmand.currentHP);
        Assert.AreEqual(500, ally.currentHP);
    }

    [Test]
    public void AreaAttacks_NeverKnockBack()
    {
        CardInstance attacker = Put(board, 1, 3, 3);
        CardInstance target = Put(board, 2, 4, 3);
        AttackData attack = Attack(100);
        attack.area = AttackArea.AroundTarget;
        attack.hasKnockback = true;

        AttackResult result = CombatResolver.ResolveAttack(attacker, attack, target, board);
        Assert.IsFalse(result.wasKnockedBack);
        Assert.AreEqual(4, target.row);
    }

    // ---- Knockback ----

    AttackData Shove(int collision = 10)
    {
        AttackData shove = Attack(100);
        shove.hasKnockback = true;
        shove.knockbackCollisionDamage = collision;
        return shove;
    }

    [Test]
    public void Knockback_PushesTargetAway()
    {
        CardInstance attacker = Put(board, 1, 3, 3);
        CardInstance target = Put(board, 2, 4, 3);

        AttackResult result = CombatResolver.ResolveAttack(attacker, Shove(), target, board);
        Assert.IsTrue(result.wasKnockedBack);
        Assert.AreEqual(5, target.row);
        Assert.AreEqual(target, board.GetCard(5, 3));
    }

    [Test]
    public void Knockback_IntoWall_DealsCollisionDamage()
    {
        CardInstance attacker = Put(board, 1, 6, 3);
        CardInstance target = Put(board, 2, 7, 3);

        AttackResult result = CombatResolver.ResolveAttack(attacker, Shove(50), target, board);
        Assert.IsTrue(result.hitObstacle);
        Assert.IsNull(result.collidedWith);
        Assert.AreEqual(7, target.row);
        Assert.AreEqual(1000 - 100 - 50, target.currentHP);
    }

    [Test]
    public void Knockback_IntoEnemyOfAttacker_DamagesBoth()
    {
        CardInstance attacker = Put(board, 1, 3, 3);
        CardInstance target = Put(board, 2, 4, 3);
        CardInstance behind = Put(board, 2, 5, 3);

        AttackResult result = CombatResolver.ResolveAttack(attacker, Shove(50), target, board);
        Assert.AreEqual(behind, result.collidedWith);
        Assert.AreEqual(950, behind.currentHP);
        Assert.AreEqual(850, target.currentHP);
    }

    [Test]
    public void Knockback_IntoAttackersAlly_OnlyTargetTakesCollision()
    {
        CardInstance attacker = Put(board, 1, 3, 3);
        CardInstance target = Put(board, 2, 4, 3);
        CardInstance ally = Put(board, 1, 5, 3);

        CombatResolver.ResolveAttack(attacker, Shove(50), target, board);
        Assert.AreEqual(1000, ally.currentHP);
        Assert.AreEqual(850, target.currentHP);
    }

    [Test]
    public void KnockbackImmune_IgnoresKnockbackAndCollision()
    {
        AttackData anchored = Attack(0);
        anchored.whileOnCooldown = CooldownPassive.KnockbackImmune;
        CardInstance attacker = Put(board, 1, 6, 3);
        CardInstance target = Put(board, 2, 7, 3);
        target.RecordAttackUsed(anchored);

        AttackResult result = CombatResolver.ResolveAttack(attacker, Shove(50), target, board);
        Assert.IsTrue(result.knockbackBlocked);
        Assert.IsFalse(result.hitObstacle);
        Assert.AreEqual(900, target.currentHP);
    }

    [Test]
    public void CollisionDamage_CanDefeatCards()
    {
        CardInstance attacker = Put(board, 1, 3, 3);
        CardInstance target = Put(board, 2, 4, 3, hp: 1000);
        CardInstance behind = Put(board, 2, 5, 3, hp: 30);

        AttackResult result = CombatResolver.ResolveAttack(attacker, Shove(50), target, board);
        Assert.Contains(behind, result.defeatedCards);
        Assert.IsFalse(board.IsOnBoard(behind));
    }
}
