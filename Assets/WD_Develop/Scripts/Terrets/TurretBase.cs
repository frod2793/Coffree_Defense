using UnityEngine;
using EPOOutline;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

/// <summary>
/// 터렛의 기본 기능을 정의하는 베이스 클래스
/// UniTask 기반 비동기 처리로 최적화된 성능을 제공합니다.
/// </summary>
[RequireComponent(typeof(Outlinable))]
public class TurretBase : MonoBehaviour
{
    #region 열거형 및 상수
    
    /// <summary>
    /// 터렛의 상태를 정의하는 열거형
    /// </summary>
    public enum TerretState 
    { 
        Placement,  // 배치 중
        Idle,       // 대기 상태
        Active,     // 활성화 상태 (공격 중)
        Destroyed,  // 파괴됨
        Combining   // 조합 중
    }
    
    // 성능 최적화를 위한 상수
    private const float ActivationDelay = 2.0f;
    private const float DefaultOutlineWidth = 5f;
    private const float CombinationOutlineWidth = 6f;
    private const int DamageDelayMs = 16; // 약 1프레임
    
    #endregion

    #region 필드 및 속성

    [Header("터렛 기본 스탯")]
    [SerializeField] protected float attackPower = 10f;
    [SerializeField] protected float attackSpeed = 1f;
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private float currentHp = 100f;
    
    [Header("시각적 효과")]
    [SerializeField] private GameObject destructionEffect;
    
    // 상태 관리
    public TerretState currentState { get; private set; }
    
    // 컴포넌트 캐시
    private Outlinable outlineComponent;
    private CombinationEffect combinationEffect;
    
    // UniTask 관련
    private CancellationTokenSource cancellationTokenSource;
    private bool isInitialized;
    
    // 성능 최적화를 위한 캐시
    private static readonly Dictionary<Color, (Color color, float width)> OutlineParametersCache = 
        new Dictionary<Color, (Color, float)>();
    
    // 공개 속성
    public float AttackPower => attackPower;
    public float AttackSpeed => attackSpeed;
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float HpPercentage => maxHp > 0 ? currentHp / maxHp : 0f;
    public bool IsAlive => currentState != TerretState.Destroyed && currentHp > 0;
    public bool CanAttack => currentState == TerretState.Active && IsAlive;

    #endregion

    #region 유니티 생명주기

    void Awake()
    {
        InitializeComponents();
    }

    protected virtual async void Start()
    {
        cancellationTokenSource = new CancellationTokenSource();
        await InitializeAsync(cancellationTokenSource.Token);
    }

    protected virtual void Update()
    {
        if (!isInitialized || !ShouldUpdate()) return;
        
        // 공격 로직은 자식 클래스에서 구현
        UpdateTurret();
    }

    void OnDestroy()
    {
        CleanupResources();
    }

    #endregion

    #region 초기화

    private void InitializeComponents()
    {
        // 컴포넌트 캐시
        outlineComponent = GetComponent<Outlinable>();
        if (outlineComponent != null)
        {
            outlineComponent.enabled = false;
        }
        
        combinationEffect = GetComponent<CombinationEffect>();
        
        // HP 초기화
        currentHp = maxHp;
    }

    private async UniTask InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.Yield(cancellationToken);
            
            currentState = TerretState.Placement;
            
            // 필수 컴포넌트 검증
            ValidateRequiredComponents();
            
            isInitialized = true;
            
            Debug.Log($"[{gameObject.name}] 터렛 초기화 완료");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[{gameObject.name}] 터렛 초기화 실패: {ex.Message}");
        }
    }

    private void ValidateRequiredComponents()
    {
        if (outlineComponent == null)
        {
            Debug.LogWarning($"[{gameObject.name}] Outlinable 컴포넌트가 없습니다.");
        }
    }

    #endregion

    #region 상태 관리

    private bool ShouldUpdate()
    {
        return currentState != TerretState.Placement && 
               currentState != TerretState.Destroyed && 
               currentState != TerretState.Combining;
    }

    protected virtual void UpdateTurret()
    {
        // 자식 클래스에서 구체적인 업데이트 로직 구현
    }

    /// <summary>
    /// 터렛 상태를 안전하게 변경합니다.
    /// </summary>
    protected virtual void ChangeState(TerretState newState)
    {
        if (currentState == newState) return;
        
        var previousState = currentState;
        currentState = newState;
        
        OnStateChanged(previousState, newState);
    }

    /// <summary>
    /// 외부에서 터렛을 배치 모드로 설정합니다.
    /// </summary>
    public void SetPlacementMode()
    {
        if (CanBeMoved())
        {
            ChangeState(TerretState.Placement);
        }
    }

    /// <summary>
    /// 외부에서 터렛 상태를 강제로 설정합니다. (신중하게 사용)
    /// </summary>
    public void ForceSetState(TerretState newState)
    {
        ChangeState(newState);
    }

    protected virtual void OnStateChanged(TerretState previousState, TerretState newState)
    {
        Debug.Log($"[{gameObject.name}] 상태 변경: {previousState} → {newState}");
    }

    #endregion

    #region 아웃라인 시스템

    /// <summary>
    /// 터렛의 아웃라인을 설정합니다. (성능 최적화된 버전)
    /// </summary>
    public void SetOutline(bool active, Color? color = null, float width = DefaultOutlineWidth)
    {
        if (outlineComponent == null) return;

        outlineComponent.enabled = active;
        
        if (active && color.HasValue)
        {
            SetOutlineParameters(color.Value, width);
        }
    }

    private void SetOutlineParameters(Color color, float width)
    {
        // 아웃라인 파라미터 캐싱으로 성능 최적화
        if (!OutlineParametersCache.TryGetValue(color, out var parameters))
        {
            parameters = (color, width);
            OutlineParametersCache[color] = parameters;
        }

        outlineComponent.OutlineParameters.Color = color;
        outlineComponent.OutlineParameters.DilateShift = width;
    }

    /// <summary>
    /// 드래그 중 아웃라인 활성화 (조합 가능한 경우)
    /// </summary>
    public void EnableDragOutline()
    {
        SetOutline(true, Color.green);
    }

    /// <summary>
    /// 조합 준비 상태 아웃라인 활성화
    /// </summary>
    public void EnableCombinationReadyOutline()
    {
        SetOutline(true, Color.yellow, 3f);
    }

    /// <summary>
    /// 아웃라인 비활성화
    /// </summary>
    public void DisableOutline()
    {
        SetOutline(false);
    }

    #endregion

    #region 배치 및 활성화

    public void OnMouseUp()
    {
        if (currentState == TerretState.Placement && cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
        {
            ActivateAfterDelayAsync(ActivationDelay, cancellationTokenSource.Token).Forget();
        }
        else if (cancellationTokenSource == null)
        {
            Debug.LogWarning($"[{gameObject.name}] CancellationTokenSource가 null입니다. 터렛이 초기화되지 않았을 수 있습니다.");
        }
    }

    /// <summary>
    /// 지연 후 터렛을 활성화합니다. (비동기 버전)
    /// </summary>
    private async UniTask ActivateAfterDelayAsync(float delay, CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.Delay((int)(delay * 1000), DelayType.DeltaTime, 
                PlayerLoopTiming.Update, cancellationToken);
            
            // 오브젝트가 파괴되었는지 안전하게 확인 (MissingReferenceException 방지)
            if (!IsObjectValid() || cancellationToken.IsCancellationRequested)
            {
                Debug.Log("터렛 오브젝트가 파괴되어 활성화를 취소합니다.");
                return;
            }
            
            if (IsAlive)
            {
                ChangeState(TerretState.Idle);
                OnTurretActivated();
            }
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("터렛 활성화가 취소되었습니다.");
        }
        catch (System.Exception ex)
        {
            // MissingReferenceException이나 기타 예외 처리
            if (ex is MissingReferenceException)
            {
                Debug.Log("터렛 오브젝트가 이미 파괴되었습니다.");
            }
            else
            {
                Debug.LogError($"터렛 활성화 중 오류 발생: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// 오브젝트가 여전히 유효한지 안전하게 확인합니다.
    /// </summary>
    private bool IsObjectValid()
    {
        try
        {
            // this와 gameObject에 접근해서 예외가 발생하면 오브젝트가 파괴된 것
            return this != null && gameObject != null && gameObject.activeInHierarchy;
        }
        catch (MissingReferenceException)
        {
            return false;
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    protected virtual void OnTurretActivated()
    {
        Debug.Log($"[{gameObject.name}] 터렛 활성화!");
    }

    #endregion

    #region 데미지 및 파괴 시스템

    /// <summary>
    /// 터렛이 데미지를 받습니다. (비동기 버전)
    /// </summary>
    public async UniTask TakeDamageAsync(float damage, CancellationToken cancellationToken = default)
    {
        if (currentState == TerretState.Destroyed || damage <= 0) return;

        var previousHp = currentHp;
        currentHp = Mathf.Max(0, currentHp - damage);
        
        // 데미지 처리를 다음 프레임으로 분산
        await UniTask.Delay(DamageDelayMs, DelayType.DeltaTime, 
            PlayerLoopTiming.Update, cancellationToken);
        
        OnDamageTaken(damage, previousHp, currentHp);
        
        if (currentHp <= 0)
        {
            await DestroyTurretAsync(cancellationToken);
        }
    }

    /// <summary>
    /// 터렛이 데미지를 받습니다. (동기 버전 - 하위 호환성)
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (currentState == TerretState.Destroyed || damage <= 0) return;

        var previousHp = currentHp;
        currentHp = Mathf.Max(0, currentHp - damage);
        
        OnDamageTaken(damage, previousHp, currentHp);
        
        if (currentHp <= 0)
        {
            DestroyTurret();
        }
    }

    /// <summary>
    /// HP를 회복합니다.
    /// </summary>
    public void Heal(float amount)
    {
        if (currentState == TerretState.Destroyed || amount <= 0) return;

        var previousHp = currentHp;
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        
        OnHealed(amount, previousHp, currentHp);
    }

    protected virtual void OnDamageTaken(float damage, float previousHp, float newHp)
    {
        Debug.Log($"[{gameObject.name}] 데미지 {damage:F1} 받음. HP: {previousHp:F1} → {newHp:F1}");
    }

    protected virtual void OnHealed(float amount, float previousHp, float newHp)
    {
        Debug.Log($"[{gameObject.name}] 회복 {amount:F1}. HP: {previousHp:F1} → {newHp:F1}");
    }

    /// <summary>
    /// 터렛을 파괴합니다. (비동기 버전)
    /// </summary>
    private async UniTask DestroyTurretAsync(CancellationToken cancellationToken)
    {
        ChangeState(TerretState.Destroyed);
        
        await PlayDestructionEffectAsync(cancellationToken);
        
        OnTurretDestroyed();
        
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 터렛을 파괴합니다. (동기 버전 - 하위 호환성)
    /// </summary>
    private void DestroyTurret()
    {
        ChangeState(TerretState.Destroyed);
        
        PlayDestructionEffect();
        
        OnTurretDestroyed();
        
        gameObject.SetActive(false);
    }

    private async UniTask PlayDestructionEffectAsync(CancellationToken cancellationToken)
    {
        if (destructionEffect != null)
        {
            await UniTask.Yield(cancellationToken);
            Instantiate(destructionEffect, transform.position, Quaternion.identity);
        }
    }

    private void PlayDestructionEffect()
    {
        if (destructionEffect != null)
        {
            Instantiate(destructionEffect, transform.position, Quaternion.identity);
        }
    }

    protected virtual void OnTurretDestroyed()
    {
        Debug.Log($"[{gameObject.name}] 터렛 파괴됨");
    }

    #endregion

    #region 이동 및 조합 시스템

    /// <summary>
    /// 터렛이 현재 이동 가능한 상태인지 확인합니다.
    /// </summary>
    public bool CanBeMoved()
    {
        return currentState != TerretState.Combining && 
               currentState != TerretState.Destroyed &&
               IsAlive;
    }

    /// <summary>
    /// 조합 모드를 시작합니다. (비동기 버전)
    /// </summary>
    public async UniTask StartCombiningAsync(CancellationToken cancellationToken = default)
    {
        if (!CanStartCombining()) return;
            
        ChangeState(TerretState.Combining);
        
        // 조합 효과 표시를 다음 프레임으로 분산
        await UniTask.Yield(cancellationToken);
        
        ShowCombiningEffect();
        SetOutline(true, Color.cyan, CombinationOutlineWidth);
        
        OnCombiningStarted();
    }

    /// <summary>
    /// 조합 모드를 시작합니다. (동기 버전 - 하위 호환성)
    /// </summary>
    public void StartCombining()
    {
        if (!CanStartCombining()) return;
            
        ChangeState(TerretState.Combining);
        
        ShowCombiningEffect();
        SetOutline(true, Color.cyan, CombinationOutlineWidth);
        
        OnCombiningStarted();
    }

    private bool CanStartCombining()
    {
        return currentState != TerretState.Combining && 
               currentState != TerretState.Destroyed &&
               IsAlive;
    }

    private void ShowCombiningEffect()
    {
        combinationEffect?.ShowCombiningEffect();
    }

    protected virtual void OnCombiningStarted()
    {
        Debug.Log($"[{gameObject.name}] 조합 모드 시작");
    }
    
    /// <summary>
    /// 조합 모드를 종료합니다. (비동기 버전)
    /// </summary>
    public async UniTask EndCombiningAsync(CancellationToken cancellationToken = default)
    {
        if (currentState != TerretState.Combining) return;
            
        ChangeState(TerretState.Idle);
        
        // 조합 효과 처리를 다음 프레임으로 분산
        await UniTask.Yield(cancellationToken);
        
        HideCombiningEffect();
        DisableOutline();
        
        OnCombiningEnded();
    }

    /// <summary>
    /// 조합 모드를 종료합니다. (동기 버전 - 하위 호환성)
    /// </summary>
    public void EndCombining()
    {
        if (currentState != TerretState.Combining) return;
            
        ChangeState(TerretState.Idle);
        
        HideCombiningEffect();
        DisableOutline();
        
        OnCombiningEnded();
    }

    private void HideCombiningEffect()
    {
        combinationEffect?.HideCombiningEffect();
    }

    protected virtual void OnCombiningEnded()
    {
        Debug.Log($"[{gameObject.name}] 조합 모드 종료");
    }

    /// <summary>
    /// 아이템과 조합 가능한지 확인합니다.
    /// </summary>
    public bool CanCombineWithItem(ItemA item)
    {
        if (item == null || !CanStartCombining()) return false;
        
        // TurretCombinationManager를 통해 조합 가능성 확인
        var combinationManager = FindAnyObjectByType<TurretCombinationManager>();
        return combinationManager?.CanCombine(this, item) ?? false;
    }

    #endregion

    #region 유틸리티 메서드

    /// <summary>
    /// 터렛의 스탯을 업그레이드합니다.
    /// </summary>
    public virtual void UpgradeStats(float attackPowerMultiplier = 1f, float attackSpeedMultiplier = 1f, float hpMultiplier = 1f)
    {
        attackPower *= attackPowerMultiplier;
        attackSpeed *= attackSpeedMultiplier;
        
        var hpIncrease = maxHp * (hpMultiplier - 1f);
        maxHp *= hpMultiplier;
        currentHp += hpIncrease; // 현재 HP도 비례적으로 증가
        
        Debug.Log($"[{gameObject.name}] 스탯 업그레이드 완료");
    }

    /// <summary>
    /// 터렛의 현재 상태 정보를 반환합니다.
    /// </summary>
    public string GetStatusInfo()
    {
        return $"상태: {currentState}, HP: {currentHp:F1}/{maxHp:F1}, 공격력: {attackPower:F1}, 공격속도: {attackSpeed:F1}";
    }

    #endregion

    #region 리소스 정리

    /// <summary>
    /// 리소스를 정리합니다.
    /// </summary>
    private void CleanupResources()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        
        Debug.Log($"[{gameObject.name}] 리소스 정리 완료");
    }

    #endregion
}
