using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NormalProjectileAttackEffectVisual", menuName = "SRPG/Effects/Client/NormalProjectileAttack")]
public class NormalProjectileAttackEffectVisual : ClientActiveEffectBase
{
    public override InstantType InstantType => InstantType.NonInstant;
    public string sourceVFXname;
    public string targetVFXname;
    public AudioClip shootSfx;
    public AudioClip impactSfx;
    [UnityEngine.Tooltip("Art is drawn left→right: mirror the VFX when the target is to the attacker's left")]
    public bool flipTowardTarget = true;

    public override string GetDescription(UnitData unit, ClientEffectRegistry effectRegistry)
    {
        return " Deal " + Mathf.RoundToInt(baseValue * unit.DamageMultiplier / 100) + " damage to selected enemy target.";
    }

    public override IEnumerator Replay(
        ResolveResult result,
        ClientScene scene,
        ClientMatchSession session)
    {

        ClientUnit sceneUnit = scene.GetSceneUnitById(result.SourceUnitId);
        if (sceneUnit == null) yield break;
        scene.cameraController.CenterOn(sceneUnit.transform.position);

        if (result.TargetCells == null || result.TargetCells.Count == 0)
            yield break;
        if (scene.vfx == null)
        {
            Debug.LogWarning("[ProjectileVisual] ClientScene.vfx not assigned — skipping VFX.");
            yield break;
        }

        // VFX are spawned by VfxPlayer — the unit's own sprite is never moved, and every hit cell gets an impact
        Vector3 startPos = sceneUnit.transform.position;
        var targetPositions = new List<Vector3>();
        foreach (var cell in result.TargetCells)
            targetPositions.Add(scene.movableTilemap.GetCellCenterWorld(cell));

        scene.cameraController.CenterOn((startPos + targetPositions[0]) / 2f);

        if (AudioManager.Instance != null && shootSfx != null)
            AudioManager.Instance.PlaySFX(shootSfx);

        // Source faces the aimed (first) target; each impact faces away from the attacker
        bool sourceFlip = flipTowardTarget && targetPositions[0].x < startPos.x;
        float shotStart = Time.time;
        yield return scene.vfx.Play(sourceVFXname, startPos, sourceFlip);
        float animLength = Time.time - shotStart;
        yield return new WaitForSeconds(MathF.Max(resolveDuration - animLength * 2, 0)); // same pacing as before

        if (AudioManager.Instance != null && impactSfx != null)
            AudioManager.Instance.PlaySFX(impactSfx);

        yield return scene.vfx.PlayAll(targetVFXname, targetPositions, flipTowardTarget ? startPos.x : (float?)null);
    }
}