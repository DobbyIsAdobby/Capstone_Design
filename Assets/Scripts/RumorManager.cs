using UnityEngine;

// 정보의 진위 여부(enum)
public enum RumorType
{
    True, //진짜 정보 (다음 턴 방향과 일치)
    Fake  //거짓 정보 (다음 턴 방향과 반대 또는 잘못된 조언)
}

public class RumorManager : Singleton<RumorManager>
{
    /*
    Inspector Zone
    */
    //public static RumorManager Instance;

    [Header("Rumor Settings")]
    public int rumorCost = 100000; // 정보 1회 열람 비용 (10만 원)

    //가챠 확률 (ex : True-40% / Fake-60%)
    [Range(0f,1f)]
    public float trueRumorProbability = 0.4f;

    /*
    function Zone
    */

    //싱글톤 패턴
    /*private void Awake()
    {
        if(Instance == null) Instance = this;
        else Destroy(gameObject);
    }*/

    /// <summary>
    /// UI [정보 대화방] 버튼을 통해 호출되는 함수
    /// </summary>
    public void OnClickBuyRumorGacha()
    {
        //1. 현금 확인
        if(GameManager.Instance.availableCash < rumorCost)
        {
            Debug.Log("가용 현금이 부족하여 정보를 구매할 수 없습니다.");
            return;
        }

        //2. 비용 지불
        GameManager.Instance.availableCash -= rumorCost;

        //3. 확률에 따른 진위 여부 판별
        RumorType resultType = (Random.value <= trueRumorProbability) ? RumorType.True : RumorType.Fake;

        //4. 정보 텍스트 생성 및 출력
        string rumorMessage = GenerateRumorText(resultType);

        Debug.Log($"정보 도착. 비용 : -{rumorCost}원 / 진위 : {resultType}");
        Debug.Log($"메시지 내용 : {rumorMessage}");

        if(UIManager.Instance != null)
        {
            UIManager.Instance.RefreshUI();
        }

        //추후 UIManager를 통해 인게임 채팅방 UI에 텍스트 띄우기
        //UIManager.Instance.ShowRumorUI(rumorMessage);
    }

    /// <summary>
    /// 진위 여부에 따라 출력할 텍스트를 생성하는 함수 (프로토타입용)
    /// </summary>
    private string GenerateRumorText(RumorType type)
    {
        //프로토타입 단계이므로 임시 텍스트 배열 사용(현재 부분은 생성형AI를 통해 텍스트를 제작했습니다.)
        //추후 DB 담당 팀원 분께서 제작한 CSV 데이터를 파싱해서 불러오도록 변경 예정

        string[] trueRumors = {
            "여의도 기관들 지금 다 숏(하락) 치고 있다 조심해라.",
            "다음 달 금리 인상 확정이란 썰 돌더라. 현금 관망 추천.",
            "[단독] 기술주 섹터 대규모 실적 쇼크 예상... 폭락 주의"
        };

        string[] fakeRumors = {
            "나스닥 3배 레버리지 지금이 바닥이다. 내일 무조건 쏜다.",
            "아는 형이 펀드매니저인데 이번 하락장 페이크래. 풀매수 가라.",
            "다음 달 증시 V자 반등 무조건 나옴. 빚내서라도 타라."
        };

        if(type == RumorType.True)
        {
            return trueRumors[Random.Range(0, trueRumors.Length)];
        }
        else
        {
            return fakeRumors[Random.Range(0, fakeRumors.Length)];
        }
    }
}
