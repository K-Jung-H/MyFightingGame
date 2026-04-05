using UnityEngine;
using UnityEngine.UI;
using System;

public class ErrorPanelController : MonoBehaviour
{
    public GameObject passwordErrorPanel;
    public Button btnClosePasswordError;

    public GameObject roomFullErrorPanel;
    public Button btnCloseRoomFullError;

    public event Action OnPasswordErrorClosed;
    public event Action OnRoomFullErrorClosed;

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
    }

    public void ShowPasswordError()
    {
        gameObject.SetActive(true);
        if (passwordErrorPanel != null) passwordErrorPanel.SetActive(true);
        if (roomFullErrorPanel != null) roomFullErrorPanel.SetActive(false);
    }

    public void ShowRoomFullError()
    {
        gameObject.SetActive(true);
        if (roomFullErrorPanel != null) roomFullErrorPanel.SetActive(true);
        if (passwordErrorPanel != null) passwordErrorPanel.SetActive(false);
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
}