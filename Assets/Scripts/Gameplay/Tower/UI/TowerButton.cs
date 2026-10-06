using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace TowerDefense.UI
{
  // IBeginDragHandler adds press-and-drag-onto-the-board as a second way to
  // place a tower, alongside the existing tap-card-then-tap-board flow (the
  // Button's own OnClick, wired below, is untouched by this).
  //
  // Unity's EventSystem treats a drag and a click as mutually exclusive for
  // the same gesture: OnClick only fires if the pointer never crossed the drag
  // threshold, and once it does, OnClick is suppressed and OnBeginDrag fires
  // instead. So a quick tap always goes through StartPlacement exactly as
  // before, and only an actual drag reaches StartPlacementFromDrag - there is
  // no double-arm on a plain click.
  //
  // No IDragHandler/IEndDragHandler needed: TowerPlacement.Update() already
  // polls the live pointer position and Input.GetMouseButtonUp every frame
  // once a tower is armed, regardless of what armed it.
  public class TowerSelectionButton : MonoBehaviour, IBeginDragHandler, IPointerClickHandler
  {
    [SerializeField] private Image towerIcon;
    [SerializeField] private Image goldIcon;
    [SerializeField] private Image lockIcon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI costText;

    private Button button;
    private TowerConfig towerConfig;
    private System.Action<TowerConfig> onSelected;
    private System.Action<TowerConfig> onDragStarted;

    private void Awake()
    {
      button = GetComponent<Button>();
    }

    public void Initialize(TowerConfig config, System.Action<TowerConfig> onSelectedCallback,
      System.Action<TowerConfig> onDragStartedCallback = null)
    {
      towerConfig = config;
      onSelected = onSelectedCallback;
      onDragStarted = onDragStartedCallback;

      nameText.text = config.towerName;
      costText.text = config.cost.ToString();

      if (config.towerIcon != null)
      {
        towerIcon.sprite = config.towerIcon;
        // The icons are square cut-outs now (TowerIconRender); the old ones were
        // odd-sized photos, and a stretched sprite squashes the tower.
        towerIcon.preserveAspect = true;
        // Behind the name and the price: the prefab has the icon as its last
        // child, so a tall cut-out drew over both labels. Trimmed to the band
        // between them.
        towerIcon.transform.SetAsFirstSibling();
        var iconRect = towerIcon.rectTransform;
        iconRect.anchorMin = new Vector2(0.08f, 0.25f);
        iconRect.anchorMax = new Vector2(0.92f, 0.78f);
        iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
      }

      Style();
      button.onClick.AddListener(HandleClick);
      UpdateInteractability();
    }

    // The card was a plain white box with black text. Restyled here rather than
    // in the prefab so it stays in step with the rest of the skin.
    private void Style()
    {
      var background = GetComponent<Image>();
      if (background != null)
      {
        UiSkin.Panel(background, UiSkin.PanelRaised, UiSkin.RadiusButton);
        button.targetGraphic = background;
      }

      var colors = button.colors;
      colors.normalColor = Color.white;
      colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
      colors.pressedColor = new Color(0.78f, 0.80f, 0.86f, 1f);
      colors.disabledColor = new Color(0.6f, 0.6f, 0.65f, 0.7f);
      colors.fadeDuration = 0.08f;
      button.colors = colors;

      UiSkin.Label(nameText, UiSkin.Role.Caption);
      nameText.fontSizeMin = 12f;
      nameText.fontSizeMax = 17f;
      nameText.alignment = TextAlignmentOptions.Center;
      nameText.rectTransform.anchorMin = new Vector2(0.04f, 0.78f);
      nameText.rectTransform.anchorMax = new Vector2(0.96f, 0.98f);
      nameText.rectTransform.offsetMin = nameText.rectTransform.offsetMax = Vector2.zero;
      UiSkin.Label(costText, UiSkin.Role.Value, UiSkin.Gold);
      costText.fontSizeMin = 16f;
      costText.fontSizeMax = 21f;
      costText.alignment = TextAlignmentOptions.MidlineLeft;

      // The affordability markers were tiny sprites; a coin and a dimmed coin
      // read better at card size and need no extra art.
      if (goldIcon != null)
      {
        goldIcon.sprite = UiSprites.Coin();
        goldIcon.color = UiSkin.Gold;
        goldIcon.preserveAspect = true;
      }
      if (lockIcon != null)
      {
        lockIcon.sprite = UiSprites.Coin();
        lockIcon.color = UiSkin.Danger;
        lockIcon.preserveAspect = true;
      }

      StyleCostRow();
    }

    // The prefab authored the coin icon at 15x15 next to a price label that
    // auto-sizes up to 40pt, and the "can't afford" lock icon at a mismatched
    // 30x30 - the coin read as a stray dot next to an oversized number, and
    // swapping states changed the icon's size. A HorizontalLayoutGroup gives
    // both icons one consistent size and keeps the price snug beside whichever
    // one is showing instead of independently centred in its own box.
    //
    // The container itself is also re-anchored here, to the card's BOTTOM edge
    // instead of the prefab's fixed offset from centre. The towers rail
    // (HudTheme.StyleTowersPanel) shrinks each card's cell height to fit more
    // rows in the rail, and GridLayoutGroup resizes a centre-pivoted card
    // symmetrically - so a fixed centre offset calibrated for the full-height
    // card ends up UNDER the shrunk card's real bottom edge, which is exactly
    // what put the coin icon and price outside the card. Bottom-anchoring
    // means this row always sits a fixed distance above whatever the card's
    // actual bottom edge turns out to be.
    private const float CostRowBottomMargin = 6f;

    private void StyleCostRow()
    {
      if (costText == null) return;
      Transform container = costText.transform.parent;
      if (container == null) return;

      var containerRect = (RectTransform)container;
      containerRect.anchorMin = new Vector2(0.5f, 0f);
      containerRect.anchorMax = new Vector2(0.5f, 0f);
      containerRect.pivot = new Vector2(0.5f, 0f);
      containerRect.anchoredPosition = new Vector2(0f, CostRowBottomMargin);
      containerRect.sizeDelta = new Vector2(86f, 24f);

      var row = container.GetComponent<HorizontalLayoutGroup>();
      if (row == null) row = container.gameObject.AddComponent<HorizontalLayoutGroup>();
      row.spacing = 6f;
      row.childAlignment = TextAnchor.MiddleCenter;
      row.childControlWidth = true;
      row.childControlHeight = true;
      row.childForceExpandWidth = false;
      row.childForceExpandHeight = true;

      const float iconSize = 18f;
      SizeIcon(goldIcon, iconSize);
      SizeIcon(lockIcon, iconSize);

      var textElement = costText.GetComponent<LayoutElement>();
      if (textElement == null) textElement = costText.gameObject.AddComponent<LayoutElement>();
      textElement.flexibleWidth = 1f;
      textElement.minWidth = 0f;
    }

    private static void SizeIcon(Image icon, float size)
    {
      if (icon == null) return;
      icon.rectTransform.sizeDelta = new Vector2(size, size);

      var element = icon.GetComponent<LayoutElement>();
      if (element == null) element = icon.gameObject.AddComponent<LayoutElement>();
      element.preferredWidth = size;
      element.preferredHeight = size;
      element.flexibleWidth = 0f;
    }

    public void UpdateInteractability()
    {
      RefreshAffordability(GameManager.Instance != null && towerConfig != null
        && GameManager.Instance.CanAfford(towerConfig.cost));
    }

    public void RefreshAffordability(bool canAfford)
    {
      button.interactable = canAfford;
      Color iconColor = towerIcon.color;
      iconColor.a = canAfford ? 1f : 0.45f;
      towerIcon.color = iconColor;

      // Whole card dims when unaffordable, not just the tower icon
      var background = GetComponent<Image>();
      if (background != null)
      {
        background.color = canAfford ? UiSkin.PanelRaised
          : new Color(UiSkin.PanelDark.r, UiSkin.PanelDark.g, UiSkin.PanelDark.b, 0.75f);
      }
      if (costText != null) costText.color = canAfford ? UiSkin.Gold : UiSkin.Danger;

      goldIcon.gameObject.SetActive(canAfford);
      lockIcon.gameObject.SetActive(!canAfford);
    }

    private void HandleClick()
    {
      onSelected?.Invoke(towerConfig);
    }

    // A card the player cannot afford is a disabled Button, which swallows the
    // tap: pressing it did nothing, not even a buzz. It answers with the locked
    // thud and a shake instead. Affordable cards are left to the Button.
    public void OnPointerClick(PointerEventData eventData)
    {
      if (button == null || button.interactable) return;
      AudioManager.Instance?.PlayLocked();
      UiShake.Nudge((RectTransform)transform, 6f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
      // Selectable.interactable does not gate a co-located IBeginDragHandler on
      // its own, so an unaffordable/locked card must be checked here too.
      if (button == null || !button.interactable) return;
      onDragStarted?.Invoke(towerConfig);
    }

    private void OnDestroy()
    {
      button.onClick.RemoveListener(HandleClick);
    }
  }
}