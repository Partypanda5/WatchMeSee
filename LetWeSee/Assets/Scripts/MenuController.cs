using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MenuController : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private string playSceneName = "SampleScene";

    private void Awake()
    {
        if (playButton != null) playButton.onClick.AddListener(Play);
        if (quitButton != null) quitButton.onClick.AddListener(Quit);
    }

    private void OnDestroy()
    {
        if (playButton != null) playButton.onClick.RemoveListener(Play);
        if (quitButton != null) quitButton.onClick.RemoveListener(Quit);
    }

    public void Play()
    {
        if (Application.CanStreamedLevelBeLoaded(playSceneName))
            SceneManager.LoadScene(playSceneName);
        else
            Debug.LogError("The menu could not load scene '" + playSceneName + "'. Add it to Build Settings.");
    }

    public void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}