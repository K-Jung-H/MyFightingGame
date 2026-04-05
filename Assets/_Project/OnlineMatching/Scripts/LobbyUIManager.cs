using UnityEngine;
using System;

public class LobbyUIManager : MonoBehaviour
{
    public BaseLobbyCanvasController baseCanvas;
    public CreateRoomPanelController createRoomPanel;
    public RoomSearchPanelController searchPanel;
    public PasswordPanelController passwordPanel;
    public ErrorPanelController errorPanel;

    public event Action<RoomCreateData> OnCreateRoomRequested;
    public event Action<string> OnTitleSearchRequested;
    public event Action<string> OnCodeJoinRequested;
    public event Action<string, string> OnJoinWithPasswordRequested;

    private void Start()
    {
        if (baseCanvas != null)
        {
            baseCanvas.OnOpenCreateClicked += OpenCreateRoomCanvas;
            baseCanvas.OnOpenJoinSearchClicked += OpenSearchRoomCanvas;
        }

        if (createRoomPanel != null)
        {
            createRoomPanel.OnSubmitRequested += (data) => OnCreateRoomRequested?.Invoke(data);
            createRoomPanel.OnCloseClicked += CloseAllOverlayCanvases;
        }

        if (searchPanel != null)
        {
            searchPanel.OnSearchRequested += HandleSearchRequested;
            searchPanel.OnJoinRequested += HandleJoinRequested;
            searchPanel.OnCloseClicked += CloseAllOverlayCanvases;
        }

        if (passwordPanel != null)
        {
            passwordPanel.OnSubmitRequested += (code, pwd) =>
            {
                OnJoinWithPasswordRequested?.Invoke(code, pwd);
            };
            passwordPanel.OnCloseClicked += () => passwordPanel.ClosePrompt();
        }

        if (errorPanel != null)
        {
            errorPanel.OnPasswordErrorClosed += HandlePasswordErrorClosed;
            errorPanel.OnRoomFullErrorClosed += HandleRoomFullErrorClosed;
            errorPanel.gameObject.SetActive(false);
        }

        SwitchCanvas(null);
    }

    private void HandleSearchRequested(byte searchType, string query)
    {
        if (searchType == 0)
        {
            OnTitleSearchRequested?.Invoke(query);
        }
        else if (searchType == 1)
        {
            OnCodeJoinRequested?.Invoke(query);
        }
    }

    private void HandleJoinRequested(string roomCode, bool hasPassword)
    {
        if (hasPassword)
        {
            OpenPasswordPrompt(roomCode);
        }
        else
        {
            OnJoinWithPasswordRequested?.Invoke(roomCode, string.Empty);
        }
    }
    
    public void HandlePasswordFailure()
    {
        if (passwordPanel != null && passwordPanel.gameObject.activeSelf)
        {
            passwordPanel.panelInput.SetActive(false);
        }

        if (errorPanel != null)
        {
            errorPanel.ShowPasswordError();
        }
    }
    
    public void HandleRoomFullFailure()
    {
        if (passwordPanel != null && passwordPanel.gameObject.activeSelf)
        {
            passwordPanel.panelInput.SetActive(false);
        }

        if (errorPanel != null)
        {
            errorPanel.ShowRoomFullError();
        }
    }

    private void HandlePasswordErrorClosed()
    {
        if (passwordPanel != null && passwordPanel.gameObject.activeSelf)
        {
            passwordPanel.ResetForRetry();
        }
    }

    private void HandleRoomFullErrorClosed()
    {
        if (passwordPanel != null && passwordPanel.gameObject.activeSelf)
        {
            passwordPanel.ClosePrompt();
        }
    }

    public void OpenCreateRoomCanvas()
    {
        if (createRoomPanel != null)
        {
            createRoomPanel.ClearInputs();
            SwitchCanvas(createRoomPanel.gameObject);
        }
    }

    public void OpenSearchRoomCanvas()
    {
        if (searchPanel != null)
        {
            searchPanel.ClearPanel();
            SwitchCanvas(searchPanel.gameObject);
        }
    }

    public void CloseAllOverlayCanvases()
    {
        SwitchCanvas(null);
    }

    private void SwitchCanvas(GameObject targetCanvas)
    {
        if (createRoomPanel != null) createRoomPanel.gameObject.SetActive(false);
        if (searchPanel != null) searchPanel.gameObject.SetActive(false);
        if (passwordPanel != null) passwordPanel.gameObject.SetActive(false);

        if (targetCanvas != null) targetCanvas.SetActive(true);
    }

    public void ShowSearchResults(RoomMetadata[] rooms)
    {
        if (searchPanel != null)
        {
            SwitchCanvas(searchPanel.gameObject);
            searchPanel.PopulateList(rooms);
        }
    }

    public void OpenPasswordPrompt(string roomCode)
    {
        if (passwordPanel != null)
        {
            passwordPanel.OpenPrompt(roomCode);
        }
    }
}