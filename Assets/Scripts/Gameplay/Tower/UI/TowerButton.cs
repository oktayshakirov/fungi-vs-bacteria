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
  public class TowerSelectionButton : MonoBehaviour, IBeginDragHandler
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
      UiSkin.Label(costText, UiSkin.Role.Value, UiSkin.Gold);

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
    }

    public void UpdateInteractability()
    {
      bool canAfford = GameManager.Instance.CanAfford(towerConfig.cost);

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