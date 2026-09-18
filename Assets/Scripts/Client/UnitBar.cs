using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum BarType
{
    SkillPoint,
    AllyHP,
    EnemyHP
}
public class UnitBar : MonoBehaviour
{
    [SerializeField] private GameObject barContainer;
    [SerializeField] private GameObject barFillPrefab;

    private List<Image> fillImages = new List<Image>();

    public void Init(int maxValue, BarType type)
    {
        ClearFills();
        for (int i = 0; i < maxValue; i++)
        {
            GameObject fill = Instantiate(barFillPrefab, barContainer.transform);
            Image fillImage = fill.GetComponent<Image>();
            fillImages.Add(fillImage);
            switch (type)
            {
                case BarType.SkillPoint:
                    fillImage.color = Color.yellow;
                    break;
                case BarType.AllyHP:
                    fillImage.color = Color.green;
                    break;
                case BarType.EnemyHP:
                    fillImage.color = Color.red;
                    break;
            }
        }
    }

    public void UpdateValue(int currentValue)
    {
        for (int i = 0; i < fillImages.Count; i++)
        {
            fillImages[i].enabled = i < currentValue;
        }
    }

    private void ClearFills()
    {
        while (fillImages.Count > 0)
        {
            Image img = fillImages[0];
            fillImages.RemoveAt(0);
            Destroy(img.gameObject);
        }
    }
}
