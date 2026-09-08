using System.Collections.Generic;
using Unity.Netcode;

public class TeamData : INetworkSerializable
{
    public int teamId;
    public ulong clientId;
    public List<UnitData> units;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref teamId);
        serializer.SerializeValue(ref clientId);
        
        if (serializer.IsReader)
        {
            var count = 0;
            serializer.SerializeValue(ref count);
            units = new List<UnitData>(count);
            for (var i = 0; i < count; i++)
            {
                var unit = new UnitData();
                unit.NetworkSerialize(serializer);
                units.Add(unit);
            }
        }
        else
        {
            var count = units?.Count ?? 0;
            serializer.SerializeValue(ref count);
            if (units != null)
            {
                foreach (var unit in units)
                    unit.NetworkSerialize(serializer);
            }
        }
    }
}