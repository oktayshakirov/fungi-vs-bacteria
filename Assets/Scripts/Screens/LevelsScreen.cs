using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class LevelSelectionScreen : MonoBehaviour
{
  [Header("Level Card Setup")]
  [SerializeField] private GameObject levelCardPrefab;
  [SerializeField] private Transform cardsContainer;

  [Header("Back Button")]
  [SerializeField] private Button backButton;

  private void OnLevelSelected(LevelConfig level)
  {
    Debug.Log($"Selected Level: {level.levelNumber} ({level.environmentName})");
    GameSession.SelectedLevel = level;
    AudioManager.Instance?.PlaySound(AudioManager.SoundType.LevelPicked);
    SceneController.Instance.LoadScene(SceneController.GameScene.MainGame);
    gameObject.SetActive(false);
  }

  private void OnBack()
  {
    Destroy(gameObject);
    if (EnvironmentsScreen.Instance != null)
    {
      AudioManager.Instance?.PlaySound(AudioManager.SoundType.ButtonClick);
      EnvironmentsScreen.Instance.gameObject.SetActive(true);
    }
  }
}
