using UnityEngine;
using EPOOutline;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 터렛의 기본 기능을 정의하는 베이스 클래스.
/// DOTween을 사용한 안정적인 회전과 UniTask 기반 비동기 처리를 사용합니다.
/// </summary>
[RequireComponent(typeof(Outlinable))]
public class TurretBase : MonoBehaviour
{
    #region 열거형 및 상수
    
    public enum TerretState 
    { 
        Placement,  // 배치 중
        Idle,       // 대기 상태
        Active,     // 활성화 상태 (공격 중)
        Destroyed,  // 파괴됨
        Combining   // 조합 중
    }
    
    private const float ActivationDelay = 2.0f;
    private const float DefaultOutlineWidth = 5f;
    private const float CombinationOutlineWidth = 6f;
    
    #endregion

    #region 필드 및 속성

    [Header("터렛 공통 스탯")]
    [SerializeField] protected float attackPower = 10f;
    [SerializeField] protected float attackSpeed = 1f;
    [SerializeField] private float maxHp = 100f;
    [SerializeField] private float currentHp = 100f;
    [SerializeField] protected float range = 15f;
    
    [Header("회전 설정 (DOTween)")]
    [Tooltip("목표 각도까지 회전하는 데 걸리는 시간(초)입니다. 낮을수록 빠릅니다.")]
    [SerializeField] protected float rotationDuration = 0.15f; // 회전 속도를 약간 높여 반응성을 개선

    [Header("터렛 공통 설정")]
    [SerializeField] protected Transform turretHead;
    [SerializeField] protected string enemyTag = "Enemy";
    [SerializeField] protected float targetUpdateInterval = 0.5f;

    [Header("시각적 효과")]
    [SerializeField] private Vector3 hpBarOffset = new Vector3(0, -1.5f, 0);

    // 상태 및 타겟 관리
    public TerretState currentState { get; private set; }
    protected Transform target;
    
    // 컴포넌트 캐시
    private Outlinable outlineComponent;
    private HPBarController hpBarController;
    private InGameUIManager inGameUIManager;

    // UniTask 및 회전 관련
    private CancellationTokenSource cancellationTokenSource;
    private bool isInitialized;
    private Quaternion initialLocalRotation; // 터렛의 초기 로컬 회전값

    private static readonly Dictionary<Color, (Color color, float width)> OutlineParametersCache = new();
    
    // 공개 속성
    public float AttackPower => attackPower;
    public float AttackSpeed => attackSpeed;
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;
    public float HpPercentage => maxHp > 0 ? currentHp / maxHp : 0f;
    public bool IsAlive => currentState != TerretState.Destroyed && currentHp > 0;
    public bool CanAttack => currentState == TerretState.Active && IsAlive;
    public bool HasTarget => target != null;

    #endregion

    #region 유니티 생명주기

    void Awake()
    {
        InitializeComponents();
    }

    protected virtual async void Start()
    {
        cancellationTokenSource = new CancellationTokenSource();
        InitializeRotation();
        await InitializeAsync(cancellationTokenSource.Token);
    }

    protected virtual void LateUpdate()
    {
        if (!isInitialized || !ShouldUpdate()) return;
        RotateToTarget();
    }

    void OnDestroy()
    {
        CleanupResources();
    }

    void OnDisable()
    {
        if (turretHead != null) turretHead.DOKill();
    }

    #endregion

    #region 초기화

    private void InitializeComponents()
    {
        outlineComponent = GetComponent<Outlinable>();
        if (outlineComponent != null) outlineComponent.enabled = false;
        inGameUIManager = FindAnyObjectByType<InGameUIManager>();
        currentHp = maxHp;
    }

    private void InitializeRotation()
    {
        if (turretHead == null) return;
        initialLocalRotation = turretHead.localRotation;
    }

    private async UniTask InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.Yield(cancellationToken);
            currentState = TerretState.Placement;
            ValidateRequiredComponents();
            SetupHPBar();
            isInitialized = true;
            StartPeriodicTargetUpdateAsync(cancellationToken).Forget();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[{gameObject.name}] 터렛 초기화 실패: {ex.Message}");
        }
    }

    private void ValidateRequiredComponents()
    {
        if (inGameUIManager == null) Debug.LogError("[TurretBase] InGameUIManager를 씬에서 찾을 수 없습니다!");
        if (turretHead == null) Debug.LogWarning($"[{gameObject.name}] Turret Head가 할당되지 않았습니다.", this);
    }

    #endregion

    #region 상태 및 업데이트 관리

    protected bool ShouldUpdate()
    {
        return currentState != TerretState.Placement && 
               currentState != TerretState.Destroyed && 
               currentState != TerretState.Combining;
    }

    /// <summary>
    /// 목표를 향해 터렛을 회전시킵니다. 이 함수는 LateUpdate에서 호출되어 최종 위치를 기준으로 계산합니다.
    /// 로컬 회전을 사용하여 부모 오브젝트의 회전에 영향을 받지 않고, X/Y축 회전을 0으로 유지합니다.
    /// </summary>
    protected virtual void RotateToTarget()
    {
        if (turretHead == null) return;

        Quaternion targetLocalRotation;

        if (target != null)
        {
            // 목표 방향을 월드 좌표에서 구한 뒤, 터렛 헤드의 부모를 기준으로 하는 로컬 좌표로 변환합니다.
            // 이렇게 하면 부모의 회전에 관계없이 자식(터렛 헤드)의 로컬 회전만을 제어할 수 있습니다.
            Vector3 directionToTargetWorld = target.position - turretHead.position;
            Vector3 directionToTargetLocal = turretHead.parent.InverseTransformDirection(directionToTargetWorld);

            if (directionToTargetLocal.sqrMagnitude > 0.001f)
            {
                // 로컬 방향 벡터를 사용하여 Z축 회전 각도를 계산합니다.
                float angle = Mathf.Atan2(directionToTargetLocal.y, directionToTargetLocal.x) * Mathf.Rad2Deg;
                
                // X, Y축은 0으로 고정한 채 Z축만 회전하는 로컬 회전값을 생성합니다.
                // +90f 보정은 터렛 스프라이트의 '앞' 방향이 아래(-Y)를 향하고 있을 경우를 위한 것입니다.
                targetLocalRotation = Quaternion.Euler(0, 0, angle + 90f);
            }
            else
            {
                targetLocalRotation = turretHead.localRotation; // 목표가 너무 가까우면 현재 회전 유지
            }
        }
        else
        {
            // 목표가 없으면 초기 로컬 회전으로 복귀
            targetLocalRotation = initialLocalRotation;
        }

        // Slerp를 사용하여 현재 로컬 회전에서 목표 로컬 회전으로 부드럽게 보간합니다.
        turretHead.localRotation = Quaternion.Slerp(turretHead.localRotation, targetLocalRotation, Time.deltaTime * (1.0f / rotationDuration));
    }


    protected virtual void ChangeState(TerretState newState)
    {
        if (currentState == newState) return;
        var previousState = currentState;
        currentState = newState;
        OnStateChanged(previousState, newState);
    }

    public void SetPlacementMode()
    {
        if (CanBeMoved()) ChangeState(TerretState.Placement);
    }

    public void ForceSetState(TerretState newState)
    {
        ChangeState(newState);
    }

    protected virtual void OnStateChanged(TerretState previousState, TerretState newState)
    {
        Debug.Log($"[{gameObject.name}] 상태 변경: {previousState} → {newState}");
    }

    #endregion
    
    #region 타겟팅 시스템

    private async UniTask StartPeriodicTargetUpdateAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && this != null)
        {
            await UpdateTargetAsync(cancellationToken);
            await UniTask.Delay((int)(targetUpdateInterval * 1000), cancellationToken: cancellationToken);
        }
    }

    private async UniTask UpdateTargetAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float shortestDistance = Mathf.Infinity;
        Transform nearestEnemy = null;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;
            float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
            if (distanceToEnemy < shortestDistance && distanceToEnemy <= range)
            {
                shortestDistance = distanceToEnemy;
                nearestEnemy = enemy.transform;
            }
        }
        target = nearestEnemy;
    }

    #endregion

    #region 아웃라인 시스템

    public void SetOutline(bool active, Color? color = null, float width = DefaultOutlineWidth)
    {
        if (outlineComponent == null) return;
        outlineComponent.enabled = active;
        if (active && color.HasValue) SetOutlineParameters(color.Value, width);
    }

    private void SetOutlineParameters(Color color, float width)
    {
        if (!OutlineParametersCache.TryGetValue(color, out var parameters))
        {
            parameters = (color, width);
            OutlineParametersCache[color] = parameters;
        }
        outlineComponent.OutlineParameters.Color = color;
        outlineComponent.OutlineParameters.DilateShift = width;
    }

    public void EnableDragOutline() => SetOutline(true, Color.green);
    public void EnableCombinationReadyOutline() => SetOutline(true, Color.yellow, 3f);
    public void DisableOutline() => SetOutline(false);

    #endregion

    #region 배치 및 활성화

    public void OnMouseUp()
    {
        if (currentState == TerretState.Placement && cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
        {
            ActivateAfterDelayAsync(ActivationDelay, cancellationTokenSource.Token).Forget();
        }
    }

    private async UniTask ActivateAfterDelayAsync(float delay, CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.Delay((int)(delay * 1000), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            if (!IsObjectValid() || cancellationToken.IsCancellationRequested) return;
            if (IsAlive) ChangeState(TerretState.Idle);
        }
        catch (System.OperationCanceledException) { }
    }
    
    private bool IsObjectValid()
    {
        return this != null && gameObject != null;
    }

    #endregion

    #region 데미지 및 파괴 시스템

    public void TakeDamage(float damage)
    {
        if (currentState == TerretState.Destroyed || damage <= 0) return;
        currentHp = Mathf.Max(0, currentHp - damage);
        hpBarController?.UpdateHP(currentHp, maxHp);
        if (currentHp <= 0) DestroyTurret();
    }

    private void DestroyTurret()
    {
        ChangeState(TerretState.Destroyed);
        if (EffectManager.Instance != null) EffectManager.Instance.PlayEffect(EffectType.TurretDestroy, transform.position);
        gameObject.SetActive(false);
    }

    #endregion

    #region HP 바 시스템

    private void SetupHPBar()
    {
        if (inGameUIManager == null) return;
        hpBarController = inGameUIManager.RequestTurretHPBar(transform, hpBarOffset);
        if (hpBarController != null) hpBarController.UpdateHP(currentHp, maxHp);
    }

    #endregion

    #region 이동 및 조합 시스템

    public bool CanBeMoved() => currentState != TerretState.Combining && currentState != TerretState.Destroyed && IsAlive;

    public async UniTask StartCombiningAsync(CancellationToken cancellationToken)
    {
        if (!CanStartCombining()) return;
        ChangeState(TerretState.Combining);
        if (EffectManager.Instance != null) EffectManager.Instance.PlayLoopingEffect(EffectType.CombinationHighlight, transform, this);
        SetOutline(true, Color.cyan, CombinationOutlineWidth);
        await UniTask.Yield(cancellationToken);
    }

    private bool CanStartCombining() => currentState != TerretState.Combining && currentState != TerretState.Destroyed && IsAlive;

    public async UniTask EndCombiningAsync(CancellationToken cancellationToken)
    {
        if (currentState != TerretState.Combining) return;
        ChangeState(TerretState.Idle);
        if (EffectManager.Instance != null) EffectManager.Instance.StopLoopingEffect(this);
        DisableOutline();
        await UniTask.Yield(cancellationToken);
    }

    #endregion

    #region 유틸리티 메서드

    public virtual void UpgradeStats(float attackPowerMultiplier = 1f, float attackSpeedMultiplier = 1f, float hpMultiplier = 1f)
    {
        attackPower *= attackPowerMultiplier;
        attackSpeed *= attackSpeedMultiplier;
        var hpIncrease = maxHp * (hpMultiplier - 1f);
        maxHp *= hpMultiplier;
        currentHp += hpIncrease;
    }

    #endregion

    #region 리소스 정리

    private void CleanupResources()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
    }

    #endregion
    
    #region 기즈모

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
        
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(turretHead.position, target.position);
        }
    }

    #endregion
}
