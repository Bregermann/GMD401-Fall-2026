using UnityEngine;

public class CardGrid : MonoBehaviour
{
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private int rows = 8;
    [SerializeField] private int columns = 8;
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
                slot.name = $"Slot_{row}_{col}";
                slots[row, col] = slot.transform;
            }
        }
    }
    public Transform GetSlot(int row, int col)
    {
        if (slots == null || row < 0 || row >= rows || col < 0 || col >= columns)
            return null;

        return slots[row, col];
    }

    public Transform GetNearestEmptySlot(Vector3 worldPosition, float maxDistance)
    {
        if (slots == null) return null;

        Transform nearest = null;
        float closest = float.MaxValue;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                Transform slot = slots[r, c];
                if (slot == null) continue;

                // Skip slots that already have a card
                if (slot.childCount > 0) continue;

                float dist = Vector3.Distance(worldPosition, slot.position);
                if (dist < closest && dist <= maxDistance)
                {
                    closest = dist;
                    nearest = slot;
                }
            }
        }

        return nearest;
    }
}
