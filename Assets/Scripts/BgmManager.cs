using UnityEngine;

/// <summary>
/// 현재 Scene의 배경음 담당ㅇ
/// </summary>
public class BgmManager : Singleton<BgmManager>
{
    // 게임 저장 슬롯과 별도로 관리할 기기 공통 설정.
    private const string MuteKey = "Audio.BgmMuted";

    [Header("AudioSource")]
    [SerializeField] private AudioSource bgmSource;

    [Header("Lobby Start")]
    [SerializeField] private bool playLobbyOnStart;

    [Header("Lobby")]
    [SerializeField] private AudioClip lobbyClip;

    [Header("Game Turn Bgm")]
    [SerializeField] private AudioClip earlyGameClip;
    [SerializeField] private AudioClip middleGameClip;
    [SerializeField] private AudioClip lateGameClip;

    [Header("Ending")]
    [SerializeField] private AudioClip badEndingClip;
    [SerializeField] private AudioClip normalEndingClip;
    [SerializeField] private AudioClip happyEndingClip;

    /// <summary>
    /// 저장된 음소거 설정을 가져옴.
    /// 설정이 없으면 기본적으로 음악을 킴.
    /// </summary>
    public static bool IsMuted => PlayerPrefs.GetInt(MuteKey, 0) == 1;

    protected override void OnSingletonAwake()
    {
        if (bgmSource == null)
        {
            Debug.LogError("BgmManager의 Bgm Source를 연결하세요.", this);
            return;
        }

        bgmSource.playOnAwake = false;

        // 곡이 끝나면 같은 곡을 반복.
        bgmSource.loop = true;

        // 카메라나 캐릭터의 위치에 영향을 받지 않는 2D 음악임.
        bgmSource.spatialBlend = 0f;

        // 씬에 진입할 때 이전 음소거 설정을 적용.
        bgmSource.mute = IsMuted;
    }

    private void Start()
    {
        // 로비만 씬 시작과 함께 자동 재생.
        // 게임과 엔딩은 결과가 준비된 시점에 요청.
        if (playLobbyOnStart)
        {
            PlayClip(lobbyClip);
        }
    }

    /// <summary>
    /// 현재 턴에 맞는 게임 배경음을 선택.
    /// 새 턴 시작과 저장 데이터 복원 완료 시 호출.
    /// </summary>
    /// <param name="turn"></param>
    public void PlayGameTurn(int turn)
    {
        if (turn < 1 || turn > 60)
        {
            Debug.LogWarning($"배경음 재생 요청의 턴이 잘못되었습니다: {turn}", this);
            return;
        }

        if (turn <= 20)
        {
            PlayClip(earlyGameClip);
        }
        else if (turn <= 40)
        {
            PlayClip(middleGameClip);
        }
        else
        {
            PlayClip(lateGameClip);
        }
    }

    /// <summary>
    /// 확정된 엔딩 결과에 맞는 음악을 재생.
    /// </summary>
    /// <param name="ending"></param>
    public void PlayEnding(EndingType ending)
    {
        switch (ending)
        {
            case EndingType.Bad:
                PlayClip(badEndingClip);
                break;

            case EndingType.Normal:
                PlayClip(normalEndingClip);
                break;

            case EndingType.Happy:
                PlayClip(happyEndingClip);
                break;
        }
    }

    /// <summary>
    /// 음소거 설정을 저장하고 현재 재생 장치에도 반영.
    /// 씬이 바뀌어도 다음 관리자가 같은 설정을 읽어옴.
    /// </summary>
    /// <param name="muted"></param>
    public static void SetMuted(bool muted)
    {
        PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
        PlayerPrefs.Save();

        if (Instance != null && Instance.bgmSource != null)
        {
            // 음악을 정지하지 않고 소리만 끔.
            // 다시 켰을 때 처음부터 재생되지 않음.
            Instance.bgmSource.mute = muted;
        }
    }

    /// <summary>
    /// AudioSource 재생 처리.
    /// 같은 곡이 이미 재생 중이면 그대로 유지.
    /// </summary>
    /// <param name="clip"></param>
    private void PlayClip(AudioClip clip)
    {
        if (bgmSource == null)
        {
            return;
        }

        if (clip == null)
        {
            // 연결 누락 시 이전 구간의 음악이 계속 나오는 것을 막음.
            bgmSource.Stop();
            bgmSource.clip = null;

            Debug.LogWarning("현재 상황에 해당하는 배경음이 연결되지 않았습니다.", this);
            return;
        }

        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            return;
        }

        // 다른 곡으로 전환할 때만 처음부터 재생.
        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.mute = IsMuted;
        bgmSource.Play();
    }
}
