using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatController : MonoBehaviour
{
    public ChatUIView uiView;
    public TMP_InputField chatInputField;
    public Button sendButton;

    private void Start()
    {
        sendButton.onClick.AddListener(OnSendClicked);
        chatInputField.onSubmit.AddListener(OnSubmitChat);
    }

    private void OnEnable()
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnChatMessageReceived += HandleChatMessage;
        }
    }

    private void OnDisable()
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnChatMessageReceived -= HandleChatMessage;
        }
    }

    private void OnSendClicked()
    {
        SendMessageToServer();
    }

    private void OnSubmitChat(string text)
    {
        SendMessageToServer();
    }

    private void SendMessageToServer()
    {
        string msg = chatInputField.text;
        
        if (string.IsNullOrWhiteSpace(msg)) return;

        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.SendChatMessage(msg);
        }

        chatInputField.text = string.Empty;
        chatInputField.ActivateInputField();
    }

    private void HandleChatMessage(byte senderType, string message)
    {
        string formattedMessage = string.Empty;

        switch (senderType)
        {
            case 0:
                formattedMessage = $"<color=yellow>[Server] {message}</color>";
                break;
            case 1:
                formattedMessage = $"<color=#00FFCC>[P1] {message}</color>";
                break;
            case 2:
                formattedMessage = $"<color=#FF9999>[P2] {message}</color>";
                break;
            default:
                formattedMessage = $"[Unknown] {message}";
                break;
        }

        uiView.AddMessage(formattedMessage);
    }
}