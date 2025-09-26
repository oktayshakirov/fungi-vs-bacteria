using UnityEngine;
using UnityEngine.UI;
using System;

public class PauseGameScreen : MonoBehaviour
{
  [Header("Pause Game Screen")]
  [SerializeField] private Button resumeGameButton;
  [SerializeField] private Button returnToMainMenuButton;

  private Action onScreenClosed;

  public void Initialize(Action onScreenClosed)
  {
    this.onScreenClosed = onScreenClosed;

    resumeGameButton.onClick.AddListener(ResumeGame);
    returnToMainMenuButton.onClick.AddListener(ReturnToMainMenu);
  }

  public void Show()
  {
    gameObject.SetActive(true);
    GameManager.Instance.PauseGame();
  }

  private void ResumeGame()
  {
    gameObject.SetActive(false);
    GameManager.Instance.ResumeGame();
    AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
    onScreenClosed?.Invoke();
  }

  private void ReturnToMainMenu()
  {
    gameObject.SetActive(false);
    GameManager.Instance.ReturnToMainMenu();
    AudioManager.Instance.PlaySound(AudioManager.SoundType.ButtonClick);
    onScreenClosed?.Invoke();
  }
}