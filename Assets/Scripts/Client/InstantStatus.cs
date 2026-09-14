using UnityEngine;
using UnityEngine.UI;

public class InstantStatus : MonoBehaviour
{
    [SerializeField] private Sprite OpenSprite;
    [SerializeField] private Sprite CloseSprite;
    [SerializeField] private Image StatusImg;

    public void UpdateInstantStatus(bool isInstantEnded)
    {
        if (isInstantEnded)
        {
            StatusImg.sprite = CloseSprite;
        }
        else
        {
            StatusImg.sprite = OpenSprite;
        }
    }

}
