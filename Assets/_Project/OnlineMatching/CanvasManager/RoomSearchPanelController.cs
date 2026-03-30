using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class RoomSearchPanelController : MonoBehaviour
{
    public TMP_InputField inputRoomName;
    public Button btnSearchName;
    public TMP_InputField inputRoomCode;
    public Button btnSearchCode;
    
    public Transform contentTransform;
    public GameObject roomListItemPrefab;
    public Button btnRefresh;
    public Button btnEnter;
    public Button btnClose;

    private RoomListItem currentSelectedItem;
    public GameObject objNoResult;

    public event Action<byte, string> OnSearchRequested;
    public event Action<string, bool> OnJoinRequested;
    public event Action OnCloseClicked;

    private byte lastSearchType;
    private string lastSearchQuery;
    private string selectedRoomCode;
    private bool isPasswordRequired;

    private void Start()
    {
        btnSearchName.onClick.AddListener(RequestNameSearch);
        btnSearchCode.onClick.AddListener(RequestCodeSearch);
        btnRefresh.onClick.AddListener(RequestRefresh);
        btnEnter.onClick.AddListener(RequestJoinSelected);
        btnClose.onClick.AddListener(() => OnCloseClicked?.Invoke());
    }

    public void ClearPanel()
    {
        inputRoomName.text = string.Empty;
        inputRoomCode.text = string.Empty;
        lastSearchType = 0;
        lastSearchQuery = string.Empty;
        selectedRoomCode = string.Empty;
        isPasswordRequired = false;
        btnEnter.interactable = false;
        ClearListItems();
    }

    public void PopulateList(RoomMetadata[] rooms)
    {
        ClearListItems();
        selectedRoomCode = string.Empty;
        currentSelectedItem = null;
        btnEnter.interactable = false;

        if (rooms == null || rooms.Length == 0)
        {
            if (objNoResult != null) objNoResult.SetActive(true);
            return;
        }

        if (objNoResult != null) objNoResult.SetActive(false);

        foreach (var room in rooms)
        {
            GameObject obj = Instantiate(roomListItemPrefab, contentTransform);
            RoomListItem item = obj.GetComponent<RoomListItem>();
            
            if (item != null)
            {
                item.Setup(room.RoomCode, room.RoomTitle, room.PlayerCount, 2, room.HasPassword);
                item.btnSelect.onClick.AddListener(() => OnRoomItemSelected(item, room.RoomCode, room.HasPassword));
            }
        }
    }

    private void RequestNameSearch()
    {
        lastSearchType = 0;
        lastSearchQuery = inputRoomName.text;
        OnSearchRequested?.Invoke(lastSearchType, lastSearchQuery);
    }


    private void RequestCodeSearch()
    {
        lastSearchType = 1;
        lastSearchQuery = inputRoomCode.text.ToUpper();
        OnSearchRequested?.Invoke(lastSearchType, lastSearchQuery);
    }


    private void RequestRefresh()
    {
        if (string.IsNullOrEmpty(lastSearchQuery)) return;
        OnSearchRequested?.Invoke(lastSearchType, lastSearchQuery);
    }

    private void RequestJoinSelected()
    {
        if (string.IsNullOrEmpty(selectedRoomCode)) return;
        OnJoinRequested?.Invoke(selectedRoomCode, isPasswordRequired);
    }

    private void OnRoomItemSelected(RoomListItem clickedItem, string roomCode, bool hasPassword)
    {
        if (currentSelectedItem != null)
        {
            currentSelectedItem.SetSelected(false);
        }

        currentSelectedItem = clickedItem;
        
        if (currentSelectedItem != null)
        {
            currentSelectedItem.SetSelected(true);
        }

        selectedRoomCode = roomCode;
        isPasswordRequired = hasPassword;
        btnEnter.interactable = true;
    }

    private void ClearListItems()
    {
        foreach (Transform child in contentTransform)
        {
            Destroy(child.gameObject);
        }
    }
}