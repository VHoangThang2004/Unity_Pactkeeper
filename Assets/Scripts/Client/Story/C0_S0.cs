using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class C0_S0 : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private BackendConfig config;
    [SerializeField] private SceneConfig sceneConfig;

    public void CompleteAndContinue()
    {
        StartCoroutine(StoryClient.CompleteAndAdvance(
            config, this,
            next => SceneManager.LoadScene(sceneConfig.mainMenuScene),
            () => Debug.LogWarning("[MainMenu] Story advance failed.")
        ));
    }
}
