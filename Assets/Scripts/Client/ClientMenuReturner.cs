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
        if (clientId != NetworkManager.Singleton.LocalClientId) return;
        Debug.Log("[Client] Disconnected — returning to main menu.");
        SceneManager.LoadScene(sceneConfig.mainMenuScene);
    }
}