using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnvironmentCard : MonoBehaviour
{
    [SerializeField] private Image environmentImage;
    [SerializeField] private TextMeshProUGUI environmentTitle;
    [SerializeField] private Image lockIcon;


    // Opts a child out of any layout group on the card, so the anchors set here
    // are what actually decide where it sits.
    private static void Unmanaged(RectTransform rect)
    {
        var element = rect.GetComponent<LayoutElement>();
        if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
        element.ignoreLayout = true;
    }

    public void Setup(Sprite image, string title, bool isLocked)
    {
        // The prefab root carries no Image, so a card with no preview art (the
        // locked environments) rendered as nothing but a floating padlock.
        // Set on the rect directly, and mirrored onto a LayoutElement, so the
        // card is the right size whether or not the row controls its children.
        var rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(340f, 440f);

        var size = GetComponent<LayoutElement>();
        if (size == null) size = gameObject.AddComponent<LayoutElement>();
        size.minWidth = 340f;
        size.minHeight = 440f;
        size.preferredWidth = 340f;
        size.preferredHeight = 440f;
        size.flexibleWidth = 0f;
        size.flexibleHeight = 0f;

        // NOTE: no SetAsFirstSibling here. This Image lives on the card root, so
        // reordering it reorders the *card* among its siblings — every card
        // jumped to the front as it was built, which is what reversed the list
        // into 7..1. A root Image already draws behind its own children.
        var background = GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        UiSkin.Panel(background,
            isLocked ? UiSkin.PanelDark : UiSkin.PanelRaised, UiSkin.RadiusPanel);

        if (environmentImage != null)
        {
            // Pinned inside the card. The prefab sizes this for a much larger
            // card, so it spilled far past the rounded panel once the cards
            // were shrunk to thumbnails.
            Unmanaged(environmentImage.rectTransform);
            environmentImage.rectTransform.anchorMin = Vector2.zero;
            environmentImage.rectTransform.anchorMax = Vector2.one;
            environmentImage.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            environmentImage.rectTransform.offsetMin = new Vector2(12f, 62f);  // room for the title
            environmentImage.rectTransform.offsetMax = new Vector2(-12f, -12f);

            environmentImage.sprite = image;
            environmentImage.preserveAspect = false;
            // Locked environments read as a dimmed thumbnail rather than a hole
            environmentImage.color = isLocked ? new Color(0.45f, 0.45f, 0.52f, 1f) : Color.white;
            environmentImage.enabled = image != null;
        }

        if (environmentTitle != null)
        {
            Unmanaged(environmentTitle.rectTransform);
            environmentTitle.rectTransform.anchorMin = new Vector2(0f, 0f);
            environmentTitle.rectTransform.anchorMax = new Vector2(1f, 0f);
            environmentTitle.rectTransform.pivot = new Vector2(0.5f, 0f);
            environmentTitle.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            environmentTitle.rectTransform.sizeDelta = new Vector2(-20f, 42f);

            environmentTitle.text = title;
            UiSkin.Label(environmentTitle, UiSkin.Role.Heading,
                isLocked ? UiSkin.TextMuted : UiSkin.TextPrimary);
            environmentTitle.alignment = TextAlignmentOptions.Center;
            environmentTitle.outlineWidth = 0.2f;
            environmentTitle.outlineColor = new Color32(10, 12, 20, 220);
        }

        if (lockIcon != null)
        {
            lockIcon.gameObject.SetActive(isLocked);
            lockIcon.sprite = UiSprites.Lock();
            lockIcon.color = UiSkin.TextPrimary;
            lockIcon.preserveAspect = true;

            // The prefab stretches this to fill the card; a badge reads better.
            // It also has to opt out of the card's layout group, or the group
            // resizes it straight back.
            var badge = lockIcon.rectTransform;
            var element = badge.GetComponent<LayoutElement>();
            if (element == null) element = badge.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            badge.anchorMin = new Vector2(0.5f, 0.5f);
            badge.anchorMax = new Vector2(0.5f, 0.5f);
            badge.pivot = new Vector2(0.5f, 0.5f);
            badge.anchoredPosition = Vector2.zero;
            badge.sizeDelta = new Vector2(130f, 130f);
        }
    }
}
