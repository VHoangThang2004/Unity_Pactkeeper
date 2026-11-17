using UnityEngine;

/// <summary>
/// Một entry ánh xạ ngưỡng số lượng gems → sprite icon tương ứng.
/// Dùng trong GachaManager.gemIcons[] để chọn đúng UI gem theo amount.
/// </summary>
[System.Serializable]
public struct GemIconEntry
{
    [Tooltip("Số gems tối thiểu để dùng icon này (ví dụ: 0, 51, 101)")]
    public int minAmount;

    [Tooltip("Sprite icon hiển thị cho mức gems này")]
    public Sprite icon;
}
