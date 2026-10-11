using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼 연결이 필수임
/// </summary>
[RequireComponent(typeof(Button))]
public class MutedMarkToggleUI : MonoBehaviour
{
    [SerializeField] private GameObject mutedMark;

    private Button toggleButton;

    private void Awake()
    {
        toggleButton = GetComponent<Button>();

        if (mutedMark == null)
        {
            Debug.LogError("MutedMark를 연결하세요.", this);
            return;
        }

        // 버튼을 클릭하면 표시 상태를 반환함
        toggleButton.onClick.AddListener(ToggleMutedMark);
    }

    private void ToggleMutedMark()
    {
        // 꺼져있으면 켜고, 켜져있으면 끔
        mutedMark.SetActive(!mutedMark.activeSelf);
    }

    private void OnDestroy()
    {
        // 이벤트 제거
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleMutedMark);
        }
    }
}
