using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelSelectionScreen : MonoBehaviour
{
    [SerializeField] private GameObject levelCardPrefab;
    [SerializeField] private Transform cardsContainer;
    [SerializeField] private Button backButton;
    SelectionScreenView view;
    List<LevelConfig> levels;
    RectTransform route;
    TMP_Text selection, description, pageLabel, status;
    RectTransform detailStars;
    Button play, previous, next;
    int selectedIndex, page, columns, nextIndex;
    float lastWidth;
    readonly Dictionary<int,Button> levelButtons = new Dictionary<int,Button>();
    RectTransform selectionRing, currentBase;
    SelectionIslandPreview basePreview, sceneryPreview;
    readonly List<GameObject> nodes = new List<GameObject>();
    public int PageSize => columns * 3;

    void Start()
    {
        if (view != null) return;
        levels = LevelRepository.GetLevelsForEnvironment(GameSession.SelectedEnvironment);
        view = new SelectionScreenView(transform, EnvironmentInfo.DisplayName(GameSession.SelectedEnvironment).ToUpperInvariant(), OnBack);
        view.Theme(GameSession.SelectedEnvironment);
        view.MapTexture(GameSession.SelectedEnvironment);
        // A quiet veil keeps the live ground texture from competing with the route.
        var veil = SelectionScreenView.Rect("MapVeil", transform.Find("SelectionBackdrop"), Vector2.zero, Vector2.one);
        var veilImage = veil.gameObject.AddComponent<Image>();
        veilImage.color = new Color(.025f, .065f, .075f, .48f);
        veilImage.raycastTarget = false;
        view.Title.rectTransform.anchorMax = new Vector2(.70f, .98f);
        view.ProgressBadge.anchorMin = new Vector2(.70f, .90f);
        view.ProgressBadge.anchorMax = new Vector2(.87f, .975f);
        SelectionScreenView.ButtonIcon(SelectionScreenView.Button(SelectionScreenView.Rect("Home", view.Root, new Vector2(.89f, .89f), new Vector2(.975f, .975f)), "HOME", UiSkin.Neutral, OnHome), UiSprites.Home(), 0, true);
        int stars = 0;
        foreach (var level in levels) stars += Mathf.Clamp(LevelProgress.GetStars(level.environmentName, level.levelNumber), 0, 3);
        view.Progress.text = $"{stars} / {levels.Count * 3}";
        var heading = SelectionScreenView.Label(SelectionScreenView.Rect("JourneyHeading", view.Root,
            new Vector2(.05f,.79f), new Vector2(.65f,.85f)), "CHOOSE YOUR NEXT LEVEL", 18, true);
        heading.color = SelectionScreenView.Muted;
        route = SelectionScreenView.Rect("LevelTrail", view.Root, new Vector2(.035f, .12f), new Vector2(.68f, .80f));
        var card = SelectionScreenView.Rect("LevelDetails", view.Root, new Vector2(.715f, .16f), new Vector2(.975f, .81f));
        UiSkin.Panel(card.gameObject.AddComponent<Image>(), new Color(.075f, .12f, .13f, .96f), 28);
        status = SelectionScreenView.Label(SelectionScreenView.Rect("Status", card, new Vector2(.08f,.87f), new Vector2(.92f,.96f)), "", 14, true);
        status.alignment = TextAlignmentOptions.Center;
        currentBase = SelectionScreenView.Rect("CurrentLevelBase", card, new Vector2(.5f,.67f), new Vector2(.5f,.67f));
        currentBase.sizeDelta = new Vector2(132,132);
        basePreview = SelectionIslandPreview.Create(currentBase);
        selection = SelectionScreenView.Label(SelectionScreenView.Rect("SelectedLevel", card, new Vector2(.08f,.43f), new Vector2(.92f,.55f)), "", 32, true);
        selection.alignment = TextAlignmentOptions.Center;
        detailStars = SelectionScreenView.Rect("BestStars", card, new Vector2(.5f,.38f), new Vector2(.5f,.38f));
        description = SelectionScreenView.Label(SelectionScreenView.Rect("Description", card, new Vector2(.09f,.22f), new Vector2(.91f,.31f)), "", 17);
        description.alignment = TextAlignmentOptions.Center;
        play = SelectionScreenView.Button(SelectionScreenView.Rect("Play", card, new Vector2(.08f,.055f), new Vector2(.92f,.19f)), "PLAY", UiSkin.Primary, Play);
        previous = SelectionScreenView.Button(SelectionScreenView.Rect("PreviousPage", view.Root, new Vector2(.25f,.025f), new Vector2(.30f,.095f)), "", UiSkin.Neutral, () => ChangePage(-1));
        next = SelectionScreenView.Button(SelectionScreenView.Rect("NextPage", view.Root, new Vector2(.42f,.025f), new Vector2(.47f,.095f)), "", UiSkin.Neutral, () => ChangePage(1));
        EnvironmentsScreen.Arrow(previous, false); EnvironmentsScreen.Arrow(next, true);
        pageLabel = SelectionScreenView.Label(SelectionScreenView.Rect("Page", view.Root, new Vector2(.30f,.025f), new Vector2(.42f,.095f)), "", 16);
        pageLabel.alignment = TextAlignmentOptions.Center;
        selectedIndex = levels.FindIndex(l => LevelProgress.IsLevelUnlocked(l.environmentName, l.levelNumber) && l.levelNumber > LevelProgress.GetHighestCompletedLevel(l.environmentName));
        nextIndex = selectedIndex < 0 ? Mathf.Max(0,levels.Count-1) : selectedIndex;
        if (selectedIndex < 0) selectedIndex = Mathf.Max(0, levels.Count - 1);
        Relayout();
        Select(selectedIndex);
        ScreenFade.In(transform);
    }
    void Update()
    {
        if (view == null) return;
        float width = ScreenTheme.LayoutWidth(view.Root);
        if (Mathf.Abs(width - lastWidth) > 1f) Relayout();
    }
    void Relayout()
    {
        lastWidth = ScreenTheme.LayoutWidth(view.Root);
        columns = SelectionRouteLayout.Columns(lastWidth);
        page = SelectionRouteLayout.PageForIndex(selectedIndex, PageSize);
        BuildRoute();
    }
    void ChangePage(int direction)
    {
        int count = SelectionRouteLayout.PageCount(levels.Count, PageSize);
        page = Mathf.Clamp(page + direction, 0, count - 1);
        selectedIndex = page * PageSize;
        BuildRoute(); Select(selectedIndex);
    }
    void BuildRoute()
    {
        foreach (GameObject go in nodes)
        {
            go.SetActive(false);
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }
        nodes.Clear();
        int start = page * PageSize, count = SelectionRouteLayout.VisibleCount(levels.Count, page, PageSize);
        levelButtons.Clear();
        selectionRing=null;
        var positions = new List<Vector2>();
        for (int slot = 0; slot < count; slot++)
        {
            SelectionRouteLayout.Position(slot, columns, out float x, out float y, page * 31 + EnvironmentInfo.DisplayName(GameSession.SelectedEnvironment).Length * 17, count);
            positions.Add(new Vector2(x, y));
        }
        var palette = EnvironmentTheme.PaletteFor(GameSession.SelectedEnvironment);
        for(int layer=0;layer<2;layer++)
        {
            var r = SelectionScreenView.Rect(layer==0 ? "TrailEdge" : "TrailSurface", route, Vector2.zero, Vector2.one);
            var trail = r.gameObject.AddComponent<SelectionTrailGraphic>();
            trail.points=positions;
            trail.width=layer==0 ? 22 : 16;
            Color sand = Color.Lerp(palette.pathColor, new Color(.76f,.73f,.57f), .65f);
            trail.color=layer==0 ? new Color(sand.r,sand.g,sand.b,.12f) : new Color(sand.r,sand.g,sand.b,.64f);
            if(layer==1)
            {
                int completed=LevelProgress.GetHighestCompletedLevel(GameSession.SelectedEnvironment);
                int traversed=0;
                for(int i=0;i<count-1;i++) if(levels[start+i].levelNumber<=completed) traversed++;
                trail.completedSegments=traversed;
                trail.completedColor=new Color(.75f,.81f,.46f);
            }
            trail.texture=PathSurface.Grain;
            trail.raycastTarget=false;
            trail.SetAllDirty();
            nodes.Add(r.gameObject);
        }
        ScatterScenery(positions);
        for (int slot = 0; slot < count; slot++)
        {
            int index = start + slot; LevelConfig level = levels[index];
            var r = SelectionScreenView.Rect("Level_" + level.levelNumber, route, positions[slot], positions[slot]);
            r.sizeDelta = new Vector2(72, 72);
            bool unlocked = LevelProgress.IsLevelUnlocked(level.environmentName, level.levelNumber);
            Color fill = index == selectedIndex ? UiSkin.Primary : UiSkin.Neutral;
            if (!unlocked) fill = UiSkin.PanelRaised;
            fill.a=1;
            Button b = SelectionScreenView.Button(r, level.levelNumber.ToString(), fill, () => Select(index));
            var plate = b.GetComponent<Image>();
            plate.sprite = UiSprites.Circle(128); plate.type = Image.Type.Simple;
            var rim = SelectionScreenView.Rect("Rim", r, Vector2.zero, Vector2.one);
            rim.offsetMin = new Vector2(-4,-4); rim.offsetMax = new Vector2(4,4);
            SelectionScreenView.Icon(rim, UiSprites.Circle(128), new Color(.79f,.81f,.67f,.22f));
            rim.SetAsFirstSibling();
            // Rim sits behind an inset disc; all artwork is cached UI geometry.
            var inset = SelectionScreenView.Rect("Face", rim, Vector2.zero, Vector2.one);
            inset.offsetMin = new Vector2(4,4); inset.offsetMax = new Vector2(-4,-4);
            SelectionScreenView.Icon(inset, UiSprites.Circle(128), fill);
            levelButtons[index]=b;
            int earned = Mathf.Clamp(LevelProgress.GetStars(level.environmentName, level.levelNumber), 0, 3);
            if(unlocked)
            {
                var stars = SelectionScreenView.Rect("Stars", r, new Vector2(.5f,-.22f),new Vector2(.5f,-.22f));
                StarSprite.BuildRow(stars,earned,16);
            }
            if (!unlocked)
            {
                var badge = SelectionScreenView.Rect("Lock", r, new Vector2(.72f, .0f), new Vector2(.94f, .22f));
                var icon = badge.gameObject.AddComponent<Image>();
                icon.sprite = UiSprites.Lock(); icon.color = SelectionScreenView.Paper; icon.raycastTarget = false;
            }
            nodes.Add(r.gameObject);
        }
        if(levels.Count > 0) basePreview.ShowBase(levels[nextIndex]);
        UpdateSelection();
        int pages = SelectionRouteLayout.PageCount(levels.Count, PageSize);
        previous.gameObject.SetActive(pages > 1);
        next.gameObject.SetActive(pages > 1);
        pageLabel.gameObject.SetActive(pages > 1);
        pageLabel.text = $"{page + 1}/{pages}";
        previous.interactable = page > 0; next.interactable = page < pages - 1;
    }
    void UpdateSelection()
    {
        foreach(var entry in levelButtons)
        {
            var level=levels[entry.Key];
            bool unlocked=LevelProgress.IsLevelUnlocked(level.environmentName,level.levelNumber);
            Color fill=entry.Key==selectedIndex ? (unlocked ? UiSkin.Primary : UiSkin.Neutral) : (unlocked ? new Color(.36f,.44f,.29f) : new Color(.19f,.26f,.23f));
            fill.a=1;
            entry.Value.GetComponent<Image>().color=fill;
            entry.Value.transform.Find("Rim/Face").GetComponent<Image>().color=fill;
            entry.Value.GetComponentInChildren<TMP_Text>().color=unlocked && entry.Key==selectedIndex ? UiSkin.TextDark : (unlocked ? UiSkin.TextPrimary : new Color(.73f,.78f,.71f));
        }
        if(!levelButtons.TryGetValue(selectedIndex,out var selected)) return;
        if(selectionRing==null)
        {
            var ring=UiSkin.AddBorder((RectTransform)selected.transform,48,2);
            ring.color=SelectionScreenView.Paper; selectionRing=ring.rectTransform;
        }
        selectionRing.SetParent(selected.transform,false);
        selectionRing.anchorMin=Vector2.zero; selectionRing.anchorMax=Vector2.one;
        selectionRing.offsetMin=new Vector2(-8,-8); selectionRing.offsetMax=new Vector2(8,8);
    }
    void ScatterScenery(List<Vector2> positions)
    {
        if(levels.Count==0) return;
        if(sceneryPreview==null)
        {
            var source=SelectionScreenView.Rect("ScenerySource",route,Vector2.zero,Vector2.zero);
            sceneryPreview=SelectionIslandPreview.Create(source);
        }
        sceneryPreview.ShowScenery(levels[0]);
        var sourceImage=sceneryPreview.GetComponent<RawImage>();
        sourceImage.enabled=false;
        Canvas.ForceUpdateCanvases();
        Vector2 size=route.rect.size;
        var rng=new System.Random(page*31+GameSession.SelectedEnvironment.Length*71+columns);
        var placed=new List<Vector2>();
        for(int attempt=0;attempt<120 && placed.Count<3;attempt++)
        {
            Vector2 p=new Vector2(.04f+(float)rng.NextDouble()*.92f,.05f+(float)rng.NextDouble()*.90f);
            bool clear=true;
            foreach(var node in positions) if(Vector2.Scale(p-node,size).magnitude<118) clear=false;
            foreach(var other in placed) if(Vector2.Scale(p-other,size).magnitude<100) clear=false;
            for(int i=1;i<positions.Count;i++)
            {
                Vector2 a=Vector2.Scale(positions[i-1],size), b=Vector2.Scale(positions[i],size), v=Vector2.Scale(p,size);
                Vector2 q=a+Vector2.ClampMagnitude(b-a,Vector2.Distance(a,b)*Mathf.Clamp01(Vector2.Dot(v-a,b-a)/(b-a).sqrMagnitude));
                if(Vector2.Distance(v,q)<60) clear=false;
            }
            if(!clear) continue;
            var icon=SelectionScreenView.Rect("GroundScenery",route,p,p);
            float scale=42+(float)rng.NextDouble()*16;
            icon.sizeDelta=new Vector2(scale,scale);
            var image=icon.gameObject.AddComponent<RawImage>();
            image.texture=sourceImage.texture; image.color=new Color(.77f,.83f,.72f,.65f); image.raycastTarget=false;
            if(rng.Next(2)==0) image.uvRect=new Rect(1,0,-1,1);
            nodes.Add(icon.gameObject); placed.Add(p);
        }
    }
    void Select(int index)
    {
        if (index < 0 || index >= levels.Count)
        {
            selection.text = "No levels yet"; description.text = "More adventures coming soon"; play.interactable = false; return;
        }
        bool changed = selectedIndex != index;
        selectedIndex = index;
        LevelConfig level = levels[index];
        bool unlocked = LevelProgress.IsLevelUnlocked(level.environmentName, level.levelNumber);
        selection.text = $"LEVEL {level.levelNumber}";
        int waves = level.waveConfig != null && level.waveConfig.waves != null ? level.waveConfig.waves.Length : 0;
        status.text = !unlocked ? "KEEP EXPLORING" : index == nextIndex ? "YOUR NEXT LEVEL" : "READY TO REPLAY";
        status.color = unlocked ? new Color(.75f,.81f,.46f) : SelectionScreenView.Muted;
        for(int i=detailStars.childCount-1;i>=0;i--) { var child=detailStars.GetChild(i).gameObject; child.SetActive(false); if(Application.isPlaying) Destroy(child); else DestroyImmediate(child); }
        StarSprite.BuildRow(detailStars, Mathf.Clamp(LevelProgress.GetStars(level.environmentName,level.levelNumber),0,3), 24);
        description.text = unlocked ? $"{waves} waves to defend"
          : index > 0 ? $"Complete level {levels[index - 1].levelNumber} to unlock" : "Locked";
        play.interactable = unlocked;
        SelectionScreenView.ButtonText(play, unlocked ? "PLAY LEVEL" : "LOCKED");
        SelectionScreenView.ButtonIcon(play, unlocked ? UiSprites.Play() : UiSprites.Lock());
        if (changed) UpdateSelection();
    }
    void Play()
    {
        if (selectedIndex < 0 || selectedIndex >= levels.Count) return;
        LevelConfig level = levels[selectedIndex];
        if (!LevelProgress.IsLevelUnlocked(level.environmentName, level.levelNumber)) return;
        GameSession.SelectedLevel = level;
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.LevelPicked);
        SceneController.Instance.LoadScene(SceneController.GameScene.MainGame);
        gameObject.SetActive(false);
    }
    void OnHome()
    {
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
        if (EnvironmentsScreen.Instance != null) EnvironmentsScreen.Instance.ReturnToMenu();
        Destroy(gameObject);
    }
    void OnBack()
    {
        if (EnvironmentsScreen.Instance != null) EnvironmentsScreen.Instance.gameObject.SetActive(true);
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
        Destroy(gameObject);
    }
    void OnDestroy() { view?.Dispose(); }
}
