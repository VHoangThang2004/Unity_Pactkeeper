using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public struct ResolveData : INetworkSerializable
{
    public bool HasResolve;
    public int SkillCardId;
    public DecisionType decision;
    public List<ResolveResult> ResolveResults;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref HasResolve);
        serializer.SerializeValue(ref SkillCardId);

        byte decisionByte = (byte)decision;
        serializer.SerializeValue(ref decisionByte);
        decision = (DecisionType)decisionByte;

        if (serializer.IsReader)
        {
            int count = 0;
            serializer.SerializeValue(ref count);
            ResolveResults = new List<ResolveResult>(count);
            for (int i = 0; i < count; i++)
            {
                var result = new ResolveResult();
                result.NetworkSerialize(serializer);
                ResolveResults.Add(result);
            }
        }
        else
        {
            int count = ResolveResults?.Count ?? 0;
            serializer.SerializeValue(ref count);
            if (ResolveResults != null)
                foreach (var result in ResolveResults)
                {
                    var copy = result;
                    copy.NetworkSerialize(serializer);
                }
        }
    }
}