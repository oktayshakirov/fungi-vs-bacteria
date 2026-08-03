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

        // The prefab's VerticalLayoutGroup stacked the number and the star row,
        // which overflows once the level grid shrinks the cell. Everything on
        // the card is placed on its own anchors instead.
        var stack = GetComponent<VerticalLayoutGroup>();
        if (stack != null) stack.enabled = false;

        if (levelText != null)
        {
            levelText.text = level.ToString();
            UiSkin.Label(levelText, UiSkin.Role.Heading,
                isLocked ? UiSkin.TextMuted : UiSkin.TextPrimary);
            levelText.alignment = TextAlignmentOptions.Center;

            RectTransform numberRect = levelText.rectTransform;
            Detach(numberRect);
            numberRect.anchorMin = new Vector2(0.5f, 0.5f);
            numberRect.anchorMax = new Vector2(0.5f, 0.5f);
            numberRect.pivot = new Vector2(0.5f, 0.5f);
            numberRect.anchoredPosition = new Vector2(0f, 16f);   // above the stars
            numberRect.sizeDelta = new Vector2(120f, 70f);
        }
        onCardClicked = callback;

        var background = GetComponent<Image>();
        if (background != null)
        {
            UiSkin.Panel(background,
                isLocked ? UiSkin.PanelDark : UiSkin.PanelRaised, UiSkin.RadiusButton);
        }
        if (button != null && background != null) button.targetGraphic = background;

        ShowLock(isLocked);

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

    private RectTransform lockBadge;

    // The card prefab has a VerticalLayoutGroup, which would treat these
    // decorations as content and push them out of the bottom of the card once
    // the level grid shrinks the cell. Opting out keeps them on their anchors.
    private static void Detach(RectTransform rect)
    {
        var element = rect.gameObject.GetComponent<LayoutElement>();
        if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
        element.ignoreLayout = true;
    }

    // A padlock over locked cards, so "locked" reads without relying on the
    // greyed-out button alone.
    private void ShowLock(bool isLocked)
    {
        if (lockBadge != null) Destroy(lockBadge.gameObject);
        if (!isLocked) return;

        Image padlock = UiSkin.Icon(transform, UiSprites.Lock(), UiSkin.TextMuted, 46f);
        lockBadge = (RectTransform)padlock.transform;
        Detach(lockBadge);
        lockBadge.anchorMin = new Vector2(0.5f, 0.5f);
        lockBadge.anchorMax = new Vector2(0.5f, 0.5f);
        lockBadge.anchoredPosition = Vector2.zero;

        if (levelText != null) levelText.gameObject.SetActive(false);
    }

    // stars < 0 means locked (no row); 0..3 shows filled/empty stars
    private void ShowStars(int stars)
    {
        if (starsRow != null) Destroy(starsRow.gameObject);
        if (stars < 0) return;

        var go = new GameObject("Stars", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        starsRow = (RectTransform)go.transform;
        Detach(starsRow);
        starsRow.anchorMin = new Vector2(0.5f, 0f);
        starsRow.anchorMax = new Vector2(0.5f, 0f);
        starsRow.pivot = new Vector2(0.5f, 0f);
        starsRow.anchoredPosition = new Vector2(0f, 14f);
        starsRow.sizeDelta = new Vector2(120f, 36f);

        StarSprite.BuildRow(starsRow, Mathf.Clamp(stars, 0, 3), 32f);
    }
}
