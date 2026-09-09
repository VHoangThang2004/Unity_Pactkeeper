using UnityEngine;

public class ActionMenuUI : MonoBehaviour
{
    [SerializeField] private ClientController controller;
    [SerializeField] private GameObject actionMenuCanvas;
    [SerializeField] private GameObject standByButton;
    public void ShowForUnit(ClientUnit selectedUnit)
    {
        standByButton.SetActive(controller.clientSession.IsMyUnit(selectedUnit.data.Id) && controller.clientSession.IsMyTurn() && controller.clientSession.LastDecisionRequest.ActionDuration > 0);
        actionMenuCanvas.SetActive(true);
    }
    public void Hide()
    {
        actionMenuCanvas.SetActive(false);
    }
}