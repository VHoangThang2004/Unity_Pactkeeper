using UnityEngine;
using Unity.Netcode;

public class UnitData : INetworkSerializable
{
    public int Id;
    public int UId;
    public int team;
    public Vector3Int CurrentCell;
    public int MoveRange;

    // Timeline
    public int Speed;           // unit stat — copied from UnitDefinition at spawn
    public int CurrentStep;     // counts down each instant — client can see this
    public bool stepAlt;  // alternating tracker — SERVER ONLY, never serialized

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref UId);
        serializer.SerializeValue(ref team);
        serializer.SerializeValue(ref CurrentCell);
        serializer.SerializeValue(ref MoveRange);
        serializer.SerializeValue(ref Speed);
        serializer.SerializeValue(ref CurrentStep);
        // stepAlt intentionally NOT serialized — elite knowledge
    }
}