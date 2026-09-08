using UnityEngine;
using Unity.Netcode;

public class ClientOnly : MonoBehaviour
{
    void Awake()
    {
        if (IsServer())
        {
            Destroy(gameObject);
        }
    }

    bool IsServer()
    {
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsServer;
    }
}