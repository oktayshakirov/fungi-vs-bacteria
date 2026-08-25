using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverScreen : MonoBehaviour
{
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    private Button continueButton, watchButton;
    private TMP_Text continueLabel, watchLabel, statusLabel;
    private string statusText = "";
    private bool awaitingAd;
    private bool levelEndCounted;

    private void Awake()
    {
        if (mainMenuButton == null || restartButton == null)
        {
            Debug.LogError("Buttons are not assigned in the inspector!");
        }
    }

    public void Initialize()
    {
        if (restartButton == null || mainMenuButton == null)
        {
            return;
        }

        restartButton.onClick.RemoveAllListeners();
        mainMenuButton.onClick.RemoveAllListeners();

        restartButton.onClick.AddListener(OnRestartClicked);
        mainMenuButton.onClick.AddListener(ReturnToMainMenu);

        // Losing screen: the retry button is the call to action, in danger red
        ScreenTheme.Apply(transform, restartButton, UiSkin.Danger);

        BuildContinueOffer();

        // Also run the per-show pass here. Depending on whether the prefab root
        // is active, OnEnable can fire during Instantiate - before the buttons
        // exist - and then not fire again for the SetActive(true) that follows,
        // because the object was already active. PrepareForShow is idempotent,
        // so running it from both places is safe and neither path can miss it.
        PrepareForShow();
    }

    // HUDManager instantiates this screen once and re-shows it, so Initialize
    // runs a single time while the screen can be shown several times in one run
    // - die, continue, die again. Everything that depends on current state has
    // to live here instead, or the second death shows the first death's UI:
    // a stale "Loading ad..." that never resolves, and a continue offer that
    // was already spent.
    private void OnEnable()
    {
        Ads.OnRewardedAvailabilityChanged += Refresh;
        PrepareForShow();
    }

    private void PrepareForShow()
    {
        statusText = "";
        awaitingAd = false;

        // Coin continues are always offered - the escalating price is what caps
        // them. The free ad continue is the part limited to once per run, so
        // only that button comes and goes.
        bool adAvailable = Boosters.CanContinueWithAd;
        if (watchButton != null) watchButton.gameObject.SetActive(adAvailable);
        if (adAvailable) Ads.Prewarm();

        // Counted once per run, not once per death: continuing means the level
        // did not actually end, and pacing interstitials off death count would
        // punish players who continue.
        if (!levelEndCounted)
        {
            levelEndCounted = true;
            Ads.OnLevelEnded();
        }

        Refresh();
    }

    // The single highest-value moment for both currencies: the player has just
    // lost a run they were invested in. They can pay coins or watch a rewarded
    // ad, and either way the run resumes in place rather than restarting.
    //
    // Offered once per run (Boosters.ContinueUsedThisRun) — with unlimited
    // continues a player holding coins can never lose, and the difficulty curve
    // stops meaning anything.
    private void BuildContinueOffer()
    {
        Transform panel = restartButton.transform.parent;

        continueButton = BuildCardButton(panel, "ContinueCoins", UiSkin.Gold,
            out continueLabel, OnContinueWithCoins);
        watchButton = BuildCardButton(panel, "ContinueAd", UiSkin.Accent,
            out watchLabel, OnContinueWithAd);

        // Above Retry and Main Menu: continuing is the offer, restarting is the
        // fallback.
        continueButton.transform.SetAsFirstSibling();
        watchButton.transform.SetSiblingIndex(1);

        statusLabel = BuildStatusLabel(panel);
        statusLabel.transform.SetSiblingIndex(2);

        Refresh();
    }

    // Carries the "not enough coins" / "no ad" feedback. Zero-height until it
    // has something to say, so the card does not grow a blank gap.
    private TMP_Text BuildStatusLabel(Transform parent)
    {
        var go = new GameObject("Status", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var label = go.AddComponent<TextMeshProUGUI>();
        UiSkin.Label(label, UiSkin.Role.Caption, UiSkin.TextMuted);
        label.alignment = TextAlignmentOptions.Center;
        label.text = "";

        // Collapsed until it has something to say, so the card carries no blank
        // band in the common case where nothing went wrong.
        var element = go.AddComponent<LayoutElement>();
        element.minHeight = 0f;
        element.preferredHeight = 0f;
        return label;
    }

    private Button BuildCardButton(Transform parent, string name, Color tint,
        out TMP_Text label, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Button button = UiSkin.IconButton(go, UiSprites.Coin(), tint, out label,
            UiSkin.RadiusButton, UiSkin.Gold);
        label.alignment = TextAlignmentOptions.Center;

        // ScreenTheme.Card sizes the panel's buttons through LayoutElement; a
        // runtime addition has to opt into the same sizing or it collapses.
        var element = go.GetComponent<LayoutElement>();
        if (element == null) element = go.AddComponent<LayoutElement>();
        element.minHeight = 76f;
        element.preferredHeight = 84f;

        button.onClick.AddListener(onClick);
        return button;
    }

    private void OnContinueWithCoins()
    {
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);

        if (!Wallet.TrySpend(Boosters.ContinueCost))
        {
            statusText = "Not enough coins - watch an ad instead.";
            Refresh();
            return;
        }

        Continue(false);
    }

    private void OnContinueWithAd()
    {
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);

        awaitingAd = true;
        statusText = "";
        Refresh();

        Ads.ShowRewarded(
            amount =>
            {
                // The dashboard's reward amount is coins; a continue bought with
                // an ad also banks them, so the ad is never worse than watching
                // one from the wallet.
                Wallet.Add(amount);
                Ads.DeferInterstitial();
                Continue(true);
            },
            () =>
            {
                // Always clears awaitingAd: this is the callback that used to
                // leave the button dead on a second death, because nothing
                // reset the pending state once the first attempt finished.
                awaitingAd = false;
                statusText = "No ad available right now.";
                Ads.Prewarm();
                Refresh();
            });
    }

    private void Continue(bool viaAd)
    {
        awaitingAd = false;
        Haptics.Play(Haptics.Style.Success);
        gameObject.SetActive(false);
        GameManager.Instance.ContinueRun(Boosters.ContinueHealth, viaAd);
    }

    private void Refresh()
    {
        if (continueButton != null)
        {
            continueButton.interactable = Wallet.CanAfford(Boosters.ContinueCost);
            continueLabel.text = $"CONTINUE  {Boosters.ContinueCost}";
        }

        if (watchButton != null)
        {
            // Three distinct states rather than a status string that can get
            // stuck: an ad in hand, one on the way, or none to be had.
            bool ready = Ads.IsRewardedReady;
            bool loading = awaitingAd || Ads.IsRewardedLoading;

            watchButton.interactable = ready && !awaitingAd;
            watchLabel.text = ready ? "CONTINUE - WATCH AD"
                            : loading ? "LOADING AD..."
                            : "AD UNAVAILABLE";
        }

        if (statusLabel != null)
        {
            statusLabel.text = statusText;
            var element = statusLabel.GetComponent<LayoutElement>();
            if (element != null) element.preferredHeight = string.IsNullOrEmpty(statusText) ? 0f : 34f;
        }
    }

    private void OnDisable()
    {
        Ads.OnRewardedAvailabilityChanged -= Refresh;
    }

    private void OnRestartClicked()
    {
        gameObject.SetActive(false);
        GameManager.Instance.RestartGame();
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    }

    private void ReturnToMainMenu()
    {
        gameObject.SetActive(false);
        GameManager.Instance.ReturnToMainMenu();
        AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
    }
}