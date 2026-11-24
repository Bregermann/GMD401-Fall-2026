using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static TestHelpers;

// The computer opponent's draft picks and placement squares (randomness off so results are the same every run)
public class ComputerDrafterTests
{
    static List<Vector2Int> PlacementSquares(int owner)
    {
        List<Vector2Int> squares = new List<Vector2Int>();
        for (int row = 0; row < BoardState.Rows; row++)
            if (BoardState.IsInPlacementZone(row, owner))
                for (int column = 0; column < BoardState.Columns; column++) squares.Add(new Vector2Int(row, column));
        return squares;
    }

    [Test]
    public void PicksTheStrongestCardWhenTheTeamDoesntMatter()
    {
        List<CardData> pool = new List<CardData>
        {
            Card("Weak", CharacterClass.Attacker, hp: 1000),
            Card("Strong", CharacterClass.Attacker, hp: 4000, physicalDefense: 80),
            Card("Middle", CharacterClass.Attacker, hp: 2000),
        };

        int pick = ComputerDrafter.ChoosePick(pool, new List<CardData>(), 0f);

        Assert.AreEqual("Strong", pool[pick].cardName);
    }

    [Test]
    public void PrefersANewClassOverAThirdOfTheSame()
    {
        List<CardData> mine = new List<CardData> { Card("Mage A", CharacterClass.Mage), Card("Mage B", CharacterClass.Mage) };
        List<CardData> pool = new List<CardData>
        {
            Card("Another Mage", CharacterClass.Mage, hp: 2000),
            Card("Attacker", CharacterClass.Attacker, hp: 2000),
        };

        int pick = ComputerDrafter.ChoosePick(pool, mine, 0f);

        Assert.AreEqual("Attacker", pool[pick].cardName);
    }

    [Test]
    public void WantsADefenderByTheMiddleOfTheDraft()
    {
        List<CardData> mine = new List<CardData> { Card("A", CharacterClass.Attacker), Card("M", CharacterClass.Mage) };
        List<CardData> pool = new List<CardData>
        {
            Card("Assassin", CharacterClass.Assassin, hp: 2000),
            Card("Defender", CharacterClass.Defender, hp: 2000),
        };

        int pick = ComputerDrafter.ChoosePick(pool, mine, 0f);

        Assert.AreEqual("Defender", pool[pick].cardName);
    }

    [Test]
    public void DefenderGoesInTheFrontRow()
    {
        BoardState board = new BoardState();
        Vector2Int square = ComputerDrafter.ChooseSquare(Card("D", CharacterClass.Defender), 2, board, PlacementSquares(2), 0f);
        Assert.AreEqual(BoardState.Rows - 2, square.x); // Player 2's front row is 6
    }

    [Test]
    public void HealerGoesInTheBackRow()
    {
        BoardState board = new BoardState();
        Vector2Int square = ComputerDrafter.ChooseSquare(Card("H", CharacterClass.Healer), 1, board, PlacementSquares(1), 0f);
        Assert.AreEqual(0, square.x); // Player 1's back row is 0
    }

    [Test]
    public void PlacesFrontLinersFirst()
    {
        List<CardData> unplaced = new List<CardData> { Card("M", CharacterClass.Mage), Card("D", CharacterClass.Defender), Card("H", CharacterClass.Healer) };
        Assert.AreEqual("D", unplaced[ComputerDrafter.ChooseCardToPlace(unplaced)].cardName);
    }

    [Test]
    public void SpreadsOutInsteadOfClumping()
    {
        BoardState board = new BoardState();
        Put(board, Card("Ally", CharacterClass.Attacker), 2, 6, 3);
        Put(board, Card("Ally", CharacterClass.Attacker), 2, 6, 4);

        Vector2Int square = ComputerDrafter.ChooseSquare(Card("A", CharacterClass.Attacker), 2, board, PlacementSquares(2), 0f);

        Assert.IsFalse(square.x == 6 && (square.y == 2 || square.y == 5), $"Placed right next to both allies at {square}");
    }
}
