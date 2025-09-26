using UnityEngine;
using UnityEngine.UI;
using System;

public class GameOverScreen : MonoBehaviour
{
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

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
    }

    private void OnRestartClicked()
    {
        gameObject.SetActive(false);
        GameManager.Instance.RestartGame();
        AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
    }

    private void ReturnToMainMenu()
    {
        gameObject.SetActive(false);
        GameManager.Instance.ReturnToMainMenu();
        AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
    }
}