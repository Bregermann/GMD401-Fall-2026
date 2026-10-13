using System.Collections.Generic;

// Combat's record of which card is on which slot. Separate from the visual CardGrid.
// Uses [row, column] the same as CardGrid. Player 1's back row is 0, Player 2's is 7.
//
// CombatManager (in the scene) owns this and handles the scene side of attacks
// (moving knocked-back cards, removing defeated cards).
// STILL NEEDED: CardMover has to tell combat when a card is placed or moved, or combat won't know where it is.
// In CardMover.SmoothSnap after the card snaps, add:
//     CombatManager.Instance.UpdateCardPosition(GetComponent<CardUnit>(), targetSlot);
public class BoardState
{
    public const int Rows = 8;
    public const int Columns = 8;

    private readonly CardInstance[,] cards = new CardInstance[Rows, Columns];

    public bool IsInBounds(int row, int column)
    {
        return row >= 0 && row < Rows && column >= 0 && column < Columns;
    }

    // Returns null if the slot is empty or off the board
    public CardInstance GetCard(int row, int column)
    {
        return IsInBounds(row, column) ? cards[row, column] : null;
    }

    // Off the board counts as not empty (it's a wall)
    public bool IsEmpty(int row, int column)
    {
        return IsInBounds(row, column) && cards[row, column] == null;
    }

    // Puts a card on the board, or moves it if it's already on. Returns false if the slot is taken or off the board.
    public bool Place(CardInstance card, int row, int column)
    {
        if (!IsEmpty(row, column)) return false;

        Remove(card); // Clear its old slot if it had one
        cards[row, column] = card;
        card.row = row;
        card.column = column;
        return true;
    }

    // Same as Place, reads better for cards already on the board
    public bool MoveCard(CardInstance card, int row, int column)
    {
        return Place(card, row, column);
    }

    public bool IsOnBoard(CardInstance card)
    {
        return card != null && GetCard(card.row, card.column) == card;
    }

    public void Remove(CardInstance card)
    {
        if (GetCard(card.row, card.column) == card)
            cards[card.row, card.column] = null;
    }

    public List<CardInstance> GetAllCards()
    {
        List<CardInstance> result = new List<CardInstance>();
        foreach (CardInstance card in cards)
            if (card != null) result.Add(card);
        return result;
    }

    public List<CardInstance> GetCards(int owner)
    {
        return GetAllCards().FindAll(c => c.owner == owner);
    }
}
