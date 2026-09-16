using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class CommandLog : MonoBehaviour, IPointerClickHandler
{
    public TextMeshProUGUI textField;
    public ClientScene scene;



    public void OnPointerClick(PointerEventData eventData)
    {
        // Find which character index was clicked relative to the screen position
        int charIndex = TMP_TextUtilities.FindIntersectingCharacter(textField, eventData.position, eventData.pressEventCamera, true);

        if (charIndex != -1)
        {
            // Identify if the character belongs to a <link> tag group
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(textField, eventData.position, eventData.pressEventCamera);

            if (linkIndex != -1)
            {
                TMP_LinkInfo linkInfo = textField.textInfo.linkInfo[linkIndex];
                string linkId = linkInfo.GetLinkID();

                // Fire your custom logic based on the tag ID
                ExecuteLinkAction(linkId);
            }
        }
    }

    private void ExecuteLinkAction(string id)
    {
        string[] parts = id.Split(' ');
        if (parts.Length == 0) return;

        switch (parts[0])
        {
            case "ID":
                if (parts.Length == 2)
                {
                    // Just unit — inspect unit
                    int unitId = int.Parse(parts[1]);
                    OnUnitLinkClicked(unitId);
                }
                else if (parts.Length == 4 && parts[2] == "SID")
                {
                    // Unit + skill — inspect skill in context of that unit
                    int unitId = int.Parse(parts[1]);
                    int skillId = int.Parse(parts[3]);
                    OnSkillLinkClicked(unitId, skillId);
                }
                break;

            case "Cell":
                if (parts.Length < 3) return;
                int x = int.Parse(parts[1]);
                int y = int.Parse(parts[2]);
                OnCellLinkClicked(x, y);
                break;

            default:
                Debug.LogWarning($"[CommandLog] Unknown link: {id}");
                break;
        }
    }

    private void OnUnitLinkClicked(int unitId)
    {
        Debug.Log($"[CommandLog] Unit {unitId} clicked");
        // TODO: open unit inspect panel
        scene.clientInteractionSystem.ForceNoneState();
        scene.clientInteractionSystem.HandleDecisionSelectUnit(unitId);

    }

    private void OnSkillLinkClicked(int unitId, int skillId)
    {
        Debug.Log($"[CommandLog] Skill {skillId} on uId {unitId} clicked");
        // TODO: open skill inspect panel
        scene.clientInteractionSystem.ForceNoneState();
        scene.clientInteractionSystem.HandleDecisionSelectUnit(unitId);
        scene.clientInteractionSystem.HandleDecisionInspectSkill(skillId);
    }

    private void OnCellLinkClicked(int x, int y)
    {
        Debug.Log($"[CommandLog] Cell ({x},{y}) clicked");
        // TODO: camera center on cell
        scene.clientInteractionSystem.ForceNoneState();
        scene.cameraController.CenterOn(new Vector3Int(x, y, 0));
        scene.visualController.HighlightCell(new Vector3Int(x, y, 0));
    }
}