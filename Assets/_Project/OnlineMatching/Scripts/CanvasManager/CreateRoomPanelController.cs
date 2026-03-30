using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public struct RoomCreateData
{
    public string RoomName;
    public bool IsPrivate;
    public bool UsePassword;
    public string Password;
}

public class CreateRoomPanelController : MonoBehaviour
{
    public TMP_InputField inputRoomName;
    public Toggle toggleRoomPrivate;
    public Toggle toggleRoomPassword;
    public TMP_InputField inputRoomPassword;
    public Button buttonCreateRoom;
    public Button buttonCancel;

    public event Action<RoomCreateData> OnSubmitRequested;
    public event Action OnCloseClicked;

    private void Start()
    {
        buttonCreateRoom.onClick.AddListener(SubmitCreateRequest);
        buttonCancel.onClick.AddListener(() => OnCloseClicked?.Invoke());

        toggleRoomPassword.onValueChanged.AddListener(OnPasswordToggleChanged);
        
        OnPasswordToggleChanged(toggleRoomPassword.isOn);
    }

    private void SubmitCreateRequest()
    {
        bool usePassword = toggleRoomPassword.isOn;
        
        RoomCreateData createData = new RoomCreateData
        {
            RoomName = inputRoomName.text,
            IsPrivate = toggleRoomPrivate.isOn,
            UsePassword = usePassword,
            Password = usePassword ? inputRoomPassword.text : string.Empty
        };

        OnSubmitRequested?.Invoke(createData);
    }

    private void OnPasswordToggleChanged(bool isOn)
    {
        if (inputRoomPassword != null)
        {
            inputRoomPassword.gameObject.SetActive(isOn);
        }
    }

    public void ClearInputs()
    {
        if (inputRoomName != null) inputRoomName.text = string.Empty;
        if (inputRoomPassword != null) inputRoomPassword.text = string.Empty;
        if (toggleRoomPrivate != null) toggleRoomPrivate.isOn = false;
        
        if (toggleRoomPassword != null)
        {
            toggleRoomPassword.isOn = false;
            OnPasswordToggleChanged(false);
        }
    }
}