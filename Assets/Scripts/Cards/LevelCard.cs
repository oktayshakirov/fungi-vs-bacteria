using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelCard : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Button button;

    private System.Action<int> onCardClicked;
    private int levelNumber;
    private RectTransform starsRow;


    public void Setup(int level, bool isLocked, int stars, System.Action<int> callback)
    {
        levelNumber = level;
        if (levelText != null)
        {
            levelText.text = level.ToString();
        }
        onCardClicked = callback;

        ShowStars(isLocked ? -1 : stars);

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

    // stars < 0 means locked (no row); 0..3 shows filled/empty stars
    private void ShowStars(int stars)
    {
        if (starsRow != null) Destroy(starsRow.gameObject);
        if (stars < 0) return;

        var go = new GameObject("Stars", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        starsRow = (RectTransform)go.transform;
        starsRow.anchorMin = new Vector2(0.5f, 0f);
        starsRow.anchorMax = new Vector2(0.5f, 0f);
        starsRow.pivot = new Vector2(0.5f, 0f);
        starsRow.anchoredPosition = new Vector2(0f, 14f);
        starsRow.sizeDelta = new Vector2(120f, 36f);

        StarSprite.BuildRow(starsRow, Mathf.Clamp(stars, 0, 3), 32f);
    }
}
