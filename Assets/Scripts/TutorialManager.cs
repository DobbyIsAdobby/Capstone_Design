using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 턴에 따라 해금되는 기능 목록입니다.
/// </summary>
public enum TutorialFeature
{
    Overtime,
    Shop,
    AssetNavigation,
    BankDeposit,
    JobMinigame,
    Stock,
    Leverage,
    StockInverse,
    LeverageInverse,
    Information,
    BankLoan,
    Newspaper,
    RealEstate
}

/// <summary>
/// 스토리 데이터와 기능별 해금 턴을 관리함.
/// </summary>
public class TutorialManager : Singleton<TutorialManager>
{
    [Header("Story Data")]
    [SerializeField] private TextAsset storyJson;

    private TutorialStoryData story;

    public bool IsConfigured { get; private set; }

    protected override void OnSingletonAwake()
    {
        IsConfigured = false;

        try
        {
            if (storyJson == null)
            {
                throw new InvalidOperationException("TutorialManager에 story.json을 연결하세요.");
            }

            story = JsonUtility.FromJson<TutorialStoryData>(storyJson.text);

            if (story == null)
            {
                throw new InvalidOperationException("story.json을 읽지 못했습니다.");
            }

            story.Validate();
            IsConfigured = true;
        }
        catch (Exception exception)
        {
            // 설정 오류가 있으면 준비 상태를 false로 유지.
            Debug.LogException(exception, this);
        }
    }

    /// <summary>
    /// 기능별 해금 턴을 반환.
    /// </summary>
    /// <param name="feature"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static int GetUnlockTurn(TutorialFeature feature)
    {
        switch (feature)
        {
            case TutorialFeature.Overtime:
            case TutorialFeature.Shop:
                return 1;

            case TutorialFeature.AssetNavigation:
            case TutorialFeature.BankDeposit:
                return 2;

            case TutorialFeature.JobMinigame:
                return 4;

            case TutorialFeature.Stock:
                return 5;

            case TutorialFeature.Leverage:
            case TutorialFeature.StockInverse:
            case TutorialFeature.LeverageInverse:
            case TutorialFeature.Information:
                return 6;

            case TutorialFeature.BankLoan:
                return 8;

            case TutorialFeature.Newspaper:
                return 9;

            case TutorialFeature.RealEstate:
                return 10;

            default:
                throw new ArgumentOutOfRangeException(nameof(feature), feature, null);
        }
    }

    /// <summary>
    /// 기존 자산 종류에 대응하는 해금 항목을 반환.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static TutorialFeature GetAssetFeature(AssetType type)
    {
        switch (type)
        {
            case AssetType.Bank:
                return TutorialFeature.BankDeposit;

            case AssetType.Stock:
                return TutorialFeature.Stock;

            case AssetType.Leverage:
                return TutorialFeature.Leverage;

            case AssetType.StockInverse:
                return TutorialFeature.StockInverse;

            case AssetType.LeverageInverse:
                return TutorialFeature.LeverageInverse;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    /// <summary>
    /// 현재 턴에서 해당 기능이 해금되었는지 검사.
    /// 행동 가능 상태인지는 CanAct가 별도로 검사.
    /// </summary>
    /// <param name="feature"></param>
    /// <returns></returns>
    public static bool IsUnlocked(TutorialFeature feature)
    {
        GameManager game = GameManager.Instance;

        return Instance != null &&
               Instance.IsConfigured &&
               game != null &&
               game.currentMonth >= GetUnlockTurn(feature);
    }

    /// <summary>
    /// 실제 기능 실행 전에 사용.
    /// 잠겨 있다면 실패 사유를 반환.
    /// </summary>
    /// <param name="feature"></param>
    /// <param name="reason"></param>
    /// <returns></returns>
    public static bool Check(TutorialFeature feature, out string reason)
    {
        if (Instance == null || !Instance.IsConfigured || GameManager.Instance == null)
        {
            reason = "초반 진행 시스템이 준비되지 않았습니다.";
            return false;
        }

        if (!IsUnlocked(feature))
        {
            reason = $"해당 기능은 {GetUnlockTurn(feature)}턴부터 이용할 수 있습니다.";

            return false;
        }

        reason = "";
        return true;
    }

    /// <summary>
    /// 이벤트 대사창으로 한 줄씩 표시.각 줄마다 유저의 클릭을 대기함.
    /// </summary>
    /// <param name="turn"></param>
    /// <param name="eventUI"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public IEnumerator ShowTurnStory(int turn, EventFlowUI eventUI)
    {
        if (!IsConfigured || eventUI == null || !eventUI.IsConfigured)
        {
            throw new InvalidOperationException("스토리 데이터 또는 이벤트 대사창 연결을 확인하세요.");
        }

        string[] lines = story.GetLines(turn);

        foreach (string line in lines)
        {
            yield return eventUI.ShowDialogue(line);
        }

        // 이후 이벤트 안내가 이어짐.
        // 모든 안내가 끝나면 EventManager에서 화면을 닫음.
    }
}
