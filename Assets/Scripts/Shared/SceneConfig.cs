using UnityEngine;

[CreateAssetMenu(fileName = "SceneConfig", menuName = "SRPG/Scene Config")]
public class SceneConfig : ScriptableObject
{
    [Header("Client Scenes")]
    public string loginScene = "1_Login";
    public string devConnectScene = "2_DevConnect";
    public string mainMenuScene = "3_MainMenu";
    public string clientMatch = "5_ClientMatch";
    public string matchResultScene = "6_MatchResult";
    public string unitListScene = "7_UnitList";
    public string unitConfigScene = "8_UnitConfig";
    public string inventoryScene = "10_Inventory";
    public string gachaScene = "9_Gacha";
    public string storyScene = "5_ClientStory";


    [Header("Server Scenes")]
    public string lobbyScene = "4_Lobby"; //pvp, 2 player lobby (waits for 2 players)
    public string singleLobbyScene = "4_SingleStoryLobby"; //pve-story, 1 player only
    public string serverMatch = "5_ServerMatch";
    public string serverStoryScene = "5_ServerStory";
}