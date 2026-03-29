using UnityEngine;
using UnityEngine.UI;
using System;

public class SearchResultCanvasController : MonoBehaviour
{
    public Transform contentTransform;
    public GameObject roomListItemPrefab;
    public Button closeButton;

    public event Action OnCloseClicked;
    public event Action<string, bool> OnJoinRoomClicked;

    private void Start()
    {
        closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());
    }

    public void PopulateList(RoomMetadata[] rooms)
    {
        foreach (Transform child in contentTransform)
        {
            Destroy(child.gameObject);
        }

        foreach (var room in rooms)
        {
            GameObject obj = Instantiate(roomListItemPrefab, contentTransform);
            RoomListItem item = obj.GetComponent<RoomListItem>();
            
            if (item != null)
            {
                item.Setup(room.RoomCode, room.RoomTitle, room.PlayerCount, 2);
                item.joinButton.onClick.AddListener(() => OnJoinRoomClicked?.Invoke(room.RoomCode, room.HasPassword));
            }
        }
    }
}