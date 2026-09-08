using System.Collections.Generic;
using Unity.Netcode;

public struct SessionSnapshotData : INetworkSerializable
{
    public int Turn;
    public int CurrentPlayerTurn;
    public List<TeamData> Teams;
    public List<UnitData> Units;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Turn);
        serializer.SerializeValue(ref CurrentPlayerTurn);
        
        if (serializer.IsReader)
        {
            var teamsCount = 0;
            serializer.SerializeValue(ref teamsCount);
            Teams = new List<TeamData>(teamsCount);
            for (var i = 0; i < teamsCount; i++)
            {
                var team = new TeamData();
                team.NetworkSerialize(serializer);
                Teams.Add(team);
            }
            
            var unitsCount = 0;
            serializer.SerializeValue(ref unitsCount);
            Units = new List<UnitData>(unitsCount);
            for (var i = 0; i < unitsCount; i++)
            {
                var unit = new UnitData();
                unit.NetworkSerialize(serializer);
                Units.Add(unit);
            }
        }
        else
        {
            var teamsCount = Teams?.Count ?? 0;
            serializer.SerializeValue(ref teamsCount);
            if (Teams != null)
            {
                foreach (var team in Teams)
                    team.NetworkSerialize(serializer);
            }
            
            var unitsCount = Units?.Count ?? 0;
            serializer.SerializeValue(ref unitsCount);
            if (Units != null)
            {
                foreach (var unit in Units)
                    unit.NetworkSerialize(serializer);
            }
        }
    }
}