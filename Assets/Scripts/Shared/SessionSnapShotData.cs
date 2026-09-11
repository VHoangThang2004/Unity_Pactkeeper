using System.Collections.Generic;
using Unity.Netcode;

public struct SessionSnapshotData : INetworkSerializable
{
    // Presence flags — false = field not populated, skip on apply
    public bool HasMapId;
    public bool HasCurrentTeamTurn;
    public bool HasTeams;
    public bool HasUnits;
    public bool HasTimeline;

    // Data fields
    public string MapId;
    public int CurrentTeamTurn;
    public List<TeamData> Teams;
    public List<UnitData> Units;
    public TimelineData Timeline;

    public static SessionSnapshotData Partial(List<UnitData> changedUnits, List<TeamData> changedTeams = null)
    {
        var cloned = new List<UnitData>();
        foreach (var unit in changedUnits)
            cloned.Add(unit.Clone());

        return new SessionSnapshotData
        {
            HasUnits = true,
            Units = cloned,
            HasTeams = changedTeams != null,
            Teams = changedTeams
        };
    }

    public static SessionSnapshotData Full(string mapId, int teamTurn, List<TeamData> teams, List<UnitData> units, TimelineData timeline)
    {
        return new SessionSnapshotData
        {
            HasMapId = true,
            HasCurrentTeamTurn = true,
            HasTeams = true,
            HasUnits = true,
            HasTimeline = true,
            MapId = mapId,
            CurrentTeamTurn = teamTurn,
            Teams = teams,
            Units = units,
            Timeline = timeline
        };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref HasMapId);
        serializer.SerializeValue(ref HasCurrentTeamTurn);
        serializer.SerializeValue(ref HasTeams);
        serializer.SerializeValue(ref HasUnits);
        serializer.SerializeValue(ref HasTimeline);

        if (HasMapId)
        {
            if (!serializer.IsReader) MapId = MapId ?? string.Empty;
            serializer.SerializeValue(ref MapId);
        }

        if (HasCurrentTeamTurn)
            serializer.SerializeValue(ref CurrentTeamTurn);

        if (HasTeams)
        {
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
        }

        if (HasUnits)
        {
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
        }

        if (HasTimeline)
            Timeline.NetworkSerialize(serializer);
    }
}