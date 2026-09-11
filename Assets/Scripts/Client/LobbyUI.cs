using TMPro;
using Unity.VisualScripting;
using UnityEngine;
public class LobbyUI : MonoBehaviour
{
    [SerializeField] TMP_InputField unit1IdInput;
    [SerializeField] TMP_InputField unit2IdInput;
    public void OnClickReady()
    {
        // 1.Read the ID number you entered in the UI(assuming the default skill is 1 or 2).
        int u1 = int.Parse(unit1IdInput.text);
        int u2 = int.Parse(unit2IdInput.text);
        NetUnitLoadout unit1Data = new NetUnitLoadout { uId = u1, movementSkillId = 1, weaponSkillId = 3 };
        NetUnitLoadout unit2Data = new NetUnitLoadout { uId = u2, movementSkillId = 2, weaponSkillId = 4 };

        // 2. Send to Server
        LobbyManager.Instance.SubmitLoadoutServerRpc(unit1Data, unit2Data);

        // 3. Lock the UI and wait for the opponent
        Debug.Log("Đã gửi data, đang chờ đối thủ...");
    }
}