using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class ClientMenuReturner : MonoBehaviour
{
    [SerializeField] private SceneConfig sceneConfig;
    void Start()
    {
        NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
    }

    private void OnDisconnected(ulong clientId)
    {
        if (TutorialContext.IsActive)
        {
            Debug.Log("[Client] Disconnected during active tutorial — tutorial overlay handles scene transition.");
            return;
        }
        if (NetworkManager.Singleton != null && clientId != NetworkManager.Singleton.LocalClientId) return;
        Debug.Log("[Client] Disconnected — returning to match result.");
        SceneManager.LoadScene(sceneConfig.matchResultScene);
    }
}