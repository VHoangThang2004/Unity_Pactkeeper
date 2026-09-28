using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a sequence of tutorial steps for a story scene.
/// One asset per story scene that has tutorial content.
/// Name convention: Plot_C{chapterId}_S{sceneId}
///
/// Attach to the corresponding C{x}_S{y}_Manager via Inspector.
/// </summary>
[CreateAssetMenu(fileName = "Plot_C0_S2", menuName = "SRPG/Plot Config")]
public class PlotConfig : ScriptableObject
{
    [Header("Identity")]
    public int chapterId;
    public int sceneId;

    [Header("Nodes")]
    public List<PlotNode> nodes = new();
}