using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A level tile: a chunky rounded square with a big number, and its earned stars
// on a dark pill tucked under the bottom edge.
//
// The tile is tinted with the environment's own accent colour so the level grid
// reads as part of the biome you picked. Depth comes from four cheap layers
// rather than from art: a dropped shadow, a darker plate offset downward to
// fake a thick edge, the domed button sprite on top, and a gloss cap over the
// upper half.
public class LevelCard : MonoBehaviour
{
  [SerializeField] private TextMeshProUGUI levelText;
  [SerializeField] private Button button;

  private System.Action<int> onCardClicked;
  private int levelNumber;
  private RectTransform starsRow;
  private RectTransform lockBadge;

  // Set once per screen by LevelSelectionScreen before any card is built, from
  // the width the canvas actually has. Five 170-wide tiles plus their gaps come
  // to more than a 4:3 tablet's 960 canvas units, so a fixed size cannot hold
  // one row of five on every device.
  //
  // Static because the tile is built out of five absolutely-placed layers that
  // all measure off it, and every card on screen is the same size by
  // definition - there is never a second level grid at a second scale.
  public static float TileSize { get; private set; } = MaxTileSize;
  public const float MaxTileSize = 170f;
  private const float MinTileSize = 104f;

  // The star pill straddles the tile's bottom edge, so the cell needs only a
  // little extra height beneath it.
  public static float CellHeight => TileSize + 42f;
  private static float EdgeDepth => Mathf.Round(TileSize * 0.041f) + 1f;

  public static void SetTileSize(float size)
  {
    TileSize = Mathf.Clamp(size, MinTileSize, MaxTileSize);
  }

  // The card prefab has a VerticalLayoutGroup, which would treat these
  // decorations as content and push them out of the bottom of the card once the
  // level grid shrinks the cell. Opting out keeps them on their anchors.
  private static void Detach(RectTransform rect)
  {
    var element = rect.GetComponent<LayoutElement>();
    if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();
    element.ignoreLayout = true;
  }

  private static Color ReadableOn(Color background)
  {
    float luma = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
    return luma > 0.55f ? UiSkin.TextDark : UiSkin.TextPrimary;
  }

  private static RectTransform Layer(string name, Transform parent, Vector2 size,
    float yOffset)
  {
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    var rect = (RectTransform)go.transform;
    Detach(rect);
    rect.anchorMin = new Vector2(0.5f, 1f);
    rect.anchorMax = new Vector2(0.5f, 1f);
    rect.pivot = new Vector2(0.5f, 1f);
    rect.anchoredPosition = new Vector2(0f, yOffset);
    rect.sizeDelta = size;
    return rect;
  }

  public void Setup(int level, bool isLocked, int stars, System.Action<int> callback)
  {
    Setup(level, isLocked, stars, UiSkin.Primary, false, callback);
  }

  public void Setup(int level, bool isLocked, int stars, Color accent, bool isNext,
    System.Action<int> callback)
  {
    levelNumber = level;
    onCardClicked = callback;

    // The prefab's VerticalLayoutGroup stacked the number and the star row,
    // which overflows once the grid shrinks the cell. Everything on the card is
    // placed on its own anchors instead.
    var stack = GetComponent<VerticalLayoutGroup>();
    if (stack != null) stack.enabled = false;

    var rect = (RectTransform)transform;
    rect.sizeDelta = new Vector2(TileSize, CellHeight);

    RectTransform face = BuildFace(isLocked, accent, isNext);

    if (levelText != null)
    {
      levelText.transform.SetParent(face, false);
      levelText.text = level.ToString();
      UiSkin.Label(levelText, UiSkin.Role.Title,
        isLocked ? UiSkin.TextMuted : ReadableOn(accent));
      levelText.alignment = TextAlignmentOptions.Center;
      levelText.textWrappingMode = TextWrappingModes.NoWrap;
      levelText.raycastTarget = false;

      // Shown on LOCKED tiles too. A padlock alone says "not yet" but not
      // "not yet WHAT" - with the numbers hidden, the grid gave no sense of
      // how far the biome runs or how close the next one is. Locked tiles
      // carry the number in muted grey with the padlock demoted to a small
      // badge in the corner, so the two states stay instantly distinguishable
      // by colour and depth rather than by whether anything is written on them.
      levelText.gameObject.SetActive(true);
      levelText.enableAutoSizing = true;
      levelText.fontSizeMin = 24f;
      levelText.fontSizeMax = 68f;
      // A soft dark edge keeps the numeral legible on the brighter biomes
      // (ice blue, blossom pink) without darkening the tile itself.
      levelText.outlineWidth = 0.14f;
      levelText.outlineColor = new Color32(8, 10, 16, 150);

      RectTransform numberRect = levelText.rectTransform;
      Detach(numberRect);
      UiSkin.Stretch(numberRect);
      numberRect.offsetMin = new Vector2(0f, 6f);   // optically centred above the stars
    }

    ShowLock(isLocked, face);
    ShowStars(isLocked ? -1 : stars);

    if (button != null)
    {
      button.onClick.RemoveAllListeners();
      button.interactable = !isLocked;
      if (!isLocked)
      {
        button.onClick.AddListener(() => onCardClicked?.Invoke(levelNumber));
      }
    }
  }

  private RectTransform BuildFace(bool isLocked, Color accent, bool isNext)
  {
    // Reuse the root Image as an invisible hit area covering the whole cell.
    var root = GetComponent<Image>();
    if (root == null) root = gameObject.AddComponent<Image>();
    root.sprite = null;
    root.color = new Color(0f, 0f, 0f, 0f);
    if (button != null) button.targetGraphic = root;

    Transform stale = transform.Find("Halo");
    if (stale != null) DestroyImmediate(stale.gameObject);
    stale = transform.Find("Shadow");
    if (stale != null) DestroyImmediate(stale.gameObject);
    stale = transform.Find("Edge");
    if (stale != null) DestroyImmediate(stale.gameObject);
    stale = transform.Find("Face");
    if (stale != null) DestroyImmediate(stale.gameObject);

    // 0. Neon halo, on the next playable level only. Glow is used here as a
    // CUE, not decoration: exactly one tile on the screen carries it, so the
    // eye lands on where you left off without reading a single number.
    if (isNext)
    {
      RectTransform halo = Layer("Halo", transform,
        new Vector2(TileSize + 64f, TileSize + 64f), -EdgeDepth + 26f);
      var haloImage = halo.gameObject.AddComponent<Image>();
      haloImage.sprite = UiSprites.Glow();
      haloImage.type = Image.Type.Sliced;
      haloImage.pixelsPerUnitMultiplier = 1f;
      haloImage.color = new Color(accent.r, accent.g, accent.b, 0.85f);
      haloImage.raycastTarget = false;
      UiPulse.Attach(haloImage, 0.34f, 0.85f, 2.1f);
    }

    // 1. Dropped shadow, slightly larger and pushed down.
    RectTransform shadow = Layer("Shadow", transform,
      new Vector2(TileSize + 14f, TileSize + 14f), -EdgeDepth + 2f);
    var shadowImage = shadow.gameObject.AddComponent<Image>();
    shadowImage.sprite = UiSprites.Shadow();
    shadowImage.type = Image.Type.Sliced;
    shadowImage.pixelsPerUnitMultiplier = 1f;
    shadowImage.color = new Color(0f, 0f, 0f, isLocked ? 0.22f : 0.34f);
    shadowImage.raycastTarget = false;

    // 2. Darker plate offset down, which reads as the thickness of the tile.
    RectTransform edge = Layer("Edge", transform, new Vector2(TileSize, TileSize), -EdgeDepth);
    var edgeImage = edge.gameObject.AddComponent<Image>();
    Color edgeColor = Color.Lerp(accent, Color.black, isLocked ? 0.62f : 0.42f);
    UiSkin.Panel(edgeImage, edgeColor, UiSkin.RadiusPanel);
    edgeImage.raycastTarget = false;

    // 3. The face itself, on the domed button sprite.
    RectTransform face = Layer("Face", transform, new Vector2(TileSize, TileSize), 0f);
    var image = face.gameObject.AddComponent<Image>();
    Color fill = isLocked ? Color.Lerp(accent, UiSkin.PanelDark, 0.68f) : accent;
    image.sprite = UiSprites.Button(UiSkin.RadiusPanel);
    image.type = Image.Type.Sliced;
    image.pixelsPerUnitMultiplier = 1f;
    image.color = fill;
    image.raycastTarget = false;

    // 4. Gloss over the top half, so the tile catches light.
    if (!isLocked)
    {
      var glossGo = new GameObject("Gloss", typeof(RectTransform));
      glossGo.transform.SetParent(face, false);
      var gloss = (RectTransform)glossGo.transform;
      Detach(gloss);
      gloss.anchorMin = new Vector2(0f, 0.52f);
      gloss.anchorMax = new Vector2(1f, 1f);
      gloss.offsetMin = new Vector2(7f, 0f);
      gloss.offsetMax = new Vector2(-7f, -7f);
      var glossImage = glossGo.AddComponent<Image>();
      UiSkin.Panel(glossImage, new Color(1f, 1f, 1f, 0.20f), UiSkin.RadiusButton);
      glossImage.raycastTarget = false;
    }

    // The next level you can actually play gets a bright ring, so the eye lands
    // on it immediately instead of hunting the grid for where you left off.
    Image border = UiSkin.AddBorder(face, UiSkin.RadiusPanel, isNext ? 5f : 3f);
    if (border != null)
    {
      border.color = isLocked
        ? new Color(0f, 0f, 0f, 0.30f)
        : (isNext ? Color.white : new Color(1f, 1f, 1f, 0.36f));
    }

    return face;
  }

  // A padlock over locked tiles, so "locked" reads without relying on the
  // greyed-out button alone.
  private void ShowLock(bool isLocked, RectTransform face)
  {
    if (lockBadge != null) Destroy(lockBadge.gameObject);
    if (!isLocked) return;

    // Tucked into the corner rather than centred on the tile: the level number
    // now occupies the middle of a locked tile as well, and a padlock over it
    // read as a rendering fault rather than as a state.
    float badgeSize = Mathf.Round(TileSize * 0.26f);
    Image padlock = UiSkin.Icon(face, UiSprites.Lock(), new Color(1f, 1f, 1f, 0.62f), badgeSize);
    lockBadge = (RectTransform)padlock.transform;
    Detach(lockBadge);
    lockBadge.anchorMin = new Vector2(1f, 1f);
    lockBadge.anchorMax = new Vector2(1f, 1f);
    lockBadge.pivot = new Vector2(1f, 1f);
    lockBadge.anchoredPosition = new Vector2(-8f, -8f);
  }

  // stars < 0 means locked (no row); 0..3 shows filled/empty stars on a dark
  // pill straddling the tile's bottom edge, the way the reference layouts do it.
  private void ShowStars(int stars)
  {
    if (starsRow != null) Destroy(starsRow.gameObject);
    if (stars < 0) return;

    var pillGo = new GameObject("Stars", typeof(RectTransform));
    pillGo.transform.SetParent(transform, false);
    starsRow = (RectTransform)pillGo.transform;
    Detach(starsRow);
    starsRow.anchorMin = new Vector2(0.5f, 1f);
    starsRow.anchorMax = new Vector2(0.5f, 1f);
    starsRow.pivot = new Vector2(0.5f, 0.5f);
    // Overlaps the tile's bottom edge only slightly. Centring the pill ON the
    // edge put the stars themselves behind the tile face, which read as though
    // they had been clipped in half.
    starsRow.anchoredPosition = new Vector2(0f, -(TileSize + EdgeDepth + 9f));
    starsRow.sizeDelta = new Vector2(TileSize - 30f, 38f);

    var pill = pillGo.AddComponent<Image>();
    UiSkin.Panel(pill, new Color(0.06f, 0.07f, 0.11f, 0.88f), UiSkin.RadiusChip);
    pill.raycastTarget = false;

    var rowGo = new GameObject("Row", typeof(RectTransform));
    rowGo.transform.SetParent(pillGo.transform, false);
    var row = (RectTransform)rowGo.transform;
    Detach(row);
    UiSkin.Stretch(row);

    // Stars are sprites, never glyphs: the TMP atlases in this project are
    // static and ASCII-only, so a star character renders as a missing box.
    StarSprite.BuildRow(row, Mathf.Clamp(stars, 0, 3), 27f);
  }
}
