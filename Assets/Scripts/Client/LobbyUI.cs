using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class LobbyUI : MonoBehaviour
{
    private List<int> selectedUnitIds = new List<int>();
    private int maxUnits = 5;

    [SerializeField] TextMeshProUGUI teamListText; // The frame displays a list of 5 cells.

    void Start()
    {
        UpdateUI();
    }

    // The list of 5 boxes appears when you click on a Champion Avatar on the screen.
    public void OnClickAvatar(int chosenId)
    {
        if (selectedUnitIds.Count < maxUnits)
        {
            selectedUnitIds.Add(chosenId); // Add to the lineup
            UpdateUI();
        }
        else
        {
            Debug.LogWarning("Đội hình đã đầy (5 tướng)!");
        }
    }

    // If the player accidentally selects the wrong option, they can press the Cancel button to start over
    public void OnClickClear()
    {
        selectedUnitIds.Clear();
        UpdateUI();
    }

    // Update the text on the screen
    void UpdateUI()
    {
        string text = "ĐỘI HÌNH CỦA BẠN:\n";
        for (int i = 0; i < selectedUnitIds.Count; i++)
        {
            text += $"- Ô số {i + 1}: {GetUnitName(selectedUnitIds[i])}\n";
        }
        for (int i = selectedUnitIds.Count; i < maxUnits; i++)
        {
            text += $"- Ô số {i + 1}: (Đang trống)\n";
        }
        teamListText.text = text;
    }

    string GetUnitName(int id)
    {
        if (id == 1) return "Ếch Trắng";
        if (id == 2) return "Hiệp Sĩ";
        return "Tướng " + id;
    }

    public void OnClickReady()
    {
        if (selectedUnitIds.Count == 0)
        {
            Debug.LogWarning("Chưa chọn tướng nào!");
            return;
        }

        // Convert the List into an Array to send over the network
        int[] idsToSend = selectedUnitIds.ToArray();
        LobbyManager.Instance.SubmitLoadoutServerRpc(idsToSend);

        Debug.Log("Đã gửi đội hình lên Server!");
    }
}
