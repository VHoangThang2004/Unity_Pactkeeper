using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class C0_S0 : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig sceneConfig;

    void Start()
    {
        var replayUI = GetComponent<TutorialReplayUIController>() ?? FindAnyObjectByType<TutorialReplayUIController>();
        if (replayUI == null)
            replayUI = gameObject.AddComponent<TutorialReplayUIController>();
        replayUI.ConfigureDependencies(config, null);
        replayUI.InitializeUI();
    }

    public void CompleteAndContinue()
    {
        StartCoroutine(StoryClient.CompleteAndAdvance(
            config, this,
            next => StoryClient.RouteToStoryScene(next, config, this),
            () => Debug.LogWarning("[MainMenu] Story advance failed."),
            chapterId: 0,
            sceneId: 0
        ));
    }
}
