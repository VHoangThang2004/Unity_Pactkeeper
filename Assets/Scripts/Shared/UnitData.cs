using UnityEngine;
using Unity.Netcode;
using System.Linq;

public class UnitData : INetworkSerializable
{
    // Identity
    public int Id;
    public int UId;
    public Vector3Int CurrentCell;

    // Live values
    public int CurrentHP;
    public int CurrentSkillPoint;
    public int CurrentStep;
    public float NextStepMultiplier = 0f; // SERVER ONLY, never serialized
    public int CurrentStepBase;
    public int NextStep;
    public bool StepAlt; // SERVER ONLY, never serialized

    // Recalculated stats
    public int Speed;
    public int MaxHP;
    public int MaxSkillPoint;
    public int ConsecutiveRegenInstants = 0;
    public int DamageMultiplier = 100; // +%
    public int DamageReduction = 0;    // -%

    // Base stats from loadout (pre-effect) — SERVER ONLY, never serialized
    public int BaseMaxHP;
    public int BaseMaxSkillPoint;
    public int BaseSpeed;
    public int BaseDamageMultiplier = 100; // +%
    public int BaseDamageReduction = 0;    // -%

    // Skill slots
    public int MovementSkillId = -1;
    public int WeaponSkillId = -1;
    public int ClassSkillId = -1;
    public int TrinketSkillId = -1;
    public int PassiveSkillId = -1;
    public SkillUsageData[] SkillUsages = new SkillUsageData[0];

    public SkillSlot GetSlot(int skillId)
    {
        if (skillId == -1) return SkillSlot.None;
        if (skillId == MovementSkillId) return SkillSlot.Movement;
        if (skillId == WeaponSkillId) return SkillSlot.Weapon;
        if (skillId == ClassSkillId) return SkillSlot.Class;
        if (skillId == TrinketSkillId) return SkillSlot.Trinket;
        if (skillId == PassiveSkillId) return SkillSlot.Passive;
        return SkillSlot.None;
    }

    // Skill patterns — translated + filtered by server, ready for client to read
    public CurrentPatterns[] SkillPatterns = new CurrentPatterns[0];

    // Active effects (id + remaining) — client displays buff/debuff icons
    public ActiveEffectView[] ActiveEffects = new ActiveEffectView[0];

    public UnitData Clone()
    {
        return new UnitData
        {
            Id = Id,
            UId = UId,
            CurrentCell = CurrentCell,
            CurrentHP = CurrentHP,
            CurrentSkillPoint = CurrentSkillPoint,
            CurrentStep = CurrentStep,
            Speed = Speed,
            MaxHP = MaxHP,
            MaxSkillPoint = MaxSkillPoint,
            DamageMultiplier = DamageMultiplier,
            DamageReduction = DamageReduction,
            MovementSkillId = MovementSkillId,
            WeaponSkillId = WeaponSkillId,
            ClassSkillId = ClassSkillId,
            TrinketSkillId = TrinketSkillId,
            PassiveSkillId = PassiveSkillId,
            SkillUsages = SkillUsages?.ToArray(),
            SkillPatterns = SkillPatterns?.ToArray(),
            ActiveEffects = ActiveEffects?.ToArray(),
            ConsecutiveRegenInstants = ConsecutiveRegenInstants,
            NextStep = NextStep,
            CurrentStepBase = CurrentStepBase,
        };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref UId);
        serializer.SerializeValue(ref CurrentCell);
        serializer.SerializeValue(ref CurrentHP);
        serializer.SerializeValue(ref CurrentSkillPoint);
        serializer.SerializeValue(ref CurrentStep);
        serializer.SerializeValue(ref Speed);
        serializer.SerializeValue(ref MaxHP);
        serializer.SerializeValue(ref MaxSkillPoint);
        serializer.SerializeValue(ref DamageMultiplier);
        serializer.SerializeValue(ref DamageReduction);
        serializer.SerializeValue(ref MovementSkillId);
        serializer.SerializeValue(ref WeaponSkillId);
        serializer.SerializeValue(ref ClassSkillId);
        serializer.SerializeValue(ref TrinketSkillId);
        serializer.SerializeValue(ref PassiveSkillId);
        serializer.SerializeValue(ref ConsecutiveRegenInstants);
        serializer.SerializeValue(ref NextStep);
        serializer.SerializeValue(ref CurrentStepBase);
        serializer.SerializeValue(ref StepAlt);


        // SkillPatterns
        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            SkillPatterns = new CurrentPatterns[count];
            for (int i = 0; i < count; i++)
            {
                var o = new CurrentPatterns();
                o.NetworkSerialize(serializer);
                SkillPatterns[i] = o;
            }
        }
        else
        {
            int count = SkillPatterns?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (SkillPatterns != null)
                for (int i = 0; i < count; i++)
                    SkillPatterns[i].NetworkSerialize(serializer);
        }

        // ActiveEffects
        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            ActiveEffects = new ActiveEffectView[count];
            for (int i = 0; i < count; i++)
                ActiveEffects[i].NetworkSerialize(serializer);
        }
        else
        {
            int count = ActiveEffects?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (ActiveEffects != null)
                for (int i = 0; i < count; i++)
                    ActiveEffects[i].NetworkSerialize(serializer);
        }

        // SkillUsages
        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            SkillUsages = new SkillUsageData[count];
            for (int i = 0; i < count; i++)
            {
                var usage = new SkillUsageData();
                usage.NetworkSerialize(serializer);
                SkillUsages[i] = usage;
            }
        }
        else
        {
            int count = SkillUsages?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (SkillUsages != null)
                for (int i = 0; i < count; i++)
                    SkillUsages[i].NetworkSerialize(serializer);
        }

        // stepAlt — SERVER ONLY, never serialized
    }
}