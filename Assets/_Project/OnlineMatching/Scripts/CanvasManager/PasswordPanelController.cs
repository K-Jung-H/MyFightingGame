using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PasswordPanelController : MonoBehaviour
{
    public GameObject panelInput;
    public TMP_InputField inputPassword;
    public Button btnSubmit;
    public Button btnCloseInput;

    public event Action<string, string> OnSubmitRequested;
    public event Action OnCloseClicked;

    private string currentRoomCode;

    private void Start()
    {
        if (btnSubmit != null) btnSubmit.onClick.AddListener(HandleSubmit);
        if (btnCloseInput != null) btnCloseInput.onClick.AddListener(HandleCloseInput);
    }

    public void OpenPrompt(string roomCode)
    {
        currentRoomCode = roomCode;
        
        if (panelInput != null) panelInput.SetActive(true);
        
        if (inputPassword != null) inputPassword.text = string.Empty;
        if (btnSubmit != null) btnSubmit.interactable = true;
        
        gameObject.SetActive(true);
    }

    public void ClosePrompt()
    {
        if (inputPassword != null) inputPassword.text = string.Empty;
        gameObject.SetActive(false);
    }

    public void ResetForRetry()
    {
        if (panelInput != null) panelInput.SetActive(true);
        
        if (inputPassword != null)
        {
            inputPassword.text = string.Empty;
            inputPassword.ActivateInputField();
        }
        
        if (btnSubmit != null) btnSubmit.interactable = true;
    }

    private void HandleSubmit()
    {
        if (string.IsNullOrEmpty(currentRoomCode)) return;
        
        if (btnSubmit != null) btnSubmit.interactable = false;
        
        OnSubmitRequested?.Invoke(currentRoomCode, inputPassword.text);
    }

    private void HandleCloseInput()
    {
        OnCloseClicked?.Invoke();
    }
}