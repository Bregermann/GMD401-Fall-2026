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
    if (slots == null || data == null) return null;

    int arrayRows = slots.GetLength(0);
    int arrayCols = slots.GetLength(1);

    // Find where the card currently is
    int currentRow = -1;
    int currentCol = -1;

    if (currentSlot != null)
    {
        for (int r = 0; r < arrayRows; r++)
        {
            for (int c = 0; c < arrayCols; c++)
            {
                if (slots[r, c] == currentSlot)
                {
                    currentRow = r;
                    currentCol = c;
                    break;
                }
            }
            if (currentRow != -1) break;
        }
    }

    Transform nearest = null;
    float closestDist = float.MaxValue;

    for (int r = 0; r < arrayRows; r++)
    {
        for (int c = 0; c < arrayCols; c++)
        {
            Transform slot = slots[r, c];
            if (slot == null || slot.childCount > 0) continue;

            int rowDiff = currentRow == -1 ? 0 : Mathf.Abs(r - currentRow);
            int colDiff = currentCol == -1 ? 0 : Mathf.Abs(c - currentCol);

            
            int steps = Mathf.Max(rowDiff, colDiff);

           
            if (steps > moveSpaces) continue;

            bool isOrthogonal = rowDiff == 0 || colDiff == 0;   // pure up/down/left/right
            bool isDiagonal   = rowDiff == colDiff;             // pure diagonal

            bool allowed = false;

            if (currentRow == -1)
            {
                // First placement only in the player's own rows
                allowed = BoardState.IsInPlacementZone(r, owner);
            }
            else if (isOrthogonal)
            {
                //To move the card forward backward and side to side
                allowed = true;
            }
            else if (isDiagonal && data.canMoveDiagonally)
            {
                // Diagonal only if the card allows it
                allowed = true;
            }

            if (!allowed) continue;

            float dist = Vector3.Distance(worldPosition, slot.position);
            if (dist < closestDist && dist <= maxDistance)
            {
                closestDist = dist;
                nearest = slot;
            }
        }
    }

    return nearest;
}
}
