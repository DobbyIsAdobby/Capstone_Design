using UnityEngine;

public class PauseController : Singleton<PauseController>
{
   // 외부에서는 이 속성으로 일시정지 여부를 조회.
    public static bool IsPaused => Instance != null && Instance.paused;

    private bool paused;
    private float previousTimeScale = 1f;

    public bool TryPause()
    {
        // 중복 컴포넌트나 비활성 Manager는 작동하지 않음.
        if (Instance != this || !isActiveAndEnabled)
            return false;

        if (paused)
            return true;

        GameManager game = GameManager.Instance;

        // 초기화, 정산, 게임 종료 중에는 일시정지 메뉴를 열지 않음
        if (game == null || !game.CanOpenPause)
            return false;

        // 일시정지 직전 속도를 기억
        previousTimeScale = Time.timeScale;

        paused = true;
        Time.timeScale = 0f;

        return true;
    }

    public void Resume()
    {
        // 등록된 Manager만 시간 상태를 복구
        if (Instance != this || !paused)
            return;

        paused = false;
        Time.timeScale = previousTimeScale;
    }

    private void OnDisable()
    {
        // Manager가 비활성화되어도 시간이 0으로 남지 않게 함.
        // 중복 컴포넌트의 비활성화는 정상 Manager에 영향을 주지 않음
        if (Instance == this)
            Resume();
    }

    protected override void OnDestroy()
    {
        // 인스턴스 참조가 해제되기 전에 시간을 복구
        if (Instance == this)
            Resume();

        // Singleton<T>의 인스턴스 정리 로직을 반드시 실행.
        base.OnDestroy();
    }
}
