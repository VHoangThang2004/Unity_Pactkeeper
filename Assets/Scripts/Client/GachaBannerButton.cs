using UnityEngine;
using UnityEngine.UI;

public class GachaBannerButton : MonoBehaviour
{
    public Button button;
    public Image bannerImage;

    public void Setup(Sprite art)
    {
        if (bannerImage != null)
        {
            bannerImage.sprite = art;
            bannerImage.color = art != null ? Color.white : new Color(0.2f, 0.2f, 0.25f, 1f);
        }
    }
}
