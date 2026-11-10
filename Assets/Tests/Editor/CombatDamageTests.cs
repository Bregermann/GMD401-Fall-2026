using NUnit.Framework;
using static TestHelpers;

// Damage formula, bonuses and special attack options
public class CombatDamageTests
{
    BoardState board;
    CardInstance attacker;

    [SetUp]
    public void SetUp()
    {
        board = new BoardState();
        attacker = Put(board, 1, 3, 3, hp: 1000);
    }

    // Enemy straight in front of the attacker (attacker is behind it from Player 2's facing)
    CardInstance Enemy(int hp = 1000, int physicalDefense = 0, int magicDefense = 0, CharacterClass characterClass = CharacterClass.Attacker)
    {
        return Put(board, 2, 4, 3, hp, physicalDefense, magicDefense, characterClass);
    }

    AttackResult Hit(AttackData attack, CardInstance target) => CombatResolver.ResolveAttack(attacker, attack, target, board);

    static AttackData WithBonus(AttackData attack, AttackBonus bonus)
    {
        attack.bonuses = new[] { bonus };
        return attack;
    }

    // ---- Formula ----

    [Test]
    public void Damage_MinusDefense()
    {
        CardInstance enemy = Enemy(physicalDefense: 50, magicDefense: 80);
        Assert.AreEqual(250, Hit(Attack(300), enemy).damageDealt);
        Assert.AreEqual(220, Hit(Attack(300, DamageType.Magic), enemy).damageDealt);
    }

    [Test]
    public void Damage_HasAFloor()
    {
        Assert.AreEqual(CombatResolver.DamageFloor, Hit(Attack(20), Enemy(physicalDefense: 500)).damageDealt);
    }

    [Test]
    public void StatusOnlyAttack_DealsNoDamage()
    {
        CardInstance enemy = Enemy();
        AttackResult result = Hit(Attack(0), enemy);
        Assert.AreEqual(0, result.damageDealt);
        Assert.AreEqual(1000, enemy.currentHP);
    }

    [Test]
    public void Multipliers_RoundHalfUp()
    {
        AttackData attack = Attack(102);
        attack.hasClassBonus = true;
        attack.classBonusTarget = CharacterClass.Mage;
        attack.classMultiplier = 1.25f; // 127.5 -> 128
        Assert.AreEqual(128, Hit(attack, Enemy(characterClass: CharacterClass.Mage)).damageDealt);
    }

    [Test]
    public void PositionalMultiplier_OnlyFromRightSide()
    {
        AttackData attack = Attack(100);
        attack.positionalBonus = PositionalBonus.Front; // Attacker at row 3 is in front of a Player 2 card at row 4
        attack.positionalMultiplier = 2f;
        CardInstance enemy = Enemy();
        Assert.AreEqual(200, Hit(attack, enemy).damageDealt);

        attack.positionalBonus = PositionalBonus.Back;
        attacker.SkipAttack();
        Assert.AreEqual(100, Hit(attack, enemy).damageDealt);
    }

    [Test]
    public void AttackBuff_AddsToDamage()
    {
        attacker.ApplyStatusEffect(StatusEffectType.PhysicalAttackUp, 50);
        Assert.AreEqual(150, Hit(Attack(100), Enemy()).damageDealt);
    }

    [Test]
    public void IgnoresDefense_SkipsDefense()
    {
        AttackData attack = Attack(200, DamageType.Magic);
        attack.ignoresDefense = true;
        Assert.AreEqual(200, Hit(attack, Enemy(magicDefense: 150)).damageDealt);
    }

    [Test]
    public void Heal_RestoresHP()
    {
        CardInstance ally = Put(board, 1, 4, 3, hp: 1000);
        ally.TakeDamage(300);
        AttackResult result = Hit(Attack(200, DamageType.Heal), ally);
        Assert.AreEqual(200, result.amountHealed);
        Assert.AreEqual(900, ally.currentHP);
    }

    [Test]
    public void Attack_GoesOnCooldown()
    {
        AttackData attack = Attack(100);
        Hit(attack, Enemy());
        Assert.AreEqual(attack, attacker.lastAttackUsed);
    }

    [Test]
    public void DisallowedAttack_ReturnsNullAndChangesNothing()
    {
        CardInstance ally = Put(board, 1, 4, 3);
        Assert.IsNull(Hit(Attack(100), ally));
        Assert.AreEqual(1000, ally.currentHP);
        Assert.IsNull(attacker.lastAttackUsed);
    }

    // ---- Status effects ----

    [Test]
    public void TargetAndSelfEffects_Apply()
    {
        AttackData attack = Attack(100);
        attack.targetStatusEffects = new[] { Effect(StatusEffectType.MagicDefenseDown, 80, 3) };
        attack.selfStatusEffects = new[] { Effect(StatusEffectType.Haste) };
        CardInstance enemy = Enemy();
        Hit(attack, enemy);

        Assert.AreEqual(80, enemy.statusEffects.GetAmount(StatusEffectType.MagicDefenseDown));
        Assert.IsTrue(attacker.statusEffects.Has(StatusEffectType.Haste));
    }

    [Test]
    public void DefeatedTarget_GetsNoEffectsAndIsRemoved()
    {
        AttackData attack = Attack(500);
        attack.targetStatusEffects = new[] { Effect(StatusEffectType.Burn) };
        CardInstance enemy = Enemy(hp: 100);
        AttackResult result = Hit(attack, enemy);

        Assert.IsFalse(enemy.statusEffects.Has(StatusEffectType.Burn));
        Assert.Contains(enemy, result.defeatedCards);
        Assert.IsFalse(board.IsOnBoard(enemy));
    }

    // ---- Bonuses ----

    [Test]
    public void Bonus_TargetHasStatus()
    {
        AttackData attack = WithBonus(Attack(300), new AttackBonus { condition = BonusCondition.TargetHasStatus, status = StatusEffectType.Hindered, bonusDamage = 120 });
        CardInstance enemy = Enemy();
        Assert.AreEqual(300, Hit(attack, enemy).damageDealt);

        attacker.SkipAttack();
        enemy.ApplyStatusEffect(StatusEffectType.Hindered);
        Assert.AreEqual(420, Hit(attack, enemy).damageDealt);
    }

    [Test]
    public void Bonus_PerDebuffOnTarget()
    {
        AttackData attack = WithBonus(Attack(200), new AttackBonus { condition = BonusCondition.TargetDebuffCount, bonusDamage = 30 });
        CardInstance enemy = Enemy();
        enemy.ApplyStatusEffect(StatusEffectType.SpeedDown);
        enemy.ApplyStatusEffect(StatusEffectType.Burn);
        enemy.ApplyStatusEffect(StatusEffectType.Haste); // Buff, doesn't count
        Assert.AreEqual(260, Hit(attack, enemy).damageDealt);
    }

    [Test]
    public void Bonus_AttackerHasStatus()
    {
        AttackData attack = WithBonus(Attack(320), new AttackBonus { condition = BonusCondition.AttackerHasStatus, status = StatusEffectType.Haste, bonusDamage = 120 });
        attacker.ApplyStatusEffect(StatusEffectType.Haste);
        Assert.AreEqual(440, Hit(attack, Enemy()).damageDealt);
    }

    [Test]
    public void Bonus_AllyInPlay_OnlyCountsLivingAlliesOnBoard()
    {
        CardData fjorn = Card("Fjorn");
        AttackData attack = WithBonus(Attack(300), new AttackBonus { condition = BonusCondition.AllyInPlay, ally = fjorn, bonusDamage = 140 });
        CardInstance enemy = Enemy(hp: 100000);

        Put(board, fjorn, 2, 7, 7); // Enemy Fjorn doesn't count
        Assert.AreEqual(300, Hit(attack, enemy).damageDealt);

        attacker.SkipAttack();
        CardInstance ally = Put(board, fjorn, 1, 0, 0);
        Assert.AreEqual(440, Hit(attack, enemy).damageDealt);

        attacker.SkipAttack();
        board.Remove(ally);
        Assert.AreEqual(300, Hit(attack, enemy).damageDealt);
    }

    [Test]
    public void Bonus_AttackOnCooldown_AddsExtraEffect()
    {
        AttackData windBlade = Attack(190, name: "Wind Blade");
        AttackData zephyr = Attack(200, name: "Zephyr");
        zephyr.targetStatusEffects = new[] { Effect(StatusEffectType.PhysicalDefenseDown) };
        zephyr.bonuses = new[] { new AttackBonus { condition = BonusCondition.AttackOnCooldown, otherAttack = windBlade,
                                                   extraTargetEffects = new[] { Effect(StatusEffectType.PhysicalAttackDown) } } };
        CardInstance enemy = Enemy(hp: 100000);

        Hit(zephyr, enemy);
        Assert.IsFalse(enemy.statusEffects.Has(StatusEffectType.PhysicalAttackDown), "Wind Blade wasn't used last");

        Hit(windBlade, enemy);
        Hit(zephyr, enemy);
        Assert.IsTrue(enemy.statusEffects.Has(StatusEffectType.PhysicalAttackDown));
    }

    [Test]
    public void Bonus_FromSide()
    {
        AttackData attack = WithBonus(Attack(310), new AttackBonus { condition = BonusCondition.FromSide, side = PositionalBonus.Back, bonusDamage = 120 });
        CardInstance front = Enemy(hp: 100000); // Attacker is in front of this one
        Assert.AreEqual(310, Hit(attack, front).damageDealt);

        attacker.SkipAttack();
        CardInstance behind = Put(board, 2, 2, 3, hp: 100000); // Attacker is behind this one
        Assert.AreEqual(430, Hit(attack, behind).damageDealt);
    }

    [Test]
    public void Bonus_TargetClass()
    {
        AttackData attack = WithBonus(Attack(290), new AttackBonus { condition = BonusCondition.TargetClass, targetClass = CharacterClass.Defender, bonusDamage = 160 });
        Assert.AreEqual(450, Hit(attack, Enemy(characterClass: CharacterClass.Defender)).damageDealt);
    }

    [Test]
    public void Bonus_AttackerHP_NumberAndPercent()
    {
        CardInstance enemy = Enemy(hp: 100000);
        AttackData below = WithBonus(Attack(250), new AttackBonus { condition = BonusCondition.AttackerHPBelow, hp = 800, bonusDamage = 120 });
        Assert.AreEqual(250, Hit(below, enemy).damageDealt);

        attacker.SkipAttack();
        attacker.TakeDamage(300); // 700 HP
        Assert.AreEqual(370, Hit(below, enemy).damageDealt);

        attacker.SkipAttack();
        AttackData abovePercent = WithBonus(Attack(100), new AttackBonus { condition = BonusCondition.AttackerHPAbove, hp = 50, hpIsPercent = true, bonusDamage = 50 });
        Assert.AreEqual(150, Hit(abovePercent, enemy).damageDealt, "700 of 1000 is above 50%");
    }

    [Test]
    public void BonusDamage_IsAddedBeforeMultipliers()
    {
        AttackData attack = WithBonus(Attack(100), new AttackBonus { condition = BonusCondition.TargetClass, targetClass = CharacterClass.Attacker, bonusDamage = 100 });
        attack.hasClassBonus = true;
        attack.classBonusTarget = CharacterClass.Attacker;
        attack.classMultiplier = 1.5f;
        Assert.AreEqual(300, Hit(attack, Enemy()).damageDealt);
    }

    [Test]
    public void BonusSelfEffect_ReplacesBaseVersion()
    {
        // Rally: Defense Up, doubled below half health
        AttackData rally = Attack(0);
        rally.area = AttackArea.Self;
        rally.selfStatusEffects = new[] { Effect(StatusEffectType.PhysicalDefenseUp, 50) };
        rally.bonuses = new[] { new AttackBonus { condition = BonusCondition.AttackerHPBelow, hp = 50, hpIsPercent = true,
                                                  extraSelfEffects = new[] { Effect(StatusEffectType.PhysicalDefenseUp, 100) } } };

        Hit(rally, attacker);
        Assert.AreEqual(50, attacker.statusEffects.GetAmount(StatusEffectType.PhysicalDefenseUp));

        attacker.SkipAttack();
        attacker.TakeDamage(600);
        Hit(rally, attacker);
        Assert.AreEqual(100, attacker.statusEffects.GetAmount(StatusEffectType.PhysicalDefenseUp));
    }

    [Test]
    public void HealBonus_AddsHealing()
    {
        CardInstance ally = Put(board, 1, 4, 3, hp: 1000);
        ally.TakeDamage(500);
        AttackData heal = WithBonus(Attack(100, DamageType.Heal), new AttackBonus { condition = BonusCondition.TargetClass, targetClass = CharacterClass.Attacker, bonusDamage = 50 });
        Assert.AreEqual(150, Hit(heal, ally).amountHealed);
    }

    // ---- Special ----

    [Test]
    public void Execute_DefeatsLowHPEnemy()
    {
        AttackData belladonna = Attack(0);
        belladonna.executeAtOrBelowHP = 800;

        CardInstance healthy = Enemy(hp: 1000);
        Assert.IsFalse(Hit(belladonna, healthy).hits[0].executed);

        attacker.SkipAttack();
        healthy.TakeDamage(250);
        AttackResult result = Hit(belladonna, healthy);
        Assert.IsTrue(result.hits[0].executed);
        Assert.Contains(healthy, result.defeatedCards);
    }

    [Test]
    public void Hangry_DamagesEnemiesThatAttackIt()
    {
        AttackData hangry = Attack(0);
        hangry.whileOnCooldown = CooldownPassive.DamageAttackers;
        hangry.passiveDamage = 100;
        CardInstance nebinabi = Enemy(hp: 5000);
        nebinabi.RecordAttackUsed(hangry);

        AttackResult result = Hit(Attack(100), nebinabi);
        Assert.AreEqual(100, result.reflectedDamage);
        Assert.AreEqual(900, attacker.currentHP);
    }

    [Test]
    public void Hangry_CanDefeatTheAttacker()
    {
        AttackData hangry = Attack(0);
        hangry.whileOnCooldown = CooldownPassive.DamageAttackers;
        hangry.passiveDamage = 100;
        CardInstance nebinabi = Enemy(hp: 5000);
        nebinabi.RecordAttackUsed(hangry);
        attacker.TakeDamage(950);

        AttackResult result = Hit(Attack(100), nebinabi);
        Assert.Contains(attacker, result.defeatedCards);
        Assert.IsFalse(board.IsOnBoard(attacker));
    }

    [Test]
    public void Hangry_DoesntTriggerOnAllies()
    {
        AttackData hangry = Attack(0);
        hangry.whileOnCooldown = CooldownPassive.DamageAttackers;
        CardInstance ally = Put(board, 1, 4, 3);
        ally.RecordAttackUsed(hangry);
        ally.TakeDamage(100);

        Assert.AreEqual(0, Hit(Attack(50, DamageType.Heal), ally).reflectedDamage);
    }
}
