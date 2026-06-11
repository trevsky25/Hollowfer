using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    public Button btnContinue;
    public Button btnStory;
    public Button btnFieldGuide;
    public Button btnWren;
    public Button btnSettings;

    [Header("Loading Transition")]
    public GameObject textCard;
    public GameObject navRow;
    public GameObject btnContinueObject;
    public GameObject loadingLabel;
    public string gameSceneName = "Game";
    public float minLoadingSeconds = 2f;

    void Start()
    {
        if (btnContinue)   btnContinue.onClick.AddListener(OnContinue);
        if (btnStory)      btnStory.onClick.AddListener(OnStory);
        if (btnFieldGuide) btnFieldGuide.onClick.AddListener(OnFieldGuide);
        if (btnWren)       btnWren.onClick.AddListener(OnWren);
        if (btnSettings)   btnSettings.onClick.AddListener(OnSettings);

        if (loadingLabel) loadingLabel.SetActive(false);
    }

    void OnContinue()   => StartCoroutine(LoadGameScene());
    void OnStory()      => Debug.Log("[Menu] Story clicked");
    void OnFieldGuide() => Debug.Log("[Menu] Field Guide clicked");
    void OnWren()       => Debug.Log("[Menu] Wren clicked");
    void OnSettings()   => Debug.Log("[Menu] Settings clicked");

    IEnumerator LoadGameScene()
    {
        if (textCard)          textCard.SetActive(false);
        if (navRow)            navRow.SetActive(false);
        if (btnContinueObject) btnContinueObject.SetActive(false);
        if (loadingLabel)      loadingLabel.SetActive(true);

        var op = SceneManager.LoadSceneAsync(gameSceneName);
        op.allowSceneActivation = false;

        float elapsed = 0f;
        while (op.progress < 0.9f || elapsed < minLoadingSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        op.allowSceneActivation = true;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
