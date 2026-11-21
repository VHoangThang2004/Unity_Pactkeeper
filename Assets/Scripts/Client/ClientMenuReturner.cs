using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class ClientMenuReturner : MonoBehaviour
{
    [SerializeField] private SceneConfig sceneConfig;
    void Start()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnDisconnected;
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnDisconnected;
    }

    private void OnDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId != NetworkManager.Singleton.LocalClientId) return;

        // Ignore disconnects fired while ClientController.ConnectWithRetry() is still retrying initial UDP handshake
        var clientController = FindAnyObjectByType<ClientController>();
        if (clientController != null && clientController.IsConnecting)
        {
            Debug.Log("[ClientMenuReturner] Ignoring disconnect while ClientController is still retrying initial connection.");
            return;
        }

        // If a story overlay (Scripted, Hybrid, or Challenge) is present, let BattleTutorialStarter
        // route Victory/Defeat/Retry cleanly without kicking into PvP MatchResultScene.
        if (TutorialContext.IsActive || TutorialContext.IsFreePlayBattle || TutorialContext.HasBattleEnded)
        {
            var starter = FindAnyObjectByType<BattleTutorialStarter>();
            if (starter != null)
            {
                Debug.Log("[ClientMenuReturner] Disconnected during story battle — delegating to BattleTutorialStarter.");
                starter.HandleUnexpectedDisconnect();
                return;
            }
        }

        Debug.Log("[Client] Disconnected — returning to match result.");
        SceneManager.LoadScene(sceneConfig.matchResultScene);
    }
}
