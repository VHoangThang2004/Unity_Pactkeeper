using UnityEngine;
using UnityEngine.UI;

public class InstantStatus : MonoBehaviour
{
    [SerializeField] private Sprite OpenSprite;
    [SerializeField] private Sprite CloseSprite;
    [SerializeField] private Image StatusImg;

    public void UpdateInstantStatus(bool openEye)
    {
        if (!openEye)
        {
            StatusImg.sprite = CloseSprite;
        }
        else
        {
            StatusImg.sprite = OpenSprite;
        }
    }

}
