using UnityEngine;

[CreateAssetMenu(fileName = "ServerCredentials", menuName = "SRPG/Server Credentials")]
public class ServerCredentialsConfig : ScriptableObject
{
    public string username = "gameserver";
    public string password;
}
