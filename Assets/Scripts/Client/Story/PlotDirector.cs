/// <summary>
/// Static event bus for tutorial triggers.
/// Game systems call PlotDirector.Trigger("id") at meaningful moments.
/// PlotSequencer listens and starts the matching plot.
/// </summary>
public static class PlotDirector
{
    public static event System.Action<string> OnTrigger;

    public static void Trigger(string triggerId)
    {
        UnityEngine.Debug.Log($"[PlotDirector] Trigger: {triggerId}");
        OnTrigger?.Invoke(triggerId);
    }

    public static void Clear()
    {
        OnTrigger = null;
    }
}