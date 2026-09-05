using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The coin balance shown in the top-left of the main menu, with a "+" that
// opens the wallet.
//
// Built in code rather than authored into the menu prefab, for the same reason
// the rest of the menu is: DisplaySetup/MenuLayout own the menu's layout, and a
// scene-authored chip would drift from them. It subscribes to Wallet so the
// number updates the moment an ad pays out, without the screen polling.
public class CoinChip : MonoBehaviour
{
  private TMP_Text amountLabel;

  public static CoinChip Create(Transform parent, System.Action onPlusClicked)
  {
    var go = new GameObject("CoinChip", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(0f, 1f);
    rect.anchorMax = new Vector2(0f, 1f);
    rect.pivot = new Vector2(0f, 1f);
    // Matches the settings gear's inset on the opposite corner (28, the same
    // value every other corner button in the game uses).
    rect.anchoredPosition = new Vector2(28f, -28f);
    rect.sizeDelta = new Vector2(230f, 76f);

    var chip = go.AddComponent<CoinChip>();
    chip.Build(onPlusClicked);
    return chip;
  }

  private void Build(System.Action onPlusClicked)
  {
    var background = gameObject.AddComponent<Image>();
    UiSkin.Panel(background, UiSkin.PanelRaised, UiSkin.RadiusChip);
    background.raycastTarget = false;

    var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
    layout.padding = new RectOffset(14, 8, 6, 6);
    layout.spacing = 10f;
    layout.childAlignment = TextAnchor.MiddleLeft;
    layout.childControlWidth = true;
    layout.childControlHeight = true;
    layout.childForceExpandWidth = false;
    layout.childForceExpandHeight = true;

    Image coin = UiSkin.Icon(transform, UiSprites.Coin(), UiSkin.Gold, 40f);
    var coinElement = coin.gameObject.AddComponent<LayoutElement>();
    coinElement.preferredWidth = 40f;
    coinElement.flexibleWidth = 0f;

    var labelGo = new GameObject("Amount", typeof(RectTransform));
    labelGo.transform.SetParent(transform, false);
    amountLabel = labelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(amountLabel, UiSkin.Role.Value, UiSkin.Gold);
    amountLabel.alignment = TextAlignmentOptions.MidlineLeft;
    amountLabel.raycastTarget = false;
    labelGo.AddComponent<LayoutElement>().flexibleWidth = 1f;

    // The "+" is the entry point to the wallet. It is a real button inside the
    // chip so the balance itself stays a passive readout.
    var plusGo = new GameObject("Plus", typeof(RectTransform));
    plusGo.transform.SetParent(transform, false);
    var plusImage = plusGo.AddComponent<Image>();
    var plus = plusGo.AddComponent<Button>();
    UiSkin.StyleButton(plus, UiSkin.Primary, UiSkin.RadiusChip);
    plusImage.sprite = UiSprites.Button(UiSkin.RadiusChip);

    var plusLabelGo = new GameObject("Label", typeof(RectTransform));
    plusLabelGo.transform.SetParent(plusGo.transform, false);
    var plusLabel = plusLabelGo.AddComponent<TextMeshProUGUI>();
    UiSkin.Label(plusLabel, UiSkin.Role.ButtonLabel, UiSkin.TextDark);
    plusLabel.text = "+";
    plusLabel.alignment = TextAlignmentOptions.Center;
    plusLabel.raycastTarget = false;
    UiSkin.Stretch((RectTransform)plusLabelGo.transform);

    var plusElement = plusGo.AddComponent<LayoutElement>();
    plusElement.preferredWidth = 58f;
    plusElement.flexibleWidth = 0f;

    plus.onClick.AddListener(() =>
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      onPlusClicked?.Invoke();
    });

    Refresh(Wallet.Coins);
  }

  private void OnEnable()
  {
    Wallet.OnCoinsChanged += Refresh;
    Refresh(Wallet.Coins);
  }

  private void OnDisable()
  {
    Wallet.OnCoinsChanged -= Refresh;
  }

  private void Refresh(int coins)
  {
    if (amountLabel != null) amountLabel.text = coins.ToString();
  }
}
