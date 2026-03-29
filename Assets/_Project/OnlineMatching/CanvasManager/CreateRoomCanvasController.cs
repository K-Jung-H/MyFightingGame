using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class CreateRoomCanvasController : MonoBehaviour
{
    public TMP_InputField titleInput;
    public Toggle privateToggle;
    public TMP_InputField passwordInput;
    public Button submitButton;
    public Button closeButton;

    public event Action<string, bool, string> OnSubmitRequested;
    public event Action OnCloseClicked;

    private void Start()
    {
        submitButton.onClick.AddListener(() => OnSubmitRequested?.Invoke(titleInput.text, privateToggle.isOn, passwordInput.text));
        closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());
    }

    public void ClearInputs()
    {
        titleInput.text = string.Empty;
        passwordInput.text = string.Empty;
        privateToggle.isOn = false;
    }
}