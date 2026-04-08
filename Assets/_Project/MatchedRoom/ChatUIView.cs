using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatUIView : MonoBehaviour
{
    public ScrollRect scrollRect;
    public RectTransform contentParent;
    public TextMeshProUGUI textPrefab;
    public int maxMessages = 50;

    private List<TextMeshProUGUI> messagePool = new List<TextMeshProUGUI>();
    private int poolIndex = 0;

    private void Awake()
    {
        for (int i = 0; i < maxMessages; i++)
        {
            TextMeshProUGUI txt = Instantiate(textPrefab, contentParent);
            txt.text = string.Empty;
            txt.gameObject.SetActive(false);
            messagePool.Add(txt);
        }
    }

    public void AddMessage(string message)
    {
        TextMeshProUGUI targetText = messagePool[poolIndex];
        
        targetText.gameObject.SetActive(true);
        targetText.text = message;
        targetText.transform.SetAsLastSibling();

        poolIndex = (poolIndex + 1) % maxMessages;

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}