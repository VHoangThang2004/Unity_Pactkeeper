using UnityEngine;
using Unity.Netcode;

public class UnitData : INetworkSerializable
{
    public int Id;
    public int UId;
    public int team;
    public Vector3Int CurrentCell;
    public int MoveRange;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref UId);
        serializer.SerializeValue(ref team);
        serializer.SerializeValue(ref CurrentCell);
        serializer.SerializeValue(ref MoveRange);
    }
}