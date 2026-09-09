using System.Collections.Generic;
using Unity.Netcode;

public struct SessionSnapshotData : INetworkSerializable
{
    public string MapId;            // clients use this to look up GridMapAsset in their MapRegistry
    public int Turn;
    public int CurrentPlayerTurn;
    public List<TeamData> Teams;
    public List<UnitData> Units;
    public TimelineData Timeline;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref MapId);
        serializer.SerializeValue(ref Turn);
        serializer.SerializeValue(ref CurrentPlayerTurn);

        // Teams
        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            Teams = new List<TeamData>(count);
            for (int i = 0; i < count; i++)
            {
                var team = new TeamData();
                team.NetworkSerialize(serializer);
                Teams.Add(team);
            }
        }
        else
        {
            int count = Teams?.Count ?? 0;
            serializer.SerializeValue(ref count);
            if (Teams != null)
                foreach (var team in Teams)
                    team.NetworkSerialize(serializer);
        }

        // Units
        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            Units = new List<UnitData>(count);
            for (int i = 0; i < count; i++)
            {
                var unit = new UnitData();
                unit.NetworkSerialize(serializer);
                Units.Add(unit);
            }
        }
        else
        {
            int count = Units?.Count ?? 0;
            serializer.SerializeValue(ref count);
            if (Units != null)
                foreach (var unit in Units)
                    unit.NetworkSerialize(serializer);
        }

        // Timeline
        Timeline.NetworkSerialize(serializer);
    }
}