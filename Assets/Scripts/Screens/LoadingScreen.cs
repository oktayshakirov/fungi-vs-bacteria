using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
  [SerializeField] private TextMeshProUGUI loadingText;
  [SerializeField] private Slider progressBar;
  [SerializeField] private TextMeshProUGUI progressText;

  public void UpdateProgress(float progress)
  {
    progressBar.value = progress;
    AudioManager.Instance.PlaySound(AudioManager.SoundType.Loading);
    progressText.text = $"{(progress * 100):0}%";
  }
}
