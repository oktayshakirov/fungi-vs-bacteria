using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnvironmentCard : MonoBehaviour
{
    [SerializeField] private Image environmentImage;
    [SerializeField] private TextMeshProUGUI environmentTitle;
    [SerializeField] private Image lockIcon;


    public void Setup(Sprite image, string title, bool isLocked)
    {
        if (environmentImage != null)
        {
            environmentImage.sprite = image;
        }
        if (environmentTitle != null)
        {
            environmentTitle.text = title;
        }
        if (lockIcon != null)
        {
            lockIcon.gameObject.SetActive(isLocked);
        }
    }
}
