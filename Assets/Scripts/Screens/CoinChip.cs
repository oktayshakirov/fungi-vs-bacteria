using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The coin balance shown in the top-left of the main menu.
//
// A passive readout and nothing else. It used to carry a "+" button that
// opened the store, which asked the player to read a plus sign as "shop":
// the labelled GET COINS pill under it now says so in words, so the chip is
// back to doing one job.
//
// Built in code rather than authored into the menu prefab, for the same reason
// the rest of the menu is: DisplaySetup/MenuLayout own the menu's layout, and a
// scene-authored chip would drift from them. It subscribes to Wallet so the
// number updates the moment an ad pays out, without the screen polling.
public class CoinChip : MonoBehaviour
{
  private TMP_Text amountLabel;

  public static CoinChip Create(Transform parent)
  {
    var go = new GameObject("CoinChip", typeof(RectTransform));
    go.transform.SetParent(parent, false);

    var rect = (RectTransform)go.transform;
    rect.anchorMin = new Vector2(0f, 1f);
    rect.anchorMax = new Vector2(0f, 1f);
    rect.pivot = new Vector2(0f, 1f);
    // Matches the settings gear's inset on the opposite corner.
    rect.anchoredPosition = new Vector2(MenuLayout.CornerInset, -MenuLayout.CornerInset);
    // Narrower than the pills below it now that the "+" is gone: a chip
    // padded out to 230 with empty space read as a button missing its label.
    rect.sizeDelta = new Vector2(190f, 76f);

    var chip = go.AddComponent<CoinChip>();
    chip.Build();
    return chip;
  }

  private void Build()
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
