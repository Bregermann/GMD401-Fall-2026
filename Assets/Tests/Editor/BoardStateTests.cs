using NUnit.Framework;
using static TestHelpers;

public class BoardStateTests
{
    [Test]
    public void Place_PutsCardOnSlotAndSetsPosition()
    {
        BoardState board = new BoardState();
        CardInstance card = Put(board, 1, 2, 3);

        Assert.AreEqual(card, board.GetCard(2, 3));
        Assert.AreEqual(2, card.row);
        Assert.AreEqual(3, card.column);
        Assert.IsTrue(board.IsOnBoard(card));
    }

    [Test]
    public void Place_FailsOnTakenSlotOrOffBoard()
    {
        BoardState board = new BoardState();
        Put(board, 1, 2, 3);
        CardInstance other = new CardInstance(Card(), 2, -1, -1);

        Assert.IsFalse(board.Place(other, 2, 3));
        Assert.IsFalse(board.Place(other, -1, 0));
        Assert.IsFalse(board.Place(other, 0, BoardState.Columns));
        Assert.IsFalse(board.IsOnBoard(other));
    }

    [Test]
    public void MoveCard_FreesOldSlot()
    {
        BoardState board = new BoardState();
        CardInstance card = Put(board, 1, 2, 3);

        Assert.IsTrue(board.MoveCard(card, 4, 4));
        Assert.IsNull(board.GetCard(2, 3));
        Assert.AreEqual(card, board.GetCard(4, 4));
    }

    [Test]
    public void Remove_TakesCardOffBoard()
    {
        BoardState board = new BoardState();
        CardInstance card = Put(board, 1, 2, 3);
        board.Remove(card);

        Assert.IsFalse(board.IsOnBoard(card));
        Assert.IsTrue(board.IsEmpty(2, 3));
    }

    [Test]
    public void OffBoard_CountsAsNotEmpty()
    {
        BoardState board = new BoardState();
        Assert.IsFalse(board.IsEmpty(-1, 0));
        Assert.IsFalse(board.IsEmpty(BoardState.Rows, 0));
        Assert.IsNull(board.GetCard(99, 99));
    }

    [Test]
    public void GetCards_FiltersByOwner()
    {
        BoardState board = new BoardState();
        Put(board, 1, 0, 0);
        Put(board, 1, 0, 1);
        Put(board, 2, 7, 0);

        Assert.AreEqual(3, board.GetAllCards().Count);
        Assert.AreEqual(2, board.GetCards(1).Count);
        Assert.AreEqual(1, board.GetCards(2).Count);
    }

    [Test]
    public void PlacementZones_AreBackTwoRows()
    {
        Assert.IsTrue(BoardState.IsInPlacementZone(0, 1));
        Assert.IsTrue(BoardState.IsInPlacementZone(1, 1));
        Assert.IsFalse(BoardState.IsInPlacementZone(2, 1));
        Assert.IsTrue(BoardState.IsInPlacementZone(6, 2));
        Assert.IsTrue(BoardState.IsInPlacementZone(7, 2));
        Assert.IsFalse(BoardState.IsInPlacementZone(5, 2));
        Assert.IsTrue(BoardState.IsInPlacementZone(4, 0)); // No owner, no limit
    }

    [Test]
    public void OtherPlayer_Swaps()
    {
        Assert.AreEqual(2, BoardState.OtherPlayer(1));
        Assert.AreEqual(1, BoardState.OtherPlayer(2));
    }
}
