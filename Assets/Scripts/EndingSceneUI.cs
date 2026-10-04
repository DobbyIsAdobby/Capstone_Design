using TMPro;
using UnityEngine;

public class EndingSceneUI : MonoBehaviour
{
    [SerializeField] private TMP_Text endingText;

    private string commonMessage = "엔딩 입니다.";

    private void Start()
    {
        if (endingText == null)
        {
            Debug.LogError("EndingText를 연결하세요.", this);
            return;
        }

        // GameScene에서 전달한 결과만 사용
        // 이 Scene에서는 자산이나 생존 여부를 다시 계산하지 않음.
        if (!SceneTransitionManager.TryTakeEnding(out EndingType ending))
        {
            endingText.text = "";

            Debug.LogError("전달된 엔딩 결과가 없습니다. GameScene을 통해 진입하세요.", this);

            return;
        }

        switch (ending)
        {
            case EndingType.Bad:
                endingText.text = $"배드" + commonMessage;
                endingText.color = Color.red;
                break;

            case EndingType.Normal:
                endingText.text = $"노말" + commonMessage;
                endingText.color = Color.black;
                break;

            case EndingType.Happy:
                endingText.text = $"해피" + commonMessage;
                endingText.color = Color.green;
                break;
        }
    }
}
