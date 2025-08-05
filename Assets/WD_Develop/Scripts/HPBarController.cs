using UnityEngine;
using UnityEngine.UI;
using System.Threading; // CancellationToken을 위해 네임스페이스 추가
using Cysharp.Threading.Tasks; // UniTask를 위해 네임스페이스 추가

/// <summary>
/// Screen Space UI로 HP 바를 제어합니다.
/// UniTask를 사용하여 최적화되었습니다.
/// </summary>
public class HPBarController : MonoBehaviour
{
    [SerializeField]
    private Slider hpSlider;

    private Transform target;
    private Vector3 offset; // 화면 공간(픽셀) 오프셋
    private Camera mainCamera;
    
    // 코루틴 관련 변수는 더 이상 필요 없습니다.

    /// <summary>
    /// HP 바를 초기화하고 추적할 대상을 설정합니다.
    /// </summary>
    public void Initialize(Transform targetToFollow, Vector3 positionOffset)
    {
        target = targetToFollow;
        offset = positionOffset;
        mainCamera = Camera.main;

        if (hpSlider == null)
        {
            hpSlider = GetComponentInChildren<Slider>();
            if (hpSlider == null)
            {
                Debug.LogError("HPBarController: 자식 오브젝트에서 Slider 컴포넌트를 찾을 수 없습니다.", this);
                gameObject.SetActive(false);
                return;
            }
        }

        // UniTask 비동기 작업을 시작하고, 이 오브젝트가 파괴될 때 자동으로 취소되도록 설정합니다.
        UpdatePositionAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>
    /// HP 슬라이더의 값을 업데이트합니다.
    /// </summary>
    public void UpdateHP(float currentHp, float maxHp)
    {
        if (hpSlider != null && maxHp > 0)
        {
            hpSlider.value = currentHp / maxHp;
        }
    }

    private void OnEnable()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    // OnDisable과 코루틴 변수는 더 이상 필요 없습니다. CancellationToken이 생명주기를 관리합니다.

    /// <summary>
    /// UniTask를 사용하여 LateUpdate 이후, 렌더링 이전에 위치를 업데이트하는 비동기 메서드입니다.
    /// </summary>
    private async UniTaskVoid UpdatePositionAsync(CancellationToken cancellationToken)
    {
        // CancellationToken이 취소 요청을 받을 때까지 무한 반복합니다.
        while (!cancellationToken.IsCancellationRequested)
        {
            if (target == null || !target.gameObject.activeInHierarchy || mainCamera == null)
            {
                if(gameObject != null) Destroy(gameObject);
                return;
            }

            Vector3 screenPoint = mainCamera.WorldToScreenPoint(target.position);
            Vector3 finalPosition = screenPoint + offset;

            if (screenPoint.z < 0)
            {
                hpSlider.gameObject.SetActive(false);
            }
            else
            {
                hpSlider.gameObject.SetActive(true);
                transform.position = finalPosition;
            }
            
            // LateUpdate가 끝난 후 다음 프레임까지 대기합니다. GC Alloc이 발생하지 않습니다.
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
        }
    }
}
