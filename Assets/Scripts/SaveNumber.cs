using System;
using System.Globalization;


/// <summary>
/// 현재 수익률과 금리는 decimal을 사용하고, 저장 시에는 string으로 저장함. 
/// 이를 서로 변환해서 저장 시 문제가 발생하지 않도록하는 class
/// </summary>
public static class SaveNumber
{
    public static string Write(decimal value)
    {
        // 소수점 형식을 기기 언어와 무관하게 고정
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static decimal Read(string value)
    {
        if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal result))
        {
            throw new FormatException("저장된 숫자 형식이 올바르지 않습니다.");
        }

        return result;
    }
}
