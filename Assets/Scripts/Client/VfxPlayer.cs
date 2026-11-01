using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns one-shot VFX from code (pooled), instead of borrowing the VFX sprite nested in each unit prefab.
/// Uses the existing VFXSprite.prefab + VfxController, so every state name ("HitVFX", "ArrowTargetVFX", …) still works.
/// Any number can play at once — e.g. one impact per AoE target.
/// </summary>
public class VfxPlayer : MonoBehaviour
{
    [Tooltip("Prefabs/VFXs/VFXSprite.prefab — root at the spawn point, Animator (VfxController) on a child")]
    [SerializeField] private GameObject vfxPrefab;

    private readonly Stack<GameObject> pool = new();

    /// <summary>
    /// Plays `state` at `worldPos` and finishes when its clip has played once. `flipX` mirrors the whole VFX
    /// (art faces right by default). `setup` can set animator params first.
    /// </summary>
    public IEnumerator Play(string state, Vector3 worldPos, bool flipX = false, Action<Animator> setup = null)
    {
        if (vfxPrefab == null || string.IsNullOrEmpty(state)) yield break;

        GameObject go = pool.Count > 0 ? pool.Pop() : Instantiate(vfxPrefab, transform);
        go.transform.position = worldPos;
        var scale = go.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (flipX ? -1f : 1f); // pooled instances may come back flipped
        go.transform.localScale = scale;
        go.SetActive(true);

        Animator anim = go.GetComponentInChildren<Animator>();
        anim.Rebind();
        anim.SetBool("IsIdling", false);
        setup?.Invoke(anim);
        anim.Play(state, 0, 0f);

        yield return null; // let the animator enter the state before reading its length
        yield return new WaitForSeconds(anim.GetCurrentAnimatorStateInfo(0).length);

        go.SetActive(false);
        pool.Push(go);
    }

    /// <summary>
    /// Plays `state` at every position at once; finishes when the longest one does.
    /// If `faceFromX` is set, each VFX is mirrored when its position is left of that x (i.e. it faces away from the source).
    /// </summary>
    public IEnumerator PlayAll(string state, IEnumerable<Vector3> positions, float? faceFromX = null)
    {
        var running = new List<Coroutine>();
        foreach (var pos in positions)
            running.Add(StartCoroutine(Play(state, pos, faceFromX.HasValue && pos.x < faceFromX.Value)));
        foreach (var c in running)
            yield return c;
    }
}
