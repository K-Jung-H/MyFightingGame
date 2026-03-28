using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OnlineMatchingManager : MonoBehaviour
{
    public Button leftSideButton;
    public Button rightSideButton;
    
    public Button p1KeyBindButton;
    public Button p2KeyBindButton;
    
    public Button startButton;

    public TextMeshProUGUI currentSideStatusText;
    public Image currentSideStatusImage;
    
    public TextMeshProUGUI currentKeyBindStatusText;
    public Image currentKeyBindStatusImage;

    public RectTransform roomListContent;
    public GameObject roomBoxPrefab;

    public Side_Select_PanelPresetManager sideSelectManager;

    private int selectedSide = 0;
    private int selectedKeyBind = 0;

    private void Start()
    {
        leftSideButton.onClick.AddListener(() => SelectSide(0));
        rightSideButton.onClick.AddListener(() => SelectSide(1));
        
        p1KeyBindButton.onClick.AddListener(() => SelectKeyBind(0));
        p2KeyBindButton.onClick.AddListener(() => SelectKeyBind(1));

        startButton.onClick.AddListener(OnStartButtonClicked);

        SelectSide(0);
        SelectKeyBind(0);
        
        RefreshRoomListUI();
    }

    private void SelectSide(int side)
    {
        selectedSide = side;
        
        if (currentSideStatusText != null)
        {
            currentSideStatusText.text = (side == 0) ? "Selected: Left Side" : "Selected: Right Side";
        }
        
        if (currentSideStatusImage != null)
        {
            currentSideStatusImage.gameObject.SetActive(true);
        }

        if (sideSelectManager != null)
        {
            sideSelectManager.UpdateSideSelection(side);
        }
    }

    private void SelectKeyBind(int bind)
    {
        selectedKeyBind = bind;

        if (currentKeyBindStatusText != null)
        {
            currentKeyBindStatusText.text = (bind == 0) ? "P1 Keys Mapped" : "P2 Keys Mapped";
        }
        
        if (currentKeyBindStatusImage != null)
        {
            currentKeyBindStatusImage.gameObject.SetActive(true);
        }
    }

    private void OnStartButtonClicked()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.StartOnlineMatch(selectedSide, selectedKeyBind);
        }
    }

    public void RefreshRoomListUI()
    {
        // foreach (Transform child in roomListContent)
        // {
        //     Destroy(child.gameObject);
        // }

        // for (int i = 0; i < 5; i++)
        // {
        //     GameObject roomObj = Instantiate(roomBoxPrefab, roomListContent);
        //     RoomListItem roomItem = roomObj.GetComponent<RoomListItem>();
            
        //     if (roomItem != null)
        //     {
        //         roomItem.Setup(i, $"Fight Club {i}", 1, 2);
        //         roomItem.joinButton.onClick.AddListener(() => RequestJoinRoom(roomItem.currentRoomId));
        //     }
        // }
    }

    private void RequestJoinRoom(int roomId)
    {
        Debug.Log($"Room Join Request Sent for ID: {roomId}");
    }
}