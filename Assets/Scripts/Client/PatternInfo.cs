using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PatternInfo : MonoBehaviour
{
    [SerializeField] public Transform cellListContainer;
    [SerializeField] public GameObject singleCellPrefab;
    [SerializeField] public GameObject centerCellPrefab;

    private List<Image> cells = new List<Image>();

    public void Init(List<Vector2Int> patternCells, bool isTargetPattern)
    {
        cells.Clear();
        foreach (Transform child in cellListContainer)
            Destroy(child.gameObject);

        Color highlightColor = isTargetPattern ? Color.blue : Color.magenta;
        int size = 7;
        int center = size / 2; // = 3

        var patternSet = new HashSet<Vector2Int>();
        foreach (var cell in patternCells)
            patternSet.Add(new Vector2Int(cell.x, cell.y));

        for (int row = size - 1; row >= 0; row--)
        {
            for (int col = 0; col < size; col++)
            {
                bool isCenter = col == center && row == center;
                var go = Instantiate(isCenter ? centerCellPrefab : singleCellPrefab, cellListContainer);
                var img = go.GetComponent<Image>();
                cells.Add(img);

                int offsetX = col - center;
                int offsetY = row - center;

                if (patternSet.Contains(new Vector2Int(offsetX, offsetY)))
                    img.color = highlightColor;
            }
        }
    }
}