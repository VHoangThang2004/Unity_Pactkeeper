using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Row of status icons (buffs/debuffs/passives) for the currently selected unit — own or enemy.
/// Data comes from UnitData.ActiveEffects (server-built: id + expiry instant only).
/// Clicking an icon shows its name, description and remaining duration in the inspect panel.
/// </summary>
public class UnitEffectStrip : MonoBehaviour
{
    [SerializeField] private ClientMatchSession session;
    [SerializeField] private ClientScene scene;
    [SerializeField] private Transform iconContainer;
    [SerializeField] private EffectIconUI iconPrefab;

    private readonly List<EffectIconUI> icons = new();
    private int shownUnitId = int.MinValue;
    private int shownInstant = int.MinValue;
    private ActiveEffectView[] shownEffects;
    private float nextRefresh;

    void Update()
    {
        // Dumb poll like ClientVisualController.UILoop — cheap, and catches snapshot + tick changes alike
        if (Time.time < nextRefresh) return;
        nextRefresh = Time.time + 0.2f;

        UnitData unit = session.selectedUnitId == -1 ? null : session.GetUnitDataById(session.selectedUnitId);
        var effects = unit?.ActiveEffects;
        int instant = session.Timeline.currentInstant;

        if (unit?.Id == shownUnitId && instant == shownInstant && ReferenceEquals(effects, shownEffects)) return;
        shownUnitId = unit?.Id ?? int.MinValue;
        shownInstant = instant;
        shownEffects = effects;
        Rebuild(unit);
    }

    void Rebuild(UnitData unit)
    {
        int count = 0;
        if (unit?.ActiveEffects != null)
        {
            foreach (var view in unit.ActiveEffects)
            {
                if (!scene.effectRegistry.Exists(view.EffectId)) continue; // server-only effect with no client asset
                var effect = scene.effectRegistry.Get(view.EffectId);

                if (count == icons.Count) icons.Add(Instantiate(iconPrefab, iconContainer));
                var icon = icons[count++];
                icon.gameObject.SetActive(true);

                var v = view; // capture
                int unitId = unit.Id;
                icon.Set(effect.statusIcon, v.RemainingAt(shownInstant), v.Stacks, () =>
                {
                    var current = session.GetUnitDataById(unitId);
                    if (current != null) scene.visualController.ShowEffectInspect(effect, v, current);
                });
            }
        }

        for (int i = count; i < icons.Count; i++) icons[i].gameObject.SetActive(false);
        iconContainer.gameObject.SetActive(count > 0);
    }
}
