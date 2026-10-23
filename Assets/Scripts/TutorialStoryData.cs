using System;

/// <summary>
/// story.json의 내용을 읽는 데이터 클래스
/// JSON에 없는 해금 정보는 여기서 임의로 읽지 않음.
/// 변수 이름은 JSON의 필드 이름과 정확히 같아야함.
/// </summary>
[Serializable]
public sealed class TutorialStoryData
{
    public string[] turn_1;
    public string[] turn_2;
    public string[] turn_3;
    public string[] turn_4;
    public string[] turn_5;
    public string[] turn_6;
    public string[] turn_7;
    public string[] turn_8;
    public string[] turn_9;
    public string[] turn_10;

    // 엔딩 대사는 이번 작업에서 화면에 연결하지 않음.
    public string[] bad_ending;
    public string[] happy_ending;
    public string[] normal_ending;

    /// <summary>
    /// 지정한 턴의 대사를 반환.
    /// 11턴 이후에는 빈 배열을 반환.
    /// </summary>
    /// <param name="turn"></param>
    /// <returns></returns>
    public string[] GetLines(int turn)
    {
        switch (turn)
        {
            case 1: return turn_1;
            case 2: return turn_2;
            case 3: return turn_3;
            case 4: return turn_4;
            case 5: return turn_5;
            case 6: return turn_6;
            case 7: return turn_7;
            case 8: return turn_8;
            case 9: return turn_9;
            case 10: return turn_10;

            default:
                return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 필요한 배열과 대사가 올바르게 작성되었는지 검사.
    /// 빈 배열은 허용하지만 배열 내부의 빈 문자열은 허용하지 않음.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public void Validate()
    {
        for (int turn = 1; turn <= 10; turn++)
        {
            string[] lines = GetLines(turn);

            if (lines == null)
            {
                throw new InvalidOperationException($"story.json에 turn_{turn} 배열이 없습니다.");
            }

            for (int index = 0; index < lines.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(lines[index]))
                {
                    throw new InvalidOperationException($"turn_{turn}의 {index + 1}번째 대사가 비어 있습니다.");
                }
            }
        }
    }
}
