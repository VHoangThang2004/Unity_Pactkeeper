using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Formation endpoints shared by 7_UnitList and the in-match formation prep.
/// </summary>
public static class FormationApi
{
    public static IEnumerator GetOwnedUnits(BackendConfig config, Action<List<OwnedUnitDto>> onDone)
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/PlayerProfile");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[FormationApi] GetOwnedUnits: {request.error}");
            onDone(null);
            yield break;
        }

        var profile = JsonUtility.FromJson<PlayerProfileResponse>(request.downloadHandler.text);
        onDone(profile.ownedUnits != null ? new List<OwnedUnitDto>(profile.ownedUnits) : new List<OwnedUnitDto>());
    }

    public static IEnumerator GetFormation(BackendConfig config, Action<List<int>> onDone)
    {
        using var request = UnityWebRequest.Get($"{config.backendUrl}/api/TeamLoadout");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[FormationApi] GetFormation: {request.error}");
            onDone(new List<int>());
            yield break;
        }

        var wrapper = JsonUtility.FromJson<IntArrayWrapper>("{\"items\":" + request.downloadHandler.text + "}");
        onDone(wrapper.items != null ? new List<int>(wrapper.items) : new List<int>());
    }

    public static IEnumerator SaveFormation(BackendConfig config, List<int> uIds)
    {
        string body = JsonUtility.ToJson(new SaveLoadoutDto { UIds = uIds });
        using var request = UnityWebRequest.Put($"{config.backendUrl}/api/TeamLoadout", body);
        request.SetRequestHeader("Content-Type", "application/json");
        config.SetHeaders(request, PlayerSession.Token);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
            Debug.LogError($"[FormationApi] SaveFormation failed: {request.error}");
        else
            Debug.Log("[FormationApi] Formation saved.");
    }

    [Serializable] private class SaveLoadoutDto { public List<int> UIds; }
    [Serializable] private class IntArrayWrapper { public int[] items; }
}
