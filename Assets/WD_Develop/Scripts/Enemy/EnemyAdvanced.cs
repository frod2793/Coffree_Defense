using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

/// <summary>
/// 향상된 적 AI - 포탑 공격 → 카페벽 공격 패턴
/// 1. 가장 가까운 포탑을 찾아 이동
/// 2. 포탑 공격 및 파괴
/// 3. 카페 벽으로 이동하여 공격
/// </summary>
public class EnemyAdvanced : MonoBehaviour
{
    #region 열거형 및 상수

    /// <summary>
    /// 적의 행동 상태를 정의하는 열거형
    /// </summary>
    public enum EnemyState
    {
        Spawning,           // 스폰 중
        MovingToTurret,     // 포탑으로 이동 중
        AttackingTurret,    // 포탑 공격 중
        MovingToCafe,       // 카페 벽으로 이동 중
        AttackingCafe,      // 카페 공격 중
        Dying,              // 죽는 중
        Dead                // 죽음
    }

    // 게임 상수
    private const float MOVE_UPDATE_INTERVAL = 0.02f;      // 이동 업데이트 간격
    private const float HEALTH_CHECK_INTERVAL = 0.1f;      // 체력 체크 간격
    private const float ATTACK_INTERVAL = 1.0f;            // 공격 간격
    private const float TURRET_DETECT_RANGE = 50f;         // 포탑 탐지 범위
    private const float ATTACK_RANGE = 2.0f;               // 공격 범위
    private const float ARRIVAL_THRESHOLD = 1.0f;          // 도착 판정 거리

    #endregion

    #region 필드 및 속성

    [Header("적 기본 스탯")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected int coinReward = 10;
    [SerializeField] protected float attackDamage = 20f;
    [SerializeField] protected float attackCooldown = 1.0f;

    [Header("타겟 설정")]
    [SerializeField] protected string turretTag = "Turret";
    [SerializeField] protected string cafeWallTag = "CafeWall";
    
    [Header("시각적 효과")]
    [SerializeField] protected GameObject deathEffect;
    [SerializeField] protected GameObject hitEffect;
    [SerializeField] protected GameObject attackEffect;

    // 상태 관리
    public EnemyState currentState { get; private set; }
    protected float currentHealth;
    
    // 타겟 관리
    protected GameObject currentTarget;
    protected Vector3 targetPosition;
    protected bool hasFoundTurret = false;
    
    // 이동 관련
    protected Vector3 moveDirection;
    protected float lastAttackTime = 0f;
    
    // UniTask 관련
    protected CancellationTokenSource cancellationTokenSource;
    protected bool isInitialized = false;

    // 이벤트
    public event Action<GameObject> OnEnemyKilled;
    public event Action<EnemyAdvanced, float> OnHealthChanged;
    public event Action<EnemyAdvanced> OnTurretDestroyed;
    public event Action<EnemyAdvanced> OnCafeReached;

    // 공개 속성
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public bool IsAlive => currentState != EnemyState.Dead && currentHealth > 0;
    public float MoveSpeed => moveSpeed;
    public int CoinReward => coinReward;
    public EnemyState CurrentState => currentState;

    #endregion

    #region 유니티 생명주기

    protected virtual void Awake()
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
        
        UpdateEnemy();
    }

    protected virtual void OnDestroy()
    {
        CleanupResources();
    }

    #endregion

    #region 초기화

    protected virtual void InitializeComponents()
    {
        currentHealth = maxHealth;
        currentState = EnemyState.Spawning;
        lastAttackTime = Time.time;
    }

    protected virtual async UniTask InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.Yield(cancellationToken);
            
            // 가장 가까운 포탑 찾기
            await FindNearestTurretAsync(cancellationToken);
            
            // 이동 시작
            StartMovementAsync(cancellationTokenSource.Token).Forget();
            
            // 체력 모니터링 시작
            StartHealthMonitoringAsync(cancellationTokenSource.Token).Forget();
            
            // 실시간 포탑 감지 시스템 시작
            StartRealTimeTurretDetectionAsync(cancellationTokenSource.Token).Forget();
            
            isInitialized = true;
            ChangeState(EnemyState.MovingToTurret);
            
            Debug.Log($"[{gameObject.name}] 적 초기화 완료 - 타겟: {(currentTarget ? currentTarget.name : "없음")}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{gameObject.name}] 적 초기화 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 실시간으로 포탑 상태를 감지하고 타겟을 재설정합니다.
    /// </summary>
    private async UniTask StartRealTimeTurretDetectionAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsAlive)
        {
            try
            {
                // 포탑으로 이동 중일 때: 기존 로직 유지
                if (currentState == EnemyState.MovingToTurret && currentTarget != null)
                {
                    // 현재 타겟이 파괴되었거나 비활성화되었는지 확인
                    if (IsTargetInvalid(currentTarget))
                    {
                        Debug.Log($"[{gameObject.name}] 타겟 포탑이 파괴되어 새로운 포탑을 찾습니다.");
                        await FindNextTurretAsync(cancellationToken);
                    }
                    // 더 가까운 새로운 포탑이 생겼는지 확인 (조합으로 생성된 포탑 포함)
                    else
                    {
                        await CheckForBetterTurretTargetAsync(cancellationToken);
                    }
                }
                // 카페로 이동 중일 때: 포탑이 있으면 포탑으로 방향 전환
                else if (currentState == EnemyState.MovingToCafe)
                {
                    await CheckForTurretWhileMovingToCafeAsync(cancellationToken);
                }
                
                // 0.5초마다 검사
                await UniTask.Delay(500, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{gameObject.name}] 실시간 포탑 감지 오류: {ex.Message}");
                await UniTask.Delay(1000, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 현재 타겟이 유효하지 않은지 확인합니다.
    /// </summary>
    private bool IsTargetInvalid(GameObject target)
    {
        if (target == null) return true;
        
        // 오브젝트가 비활성화되었거나 파괴되었는지 확인
        if (!target.activeInHierarchy) return true;
        
        // TurretBase 컴포넌트 확인
        var turretBase = target.GetComponent<TurretBase>();
        if (turretBase != null && turretBase.currentState == TurretBase.TerretState.Destroyed)
            return true;
        
        return false;
    }

    /// <summary>
    /// 더 나은 포탑 타겟이 있는지 확인합니다 (조합으로 새로 생성된 포탑 포함).
    /// </summary>
    private async UniTask CheckForBetterTurretTargetAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        // 현재 타겟까지의 거리
        float currentDistance = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        // 모든 포탑 검색
        GameObject[] turrets = GameObject.FindGameObjectsWithTag(turretTag);
        
        foreach (var turret in turrets)
        {
            if (turret == null || turret == currentTarget) continue;
            
            // 포탑이 유효한지 확인
            var turretBase = turret.GetComponent<TurretBase>();
            if (turretBase != null && turretBase.currentState == TurretBase.TerretState.Destroyed)
                continue;
            
            float distance = Vector3.Distance(transform.position, turret.transform.position);
            
            // 더 가까운 포탑을 발견했고, 탐지 범위 내에 있으면 타겟 변경
            if (distance < currentDistance - 2f && distance <= TURRET_DETECT_RANGE) // 2m 이상 가까울 때만 변경 (떨림 방지)
            {
                Debug.Log($"[{gameObject.name}] 더 가까운 포탑 발견: {turret.name} (기존: {currentDistance:F1}m → 새로운: {distance:F1}m)");
                
                currentTarget = turret;
                targetPosition = turret.transform.position;
                hasFoundTurret = true;
                
                // 상태가 MovingToTurret이 아니라면 변경
                if (currentState != EnemyState.MovingToTurret)
                {
                    ChangeState(EnemyState.MovingToTurret);
                }
                break;
            }
        }
    }

    /// <summary>
    /// 카페로 이동 중에 포탑이 감지되면 포탑으로 방향을 전환합니다.
    /// </summary>
    private async UniTask CheckForTurretWhileMovingToCafeAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        // 모든 포탑 검색
        GameObject[] turrets = GameObject.FindGameObjectsWithTag(turretTag);
        
        GameObject nearestTurret = null;
        float nearestDistance = float.MaxValue;
        
        foreach (var turret in turrets)
        {
            if (turret == null) continue;
            
            // 포탑이 유효한지 확인
            var turretBase = turret.GetComponent<TurretBase>();
            if (turretBase != null && turretBase.currentState == TurretBase.TerretState.Destroyed)
                continue;
            
            float distance = Vector3.Distance(transform.position, turret.transform.position);
            
            // 탐지 범위 내에 있는 가장 가까운 포탑을 찾습니다
            if (distance <= TURRET_DETECT_RANGE && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestTurret = turret;
            }
        }
        
        // 포탑을 발견했으면 타겟을 변경하고 포탑으로 이동
        if (nearestTurret != null)
        {
            Debug.Log($"[{gameObject.name}] 카페로 이동 중 포탑 발견! 포탑으로 이동 전환: {nearestTurret.name} (거리: {nearestDistance:F1}m)");
            
            currentTarget = nearestTurret;
            targetPosition = nearestTurret.transform.position;
            hasFoundTurret = true;
            
            // 상태를 포탑으로 이동 중으로 변경
            ChangeState(EnemyState.MovingToTurret);
        }
    }

    protected virtual async UniTask FindNearestTurretAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        // 맵 내에 모든 포탑을 검색하여 가장 가까운 포탑을 찾습니다.
        GameObject[] turrets = GameObject.FindGameObjectsWithTag(turretTag);
        
        if (turrets.Length == 0)
        {
            Debug.LogWarning($"[{gameObject.name}] 포탑을 찾을 수 없습니다. 카페로 직접 이동합니다.");
            await FindCafeWallAsync(cancellationToken);
            hasFoundTurret = false;
            return;
        }

        GameObject nearestTurret = null;
        float nearestDistance = float.MaxValue;

        // 모든 포탑을 검사하여 가장 가까운 포탑을 찾습니다
        foreach (var turret in turrets)
        {
            if (turret == null) continue;
            
            // 포탑이 활성화되어 있고 파괴되지 않았는지 확인
            var turretBase = turret.GetComponent<TurretBase>();
            if (turretBase != null && turretBase.currentState == TurretBase.TerretState.Destroyed)
                continue;
            
            float distance = Vector3.Distance(transform.position, turret.transform.position);
            if (distance < nearestDistance && distance <= TURRET_DETECT_RANGE)
            {
                nearestDistance = distance;
                nearestTurret = turret;
            }
        }

        if (nearestTurret != null)
        {
            currentTarget = nearestTurret;
            targetPosition = nearestTurret.transform.position;
            hasFoundTurret = true;
            Debug.Log($"[{gameObject.name}] 타겟 포탑 설정: {nearestTurret.name} (거리: {nearestDistance:F1})");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] 범위 내 포탑을 찾을 수 없습니다. 카페로 이동합니다.");
            await FindCafeWallAsync(cancellationToken);
            hasFoundTurret = false;
        }
    }

    /// <summary>
    /// 현재 타겟 포탑이 파괴된 후 다음 포탑을 찾습니다.
    /// </summary>
    protected virtual async UniTask FindNextTurretAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        Debug.Log($"[{gameObject.name}] 포탑이 파괴되어 다음 포탑을 찾습니다.");
        
        // 현재 타겟을 null로 설정
        currentTarget = null;
        hasFoundTurret = false;
        
        // 새로��� 가장 가까운 포탑 찾기
        await FindNearestTurretAsync(cancellationToken);
        
        // 포탑을 찾았으면 이동 상태로 변경, 못 찾았으면 카페로 이동
        if (hasFoundTurret && currentTarget != null)
        {
            ChangeState(EnemyState.MovingToTurret);
        }
        else
        {
            ChangeState(EnemyState.MovingToCafe);
        }
    }

    /// <summary>
    /// 카페 벽을 찾습니다.
    /// </summary>
    protected virtual async UniTask FindCafeWallAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        GameObject cafeWall = GameObject.FindGameObjectWithTag(cafeWallTag);
        
        if (cafeWall != null)
        {
            currentTarget = cafeWall;
            targetPosition = cafeWall.transform.position;
            Debug.Log($"[{gameObject.name}] 카페 벽 타겟 설정: {cafeWall.name}");
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] 카페 벽을 찾을 수 없습니다!");
            // 기본 위치로 설정 (씬 중앙)
            targetPosition = Vector3.zero;
        }
    }

    #endregion

    #region 상태 관리

    protected virtual bool ShouldUpdate()
    {
        return currentState != EnemyState.Dead && currentState != EnemyState.Spawning;
    }

    protected virtual void UpdateEnemy()
    {
        switch (currentState)
        {
            case EnemyState.MovingToTurret:
                UpdateMovingToTurret();
                break;
            case EnemyState.AttackingTurret:
                UpdateAttackingTurret();
                break;
            case EnemyState.MovingToCafe:
                UpdateMovingToCafe();
                break;
            case EnemyState.AttackingCafe:
                UpdateAttackingCafe();
                break;
        }
    }

    protected virtual void ChangeState(EnemyState newState)
    {
        if (currentState == newState) return;

        var previousState = currentState;
        currentState = newState;

        OnStateChanged(previousState, newState);
    }

    protected virtual void OnStateChanged(EnemyState previousState, EnemyState newState)
    {
        Debug.Log($"[{gameObject.name}] 상태 변경: {previousState} → {newState}");

        switch (newState)
        {
            case EnemyState.MovingToTurret:
                break;
            case EnemyState.AttackingTurret:
                break;
            case EnemyState.MovingToCafe:
                FindCafeWallAsync(cancellationTokenSource.Token).Forget();
                break;
            case EnemyState.AttackingCafe:
                OnCafeReached?.Invoke(this);
                break;
            case EnemyState.Dead:
                OnEnemyDeath();
                break;
        }
    }

    #endregion

    #region 이동 시스템

    protected virtual async UniTask StartMovementAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsAlive)
        {
            try
            {
                await UpdateMovementAsync(cancellationToken);
                
                await UniTask.Delay((int)(MOVE_UPDATE_INTERVAL * 1000), 
                    DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{gameObject.name}] 이동 업데이트 오류: {ex.Message}");
                await UniTask.Delay(100, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }

    protected virtual async UniTask UpdateMovementAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);

        if (currentState == EnemyState.MovingToTurret || currentState == EnemyState.MovingToCafe)
        {
            await MoveTowardsTarget(cancellationToken);
        }
    }

    protected virtual void UpdateMovingToTurret()
    {
        if (currentTarget == null)
        {
            // 포탑이 파괴되었거나 없으면 카페로 이동
            ChangeState(EnemyState.MovingToCafe);
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        if (distanceToTarget <= ATTACK_RANGE)
        {
            ChangeState(EnemyState.AttackingTurret);
        }
    }

    protected virtual void UpdateAttackingTurret()
    {
        if (currentTarget == null)
        {
            // 포탑이 파괴되었으면 카페로 이동
            ChangeState(EnemyState.MovingToCafe);
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        if (distanceToTarget > ATTACK_RANGE)
        {
            // 포탑에서 멀어졌으면 다시 접근
            ChangeState(EnemyState.MovingToTurret);
            return;
        }

        // 공격 실행
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            AttackTarget(currentTarget);
            lastAttackTime = Time.time;
        }
    }

    protected virtual void UpdateMovingToCafe()
    {
        if (currentTarget == null)
        {
            FindCafeWallAsync(cancellationTokenSource.Token).Forget();
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        if (distanceToTarget <= ATTACK_RANGE)
        {
            ChangeState(EnemyState.AttackingCafe);
        }
    }

    protected virtual void UpdateAttackingCafe()
    {
        if (currentTarget == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
        
        if (distanceToTarget > ATTACK_RANGE)
        {
            // 카페에서 멀어졌으면 다시 접근
            ChangeState(EnemyState.MovingToCafe);
            return;
        }

        // 카페 공격 실행
        if (Time.time - lastAttackTime >= attackCooldown)
        {
            AttackTarget(currentTarget);
            lastAttackTime = Time.time;
        }
    }

    protected virtual async UniTask MoveTowardsTarget(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);

        if (currentTarget == null) return;

        Vector3 direction = (currentTarget.transform.position - transform.position).normalized;
        direction.y = 0; // Y축 이동 제한

        if (direction.sqrMagnitude > 0.01f)
        {
            // 이동
            transform.position += direction * moveSpeed * Time.deltaTime;
            
            // 회전
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    #endregion

    #region 공격 시스템

    protected virtual void AttackTarget(GameObject target)
    {
        if (target == null) return;

        // 타겟이 포탑인지 확인
        if (target.CompareTag(turretTag))
        {
            AttackTurret(target);
        }
        else if (target.CompareTag(cafeWallTag))
        {
            AttackCafe(target);
        }

        // 공격 이펙트 표시
        ShowAttackEffect();
    }

    protected virtual void AttackTurret(GameObject turret)
    {
        // 포탑에 데미지 주기
        var turretHealth = turret.GetComponent<TurretHealth>();
        if (turretHealth != null)
        {
            turretHealth.TakeDamage(attackDamage);
            Debug.Log($"[{gameObject.name}] 포탑 공격: {attackDamage} 데미지");
            
            // 포탑이 파괴되었는지 확인
            if (turretHealth.CurrentHealth <= 0)
            {
                OnTurretDestroyed?.Invoke(this);
                // 포탑이 파괴된 후 다음 포탑을 찾습니다.
                FindNextTurretAsync(cancellationTokenSource.Token).Forget();
            }
        }
        else
        {
            // TurretHealth 컴포넌트가 없으면 직접 파괴
            Debug.Log($"[{gameObject.name}] 포탑 파괴: {turret.name}");
            Destroy(turret);
            OnTurretDestroyed?.Invoke(this);
            // 포탑이 파괴된 후 다음 포탑을 찾습니다.
            FindNextTurretAsync(cancellationTokenSource.Token).Forget();
        }
    }

    protected virtual void AttackCafe(GameObject cafe)
    {
        // 카페 벽에 데미지 주기
        var cafeHealth = cafe.GetComponent<CafeHealth>();
        if (cafeHealth != null)
        {
            cafeHealth.TakeDamage(attackDamage);
            Debug.Log($"[{gameObject.name}] 카페 공격: {attackDamage} 데미지");
        }
        else
        {
            Debug.Log($"[{gameObject.name}] 카페 공격 (체력 시스템 없음)");
        }
    }

    protected virtual void ShowAttackEffect()
    {
        if (attackEffect != null)
        {
            var effect = Instantiate(attackEffect, transform.position + transform.forward, transform.rotation);
            Destroy(effect, 2f);
        }
    }

    #endregion

    #region 체력 및 데미지 시스템

    protected virtual async UniTask StartHealthMonitoringAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsAlive)
        {
            try
            {
                await CheckHealthStatusAsync(cancellationToken);
                
                await UniTask.Delay((int)(HEALTH_CHECK_INTERVAL * 1000), 
                    DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{gameObject.name}] 체력 모니터링 오류: {ex.Message}");
                await UniTask.Delay(100, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }

    protected virtual async UniTask CheckHealthStatusAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        if (currentHealth <= 0 && currentState != EnemyState.Dead)
        {
            await DieAsync(cancellationToken);
        }
    }

    public virtual async UniTask TakeDamageAsync(float damage, CancellationToken cancellationToken = default)
    {
        if (currentState == EnemyState.Dead || damage <= 0) return;

        var previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - damage);

        await UniTask.Yield(cancellationToken);

        OnHealthChanged?.Invoke(this, currentHealth);
        OnDamageTaken(damage, previousHealth, currentHealth);

        // 히트 이펙트 표시
        await ShowHitEffectAsync(cancellationToken);

        if (currentHealth <= 0)
        {
            await DieAsync(cancellationToken);
        }
    }

    public virtual void TakeDamage(float damage)
    {
        TakeDamageAsync(damage, cancellationTokenSource.Token).Forget();
    }

    protected virtual void OnDamageTaken(float damage, float previousHealth, float newHealth)
    {
        Debug.Log($"[{gameObject.name}] 데미지 {damage:F1} 받음. HP: {previousHealth:F1} → {newHealth:F1}");
    }

    protected virtual async UniTask ShowHitEffectAsync(CancellationToken cancellationToken)
    {
        if (hitEffect != null)
        {
            await UniTask.Yield(cancellationToken);
            var effect = Instantiate(hitEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }

    protected virtual async UniTask DieAsync(CancellationToken cancellationToken)
    {
        if (currentState == EnemyState.Dead) return;

        ChangeState(EnemyState.Dying);
        
        await UniTask.Yield(cancellationToken);
        
        // 죽음 이펙트 표시
        await ShowDeathEffectAsync(cancellationToken);
        
        ChangeState(EnemyState.Dead);
        
        // 코인 보상 지급
        if (DataManger.IsAvailable())
        {
            DataManger.Instance.AddCoin(coinReward);
        }
        
        Debug.Log($"[{gameObject.name}] 적 사망. 코인 {coinReward} 획득");
        
        // 1초 후 오브젝트 제거
        await UniTask.Delay(1000, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
        
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    protected virtual void OnEnemyDeath()
    {
        OnEnemyKilled?.Invoke(gameObject);
    }

    protected virtual async UniTask ShowDeathEffectAsync(CancellationToken cancellationToken)
    {
        if (deathEffect != null)
        {
            await UniTask.Yield(cancellationToken);
            var effect = Instantiate(deathEffect, transform.position, Quaternion.identity);
            Destroy(effect, 3f);
        }
    }

    #endregion

    #region 오브젝�� 풀링 지원

    /// <summary>
    /// 오브젝트 풀에서 재사용할 때 적의 상태를 초기화합��다.
    /// </summary>
    public void ResetEnemy()
    {
        // 상태 초기화
        currentState = EnemyState.Spawning;
        currentHealth = maxHealth;
        lastAttackTime = Time.time;
        
        // 타겟 초기화
        currentTarget = null;
        hasFoundTurret = false;
        
        // 이동 관련 초기화
        moveDirection = Vector3.zero;
        
        // 이벤트 초기화 (기존 구독자들 정리)
        OnEnemyKilled = null;
        OnHealthChanged = null;
        OnTurretDestroyed = null;
        OnCafeReached = null;
        
        // CancellationToken 재생성
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }
        cancellationTokenSource = new CancellationTokenSource();
        
        // 초기화 플래그 리셋
        isInitialized = false;
        
        Debug.Log($"[{gameObject.name}] 적 상태 초기화 완료 (오브젝트 풀링)");
        
        // 비동기 재초기화 시작
        InitializeAsync(cancellationTokenSource.Token).Forget();
    }

    /// <summary>
    /// 오브젝트 풀링을 위한 빠른 리셋 (동기 버전)
    /// </summary>
    public void QuickReset()
    {
        // 핵심 상태��� 빠르게 초기화
        currentState = EnemyState.MovingToTurret;
        currentHealth = maxHealth;
        currentTarget = null;
        hasFoundTurret = false;
        moveDirection = Vector3.zero;
        lastAttackTime = Time.time;
        
        // 이벤트만 정리 (재초기화는 생략)
        OnEnemyKilled = null;
        OnHealthChanged = null;
        OnTurretDestroyed = null;
        OnCafeReached = null;
    }

    #endregion

    #region 유틸리티

    /// <summary>
    /// 적의 현재 상태 정보를 반환합니다.
    /// </summary>
    public string GetStatusInfo()
    {
        string targetInfo = currentTarget ? currentTarget.name : "없음";
        return $"상태: {currentState}, HP: {currentHealth:F1}/{maxHealth}, 타겟: {targetInfo}";
    }

    /// <summary>
    /// 강제로 카페로 이동시킵니다.
    /// </summary>
    public void ForceMoveToCafe()
    {
        if (IsAlive)
        {
            currentTarget = null;
            ChangeState(EnemyState.MovingToCafe);
        }
    }

    #endregion

    #region 리소스 정리

    protected virtual void CleanupResources()
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        
        Debug.Log($"[{gameObject.name}] 리소스 정리 완료");
    }

    #endregion
}

#region 헬스 컴포넌트 인터페이���

/// <summary>
/// 포탑 체력 관리 컴포넌트 (기본 구현)
/// </summary>
public class TurretHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        
        if (currentHealth <= 0)
        {
            DestroyTurret();
        }
    }

    private void DestroyTurret()
    {
        Debug.Log($"[{gameObject.name}] 포탑 파괴됨");
        Destroy(gameObject);
    }
}

/// <summary>
/// 카페 체력 관리 컴포넌트 (기본 구현)
/// </summary>
public class CafeHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 1000f;
    private float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    public event Action<float> OnHealthChanged;
    public event Action OnCafeDestroyed;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        OnHealthChanged?.Invoke(currentHealth);
        
        if (currentHealth <= 0)
        {
            DestroyCafe();
        }
    }

    private void DestroyCafe()
    {
        Debug.Log($"[{gameObject.name}] 카페 파괴됨 - 게임 오버!");
        OnCafeDestroyed?.Invoke();
        
        // 게임 오버 처리
        var gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager != null)
        {
            gameManager.TriggerGameOver();
        }
    }
}

#endregion
