using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static TestHelpers;

// The computer opponent's choices on small hand-made boards (randomness off so results are the same every run)
public class ComputerPlannerTests
{
    static readonly List<Vector2Int> NoMoves = new List<Vector2Int>();

    static CardInstance Fighter(BoardState board, int owner, int row, int column, params AttackData[] attacks)
    {
        CardData data = Card($"P{owner} fighter", CharacterClass.Attacker);
        if (attacks.Length > 0) data.attack1 = attacks[0];
        if (attacks.Length > 1) data.attack2 = attacks[1];
        if (attacks.Length > 2) data.attack3 = attacks[2];
        return Put(board, data, owner, row, column);
    }

    [Test]
    public void AttacksAnEnemyInReach()
    {
        BoardState board = new BoardState();
        CardInstance me = Fighter(board, 2, 4, 4, Attack(200));
        CardInstance enemy = Put(board, 1, 3, 4);

        ComputerPlanner.Plan plan = ComputerPlanner.Choose(me, board, NoMoves, 0f);

        Assert.AreEqual(0, plan.attackIndex);
        Assert.AreEqual(enemy, plan.target);
    }

    [Test]
    public void PrefersTheKillingBlow()
    {
        BoardState board = new BoardState();
        CardInstance me = Fighter(board, 2, 4, 4, Attack(300));
        Put(board, 1, 3, 4, hp: 1000);              // Healthy, in front
        CardInstance weak = Put(board, 1, 4, 3, hp: 250); // Dies to this hit, to the side

        ComputerPlanner.Plan plan = ComputerPlanner.Choose(me, board, NoMoves, 0f);

        Assert.AreEqual(weak, plan.target);
    }

    [Test]
    public void HealsAHurtAllyRatherThanDoingNothing()
    {
        BoardState board = new BoardState();
        CardInstance me = Fighter(board, 2, 4, 4, Attack(300, DamageType.Heal, "Heal"));
        CardInstance ally = Put(board, 2, 4, 5, hp: 1000);
        ally.currentHP = 400;
        Put(board, 1, 0, 0); // An enemy far away

        ComputerPlanner.Plan plan = ComputerPlanner.Choose(me, board, NoMoves, 0f);

        Assert.AreEqual(ally, plan.target);
    }

    [Test]
    public void DoesntHealSomeoneAtFullHealth()
    {
        BoardState board = new BoardState();
        CardInstance me = Fighter(board, 2, 4, 4, Attack(300, DamageType.Heal, "Heal"));
        Put(board, 2, 4, 5, hp: 1000); // Full HP
        Put(board, 1, 0, 0);

        ComputerPlanner.Plan plan = ComputerPlanner.Choose(me, board, NoMoves, 0f);

        Assert.AreEqual(-1, plan.attackIndex);
    }

    [Test]
    public void MovesInToAttackWhenNothingIsInReach()
    {
        BoardState board = new BoardState();
        CardInstance me = Fighter(board, 2, 6, 4, Attack(200));
        CardInstance enemy = Put(board, 1, 3, 4);
        List<Vector2Int> moves = new List<Vector2Int> { new Vector2Int(5, 4), new Vector2Int(4, 4), new Vector2Int(7, 4) };

        ComputerPlanner.Plan plan = ComputerPlanner.Choose(me, board, moves, 0f);

        Assert.AreEqual(new Vector2Int(4, 4), plan.moveTo); // Next to the enemy
        Assert.AreEqual(enemy, plan.target);
        Assert.AreEqual(6, me.row, "The board must be put back after imagining moves");
        Assert.AreEqual(me, board.GetCard(6, 4));
    }

    [Test]
    public void AvoidsAttackThatWouldDefeatItself()
    {
        BoardState board = new BoardState();
        CardInstance me = Fighter(board, 2, 4, 4, Attack(100));
        me.currentHP = 50;
        CardInstance spiky = Put(board, 1, 3, 4, hp: 2000);
        AttackData passive = Attack(0, DamageType.Physical, "Spikes");
        passive.whileOnCooldown = CooldownPassive.DamageAttackers;
        passive.passiveDamage = 100;
        spiky.lastAttackUsed = passive; // Hurts anyone who attacks it

        ComputerPlanner.Plan plan = ComputerPlanner.Choose(me, board, NoMoves, 0f);

        Assert.AreEqual(-1, plan.attackIndex);
    }

    [Test]
    public void StunIsWorthMoreWhenTheTargetIsStaggered()
    {
        BoardState board = new BoardState();
        CardInstance enemy = Put(board, 1, 0, 0);
        float plain = ComputerPlanner.EffectValue(StatusEffectType.Stun, enemy, true);
        enemy.ApplyStatusEffect(StatusEffectType.Stagger);
        float staggered = ComputerPlanner.EffectValue(StatusEffectType.Stun, enemy, true);

        Assert.Greater(staggered, plain);
    }
}
