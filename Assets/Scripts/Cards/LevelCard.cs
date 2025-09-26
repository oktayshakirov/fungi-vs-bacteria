using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelCard : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Button button;

    private System.Action<int> onCardClicked;
    private int levelNumber;


    public void Setup(int level, bool isLocked, System.Action<int> callback)
    {
        levelNumber = level;
        if (levelText != null)
        {
            levelText.text = level.ToString();
        }
        onCardClicked = callback;

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (isLocked)
            {
                button.interactable = false;
            }
            else
            {
                button.interactable = true;
                button.onClick.AddListener(() => onCardClicked?.Invoke(levelNumber));
            }
        }

    }
}
