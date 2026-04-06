using UnityEngine;
using UnityEngine.UI;
using System;

public class ErrorPanelController : MonoBehaviour
{
    public GameObject passwordErrorPanel;
    public Button btnClosePasswordError;

    public GameObject roomFullErrorPanel;
    public Button btnCloseRoomFullError;

    public GameObject noRoomsErrorPanel;
    public Button btnCloseNoRoomsError;

    public event Action OnPasswordErrorClosed;
    public event Action OnRoomFullErrorClosed;
    public event Action OnNoRoomsErrorClosed;


    private void Start()
    {
        if (btnClosePasswordError != null)
        {
            btnClosePasswordError.onClick.AddListener(HandleClosePasswordError);
        }

        if (btnCloseRoomFullError != null)
        {
            btnCloseRoomFullError.onClick.AddListener(HandleCloseRoomFullError);
        }

        if (btnCloseNoRoomsError != null)
        {
            btnCloseNoRoomsError.onClick.AddListener(HandleCloseNoRoomsError);
        }
    }

    public void ShowPasswordError()
    {
        gameObject.SetActive(true);
        if (passwordErrorPanel != null) passwordErrorPanel.SetActive(true);
        if (roomFullErrorPanel != null) roomFullErrorPanel.SetActive(false);
        if (noRoomsErrorPanel != null) noRoomsErrorPanel.SetActive(false);
    }

    public void ShowRoomFullError()
    {
        gameObject.SetActive(true);
        if (roomFullErrorPanel != null) roomFullErrorPanel.SetActive(true);
        if (passwordErrorPanel != null) passwordErrorPanel.SetActive(false);
        if (noRoomsErrorPanel != null) noRoomsErrorPanel.SetActive(false);

    }

    public void ShowNoRoomsError()
    {
        gameObject.SetActive(true);
        if (noRoomsErrorPanel != null) noRoomsErrorPanel.SetActive(true);
        if (passwordErrorPanel != null) passwordErrorPanel.SetActive(false);
        if (roomFullErrorPanel != null) roomFullErrorPanel.SetActive(false);
    }

    private void HandleClosePasswordError()
    {
        if (passwordErrorPanel != null) passwordErrorPanel.SetActive(false);
        gameObject.SetActive(false);
        OnPasswordErrorClosed?.Invoke();
    }

    private void HandleCloseRoomFullError()
    {
        if (roomFullErrorPanel != null) roomFullErrorPanel.SetActive(false);
        gameObject.SetActive(false);
        OnRoomFullErrorClosed?.Invoke();
    }

    private void HandleCloseNoRoomsError()
    {
        if (noRoomsErrorPanel != null) noRoomsErrorPanel.SetActive(false);
        gameObject.SetActive(false);
        OnNoRoomsErrorClosed?.Invoke();
    }
}