using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PasswordPromptPanelController : MonoBehaviour
{
    public TMP_InputField passwordInput;
    public Button submitButton;
    public Button closeButton;

    public event Action<string, string> OnSubmitRequested;
    public event Action OnCloseClicked;

    private string pendingRoomCode;

    private void Start()
    {
        submitButton.onClick.AddListener(() => OnSubmitRequested?.Invoke(pendingRoomCode, passwordInput.text));
        closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());
    }

    public void OpenPrompt(string roomCode)
    {
        pendingRoomCode = roomCode;
        passwordInput.text = string.Empty;
        gameObject.SetActive(true);
    }

    public void ClosePrompt()
    {
        pendingRoomCode = string.Empty;
        gameObject.SetActive(false);
    }
}