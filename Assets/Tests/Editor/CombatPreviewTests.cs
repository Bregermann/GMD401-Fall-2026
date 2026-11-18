using NUnit.Framework;
using static TestHelpers;

// The damage preview must match what the attack really does, and must not change anything
public class CombatPreviewTests
{
    BoardState board;

    [SetUp]
    public void SetUp()
    {
        board = new BoardState();
    }

    [Test]
    public void Preview_MatchesRealDamage_AndChangesNothing()
    {
        CardInstance attacker = Put(board, 1, 3, 3);
        CardInstance target = Put(board, 2, 4, 3, physicalDefense: 30);
        AttackData attack = Attack(100);

        AttackResult preview = CombatResolver.PreviewAttack(attacker, attack, target, board);
        Assert.AreEqual(1000, target.currentHP, "Preview doesn't deal damage");
        Assert.IsNull(attacker.lastAttackUsed, "Preview doesn't use the attack");

        AttackResult real = CombatResolver.ResolveAttack(attacker, attack, target, board);
        Assert.AreEqual(real.damageDealt, preview.damageDealt);
    }

    [Test]
    public void Preview_AreaAttack_HasSameHits()
    {
        CardInstance mage = Put(board, 1, 3, 3);
        CardInstance target = Put(board, 2, 4, 3);
        Put(board, 2, 5, 4);
        AttackData starfall = Attack(300, DamageType.Magic);
        starfall.area = AttackArea.AroundTarget;
        starfall.areaSize = 1;

        AttackResult preview = CombatResolver.PreviewAttack(mage, starfall, target, board);
        AttackResult real = CombatResolver.ResolveAttack(mage, starfall, target, board);
        Assert.AreEqual(real.hits.Count, preview.hits.Count);
        for (int i = 0; i < real.hits.Count; i++)
        {
            Assert.AreEqual(real.hits[i].card, preview.hits[i].card);
            Assert.AreEqual(real.hits[i].damage, preview.hits[i].damage);
        }
    }

    [Test]
    public void Preview_Lethal_ListsDefeatedCard()
    {
        CardInstance attacker = Put(board, 1, 3, 3);
        CardInstance target = Put(board, 2, 4, 3, hp: 50);

        AttackResult preview = CombatResolver.PreviewAttack(attacker, Attack(100), target, board);
        Assert.Contains(target, preview.defeatedCards);
        Assert.IsTrue(board.IsOnBoard(target), "Still on the board after a preview");
    }

    [Test]
    public void Preview_Heal_CappedAtMaxHP()
    {
        CardInstance healer = Put(board, 1, 3, 3);
        CardInstance ally = Put(board, 1, 4, 3);
        ally.TakeDamage(30);

        AttackResult preview = CombatResolver.PreviewAttack(healer, Attack(100, DamageType.Heal), ally, board);
        Assert.AreEqual(30, preview.amountHealed);
        Assert.AreEqual(970, ally.currentHP);
    }

    [Test]
    public void Preview_KnockbackIntoWall_ShowsCollision_WithoutMoving()
    {
        CardInstance attacker = Put(board, 1, 6, 3);
        CardInstance target = Put(board, 2, 7, 3);
        AttackData shove = Attack(100);
        shove.hasKnockback = true;
        shove.knockbackCollisionDamage = 50;

        AttackResult preview = CombatResolver.PreviewAttack(attacker, shove, target, board);
        Assert.IsTrue(preview.hitObstacle);
        Assert.AreEqual(50, preview.collisionDamage);
        Assert.AreEqual(1000, target.currentHP);
    }
}
