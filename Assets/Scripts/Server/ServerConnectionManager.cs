using UnityEngine;
using Unity.Netcode;

public class ServerConnectionManager : MonoBehaviour
{
    private ServerBackendClient backendClient;
    private ServerMatchSession session;
    void Start()
    {
        backendClient = FindAnyObjectByType<ServerBackendClient>();
        NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        Debug.Log("[ConnectionManager] Started.");
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    // -------------------------------------------------------
    // Approval
    // -------------------------------------------------------

    private void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        string token = System.Text.Encoding.UTF8.GetString(request.Payload);
        string playerId = TokenHelper.ExtractPlayerId(token);

        Debug.Log($"[ConnectionManager] Request — clientId={request.ClientNetworkId} playerId={playerId}");

        if (string.IsNullOrEmpty(playerId))
        {
            Debug.LogWarning("[ConnectionManager] Rejected — invalid token.");
            response.Approved = false;
            return;
        }

        if (backendClient?.LoadoutResponse != null)
        {
            Debug.Log($"[ConnectionManager] LoadoutResponse — p1={backendClient.LoadoutResponse.player1.playerId} p2={backendClient.LoadoutResponse.player2.playerId} checking for playerId={playerId}");
            string p1 = backendClient.LoadoutResponse.player1.playerId;
            string p2 = backendClient.LoadoutResponse.player2.playerId;

            if (playerId != p1 && playerId != p2)
            {
                Debug.LogWarning($"[ConnectionManager] Rejected — {playerId} not in this match.");
                response.Approved = false;
                return;
            }
            if (playerId == p1)
            {
                Debug.Log($"[ConnectionManager] Player 1 connected — playerId={playerId} with loadout {backendClient.LoadoutResponse.player1.units}");
            }
            if (playerId == p2)
            {
                Debug.Log($"[ConnectionManager] Player 2 connected — playerId={playerId} with loadout {backendClient.LoadoutResponse.player2.units}");
            }
        }


        MatchIdentityRegistry.clientPlayerMap[request.ClientNetworkId] = playerId;
        Debug.Log($"[ConnectionManager] Approved — {playerId} → clientId={request.ClientNetworkId}");

        response.Approved = true;
        response.CreatePlayerObject = false;
    }

    // -------------------------------------------------------
    // Reconnection
    // -------------------------------------------------------

    private void OnClientConnected(ulong clientId)
    {
        string playerId = MatchIdentityRegistry.GetPlayerId(clientId);
        if (string.IsNullOrEmpty(playerId)) return;

        if (session == null)
            session = FindAnyObjectByType<ServerMatchSession>();

        if (session != null)
        {
            var team = session.GetTeamByPlayerId(playerId);
            if (team != null)
            {
                team.clientId = clientId;
                Debug.Log($"[ConnectionManager] Reconnect — team {team.teamId} clientId → {clientId}");
            }
        }
    }
}