using UnityEngine;
using System.Collections;
using EPOOutline;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;

[RequireComponent(typeof(Outlinable))] // 모든 터렛에 Outlinable 컴포넌트 강제
public class TurretBase : MonoBehaviour
{
    // TurretBase 클래스는 테렛의 기본 기능을 정의합니다.
    //기능 1 공격 
    //기능 2 , 배치모드 , 활성화 모드 ,대기 모드
    //드래그중엔 배치모드 , 드래그 완료 후 2초뒤에 활성화모드 후 공격 이벤트 시작
    //변수, 공격범위 , 공격속도 , 공격력, 포탑 hp
    //기능 3 , 공격범위 내 적이 있을 경우 공격모드로 전환 후 공격
    //기능 4 , 공격범위 내 적이 없을 경우 대기모드로 전환
    //기능 5 , 포탑 hp가 0이 되면 파괴
    //기능 6 , 포탑이 파괴되면 해당 오브젝트를 비활성화
    //기능 7 , 포탑이 파괴되면 해당 오브젝트를 비활성화하고 파괴 이펙트 재생
    //기능 8 , 포탑의 머리를 회전시켜 적을 바라보게 함 
    
    public enum TerretState { Placement, Idle, Active, Destroyed, Combining }
    public TerretState currentState;

    [Header("Terret Stats")]
    [SerializeField] protected float attackPower = 10f;
    [SerializeField] protected float attackSpeed = 1f;
    [SerializeField] private float hp = 100f;
    
    [Header("Effects")]
    [SerializeField] private GameObject destructionEffect;
    
    private Outlinable outlineComponent; // Outline에서 Outlinable로 변경
    private CombinationEffect combinationEffect; // 조합 효과 컴포넌트 참조
    
    // UniTask 관련
    private CancellationTokenSource cancellationTokenSource;
    
    void Awake()
    {
        cancellationTokenSource = new CancellationTokenSource();
        
        outlineComponent = GetComponent<Outlinable>();
        if (outlineComponent != null)
        {
            outlineComponent.enabled = false; // 기본적으로 비활성화
        }
        
        // 조합 효과 컴포넌트 참조 가져오기
        combinationEffect = GetComponent<CombinationEffect>();
    }

    void Start()
    {
        currentState = TerretState.Placement;
    }

    protected virtual void Update()
    {
        // 배치 중이거나 파괴된 상태 또는 조합 중인 상태에서는 아무것도 하지 않음
        if (currentState == TerretState.Placement || 
            currentState == TerretState.Destroyed ||
            currentState == TerretState.Combining)
            return;

        // 공격 로직은 자식 클래스에서 구현해야 합니다.
    }

    public void SetOutline(bool active, Color? color = null, float width = 5f)
    {
        if (outlineComponent != null)
        {
            outlineComponent.enabled = active;
            if (active && color.HasValue)
            {
                outlineComponent.OutlineParameters.Color = color.Value;
                outlineComponent.OutlineParameters.DilateShift = width;
            }
        }
    }

    // 드래그 중일 때 아웃라인 활성화 (조합 가능한 경우)
    public void EnableDragOutline()
    {
        SetOutline(true, Color.green, 5f);
    }

    // 조합 준비 상태일 때 아웃라인 활성화
    public void EnableCombinationReadyOutline()
    {
        SetOutline(true, Color.yellow, 3f);
    }

    // 아웃라인 비활성화
    public void DisableOutline()
    {
        SetOutline(false);
    }

    public void OnMouseUp()
    {
        if (currentState == TerretState.Placement)
        {
            // 비동기로 활성화 처리
            ActivateAfterDelayAsync(2.0f, cancellationTokenSource.Token).Forget();
        }
    }

    // UniTask로 변환된 활성화 메서드
    private async UniTask ActivateAfterDelayAsync(float delay, CancellationToken cancellationToken)
    {
        try
        {
            // UniTask.Delay 매개변수 수정
            await UniTask.Delay((int)(delay * 1000), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            
            if (this != null && !cancellationToken.IsCancellationRequested)
            {
                currentState = TerretState.Idle; 
                Debug.Log("터렛 활성화!");
                // 활성화 되면 공격 로직 등을 시작할 수 있습니다.
            }
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("터렛 활성화가 취소되었습니다.");
        }
    }

    private IEnumerator ActivateAfterDelay(float delay)
    {
        // 동기 버전 유지 (하위 호환성)
        yield return new WaitForSeconds(delay);
        currentState = TerretState.Idle; 
        Debug.Log("터렛 활성화!");
        // 활성화 되면 공격 로직 등을 시작할 수 있습니다.
    }

    public async UniTask TakeDamageAsync(float damage, CancellationToken cancellationToken = default)
    {
        if (currentState == TerretState.Destroyed) return;

        hp -= damage;
        
        // 데미지 처리를 다음 프레임으로 분산
        await UniTask.Yield(cancellationToken);
        
        if (hp <= 0)
        {
            await DestroyTerretAsync(cancellationToken);
        }
    }

    public void TakeDamage(float damage)
    {
        // 동기 버전 유지 (하위 호환성)
        if (currentState == TerretState.Destroyed) return;

        hp -= damage;
        if (hp <= 0)
        {
            DestroyTerret();
        }
    }

    private async UniTask DestroyTerretAsync(CancellationToken cancellationToken)
    {
        currentState = TerretState.Destroyed;
        
        if (destructionEffect != null)
        {
            // 이펙트 생성을 다음 프레임으로 분산
            await UniTask.Yield(cancellationToken);
            Instantiate(destructionEffect, transform.position, Quaternion.identity);
        }
        
        gameObject.SetActive(false);
        Debug.Log("터렛 파괴됨");
    }

    private void DestroyTerret()
    {
        // 동기 버전 유지 (하위 호환성)
        currentState = TerretState.Destroyed;
        if (destructionEffect != null)
        {
            Instantiate(destructionEffect, transform.position, Quaternion.identity);
        }
        gameObject.SetActive(false);
        Debug.Log("터렛 파괴됨");
    }
    
    // 터렛이 현재 이동 가능한 상태인지 확인
    public bool CanBeMoved()
    {
        // 터렛이 조합 중이거나 파괴된 상태면 이동 불가
        return currentState != TerretState.Combining && 
               currentState != TerretState.Destroyed;
    }
    
    // 조합 모드 시작 - 아이템과 조합될 때 호출 (비동기 버전)
    public async UniTask StartCombiningAsync(CancellationToken cancellationToken = default)
    {
        // 이미 조합 모드인 경우 무시
        if (currentState == TerretState.Combining)
            return;
            
        // 파괴된 상태면 조합 불가
        if (currentState == TerretState.Destroyed)
            return;
            
        // 조합 모드로 변경
        currentState = TerretState.Combining;
        
        // 조합 효과 표시를 다음 프레임으로 분산
        await UniTask.Yield(cancellationToken);
        
        // 조합 효과 표시 (컴포넌트가 있는 경우)
        if (combinationEffect != null)
        {
            combinationEffect.ShowCombiningEffect();
        }
        
        // 조합 아웃라인 활성화
        SetOutline(true, Color.cyan, 6f);
        
        Debug.Log($"{gameObject.name}이(가) 조합 모드로 전환");
    }

    // 조합 모드 시작 - 아이템과 조합될 때 호출
    public void StartCombining()
    {
        // 동기 버전 유지 (하위 호환성)
        // 이미 조합 모드인 경우 무시
        if (currentState == TerretState.Combining)
            return;
            
        // 파괴된 상태면 조합 불가
        if (currentState == TerretState.Destroyed)
            return;
            
        // 조합 모드로 변경
        currentState = TerretState.Combining;
        
        // 조합 효과 표시 (컴포넌트가 있는 경우)
        if (combinationEffect != null)
        {
            combinationEffect.ShowCombiningEffect();
        }
        
        // 조합 아웃라인 활성화
        SetOutline(true, Color.cyan, 6f);
        
        Debug.Log($"{gameObject.name}이(가) 조합 모드로 전환");
    }
    
    // 조합 모드 종료 - 조합이 완료되거나 취소될 때 호출 (비동기 버전)
    public async UniTask EndCombiningAsync(CancellationToken cancellationToken = default)
    {
        // 조합 중이 아니면 무시
        if (currentState != TerretState.Combining)
            return;
            
        // 이전 상태(Idle 또는 Active)로 복원
        currentState = TerretState.Idle;
        
        // 조합 효과 처리를 다음 프레임으로 분산
        await UniTask.Yield(cancellationToken);
        
        // 조합 효과 숨기기 (컴포넌트가 있는 경우)
        if (combinationEffect != null)
        {
            combinationEffect.HideCombiningEffect();
        }
        
        // 아웃라인 비활성화
        DisableOutline();
        
        Debug.Log($"{gameObject.name}이(가) 조합 모드 종료");
    }

    // 조합 모드 종료 - 조합이 완료되거나 취소될 때 호출
    public void EndCombining()
    {
        // 동기 버전 유지 (하위 호환성)
        // 조합 중이 아니면 무시
        if (currentState != TerretState.Combining)
            return;
            
        // 이전 상태(Idle 또는 Active)로 복원
        currentState = TerretState.Idle;
        
        // 조합 효과 숨기기 (컴포넌트가 있는 경우)
        if (combinationEffect != null)
        {
            combinationEffect.HideCombiningEffect();
        }
        
        // 아웃라인 비활성화
        DisableOutline();
        
        Debug.Log($"{gameObject.name}이(가) 조합 모드 종료");
    }

    // 조합 가능한 아이템인지 확인
    public bool CanCombineWithItem(ItemA item)
    {
        if (item == null) return false;
        
        // TurretCombinationManager를 통해 조합 가능성 확인
        var combinationManager = FindAnyObjectByType<TurretCombinationManager>();
        if (combinationManager != null)
        {
            return combinationManager.CanCombine(this, item);
        }
        
        return false;
    }
    
    private void OnDestroy()
    {
        // CancellationToken 정리
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
    }
}
