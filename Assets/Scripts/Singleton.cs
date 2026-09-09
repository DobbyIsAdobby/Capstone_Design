using UnityEngine;

// <T>는 상속받을 자식 클래스의 Type.
// 제너릭 싱글톤 클래스는 상속용 클래스임. 따라서 추상클래스로 선언함
/// <summary>
/// T에는 이 싱글톤 기반 클래스를 해당 T로 상속한 타입을 넣는다. 
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    //Generic Singleton class
    public static T Instance{ get; private set; }

    protected virtual void Awake()
    {
        // 이미 등록된 다른 인스턴스가 존재할 때만 중복으로 처리
        if(Instance != null && Instance != this)
        {
            Debug.LogError($"Duplicated {typeof(T).Name}: {name}", this);

            enabled = false; // Destroy 전까지 일반적인 활성 업데이트 억제
            Destroy(this);   // 현재 컴포넌트만 파괴 => 추후 Manager 컴포넌트 내에 자식 객체가 존재할 경우 ```Destroy(gameObject);```로 수정 필수.
            return;
        }

        // Instance = this as T; // 변환 실패 시 null로 반환 => 디버깅 시 어려움이 존재하여 하단으로 수정 진행.
        Instance = (T)this;      // 변환 실패 시 예외를 발생시킴. *명시적 캐스팅
        OnSingletonAwake();
    }

    /// <summary>
    /// 자식(Manager) 초기화를 담당하는 C# 일반 virtual 함수 => 템플릿 메서드 방식 적용(자식 내 자체적인 초기화가 필요한 함수만 자식 클래스에서 등록할 것.)
    /// </summary>
    protected virtual void OnSingletonAwake() {}

    protected virtual void OnDestroy()
    {
        if(Instance == this){ Instance = null; }
    }
}
