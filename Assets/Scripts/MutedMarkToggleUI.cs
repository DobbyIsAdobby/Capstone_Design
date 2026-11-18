using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼 연결이 필수임
/// 배경음 버튼의 경우 Bgm 설정도 변경.
/// </summary>
[RequireComponent(typeof(Button))]
public class MutedMarkToggleUI : MonoBehaviour
{
    [SerializeField] private GameObject mutedMark;

    [Tooltip("배경음 버튼에서만 체크")]
    [SerializeField] private bool controlsBgm;

    private Button toggleButton;

    private void Awake()
    {
        toggleButton = GetComponent<Button>();

        if (mutedMark == null)
        {
            Debug.LogError("MutedMark를 연결하세요.", this);
            return;
        }

        toggleButton.onClick.AddListener(ToggleMutedMark);
    }

    private void OnEnable()
    {
        // 로비에 다시 돌아오거나 버튼이 다시 켜질 때, 저장된 음소거 상태와 표시를 일치시킴.
        if (controlsBgm)
        {
            RefreshBgmMark();
        }
    }

    private void ToggleMutedMark()
    {
        if (mutedMark == null)
        {
            return;
        }

        if (controlsBgm)
        {
            // 현재 설정의 반대 상태로 변경.
            BgmManager.SetMuted(!BgmManager.IsMuted);

            // 바뀐 설정을 x 표시에도 반영.
            RefreshBgmMark();
        }
        else
        {
            // 효과음 제어는 효과음 시스템을 연결할 때 추가.
            mutedMark.SetActive(!mutedMark.activeSelf);
        }
    }

    private void RefreshBgmMark()
    {
        if (mutedMark != null)
        {
            // 음소거 상태일 때만 x 표시 활성화
            mutedMark.SetActive(BgmManager.IsMuted);
        }
    }

    private void OnDestroy()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleMutedMark);
        }
    }
}
