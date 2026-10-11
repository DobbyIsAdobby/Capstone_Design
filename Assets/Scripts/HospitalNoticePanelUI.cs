using UnityEngine;
using UnityEngine.UI;

public class HospitalNoticePanelUI : MonoBehaviour
{
    [SerializeField] private GameObject noticePanel;
    [SerializeField] private Button confirmButton;

    public bool IsOpen => noticePanel != null && noticePanel.activeInHierarchy;

    private void Awake()
    {
        confirmButton.onClick.AddListener(Close);
        noticePanel.SetActive(false);
    }

    public void Open()
    {
        noticePanel.SetActive(true);

        // 다른 화면보다 앞에 표시.
        noticePanel.transform.SetAsLastSibling();
    }

    public void Close()
    {
        noticePanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(Close);
    }
}
