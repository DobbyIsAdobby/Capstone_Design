using System;

/// <summary>
/// 진행 상태를 저장할 수 있는 게임용 난수(Seed). 
/// 암호화나 보안 용도로 사용하는 것은 아님.
/// </summary>
public sealed class SaveRandom : Random
{
    private uint state;

    public long State => state;

    public SaveRandom(int seed)
    {
        state = unchecked((uint)seed);

        // 이 알고리즘은 0 상태를 사용할 수 없음
        if (state == 0)
            state = 0x6D2B79F5u;
    }

    public void Restore(long savedState)
    {
        if (savedState <= 0 || savedState > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(savedState));

        state = (uint)savedState;
    }

    private uint NextUInt()
    {
        // 상태 하나로 다음 난수와 진행 위치를 결정
        uint value = state;

        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;

        state = value;
        return value;
    }

    protected override double Sample()
    {
        return NextUInt() / 4294967296.0;   // Returns a uniformly random uint value in the interval [0, 4294967294]. - Unity Documentation>Script Reference>Unity.MMathematics>Random
    }

    public override double NextDouble()
    {
        return Sample();
    }

    public override int Next()
    {
        return Next(int.MaxValue);
    }

    public override int Next(int maxValue)
    {
        if (maxValue < 0)
            throw new ArgumentOutOfRangeException(nameof(maxValue));

        if (maxValue == 0)
            return 0;

        // 나머지 연산으로 발생하는 편향을 줄이는 거절 추출
        uint bound = (uint)maxValue;
        uint threshold = unchecked(0u - bound) % bound;

        uint value;

        do
        {
            value = NextUInt();
        }
        while (value < threshold);

        return (int)(value % bound);
    }

    public override int Next(int minValue, int maxValue)
    {
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException(nameof(minValue));

        long range = (long)maxValue - minValue;

        if (range == 0)
            return minValue;

        return (int)(minValue + (long)(NextDouble() * range));
    }
}
