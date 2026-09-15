using UnityEngine;

[CreateAssetMenu(fileName = "SceneConfig", menuName = "SRPG/Scene Config")]
public class SceneConfig : ScriptableObject
{
    [Header("Client Scenes")]
    public string loginScene = "1_Login";
    public string devConnectScene = "2_DevConnect";
    public string mainMenuScene = "3_MainMenu";
    public string lobbyScene = "4_Lobby";

    [Header("Server Scenes")]
    public string pvpScene = "5_Match";
    public string storyScene = "5_Story";
}