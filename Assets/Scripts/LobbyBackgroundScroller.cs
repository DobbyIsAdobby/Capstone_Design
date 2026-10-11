using UnityEngine;
using UnityEngine.UI;

public class LobbyBackgroundScroller : MonoBehaviour
{
    [Header("Background")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform scrollingImage;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float scrollSpeed = 12f;

    // 왼쪽 끝에서부터 이동한 거리.
    private float distance;

    private void Start()
    {
        if (viewport == null || scrollingImage == null)
        {
            Debug.LogError("Viewport와 ScrollingImage를 연결하세요.", this);

            enabled = false;
            return;
        }

        // 시작 위치 조정.
        distance = 0f;
        SetPositionX(0f);
    }

    private void Update()
    {
        // 이 거리만큼 이동하면 이미지 오른쪽 끝에 도착할거임
        float travelDistance = Mathf.Max(0f, scrollingImage.rect.width - viewport.rect.width);

        // 이미지가 화면보다 넓지 않으면 이동하지 않음
        if (travelDistance <= 0f)
        {
            distance = 0f;
            SetPositionX(0f);
            return;
        }

        // deltaTime을 사용해 초당 일정한 거리만큼 이동
        // 왕복 거리 안에서 반복시켜 누적값이 계속 커지지 않게 함.
        distance = Mathf.Repeat(distance + scrollSpeed * Time.deltaTime, travelDistance * 2f);

        // 0 → 최대 이동 거리 → 0으로 반복.
        float offset = Mathf.PingPong(distance, travelDistance);

        // 처음에는 왼쪽으로 이동하고, 오른쪽 끝에 도착하면 다시 오른쪽으로 돌아오게
        SetPositionX(-offset);
    }

    private void SetPositionX(float x)
    {
        Vector2 position = scrollingImage.anchoredPosition;
        position.x = x;

        scrollingImage.anchoredPosition = position;
    }
}
