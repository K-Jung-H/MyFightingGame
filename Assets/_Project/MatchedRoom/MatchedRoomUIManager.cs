using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public struct RuleOption
{
    public Button button;
    public int value;
}

public class MatchedRoomUIManager : MonoBehaviour
{
    [SerializeField] private MatchedRoomManager roomManager;
    
    [SerializeField] private RuleOption[] roundOptions;
    [SerializeField] private RuleOption[] timeOptions;
    
    [SerializeField] private PlayerInfoPanel p1InfoPanel;
    [SerializeField] private PlayerInfoPanel p2InfoPanel;
    
    [SerializeField] private Button readyToggleButton;
    [SerializeField] private GameObject readyStateImage;
    [SerializeField] private Button startMatchButton;
    [SerializeField] private Button leaveButton;

    private void Start()
    {
        foreach (RuleOption option in roundOptions)
        {
            if (option.button != null)
            {
                int capturedValue = option.value;
                option.button.onClick.AddListener(() => OnRoundRadioClicked(capturedValue));
            }
        }

        foreach (RuleOption option in timeOptions)
        {
            if (option.button != null)
            {
                int capturedValue = option.value;
                option.button.onClick.AddListener(() => OnTimeRadioClicked(capturedValue));
            }
        }

        readyToggleButton.onClick.AddListener(OnReadyClicked);
        startMatchButton.onClick.AddListener(OnStartClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    public void RefreshUI(RoomStateModel model, int localSlot)
    {
        bool isHost = (localSlot == 0);
        bool isP2Present = model.isP2Connected; 

        foreach (RuleOption option in roundOptions)
        {
            if (option.button == null) continue;
            option.button.interactable = isHost;
            UpdateRadioButtonVisual(option.button, model.maxRounds == option.value);
        }

        foreach (RuleOption option in timeOptions)
        {
            if (option.button == null) continue;
            option.button.interactable = isHost;
            UpdateRadioButtonVisual(option.button, model.roundTimeLimit == option.value);
        }

        p1InfoPanel.UpdatePanel("Player 1", 0, model.p1Wins, model.p1Losses, model.isP1Ready, model.isP1Connected);
        p2InfoPanel.UpdatePanel("Player 2", 0, model.p2Wins, model.p2Losses, model.isP2Ready, isP2Present);

        readyToggleButton.gameObject.SetActive(true);
        startMatchButton.gameObject.SetActive(isHost);
        
        bool canStart = model.isP1Ready && (isP2Present ? model.isP2Ready : false);
        startMatchButton.interactable = canStart;

        if (readyStateImage != null)
        {
            bool isLocalReady = isHost ? model.isP1Ready : model.isP2Ready;
            readyStateImage.SetActive(isLocalReady);
        }
    }

    private void UpdateRadioButtonVisual(Button btn, bool isSelected)
    {
        ColorBlock cb = btn.colors;
        
        if (isSelected)
        {
            cb.normalColor = Color.green;
            cb.selectedColor = Color.green;
            cb.disabledColor = Color.green;
        }
        else
        {
            cb.normalColor = Color.white;
            cb.selectedColor = Color.white;
            cb.disabledColor = ColorBlock.defaultColorBlock.disabledColor;
        }
        
        btn.colors = cb;
    }

    private void OnRoundRadioClicked(int value)
    {
        if (RoomStateManager.Instance == null) return;
        int currentTime = RoomStateManager.Instance.roomModel.roundTimeLimit;
        roomManager.RequestRuleUpdate(value, currentTime);
    }

    private void OnTimeRadioClicked(int value)
    {
        if (RoomStateManager.Instance == null) return;
        int currentRounds = RoomStateManager.Instance.roomModel.maxRounds;
        roomManager.RequestRuleUpdate(currentRounds, value);
    }

    private void OnReadyClicked()
    {
        roomManager.ToggleReadyState();
    }

    private void OnStartClicked()
    {
        roomManager.AttemptStartMatch();
    }

    private void OnLeaveClicked()
    {
        roomManager.LeaveRoom();
    }
}