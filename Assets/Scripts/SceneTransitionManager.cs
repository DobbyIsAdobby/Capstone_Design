using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : Singleton<SceneTransitionManager>
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "GameScene";
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private string endingSceneName = "EndingScene";

    [Header("Save Validation")]
    [SerializeField] private SaveValidationConfig validationConfig;

    [Header("Close Panels After Load")]
    [SerializeField] private GameObject[] closeAfterLoad = new GameObject[0];

    // Scene이 바껴도 다음 GameScene에 전달할 데이터는 남겨둠
    // GameManager가 가져간 뒤에는 비움
    private static GameSaveData pendingLoad;

    // 로비에서 시작한 불러오기가 실패했을 때 표시할 안내
    private static string lobbyMessage;

    // 같은 버튼을 연속으로 눌러 중복 전환하는 것을 막음
    private bool transitioning;

    // Scene 전환 중에만 보관하는 엔딩 결과.
    // null이면 전달받은 엔딩 결과가 없다는 의미.
    private static EndingType? pendingEnding;

    public SaveValidationConfig ValidationConfig => validationConfig;

    /// <summary>
    /// 새로운 실행을 시작할 때 이전 실행의 요청을 제거.
    /// 에디터에서 도메인 재로드를 꺼둔 경우도 처리.
    /// 일반적인 씬 이동마다 호출되는 메서드는 아님.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRequests()
    {
        pendingLoad = null;
        lobbyMessage = null;
        pendingEnding = null;
    }

    /// <summary>
    /// 새 게임으로 이동. 
    /// 불러오기 요청이 없어야 GameManager가 새 시장을 생성.
    /// </summary>
    /// <param name="error"></param>
    /// <returns></returns>
    public bool TryStartNewGame(out string error)
    {
        pendingLoad = null;
        pendingEnding = null;

        return TryChangeScene(gameSceneName, out error);
    }

    /// <summary>
    /// 로비로 이동합니다. 
    /// 자동 저장은 수행하지 않음.
    /// </summary>
    /// <param name="error"></param>
    /// <returns></returns>
    public bool TryMoveToLobby(out string error)
    {
        pendingLoad = null;
        pendingEnding = null;

        return TryChangeScene(lobbySceneName, out error);
    }

    /// <summary>
    /// 저장 데이터를 불러오는 진입점.
    /// 
    /// Lobby:
    /// 데이터를 다음 GameScene에 전달하고 Scene 이동.
    ///
    /// Game:
    /// 현재 상태를 백업한 다음 같은 Scene에서 복원.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public bool TryLoadGame(GameSaveData data, out string error)
    {
        error = "";

        if (transitioning)
        {
            error = "이미 화면을 전환 중입니다.";
            return false;
        }

        try
        {
            // 현재 상태나 Scene을 변경하기 전에 검사부터 시행.
            ValidateSave(data);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }

        if (GameManager.Instance != null)
        {
            // GameManager가 있으면 현재 게임 안에서 불러옴
            return TryRestoreCurrentGame(data, out error);
        }

        // 로비에는 GameManager가 없으므로 다음 씬에 데이터를 전달
        pendingLoad = data;

        if (TryChangeScene(gameSceneName, out error))
            return true;

        // 씬 이동 실패 시 요청이 다음 새 게임에 남지 않도록 제거
        pendingLoad = null;
        return false;
    }

    /// <summary>
    /// 현재 GameScene에서 저장 상태를 적용.
    /// 적용 전 현재 게임을 메모리에 백업.
    /// 적용에 실패하면 그 백업으로 돌아가도록 시도.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    private bool TryRestoreCurrentGame(GameSaveData data, out string error)
    {
        error = "";
        GameSaveData backup;

        try
        {
            // 진행 중 미니게임은 백업 대상이 아니므로 먼저 종료해야 함.
            if (!GameManager.Instance.CanSaveCurrentState)
            {
                throw new InvalidOperationException("미니게임을 마치고 게임이 준비된 상태에서 불러와주세요.");
            }

            // 파일에 저장하는 것이 아니라 메모리에만 잠시 보관.
            backup = GameSaveCoordinator.Capture();
            ValidateSave(backup);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }

        transitioning = true;

        try
        {
            // 구매나 정산을 다시 실행하지 않고 상태만 복원.
            GameSaveCoordinator.Restore(data);
        }
        catch (Exception exception)
        {
            error = $"불러오지 못했습니다.\n{exception.Message}";

            try
            {
                // 일부 Field가 변경된 뒤 실패했을 가능성이 있음. 
                // 적용 직전에 보관한 상태로 되돌림.
                GameSaveCoordinator.Restore(backup);
            }
            catch (Exception rollbackError)
            {
                error += "\n이전 상태 복구도 실패했습니다. 콘솔을 확인해주세요.";

                Debug.LogException(rollbackError, this);
            }

            transitioning = false;
            return false;
        }

        // 성공했을 때만 일시정지를 해제하고 기존 화면을 닫음.
        if (PauseController.Instance != null)
            PauseController.Instance.Resume();

        foreach (GameObject panel in closeAfterLoad)
        {
            if (panel != null)
                panel.SetActive(false);
        }

        Time.timeScale = 1f;
        transitioning = false;

        // 복원 직후에는 일시정지 중이었음.
        // 해제된 상태에 맞춰 HUD를 한 번 더 갱신해야함.
        if (UIManager.Instance != null)
            UIManager.Instance.RefreshUI();

        return true;
    }

    /// <summary>
    /// Unity SceneManager를 사용한 Scene 이동 메서드.
    /// 새 게임과 로비 이동에 사용. -> 추후 엔딩 Scene(bad, normal, happy) 연결 필요함
    /// </summary>
    /// <param name="sceneName"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    private bool TryChangeScene(string sceneName, out string error)
    {
        error = "";

        if (transitioning)
        {
            error = "이미 화면을 전환 중입니다.";
            return false;
        }

        // 현재 화면을 바꾸기 전에 대상 Scene이 있는지 검사
        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            error = $"씬을 불러올 수 없습니다: {sceneName}";
            return false;
        }

        bool wasPaused = PauseController.IsPaused;

        // 일시정지 직전의 게임 속도를 복구한 뒤 기억
        if (wasPaused && PauseController.Instance != null)
            PauseController.Instance.Resume();

        float previousTimeScale = Time.timeScale;

        transitioning = true;

        try
        {
            // 새로 여는 Scene이 정지 상태로 시작하지 않게 함.
            Time.timeScale = 1f;

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);

            return true;
        }
        catch (Exception exception)
        {
            // 이동 실패 시 현재 Scene의 상태로 돌아감
            transitioning = false;
            Time.timeScale = previousTimeScale;

            if (wasPaused && PauseController.Instance != null)
                PauseController.Instance.TryPause();

            error = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// 두 Scene이 공통 설정으로 저장 데이터를 검사하도록 함.
    /// </summary>
    /// <param name="data"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void ValidateSave(GameSaveData data)
    {
        if (validationConfig == null)
        {
            throw new InvalidOperationException("SaveValidationConfig를 연결하세요.");
        }

        validationConfig.Validate(data);
    }

    /// <summary>
    /// 새 GameScene의 GameManager가 전달받은 저장 데이터를 꺼냄.
    /// 
    /// 데이터를 넘긴 뒤 pendingLoad를 비워 같은 요청을 중복하지 않도록 함.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static bool TryTakePendingLoad(out GameSaveData data)
    {
        data = pendingLoad;
        pendingLoad = null;

        return data != null;
    }

    /// <summary>
    /// LobbyScene에서 GameScene으로 이동한 뒤 복원에 실패한 경우. 
    /// 안내 문구를 보관하고 LobbyScene으로 돌아감.
    /// </summary>
    /// <param name="message"></param>
    public void ReturnAfterInitialLoadFailure(string message)
    {
        lobbyMessage = $"저장 게임을 시작하지 못했습니다.\n{message}";

        if (!TryMoveToLobby(out string error))
            Debug.LogError(error, this);
    }

    /// <summary>
    /// LobbyScene에서 실패 안내를 한 번만 꺼내 표시.
    /// </summary>
    /// <returns></returns>
    public static string TakeLobbyMessage()
    {
        string message = lobbyMessage;
        lobbyMessage = null;

        return message;
    }

    /// <summary>
    /// 엔딩 결과를 전달하고 공용 EndingScene으로 이동
    /// </summary>
    /// <param name="ending"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public bool TryMoveToEnding(EndingType ending, out string error)
    {
        error = "";

        if (transitioning)
        {
            error = "이미 화면을 전환 중입니다.";
            return false;
        }

        // Scene이 바뀌면 GameManager는 소멸되니 결과를 먼저 보관해야함.
        pendingEnding = ending;

        // 공용 Scene 이동 로직을 사용
        if (TryChangeScene(endingSceneName, out error))
            return true;

        // 이동에 실패했다면 잘못된 결과를 남기지 않아야함 - 초기화
        pendingEnding = null;
        return false;
    }

    /// <summary>
    /// EndingScene에서 전달받은 결과를 한 번 가져옴
    /// </summary>
    /// <param name="ending"></param>
    /// <returns></returns>
    public static bool TryTakeEnding(out EndingType ending)
    {
        if (!pendingEnding.HasValue)
        {
            ending = default;
            return false;
        }

        ending = pendingEnding.Value;
        pendingEnding = null;
        return true;
    }
}
