// Server/MatchStats.cs
// Battle stats for match history. Fed by DamagePipeline, HealPipeline, RecordSkillUsage and the decision timeout.
// Keyed by runtime unit Id; team is captured on first sight because dead units are removed from the session.
using System.Collections.Generic;
using System.Linq;

public class MatchStats
{
    public class UnitStats
    {
        public int UId, TeamId;
        public int DamageDealt, DamageTaken, HealingDone, Kills;
        public int DiedAtInstant = -1, KilledByUId = -1;
        public Dictionary<int, int> SkillUses = new();
    }

    readonly Dictionary<int, UnitStats> units = new();
    readonly Dictionary<int, int> timeoutsByTeam = new();

    public UnitStats Get(ServerMatchSession session, UnitData unit)
    {
        if (!units.TryGetValue(unit.Id, out var s))
            units[unit.Id] = s = new UnitStats { UId = unit.UId, TeamId = session.GetTeamIdByUnitId(unit.Id) };
        return s;
    }

    public void Damage(ServerMatchSession session, UnitData source, UnitData target, int dealt, bool killed)
    {
        var t = Get(session, target);
        t.DamageTaken += dealt;
        UnitStats s = source != null ? Get(session, source) : null;
        if (s != null) s.DamageDealt += dealt;
        if (!killed) return;
        t.DiedAtInstant = session.CurrentInstant;
        if (s != null) { s.Kills++; t.KilledByUId = s.UId; }
    }

    public void Heal(ServerMatchSession session, UnitData source, int restored)
    {
        if (source != null) Get(session, source).HealingDone += restored;
    }

    public void SkillUsed(ServerMatchSession session, UnitData unit, int skillId)
    {
        var d = Get(session, unit).SkillUses;
        d[skillId] = d.GetValueOrDefault(skillId) + 1;
    }

    public void Timeout(int teamId) => timeoutsByTeam[teamId] = timeoutsByTeam.GetValueOrDefault(teamId) + 1;

    // Fills the battle-stat fields of one player's report. Call before the session is torn down.
    public void FillReport(ServerMatchSession session, int teamId, AfterMatchPlayerReport r)
    {
        foreach (var u in session.GetAllUnits()) Get(session, u); // untouched survivors still get a row

        var mine = units.Values.Where(u => u.TeamId == teamId).ToList();
        r.damageDealt = mine.Sum(u => u.DamageDealt);
        r.damageTaken = mine.Sum(u => u.DamageTaken);
        r.healingDone = mine.Sum(u => u.HealingDone);
        r.timeouts = timeoutsByTeam.GetValueOrDefault(teamId);
        r.units = mine.Select(u => new UnitStatsReport
        {
            uId = u.UId,
            alive = u.DiedAtInstant < 0,
            damageDealt = u.DamageDealt,
            damageTaken = u.DamageTaken,
            healingDone = u.HealingDone,
            kills = u.Kills,
            diedAtInstant = u.DiedAtInstant,
            killedByUId = u.KilledByUId,
            skillUses = u.SkillUses.Select(kv => new SkillUseReport { skillId = kv.Key, count = kv.Value }).ToArray()
        }).ToArray();
    }
}
