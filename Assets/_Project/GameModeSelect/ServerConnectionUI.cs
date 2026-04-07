using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Net;

public class ServerConnectionUI : MonoBehaviour
{
    public GameObject panelRoot;
    public Button closeButton;
    public TMP_InputField ipInputField;
    public Button joinButton;
    public TextMeshProUGUI statusText;

    public GameObject networkManagersPrefab;

    private void Awake()
    {
        ClearStatus();
    }

    private void Start()
    {
        closeButton.onClick.AddListener(OnCloseClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnEnable()
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnConnectionEstablished += HandleConnectionSuccess;
            ServerNetworkManager.Instance.OnConnectionFailed += HandleConnectionFailed;
        }
    }

    private void OnDisable()
    {
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnConnectionEstablished -= HandleConnectionSuccess;
            ServerNetworkManager.Instance.OnConnectionFailed -= HandleConnectionFailed;
        }
    }

    public void ShowPanel()
    {
        panelRoot.SetActive(true);
        ClearStatus();
        SetInteractable(true);
    }

    public void HidePanel()
    {
        panelRoot.SetActive(false);
    }

    private void OnCloseClicked()
    {
        HidePanel();
    }

    private void OnJoinClicked()
    {
        ClearStatus();
        
        string rawIp = ipInputField.text;
        string validIp = string.Empty;


        if (IPAddress.TryParse(rawIp, out IPAddress parsedAddress))
        {
            validIp = parsedAddress.ToString();
        }
        else if (string.IsNullOrWhiteSpace(rawIp))
        {
            ShowStatusMessage("Invalid IP address format.", Color.red);
            return;
        }
        else
        {
            ShowStatusMessage("Invalid IP address format.", Color.red);
            return;
        }

        SetInteractable(false);
        ShowStatusMessage("Connecting to server...", Color.sandyBrown);

        if (ServerNetworkManager.Instance == null && networkManagersPrefab != null)
        {
            Instantiate(networkManagersPrefab);
            
            ServerNetworkManager.Instance.OnConnectionEstablished += HandleConnectionSuccess;
            ServerNetworkManager.Instance.OnConnectionFailed += HandleConnectionFailed;
        }

        ServerNetworkManager.Instance.InitializeNetwork(validIp, 9000);
    }

    private void HandleConnectionSuccess()
    {
        HidePanel();
        GameFlowManager.Instance.SelectOnlineMode();
    }

    private void HandleConnectionFailed(string errorMessage)
    {
        SetInteractable(true);
        ShowStatusMessage(errorMessage, Color.red);
    }

    private void SetInteractable(bool state)
    {
        closeButton.interactable = state;
        ipInputField.interactable = state;
        joinButton.interactable = state;
    }

    private void ShowStatusMessage(string message, Color color)
    {
        statusText.text = message;
        statusText.color = color;
        statusText.gameObject.SetActive(true);
    }

    private void ClearStatus()
    {
        statusText.text = string.Empty;
        statusText.gameObject.SetActive(false);
    }
}