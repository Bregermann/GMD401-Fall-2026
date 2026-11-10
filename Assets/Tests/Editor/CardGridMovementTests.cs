using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static TestHelpers;

// Movement rules in CardGrid: range, straight lines, diagonals, blocking and placement zones
public class CardGridMovementTests
{
    GameObject gridObject;
    GameObject slotTemplate;
    CardGrid grid;

    [SetUp]
    public void SetUp()
    {
        slotTemplate = new GameObject("Slot template");
        gridObject = new GameObject("Grid");
        grid = gridObject.AddComponent<CardGrid>();

        SerializedObject serialized = new SerializedObject(grid);
        serialized.FindProperty("slotPrefab").objectReferenceValue = slotTemplate;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        grid.GenerateEmptyGrid();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(gridObject);
        Object.DestroyImmediate(slotTemplate);
    }

    // A card sitting on a slot (the grid checks slots for children)
    void Occupy(int row, int column)
    {
        new GameObject("Card").transform.SetParent(grid.GetSlot(row, column));
    }

    // Asks for the slot the player dropped on, returns whether the move is allowed there
    bool CanMove(int fromRow, int fromCol, int toRow, int toCol, int moveSpaces = 3, bool diagonal = false, int owner = 1)
    {
        CardData data = Card(moveSpaces: moveSpaces);
        data.canMoveDiagonally = diagonal;
        Transform from = fromRow < 0 ? null : grid.GetSlot(fromRow, fromCol);
        Transform target = grid.GetSlot(toRow, toCol);
        return grid.GetNearestValidSlot(target.position, data, moveSpaces, owner, from, 0.1f) == target;
    }

    [Test]
    public void StraightMoves_WithinRange()
    {
        Assert.IsTrue(CanMove(3, 3, 6, 3));
        Assert.IsTrue(CanMove(3, 3, 3, 0));
        Assert.IsFalse(CanMove(3, 3, 7, 3), "4 spaces with 3 move");
    }

    [Test]
    public void Diagonals_OnlyIfTheCardAllowsIt()
    {
        Assert.IsFalse(CanMove(3, 3, 5, 5));
        Assert.IsTrue(CanMove(3, 3, 5, 5, diagonal: true));
        Assert.IsFalse(CanMove(3, 3, 5, 4, diagonal: true), "Not a straight line");
    }

    [Test]
    public void CardsBlockMovement()
    {
        Occupy(4, 3);
        Assert.IsFalse(CanMove(3, 3, 5, 3), "Can't jump over a card");
        Assert.IsFalse(CanMove(3, 3, 4, 3), "Can't land on a card");
        Assert.IsTrue(CanMove(3, 3, 2, 3), "Other directions still open");
    }

    [Test]
    public void CardsBlockDiagonalMovement()
    {
        Occupy(4, 4);
        Assert.IsFalse(CanMove(3, 3, 5, 5, diagonal: true));
    }

    [Test]
    public void FirstPlacement_OnlyInOwnRows()
    {
        Assert.IsTrue(CanMove(-1, -1, 1, 4, owner: 1));
        Assert.IsFalse(CanMove(-1, -1, 2, 4, owner: 1));
        Assert.IsTrue(CanMove(-1, -1, 6, 4, owner: 2));
        Assert.IsFalse(CanMove(-1, -1, 1, 4, owner: 2));
    }
}
