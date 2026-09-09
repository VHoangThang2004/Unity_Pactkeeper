using UnityEngine;

public class ActionMenuUI : MonoBehaviour
{
    [SerializeField] private ClientController controller;
    [SerializeField] private GameObject actionMenuCanvas;
    public void ShowForUnit(ClientUnit selectedUnit, bool isOwned)
    {
        actionMenuCanvas.SetActive(true);
    }
    public void Hide()
    {
        actionMenuCanvas.SetActive(false);
    }
}