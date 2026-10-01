using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

// Selection-only styling. Does not change shared HUD/modal themes or prefabs.
public sealed class SelectionScreenView
{
    public readonly RectTransform Root;
    public readonly TMP_Text Title;
    public readonly TMP_Text Progress;
    readonly RawImage background;
    public readonly RectTransform ProgressBadge;
    Texture2D gradient;
    public static readonly Color Ink = new Color(.10f, .16f, .15f);
    public static readonly Color Paper = new Color(.96f, .95f, .88f);
    public static readonly Color Muted = new Color(.76f, .82f, .81f);

    public SelectionScreenView(Transform screen, string title, UnityAction back)
    {
        // Preserve serialized children and references, but retire their layout.
        // The existing screen components still own navigation and progression.
        foreach (Transform child in screen) child.gameObject.SetActive(false);
        var bg = Rect("SelectionBackdrop", screen, Vector2.zero, Vector2.one);
        background = bg.gameObject.AddComponent<RawImage>();
        background.raycastTarget = true;
        Root = Rect("SelectionContent", screen, Vector2.zero, Vector2.one);
        Root.gameObject.AddComponent<SafeArea>();
        if(Application.isPlaying) screen.gameObject.AddComponent<SelectionMenuBudget>();
        var backRect = Rect("Back", Root, new Vector2(.025f, .88f), new Vector2(.13f, .975f));
        ButtonIcon(Button(backRect, "BACK", UiSkin.Neutral, back), UiSprites.Chevron(), -90);
        Title = Label(Rect("Title", Root, new Vector2(.16f, .89f), new Vector2(.79f, .98f)), title, 32, true);
        ProgressBadge = Rect("StarsBadge", Root, new Vector2(.79f, .90f), new Vector2(.975f, .975f));
        UiSkin.Panel(ProgressBadge.gameObject.AddComponent<Image>(), UiSkin.PanelDark);
        Icon(Rect("Star", ProgressBadge, new Vector2(.07f,.2f),new Vector2(.25f,.8f)), StarSprite.Star, UiSkin.Gold);
        Progress = Label(Rect("Progress", ProgressBadge, new Vector2(.3f,0),new Vector2(.95f,1)), "", 20);
        Progress.alignment = TextAlignmentOptions.Center;
    }
    public void Theme(string environment)
    {
        var p = EnvironmentTheme.PaletteFor(environment);
        if (gradient == null)
        {
            gradient = new Texture2D(1, 128, TextureFormat.RGBA32, false);
            gradient.wrapMode = TextureWrapMode.Clamp;
            background.texture = gradient;
        }
        for (int y = 0; y < 128; y++)
        {
            float t = y / 127f;
            Color c = t < .45f ? Color.Lerp(p.skyBottom, p.skyHorizon, t / .45f)
              : Color.Lerp(p.skyHorizon, p.skyTop, (t - .45f) / .55f);
            // Calm, readable header/footer while retaining each palette's identity.
            c = Color.Lerp(c, new Color(.035f, .065f, .075f), .30f);
            c.a = 1;
            gradient.SetPixel(0, y, c);
        }
        gradient.Apply(false, false);
    }
    public void Dispose()
    {
        if (gradient == null) return;
        if (Application.isPlaying) Object.Destroy(gradient); else Object.DestroyImmediate(gradient);
    }
    public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = min; r.anchorMax = max;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }
    public static TMP_Text Label(RectTransform rect, string text, float size, bool display = false)
    {
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        UiFont.Apply(label, display);
        label.text = text;
        label.color = Paper;
        label.fontSize = size;
        label.enableAutoSizing = true;
        label.fontSizeMin = size * .7f;
        label.fontSizeMax = size;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }
    public static Button Button(RectTransform rect, string text, Color fill, UnityAction action)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        UiSkin.Panel(image, fill, 18);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var label = Label(Rect("Label", rect, new Vector2(.08f, .06f), new Vector2(.92f, .94f)), text, 24, true);
        label.color = Ink;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        UiSkin.StyleButton(button, fill);
        label.fontSizeMin = 15; label.fontSizeMax = 24;
        label.margin = Vector4.zero;
        if (action != null) button.onClick.AddListener(action);
        return button;
    }
    public static Image Icon(RectTransform rect, Sprite sprite, Color tint)
    {
        var icon = rect.gameObject.AddComponent<Image>();
        icon.sprite = sprite; icon.color = tint; icon.preserveAspect = true; icon.raycastTarget = false;
        return icon;
    }
    // An icon-only button keeps its anchored glyph: there is no word to pair it
    // with, so it is simply centred on the plate.
    //
    // A button that has BOTH pins the glyph to the left quarter and centres the
    // text in what is left, which reads as two separate elements at opposite
    // ends of a wide button rather than as one label - the EXPLORE and PLAY
    // LEVEL buttons were the worst of it. UiSkin lays the pair out as one
    // centred unit, and owns that rule for every icon button in the game.
    public static void ButtonIcon(Button button, Sprite sprite, float rotation = 0, bool only = false)
    {
        if (!only)
        {
            Image glyph = UiSkin.AddButtonIcon(button, sprite, null, 34f);
            if (glyph != null) glyph.rectTransform.localRotation = Quaternion.Euler(0, 0, rotation);
            return;
        }
        var existing = button.transform.Find("Icon");
        var rect = existing != null ? (RectTransform)existing : Rect("Icon", button.transform, Vector2.zero, Vector2.one);
        rect.anchorMin = new Vector2(.3f,.24f);
        rect.anchorMax = new Vector2(.7f,.76f);
        var label = button.GetComponentInChildren<TMP_Text>();
        label.rectTransform.anchorMin = new Vector2(0,.06f);
        label.rectTransform.anchorMax = new Vector2(.94f,.94f);
        label.text = "";
        var icon = rect.GetComponent<Image>();
        if(icon == null) icon = Icon(rect, sprite, label.color);
        icon.sprite = sprite; icon.color = label.color;
        rect.localRotation = Quaternion.Euler(0,0,rotation);
    }
    public void MapTexture(string environment)
    {
        var p = EnvironmentTheme.PaletteFor(environment);
        var ground = Rect("MapGround", background.transform, Vector2.zero,Vector2.one).gameObject.AddComponent<RawImage>();
        ground.texture = EnvironmentTheme.ResolveGround(p.ground);
        ground.uvRect = new Rect(0,0,3,2);
        ground.color = Color.Lerp(p.groundTint, Color.white, .3f) * .65f;
        ground.color = new Color(ground.color.r,ground.color.g,ground.color.b,1);
        ground.raycastTarget = false;
    }
    public static void ButtonText(Button button, string text) => button.GetComponentInChildren<TMP_Text>().text = text;
    public static SelectionIslandPreview Island(Transform parent, Vector2 min, Vector2 max)
    {
        var frame = Rect("IslandFrame", parent, min, max);
        var image = Rect("Island", frame, Vector2.zero, Vector2.one);
        var fit = image.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = 1.6f;
        return SelectionIslandPreview.Create(image);
    }
}
