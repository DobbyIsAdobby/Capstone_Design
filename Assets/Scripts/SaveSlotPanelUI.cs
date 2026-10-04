using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SaveSlotPanelUI : MonoBehaviour
{
    /// <summary>
    /// 슬롯 한 개에 필요한 UI Reference. 
    /// Inspector에서 총 세 개 연결.
    /// </summary>
    [Serializable]
    private sealed class SlotView
    {
        public Button button;
        public TMP_Text slotNameText;
        public TMP_Text summaryText;
    }

    [Header("Slots")]
    [SerializeField] private SlotView[] slots = new SlotView[3];
    [SerializeField] private TMP_Text titleText;

    [Header("Owner - Connect Only One")]
    [SerializeField] private PausePanelUI pauseUI;
    [SerializeField] private LobbyPanelUI lobbyUI;

    // true면 저장, false면 불러오기 모드.
    private bool saveMode;

    // 확인 팝업 때문에 입력을 막았는지 나타냄.
    private bool blocked;

    // 저장, 불러오기 작업 중 중복 입력을 막음.
    private bool busy;

    // 파일을 조회해서 확인한 각 슬롯 상태.
    private readonly SaveSlotStatus[] statuses = new SaveSlotStatus[3];

    // 등록했던 이벤트를 OnDestroy에서 해제하기 위해 보관.
    private readonly UnityAction[] handlers = new UnityAction[3];

    private SaveValidationConfig Config => SceneTransitionManager.Instance != null ? SceneTransitionManager.Instance.ValidationConfig : null;

    /// <summary>
    /// 세 슬롯 버튼에 클릭 이벤트 연결. 
    /// Inspector내 On Click에는 별도로 연결하지 않음.
    /// </summary>
    private void Awake()
    {
        for (int i = 0; i < 3; i++)
        {
            // 각 버튼이 사용할 인덱스를 별도로 보관
            int index = i;

            handlers[i] = () => OnSlotClicked(index);
            slots[i].button.onClick.AddListener(handlers[i]);
        }
    }

    /// <summary>
    /// 저장 모드로 설정하고 슬롯 파일을 조회.
    /// 패널 활성화는 PausePanelUI가 먼저 처리함.
    /// </summary>
    public void OpenSave()
    {
        saveMode = true;
        blocked = false;

        titleText.text = "저장하기";
        RefreshSlots();
    }

    /// <summary>
    /// 불러오기 모드로 설정하고 슬롯 파일을 조회.
    /// </summary>
    public void OpenLoad()
    {
        saveMode = false;
        blocked = false;

        titleText.text = "불러오기";
        RefreshSlots();
    }

    /// <summary>
    /// 확인 팝업이 열리면 true, 닫히면 false를 전달.
    /// false여도 빈 불러오기 슬롯까지 활성화되지는 않음.
    /// </summary>
    /// <param name="value"></param>
    public void SetBlocked(bool value)
    {
        blocked = value;
        ApplyInteraction();
    }

    /// <summary>
    /// 세 파일의 상태와 요약을 읽어 화면에 표시.
    /// 게임 상태를 복원하는 메서드는 아님.
    /// </summary>
    public void RefreshSlots()
    {
        for (int i = 0; i < 3; i++)
        {
            slots[i].slotNameText.text = $"세이브 파일 {i + 1}";

            statuses[i] = SaveFileService.Inspect(i + 1, Config, out GameSaveData data, out _);

            switch (statuses[i])
            {
                case SaveSlotStatus.Empty:
                    slots[i].summaryText.text = "비어있음";
                    break;

                case SaveSlotStatus.Invalid:
                    slots[i].summaryText.text = "불러올 수 없는 저장 데이터";
                    break;

                case SaveSlotStatus.Ready:
                    // 게임 속 날짜는 저장된 턴으로 계산
                    DateTime gameDate = new DateTime(2030, 1, 1).AddMonths(data.player.currentMonth - 1);

                    // 저장 시간은 기기의 현지 시간으로 표시
                    DateTimeOffset savedTime = DateTimeOffset.Parse(data.savedAtUTC).ToLocalTime();

                    slots[i].summaryText.text =
                        $"{gameDate.Year}년 {gameDate.Month}월 · " +
                        $"{data.player.currentMonth}/60턴\n" +
                        $"{data.summary.jobGradeName} · " +
                        $"{data.summary.netWorth:N0}원\n" +
                        $"저장: {savedTime:yyyy-MM-dd HH:mm}";
                    break;
            }
        }

        ApplyInteraction();
    }

    /// <summary>
    /// 현재 모드와 파일 상태에 따라 버튼 활성 여부를 결정.
    ///
    /// 저장:
    /// 게임이 저장 가능한 상태라면 빈 슬롯과 기존 슬롯 모두 선택 가능.
    ///
    /// 불러오기:
    /// 정상 파일인 Ready 슬롯만 선택 가능.
    /// 
    /// 확인 팝업이 열려 있거나 작업 중이면 모든 슬롯을 차단함.
    /// </summary>
    private void ApplyInteraction()
    {
         // 팝업 표시 중이거나 저장, 불러오기 처리 중이면 인터랙션을 막음.
        bool available = !blocked && !busy;

        for (int i = 0; i < 3; i++)
        {
            bool selectable;

            if (saveMode)
            {
                // 저장 모드에서는 빈 슬롯과 기존 슬롯 모두 선택할 수 있음.
                // 단, 게임 자체가 저장 가능한 상태여야 함.
                selectable = GameManager.Instance != null && GameManager.Instance.CanSaveCurrentState;
            }
            else
            {
                // 불러오기는 정상 파일이 있는 슬롯만 허용
                selectable = statuses[i] == SaveSlotStatus.Ready;
            }

            // 해당 버튼의 인터랙션 비활성화.
            slots[i].button.interactable = available && selectable;
        }
    }

    /// <summary>
    /// 슬롯 버튼을 눌렀을 때 실행됨.
    /// index는 배열 인덱스인 0~2, 파일 슬롯 번호는 1~3.
    /// </summary>
    /// <param name="index"></param>
    private void OnSlotClicked(int index)
    {
        if (blocked || busy)
            return;

        int slot = index + 1;

        if (saveMode)
        {
            if (SaveFileService.Exists(slot))
            {
                // 기존 파일이 있으면 유저 확인 후 저장.
                ShowConfirmation(
                    "저장된 기록을 지우고 현재 게임 내용을 덮어씌우시겠습니까?",
                    () => Save(slot, true));
            }
            else
            {
                // 빈 슬롯은 별도 덮어쓰기 확인 없이 저장.
                Save(slot, false);
            }

            return;
        }

        // 로비와 게임에서는 경고 문구가 다름.
        string message = pauseUI != null
            ? "현재 진행 중인 내용이 저장되지 않습니다.\n" +
              "저장하신 게임을 불러오시겠습니까?"
            : "선택한 게임을 불러오시겠습니까?";

        ShowConfirmation(message, () => Load(slot));
    }

    /// <summary>
    /// 저장 요청.
    /// Capture는 확인창을 연 시점이 아니라 유저가 저장을 확정한 시점에 호출.
    /// </summary>
    /// <param name="slot"></param>
    /// <param name="overwrite"></param>
    private void Save(int slot, bool overwrite)
    {
        busy = true;
        ApplyInteraction();

        string message;

        try
        {
            // 현재 Manager 상태를 파일용 데이터로 복사
            GameSaveData data = GameSaveCoordinator.Capture();

            // 파일 기록 성공 여부를 받아옴.
            bool succeeded = SaveFileService.TryWrite(slot, data, Config, overwrite, out string error);

            message = succeeded ? "현재 게임 저장이 완료되었습니다." : error;
        }
        catch (Exception exception)
        {
            // 상태 수집 자체가 실패한 경우도 안내
            message = $"저장하지 못했습니다.\n{exception.Message}";
        }
        finally
        {
            busy = false;
        }

        // 저장이 성공했다면 슬롯에 새 요약 내용이 보이고, 실패했다면 기존 파일 상태가 다시 표시됨.
        RefreshSlots();
        ShowMessage(message);
    }

    /// <summary>
    /// 불러오기 요청.
    /// 목록을 표시한 이후 파일이 변경될 수 있음. 확인창에서 ApplyButton을 누른 뒤 다시 읽고 검증.
    /// </summary>
    /// <param name="slot"></param>
    private void Load(int slot)
    {
        busy = true;
        ApplyInteraction();

        string error = "";
        bool succeeded = false;

        try
        {
            bool readSucceeded = SaveFileService.TryRead(slot, Config, out GameSaveData data, out error);

            if (readSucceeded)
            {
                if (SceneTransitionManager.Instance == null)
                {
                    error = "씬 전환 관리자가 없습니다.";
                }
                else
                {
                    // 로비인지 게임인지에 따른 처리는 전환 관리자가 결정
                    succeeded = SceneTransitionManager.Instance.TryLoadGame(data, out error);
                }
            }
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }
        finally
        {
            busy = false;
        }

        if (!succeeded)
        {
            RefreshSlots();
            ShowMessage(error);
        }

        // 성공 시에는 전환 관리자가 Scene을 이동하거나 Panel을 닫음.
    }

    /// <summary>
    /// 현재 슬롯 UI가 속한 화면의 안내 팝업을 사용.
    /// Pause UI와 Lobby UI 중 하나만 연결해야 함.
    /// </summary>
    /// <param name="message"></param>
    private void ShowMessage(string message)
    {
        if (pauseUI != null)
            pauseUI.ShowMessage(message);
        else
            lobbyUI.ShowMessage(message);
    }

    /// <summary>
    /// 기존 확인 팝업에 문구와 ApplyButton을 눌렀을 때의 작업을 전달.
    /// 이 메서드 호출만으로 저장이나 복원이 실행되지는 않음.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="onApply"></param>
    private void ShowConfirmation(string message, Action onApply)
    {
        if (pauseUI != null)
            pauseUI.ShowConfirmation(message, onApply);
        else
            lobbyUI.ShowConfirmation(message, onApply);
    }

    /// <summary>
    /// 버튼에 등록한 이벤트만 제거.
    /// 저장 파일을 삭제하는 메서드 아님.
    /// </summary>
    private void OnDestroy()
    {
        for (int i = 0; i < 3; i++)
        {
            if (slots[i].button != null && handlers[i] != null)
            {
                slots[i].button.onClick.RemoveListener(
                    handlers[i]);
            }
        }
    }
}
