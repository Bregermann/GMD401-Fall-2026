using System.Collections.Generic;
using UnityEngine;

public class CardGrid : MonoBehaviour
{
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private int rows = 8;
    [SerializeField] private int columns = 8;
    //Creates spacing in between the slots so cards cards on touching each other
    [SerializeField] private float spacingX = 1f;
    [SerializeField] private float spacingZ = 1.5f;
    private Transform[,] slots;
    void Start()
    {
        GenerateEmptyGrid();
    } 
    public void GenerateEmptyGrid()
    {
        slots = new Transform[rows, columns];    

        float totalWidth = (columns -1 ) * spacingX;
        float totalDepth = (rows -1 ) * spacingZ;

        Vector3 origin = new Vector3(-totalWidth * 0.5f, 0f, -totalDepth * 0.5f);
        for (int row = 0; row < rows; row++)
        {for (int col = 0; col < columns; col++)
            {
                Vector3 localPos = origin + new Vector3(col * spacingX, 0f, row * spacingZ);
            
        
                GameObject slot = Instantiate(slotPrefab, transform);
                slot.transform.localPosition = localPos;
                slot.transform.localRotation = Quaternion.identity;
                //Lets you read the cards
                slot.name = $"Slot_{row}_{col}";
                slots[row, col] = slot.transform;
            }
        }
    }
    //Helps you find out what row and column a slot is
    public Transform GetSlot(int row, int col)
    {
        if (slots == null || row < 0 || row >= rows || col < 0 || col >= columns)
            return null;

        return slots[row, col];
    }

   //moveSpaces is passed in so Haste and Hindered change how far the card can go
   //owner limits first placement to that player's rows (0 = anywhere)
   public Transform GetNearestValidSlot(Vector3 worldPosition, CardData data, int moveSpaces, int owner, Transform currentSlot, float maxDistance = 999f)
    {
        Transform nearest = null;
        float closestDist = float.MaxValue;

        foreach (Transform slot in GetValidSlots(data, moveSpaces, owner, currentSlot))
        {
            float dist = Vector3.Distance(worldPosition, slot.position);
            if (dist < closestDist && dist <= maxDistance)
            {
                closestDist = dist;
                nearest = slot;
            }
        }
        return nearest;
    }

    //Every empty slot the card can move to, or be placed on if it isn't on the board yet (currentSlot null)
    public List<Transform> GetValidSlots(CardData data, int moveSpaces, int owner, Transform currentSlot)
    {
        List<Transform> valid = new List<Transform>();
        if (slots == null || data == null) return valid;

        // Find where the card currently is
        FindSlot(currentSlot, out int currentRow, out int currentCol);

        for (int r = 0; r < slots.GetLength(0); r++)
        {
            for (int c = 0; c < slots.GetLength(1); c++)
            {
                Transform slot = slots[r, c];
                if (slot == null || slot.childCount > 0) continue;

                int rowDiff = currentRow == -1 ? 0 : Mathf.Abs(r - currentRow);
                int colDiff = currentCol == -1 ? 0 : Mathf.Abs(c - currentCol);
                int steps = Mathf.Max(rowDiff, colDiff);
                if (steps > moveSpaces) continue;

                bool isOrthogonal = rowDiff == 0 || colDiff == 0;   // pure up/down/left/right
                bool isDiagonal   = rowDiff == colDiff;             // pure diagonal

                bool allowed;
                if (currentRow == -1)
                    allowed = BoardState.IsInPlacementZone(r, owner); // First placement only in the player's own rows
                else if (isOrthogonal)
                    allowed = true;                                   //To move the card forward backward and side to side
                else
                    allowed = isDiagonal && data.canMoveDiagonally;   // Diagonal only if the card allows it

                if (!allowed) continue;

                // Cards can't move through other cards
                if (currentRow != -1 && !IsPathClear(currentRow, currentCol, r, c)) continue;

                valid.Add(slot);
            }
        }
        return valid;
    }

    //Row and column of a slot, or -1, -1 if it isn't one of this grid's slots
    void FindSlot(Transform slot, out int row, out int col)
    {
        row = -1;
        col = -1;
        if (slot == null || slots == null) return;

        for (int r = 0; r < slots.GetLength(0); r++)
        {
            for (int c = 0; c < slots.GetLength(1); c++)
            {
                if (slots[r, c] != slot) continue;
                row = r;
                col = c;
                return;
            }
        }
    }

    //True if every slot between the two (not counting either end) is empty. Moves are straight lines so this steps along one
    bool IsPathClear(int fromRow, int fromCol, int toRow, int toCol)
    {
        int rowStep = System.Math.Sign(toRow - fromRow);
        int colStep = System.Math.Sign(toCol - fromCol);
        int steps = Mathf.Max(Mathf.Abs(toRow - fromRow), Mathf.Abs(toCol - fromCol));

        for (int i = 1; i < steps; i++)
        {
            if (slots[fromRow + rowStep * i, fromCol + colStep * i].childCount > 0)
                return false;
        }
        return true;
    }
}