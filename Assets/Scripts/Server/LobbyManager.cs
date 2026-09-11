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
    public void SubmitLoadoutServerRpc(NetUnitLoadout unit1, NetUnitLoadout unit2, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        // Cast the received data to the type is currently using (PlayerUnitLoadout).
        List<PlayerUnitLoadout> loadout = new List<PlayerUnitLoadout>
        {
            new PlayerUnitLoadout { uId = unit1.uId, movementSkillId = unit1.movementSkillId, weaponSkillId = unit1.weaponSkillId, classSkillId = -1, equipmentSkillId = -1 },
            new PlayerUnitLoadout { uId = unit2.uId, movementSkillId = unit2.movementSkillId, weaponSkillId = unit2.weaponSkillId, classSkillId = -1, equipmentSkillId = -1 }
        };

        //Save to storage
        MatchLoadoutData[clientId] = loadout;
        readyPlayersCount++;

        Debug.Log($"[Lobby] Player {clientId} đã sẵn sàng với tướng: {unit1.uId} và {unit2.uId}");

        //Check if there are 2 people ready.
        if (readyPlayersCount >= requiredPlayers)
        {
            Debug.Log("[Lobby] Đủ 2 người đã chọn tướng. VÀO TRẬN!");
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