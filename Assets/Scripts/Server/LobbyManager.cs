using System.Collections.Generic;
using Unity.Burst.Intrinsics;
using Unity.Netcode;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance;
    [SerializeField] int requiredPlayers = 2;
    [SerializeField] string gameScene = "3_Match";

    //GET DATA: Save static data so it won't be lost in Scene 3.
    public static Dictionary<ulong, List<PlayerUnitLoadout>> MatchLoadoutData = new Dictionary<ulong, List<PlayerUnitLoadout>>();
    private int readyPlayersCount = 0;

    void Awake()
    {
        Instance = this;
    }

    // The client will call this function to send data to the server (RequireOwnership = false allows the client to call into the server's database).
    [ServerRpc(RequireOwnership = false)]
    public void SubmitLoadoutServerRpc(int[] unitIds, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        List<PlayerUnitLoadout> loadout = new List<PlayerUnitLoadout>();

        // loop iterates through all the Champions the player has selected.
        foreach (int id in unitIds)
        {
            loadout.Add(new PlayerUnitLoadout
            {
                uId = id,
                movementSkillId = 1, // Default Skill 
                weaponSkillId = 3,   // Default Skill 
                classSkillId = -1,
                equipmentSkillId = -1
            });
        }
        // Save to Data storage
        MatchLoadoutData[clientId] = loadout;
        readyPlayersCount++;
        Debug.Log($"[Lobby] Player {clientId} đã chốt {unitIds.Length} tướng.");
        // Check if there are 2 people ready.
        if (readyPlayersCount >= requiredPlayers)
        {
            Debug.Log("[Lobby] Đủ 2 người chốt đội hình. VÀO TRẬN!");
            NetworkManager.Singleton.SceneManager.LoadScene(gameScene, LoadSceneMode.Single);
        }
    }



    //[SerializeField] float checkInterval = 1f;

    //bool matchStarted = false;

    //void Start()
    //{
    //    // Only run on server
    //    if (!IsServer())
    //    {
    //        enabled = false;
    //        return;
    //    }

    //    Debug.Log("[Lobby] Server lobby running");

    //    InvokeRepeating(nameof(CheckStartGame), 1f, checkInterval);
    //}

    //void CheckStartGame()
    //{
    //    if (matchStarted) return;

    //    int playerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;

    //    Debug.Log($"[Lobby] Players: {playerCount}/{requiredPlayers}");

    //    if (playerCount >= requiredPlayers)
    //    {
    //        matchStarted = true;

    //        Debug.Log("[Lobby] Starting game");

    //        NetworkManager.Singleton.SceneManager.LoadScene(
    //            gameScene,
    //            LoadSceneMode.Single
    //        );
    //    }
    //}

    //bool IsServer()
    //{
    //    return NetworkManager.Singleton != null &&
    //           NetworkManager.Singleton.IsServer;
    //}
}