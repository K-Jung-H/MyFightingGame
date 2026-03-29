using UnityEngine;
using System;

public class LobbyUIManager : MonoBehaviour
{
    public BaseLobbyCanvasController baseCanvas;
    public CreateRoomCanvasController createRoomCanvas;
    public SearchRoomCanvasController searchRoomCanvas;
    public SearchResultCanvasController searchResultCanvas;
    public PasswordPromptCanvasController passwordCanvas;

    public event Action<string, bool, string> OnCreateRoomRequested;
    public event Action<string> OnTitleSearchRequested;
    public event Action<string> OnCodeJoinRequested;
    public event Action<string, string> OnJoinWithPasswordRequested;

    private void Start()
    {
        baseCanvas.OnOpenCreateClicked += () => 
        { 
            createRoomCanvas.ClearInputs(); 
            SwitchCanvas(createRoomCanvas.gameObject); 
        };
        baseCanvas.OnOpenJoinSearchClicked += () => SwitchCanvas(searchRoomCanvas.gameObject);

        createRoomCanvas.OnSubmitRequested += (title, isPriv, pwd) => OnCreateRoomRequested?.Invoke(title, isPriv, pwd);
        createRoomCanvas.OnCloseClicked += () => SwitchCanvas(null);

        searchRoomCanvas.OnTitleSearchRequested += (title) => OnTitleSearchRequested?.Invoke(title);
        searchRoomCanvas.OnCodeJoinRequested += (code) => OnCodeJoinRequested?.Invoke(code);
        searchRoomCanvas.OnCloseClicked += () => SwitchCanvas(null);

        searchResultCanvas.OnJoinRoomClicked += (code, hasPwd) =>
        {
            if (hasPwd) passwordCanvas.OpenPrompt(code);
            else OnJoinWithPasswordRequested?.Invoke(code, string.Empty);
        };
        searchResultCanvas.OnCloseClicked += () => SwitchCanvas(searchRoomCanvas.gameObject);

        passwordCanvas.OnSubmitRequested += (code, pwd) =>
        {
            OnJoinWithPasswordRequested?.Invoke(code, pwd);
            passwordCanvas.ClosePrompt();
        };
        passwordCanvas.OnCloseClicked += () => passwordCanvas.ClosePrompt();

        SwitchCanvas(null);
    }

    private void SwitchCanvas(GameObject targetCanvas)
    {
        createRoomCanvas.gameObject.SetActive(false);
        searchRoomCanvas.gameObject.SetActive(false);
        searchResultCanvas.gameObject.SetActive(false);
        passwordCanvas.gameObject.SetActive(false);

        if (targetCanvas != null) targetCanvas.SetActive(true);
    }

    public void ShowSearchResults(RoomMetadata[] rooms)
    {
        SwitchCanvas(searchResultCanvas.gameObject);
        searchResultCanvas.PopulateList(rooms);
    }

    public void OpenPasswordPrompt(string roomCode)
    {
        passwordCanvas.OpenPrompt(roomCode);
    }
}