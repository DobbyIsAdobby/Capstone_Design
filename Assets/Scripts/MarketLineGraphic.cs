using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 월별 수익률을 UI 위에 꺾은선으로 그리는 컴포넌트.
///
/// MaskableGraphic을 상속하므로 일반 Image처럼 Canvas 안에서 동작함.
/// 각 선마다 별도 GameObject를 생성하지 않고, 하나의 UI 그래픽에 필요한 도형을 그림.
///
/// 시장 데이터 조회와 턴 선택은 MarketChartPanelUI가 담당.
/// 이 클래스는 전달받은 숫자를 화면 좌표로 바꾸는 역할만 함.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class MarketLineGraphic : MaskableGraphic
{
    [Header("Shape")]
    [SerializeField, Min(1f)] private float lineWidth = 4f;
    [SerializeField, Min(1f)] private float pointSize = 7f;
    [SerializeField, Min(1f)] private float selectedPointSize = 12f;

    // 그래프 가장자리에서 선과 점이 잘리지 않도록 확보할 내부 여백 값
    [SerializeField, Min(0f)] private float padding = 12f;

    [Header("Colors")]
    [SerializeField] private Color lineColor = new Color32(237, 187, 80, 255);

    [SerializeField] private Color positiveColor = new Color32(0, 200, 0, 255);

    [SerializeField] private Color negativeColor = new Color32(200, 0, 0, 255);

    [SerializeField] private Color neutralColor = Color.white;

    [SerializeField] private Color zeroLineColor = new Color32(130, 130, 130, 255);

    [SerializeField] private Color selectionLineColor = new Color32(255, 255, 255, 70);

    // 선택한 턴의 강조점 색상.
    [SerializeField] private Color selectedPointColor = new Color32(56, 78, 107, 255);

    // 그래프에 표시할 %값.
    private readonly List<float> values = new List<float>();

    // 세로축의 절댓값 한계
    // 10이면 그래프 아래는 -10%, 위는 +10%임
    private float axisLimit = 5f;

    // 현재 선택한 점의 인덱스. -1은 선택할 데이터가 없다는 뜻.
    private int selectedIndex = -1;

    /// <summary>
    /// 새로운 수익률 목록과 세로축 범위를 전달받음.
    /// 차트 패널을 열 때 호출.
    /// </summary>
    /// <param name="percentages"></param>
    /// <param name="absoluteAxisLimit"></param>
    public void SetData(IReadOnlyList<float> percentages, float absoluteAxisLimit)
    {
        values.Clear();

        // 전달받은 목록 복사.
        // 외부 목록이 변경되어도 현재 그래프의 데이터가 의도치 않게 함께 변경되지 않도록 함.
        if (percentages != null)
        {
            for (int i = 0; i < percentages.Count; i++)
            {
                values.Add(percentages[i]);
            }
        }

        // 세로축 범위가 0이 되면 좌표 계산이 어려우니 최소 1 이상의 범위를 확보해야함
        axisLimit = Mathf.Max(1f, absoluteAxisLimit);

        // 처음 열었을 때는 가장 최근 턴 선택
        selectedIndex = values.Count > 0 ? values.Count - 1 : -1;

        // UI가 다시 그려져야 함을 알림
        SetVerticesDirty();
    }

    /// <summary>
    /// 슬라이더로 선택한 턴의 점을 강조.
    /// </summary>
    /// <param name="index"></param>
    public void SetSelectedIndex(int index)
    {
        selectedIndex = values.Count == 0 ? -1 : Mathf.Clamp(index, 0, values.Count - 1);

        SetVerticesDirty();
    }

    /// <summary>
    /// UI가 표시할 도형을 구성하는 메서드.
    /// </summary>
    /// <param name="vh"></param>
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        // 이전에 그린 선과 점 제거 후 새로 구성.
        vh.Clear();

        if (values.Count == 0)
            return;

        // Plot obj의 로컬 좌표 범위.
        Rect area = rectTransform.rect;

        // 선과 점이 영역 밖으로 나가지 않도록 안쪽 여백을 둠.
        float inset = Mathf.Max(padding, Mathf.Max(selectedPointSize, lineWidth) * 0.5f);

        area.xMin += inset;
        area.xMax -= inset;
        area.yMin += inset;
        area.yMax -= inset;

        // 영역이 너무 작으면 그리지 않음.
        if (area.width <= 0f || area.height <= 0f)
            return;

        // 세로축은 -axisLimit ~ +axisLimit의 대칭 범위
        float zeroY = area.center.y;

        AddLine(vh, new Vector2(area.xMin, zeroY), new Vector2(area.xMax, zeroY), 1f, zeroLineColor);

        // 선택한 턴의 세로 안내선을 먼저 그림.
        // 이후 그리는 수익률 선과 점이 위에 표시되게하기 위함.
        if (selectedIndex >= 0 && selectedIndex < values.Count)
        {
            Vector2 selected = GetPoint(selectedIndex, area);

            AddLine(vh, new Vector2(selected.x, area.yMin), new Vector2(selected.x, area.yMax), 1f, selectionLineColor);
        }

        // 인접한 턴끼리 선으로 연결.
        for (int i = 1; i < values.Count; i++)
        {
            AddLine(vh, GetPoint(i - 1, area), GetPoint(i, area), lineWidth, lineColor);
        }

        // 각 턴의 표시점.
        // 픽셀 UI에 맞춰 작은 사각형으로 표시.
        for (int i = 0; i < values.Count; i++)
        {
            Color pointColor = values[i] > 0f ? positiveColor : values[i] < 0f ? negativeColor : neutralColor;

            AddPoint(vh, GetPoint(i, area), pointSize, pointColor);
        }

        // 선택점은 마지막에 그려 다른 선에 가려지지 않게 해야함.
        if (selectedIndex >= 0 && selectedIndex < values.Count)
        {
            AddPoint(vh, GetPoint(selectedIndex, area), selectedPointSize, selectedPointColor);
        }
    }

    /// <summary>
    /// 특정 턴의 수익률을 Plot 내부 좌표로 변환.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="area"></param>
    /// <returns></returns>
    private Vector2 GetPoint(int index, Rect area)
    {
        // x축: 첫 턴은 왼쪽, 마지막 턴은 오른쪽.
        // 데이터가 1개뿐이면 왼쪽에 표시.
        float xRatio = values.Count <= 1 ? 0f : index / (float)(values.Count - 1);

        // y축: 수익률을 0~1 사이 위치 비율로 변경.
        //
        // axisLimit = 10일 때:
        // -10% >> 0   >> 그래프 맨 아래
        //   0% >> 0.5 >> 그래프 중앙
        // +10% >> 1   >> 그래프 맨 위
        float yRatio = Mathf.InverseLerp(-axisLimit, axisLimit, values[index]);

        return new Vector2(Mathf.Lerp(area.xMin, area.xMax, xRatio), Mathf.Lerp(area.yMin, area.yMax, yRatio));
    }

    /// <summary>
    /// 두 점을 연결하는 선을 그림.
    /// 긴 사각형 하나를 생성해 표현하는 방식
    /// </summary>
    /// <param name="vh"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="width"></param>
    /// <param name="color"></param>
    private static void AddLine(VertexHelper vh, Vector2 start, Vector2 end, float width, Color color)
    {
        Vector2 difference = end - start;

        // 시작과 끝이 같으면 선의 방향을 계산할 수 없으니 생략.
        if (difference.sqrMagnitude < 0.0001f)
            return;

        // 선 방향과 직각인 방향을 구함.
        // 이 방향으로 양쪽에 반 두께씩 이동하면 네 모서리가 만들어짐.
        Vector2 perpendicular = new Vector2(-difference.y, difference.x).normalized;

        Vector2 offset = perpendicular * width * 0.5f;

        AddQuad(vh, start - offset, start + offset, end + offset, end - offset, color);
    }

    /// <summary>
    /// 지정 좌표를 중심으로 사각형 점 생성.
    /// </summary>
    /// <param name="vh"></param>
    /// <param name="center"></param>
    /// <param name="size"></param>
    /// <param name="color"></param>
    private static void AddPoint(VertexHelper vh, Vector2 center, float size, Color color)
    {
        float half = size * 0.5f;

        AddQuad(vh, center + new Vector2(-half, -half), center + new Vector2(-half, half), center + new Vector2(half, half), center + new Vector2(half, -half), color);
    }

    /// <summary>
    /// 네 개의 꼭짓점으로 사각형 생성
    /// 
    /// Unity는 triangle 단위로 도형을 그림.
    /// 따라서 사각형 하나를 삼각형 두 개로 나눠서 등록함.
    /// </summary>
    /// <param name="vh"></param>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="c"></param>
    /// <param name="d"></param>
    /// <param name="color"></param>
    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = vh.currentVertCount;

        // 단색 도형이니 UV좌표는 모두 0
        vh.AddVert(a, color, Vector2.zero);
        vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero);
        vh.AddVert(d, color, Vector2.zero);

        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
