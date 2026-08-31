using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A biome thumbnail: the environment's own sky-over-ground art, with its name
// on a banner tinted to that biome's accent colour.
//
// Everything here is placed on explicit anchors and opted out of the prefab's
// layout group. The prefab was authored for a much larger card and its own
// layout kept stretching the preview and the title across the whole screen.
public class EnvironmentCard : MonoBehaviour
{
  [SerializeField] private Image environmentImage;
  [SerializeField] private TextMeshProUGUI environmentTitle;
  [SerializeField] private Image lockIcon;

  public const float CardWidth = 380f;
  public const float CardHeight = 300f;
  private const float BannerHeight = 62f;
  private const float Inset = 9f;

  // Opts a child out of any layout group on the card, so the anchors set here
  // are what actually decide where it sits.
  private static void Unmanaged(RectTransform rect)
  {
    var element = rect.GetComponent<LayoutElement>();
    if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
    element.ignoreLayout = true;
  }

  // Bright biome accents need dark text on them; the darker ones need light.
  private static Color ReadableOn(Color background)
  {
    float luma = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
    return luma > 0.55f ? UiSkin.TextDark : UiSkin.TextPrimary;
  }

  public void Setup(string environmentKey, bool isLocked, int completed, int total)
  {
    var rect = (RectTransform)transform;
    rect.sizeDelta = new Vector2(CardWidth, CardHeight);

    var size = GetComponent<LayoutElement>();
    if (size == null) size = gameObject.AddComponent<LayoutElement>();
    size.minWidth = CardWidth;
    size.minHeight = CardHeight;
    size.preferredWidth = CardWidth;
    size.preferredHeight = CardHeight;
    size.flexibleWidth = 0f;
    size.flexibleHeight = 0f;

    Color accent = EnvironmentInfo.AccentFor(environmentKey);

    // NOTE: no SetAsFirstSibling here. This Image lives on the card root, so
    // reordering it reorders the *card* among its siblings — every card jumped
    // to the front as it was built, which is what once reversed the list into
    // 7..1. A root Image already draws behind its own children.
    var background = GetComponent<Image>();
    if (background == null) background = gameObject.AddComponent<Image>();
    UiSkin.Panel(background, isLocked ? UiSkin.PanelDark : UiSkin.PanelRaised, UiSkin.RadiusPanel);

    BuildArt(environmentKey, isLocked);
    BuildBanner(environmentKey, accent, isLocked);
    BuildProgress(completed, total, isLocked);
    BuildLock(isLocked);
    BuildFrame(accent, isLocked);
  }

  private void BuildArt(string environmentKey, bool isLocked)
  {
    if (environmentImage == null) return;

    Unmanaged(environmentImage.rectTransform);
    RectTransform art = environmentImage.rectTransform;
    art.anchorMin = new Vector2(0f, 0f);
    art.anchorMax = new Vector2(1f, 1f);
    art.pivot = new Vector2(0.5f, 0.5f);
    art.offsetMin = new Vector2(Inset, BannerHeight + Inset);
    art.offsetMax = new Vector2(-Inset, -Inset);

    environmentImage.sprite = EnvironmentInfo.CardArt(environmentKey);
    environmentImage.type = Image.Type.Simple;
    environmentImage.preserveAspect = false;
    // Locked biomes are dimmed rather than hidden: you can see where you are
    // heading, which is most of the reason to show them at all.
    environmentImage.color = isLocked ? new Color(0.40f, 0.42f, 0.50f, 1f) : Color.white;
    environmentImage.enabled = true;
  }

  private void BuildBanner(string environmentKey, Color accent, bool isLocked)
  {
    if (environmentTitle == null) return;

    // The banner is a sibling BEFORE the label, never its parent's child after
    // it: UI draws parent-then-children, so a backdrop added afterwards would
    // cover the text it is backing.
    var bannerGo = new GameObject("NameBanner", typeof(RectTransform));
    bannerGo.transform.SetParent(transform, false);
    var bannerRect = (RectTransform)bannerGo.transform;
    Unmanaged(bannerRect);
    bannerRect.anchorMin = new Vector2(0f, 0f);
    bannerRect.anchorMax = new Vector2(1f, 0f);
    bannerRect.pivot = new Vector2(0.5f, 0f);
    bannerRect.anchoredPosition = new Vector2(0f, Inset);
    bannerRect.sizeDelta = new Vector2(-Inset * 2f, BannerHeight - Inset);

    var banner = bannerGo.AddComponent<Image>();
    UiSkin.Panel(banner, isLocked ? UiSkin.Neutral : accent, UiSkin.RadiusButton);
    banner.raycastTarget = false;

    Unmanaged(environmentTitle.rectTransform);
    environmentTitle.transform.SetParent(bannerGo.transform, false);
    UiSkin.Stretch(environmentTitle.rectTransform);

    environmentTitle.text = EnvironmentInfo.DisplayName(environmentKey);
    UiSkin.Label(environmentTitle, UiSkin.Role.ButtonLabel,
      isLocked ? UiSkin.TextMuted : ReadableOn(accent));
    environmentTitle.alignment = TextAlignmentOptions.Center;
    // Long names like "Volcanic Ashlands" must shrink, not wrap, inside the
    // banner — TMP auto-size wraps by default and would spill two lines.
    environmentTitle.textWrappingMode = TextWrappingModes.NoWrap;
    environmentTitle.enableAutoSizing = true;
    environmentTitle.fontSizeMin = 18f;
    environmentTitle.fontSizeMax = 30f;
    environmentTitle.margin = new Vector4(10f, 0f, 10f, 0f);
    environmentTitle.outlineWidth = 0f;
    environmentTitle.raycastTarget = false;
  }

  // "4/10" on a dark chip in the corner of the art, so progress through a biome
  // is visible without opening it.
  private void BuildProgress(int completed, int total, bool isLocked)
  {
    if (isLocked || total <= 0) return;

    var chipGo = new GameObject("Progress", typeof(RectTransform));
    chipGo.transform.SetParent(transform, false);
    var chip = (RectTransform)chipGo.transform;
    Unmanaged(chip);
    chip.anchorMin = new Vector2(1f, 1f);
    chip.anchorMax = new Vector2(1f, 1f);
    chip.pivot = new Vector2(1f, 1f);
    chip.anchoredPosition = new Vector2(-Inset - 8f, -Inset - 8f);
    chip.sizeDelta = new Vector2(96f, 40f);

    var bg = chipGo.AddComponent<Image>();
    UiSkin.Panel(bg, new Color(0.05f, 0.06f, 0.10f, 0.78f), UiSkin.RadiusChip);
    bg.raycastTarget = false;

    Image star = UiSkin.Icon(chipGo.transform, StarSprite.Star, UiSkin.Gold, 22f);
    var starRect = (RectTransform)star.transform;
    Unmanaged(starRect);
    starRect.anchorMin = new Vector2(0f, 0.5f);
    starRect.anchorMax = new Vector2(0f, 0.5f);
    starRect.pivot = new Vector2(0f, 0.5f);
    starRect.anchoredPosition = new Vector2(9f, 0f);

    var labelGo = new GameObject("Label", typeof(RectTransform));
    labelGo.transform.SetParent(chipGo.transform, false);
    var label = labelGo.AddComponent<TextMeshProUGUI>();
    var labelRect = (RectTransform)labelGo.transform;
    Unmanaged(labelRect);
    labelRect.anchorMin = new Vector2(0f, 0f);
    labelRect.anchorMax = new Vector2(1f, 1f);
    labelRect.offsetMin = new Vector2(32f, 0f);
    labelRect.offsetMax = new Vector2(-8f, 0f);

    UiSkin.Label(label, UiSkin.Role.Caption, UiSkin.TextPrimary);
    label.text = $"{completed}/{total}";
    label.alignment = TextAlignmentOptions.Center;
    label.textWrappingMode = TextWrappingModes.NoWrap;
    label.raycastTarget = false;
  }

  private void BuildLock(bool isLocked)
  {
    if (lockIcon == null) return;

    lockIcon.gameObject.SetActive(isLocked);
    if (!isLocked) return;

    lockIcon.sprite = UiSprites.Lock();
    lockIcon.color = UiSkin.TextPrimary;
    lockIcon.preserveAspect = true;

    // The prefab stretches this to fill the card; a badge reads better, and it
    // has to opt out of the layout group or the group resizes it straight back.
    var badge = lockIcon.rectTransform;
    Unmanaged(badge);
    badge.anchorMin = new Vector2(0.5f, 1f);
    badge.anchorMax = new Vector2(0.5f, 1f);
    badge.pivot = new Vector2(0.5f, 1f);
    badge.anchoredPosition = new Vector2(0f, -(CardHeight - BannerHeight) * 0.5f + 34f);
    badge.sizeDelta = new Vector2(92f, 92f);
    lockIcon.transform.SetAsLastSibling();
  }

  private void BuildFrame(Color accent, bool isLocked)
  {
    // Neon rim behind the card. Unlocked biomes glow in their own colour, which
    // is what separates "somewhere you can go" from the flat grey locked ones
    // at a glance, without needing to read the padlock.
    if (!isLocked)
    {
      var glowGo = new GameObject("Glow", typeof(RectTransform));
      glowGo.transform.SetParent(transform, false);
      var glowRect = (RectTransform)glowGo.transform;
      UiSkin.Stretch(glowRect);
      glowRect.offsetMin = new Vector2(-22f, -22f);
      glowRect.offsetMax = new Vector2(22f, 22f);
      var glow = glowGo.AddComponent<Image>();
      glow.sprite = UiSprites.Glow();
      glow.type = Image.Type.Sliced;
      glow.pixelsPerUnitMultiplier = 1f;
      glow.color = new Color(accent.r, accent.g, accent.b, 0.42f);
      glow.raycastTarget = false;
      glowGo.AddComponent<LayoutElement>().ignoreLayout = true;
      // Behind the card's own background, which lives on the root.
      glowGo.transform.SetAsFirstSibling();
    }

    Image border = UiSkin.AddBorder((RectTransform)transform, UiSkin.RadiusPanel, 3f);
    if (border != null)
    {
      border.color = isLocked
        ? UiSkin.PanelBorder
        : new Color(accent.r, accent.g, accent.b, 1f);
      border.transform.SetAsLastSibling();
    }
  }
}
