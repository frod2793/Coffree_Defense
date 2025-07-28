using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 일반 터렛 클래스 - TurretBase를 상속받아 기본적인 발사 기능을 구현
/// TurretBone 오브젝트에 ItemA 오브젝트가 드래그앤드롭으로 충돌시 NormalTurret 오브젝트로 변환
/// UniTask 기반 비동기 처리로 성능 최적화
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class NormalTurret : TurretBase
{
    #region 필드 및 속성

    [Header("일반 터렛 전용 설정")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint; // 총알이 발사되는 위치
    [SerializeField] private Transform turretHead; // 터렛 헤드 (조준용)

    [Header("터렛 스탯")]
    [SerializeField] private float range = 15f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private string enemyTag = "Enemy";
    
    [Header("성능 설정")]
    [SerializeField] private float targetUpdateInterval = 0.5f; // 타겟 업데이트 주기
    [SerializeField] private int bulletPoolMaxSize = 20; // 총알 풀 최대 크기
    
    // 타겟팅 시스템
    private Transform target;
    private float fireCountdown;
    
    // 오브젝트 풀링
    private ObjectPool<GameObject> bulletPool;
    
    // UniTask 관련
    private CancellationTokenSource normalTurretCancellationTokenSource;
    private bool isNormalTurretInitialized;
    
    // 성능 최적화를 위한 캐시
    private readonly Collider[] enemyBuffer = new Collider[50]; // 적 검색용 버퍼

    // 공개 속성
    public float Range => range;
    public bool HasTarget => target != null;
    public string TargetName => target != null ? target.name : "없음";

    #endregion

    #region 유니티 생명주기

    protected override void Start()
    {
        // TurretBase의 Start를 먼저 호출하여 기본 초기화 완료
        base.Start();
        
        normalTurretCancellationTokenSource = new CancellationTokenSource();
        
        // 필수 컴포넌트 검증
        ValidateComponents();
        
        // 비동기 초기화
        InitializeNormalTurretAsync(normalTurretCancellationTokenSource.Token).Forget();
        
        // 주기적 타겟 업데이트 시작
        StartPeriodicTargetUpdateAsync(normalTurretCancellationTokenSource.Token).Forget();
    }

    protected override void Update()
    {
        base.Update(); // TurretBase의 Update 호출
        
        if (!isNormalTurretInitialized || !ShouldUpdateNormalTurret()) return;
        
        // 터렛 헤드 회전 (매 프레임 필요)
        UpdateTurretHeadRotation();
        
        // 발사 카운트다운 업데이트
        UpdateFireCountdown();
        
        // 공격 로직
        UpdateAttackLogic();
    }

    void OnDestroy()
    {
        CleanupNormalTurretResources();
        // base.OnDestroy는 자동으로 호출됨
    }

    #endregion

    #region 초기화

    private void ValidateComponents()
    {
        if (bulletPrefab == null)
            Debug.LogError($"[{gameObject.name}] Bullet Prefab이 할당되지 않았습니다.", this);
        if (firePoint == null)
            Debug.LogError($"[{gameObject.name}] Fire Point가 할당되지 않았습니다.", this);
        if (turretHead == null)
            Debug.LogError($"[{gameObject.name}] Turret Head가 할당되지 않았습니다.", this);
    }

    private async UniTask InitializeNormalTurretAsync(CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.Yield(cancellationToken);
            
            // 오브젝트 풀 초기화
            InitializeBulletPool();
            
            isNormalTurretInitialized = true;
            
            Debug.Log($"[{gameObject.name}] 일반 터렛 초기화 완료");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[{gameObject.name}] 일반 터렛 초기화 실패: {ex.Message}");
        }
    }

    private void InitializeBulletPool()
    {
        bulletPool = new ObjectPool<GameObject>(
            CreateBullet,
            OnGetFromPool,
            OnReleaseToPool,
            OnDestroyBullet,
            maxSize: bulletPoolMaxSize
        );
    }

    #endregion

    #region 업데이트 로직

    private bool ShouldUpdateNormalTurret()
    {
        return currentState != TerretState.Placement && 
               currentState != TerretState.Destroyed && 
               currentState != TerretState.Combining;
    }

    private void UpdateTurretHeadRotation()
    {
        if (target == null || turretHead == null) return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0; // Y축 회전만 허용 (수평 회전)
        
        if (direction.sqrMagnitude > 0.01f) // 최소 거리 체크로 성능 최적화
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            turretHead.rotation = Quaternion.Slerp(
                turretHead.rotation, 
                lookRotation, 
                Time.deltaTime * turnSpeed
            );
        }
    }

    private void UpdateFireCountdown()
    {
        if (fireCountdown > 0)
        {
            fireCountdown -= Time.deltaTime;
        }
    }

    private void UpdateAttackLogic()
    {
        if (target != null)
        {
            ChangeState(TerretState.Active);
            
            if (fireCountdown <= 0f)
            {
                // 비동기 발사
                ShootAsync(normalTurretCancellationTokenSource.Token).Forget();
                fireCountdown = 1f / attackSpeed; // 공격 속도에 따라 다음 발사 시간 설정
            }
        }
        else
        {
            ChangeState(TerretState.Idle);
        }
    }

    #endregion

    #region 타겟팅 시스템

    /// <summary>
    /// 주기적으로 타겟을 업데이트합니다. (비동기 버전)
    /// </summary>
    private async UniTask StartPeriodicTargetUpdateAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && this != null)
        {
            try
            {
                await UpdateTargetAsync(cancellationToken);
                
                // 설정된 간격마다 타겟 업데이트
                await UniTask.Delay(
                    (int)(targetUpdateInterval * 1000), 
                    DelayType.DeltaTime, 
                    PlayerLoopTiming.Update, 
                    cancellationToken
                );
            }
            catch (System.OperationCanceledException)
            {
                // 정상적인 취소
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[{gameObject.name}] 타겟 업데이트 오류: {ex.Message}");
                // 오류 발생 시 잠시 대기 후 재시도
                await UniTask.Delay(1000, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 최적화된 타겟 검색 (Physics.OverlapSphere 사용)
    /// </summary>
    private async UniTask UpdateTargetAsync(CancellationToken cancellationToken)
    {
        if (currentState == TerretState.Placement)
        {
            target = null;
            return;
        }
        
        // 적 검색을 비동기로 처리
        await UniTask.Yield(cancellationToken);
        
        // Physics.OverlapSphere를 사용한 성능 최적화된 적 검색 (Enemy 태그 검색으로 변경)
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

    /// <summary>
    /// 수동으로 타겟을 설정합니다.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        if (newTarget != null && Vector3.Distance(transform.position, newTarget.position) <= range)
        {
            target = newTarget;
        }
    }

    /// <summary>
    /// 현재 타겟을 제거합니다.
    /// </summary>
    public void ClearTarget()
    {
        target = null;
    }

    #endregion

    #region 발사 시스템

    /// <summary>
    /// 비동기 발사 메서드
    /// </summary>
    private async UniTask ShootAsync(CancellationToken cancellationToken)
    {
        if (!CanShoot()) return;

        try
        {
            // 총알 생성을 다음 프레임으로 분산
            await UniTask.Yield(cancellationToken);
            
            // 오브젝트 풀에서 총알을 가져옵니다.
            GameObject bulletGo = bulletPool.Get();
            if (bulletGo == null) return;

            ConfigureBullet(bulletGo);
            
            Debug.Log($"[{gameObject.name}] 발사! 타겟: {target.name}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[{gameObject.name}] 발사 오류: {ex.Message}");
        }
    }

    private bool CanShoot()
    {
        return bulletPrefab != null && 
               firePoint != null && 
               target != null && 
               currentState == TerretState.Active;
    }

    private void ConfigureBullet(GameObject bulletGo)
    {
        bulletGo.transform.position = firePoint.position;
        bulletGo.transform.rotation = firePoint.rotation;
        
        Bullet bullet = bulletGo.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.Seek(target, bulletPool);
        }
    }

    #endregion

    #region 오브젝트 풀링

    private GameObject CreateBullet()
    {
        if (bulletPrefab == null) return null;
        
        GameObject bulletGo = Instantiate(bulletPrefab);
        Bullet bulletComponent = bulletGo.GetComponent<Bullet>();

        if (bulletComponent == null)
        {
            Debug.LogError($"[{gameObject.name}] Bullet Prefab에 Bullet 스크립트가 없습니다.", bulletGo);
            Destroy(bulletGo);
            return null;
        }
        
        bulletComponent.damage = attackPower;
        return bulletGo;
    }

    private void OnGetFromPool(GameObject bullet)
    {
        if (bullet != null)
        {
            bullet.SetActive(true);
            
            // 총알 데미지를 현재 공격력으로 업데이트
            Bullet bulletComponent = bullet.GetComponent<Bullet>();
            if (bulletComponent != null)
            {
                bulletComponent.damage = attackPower;
            }
        }
    }

    private void OnReleaseToPool(GameObject bullet)
    {
        if (bullet != null)
        {
            bullet.SetActive(false);
        }
    }

    private void OnDestroyBullet(GameObject bullet)
    {
        if (bullet != null)
        {
            Destroy(bullet);
        }
    }

    #endregion

    #region 스탯 오버라이드

    /// <summary>
    /// 스탯 업그레이드 시 총알 풀의 데미지도 업데이트
    /// </summary>
    public override void UpgradeStats(float attackPowerMultiplier = 1f, float attackSpeedMultiplier = 1f, float hpMultiplier = 1f)
    {
        base.UpgradeStats(attackPowerMultiplier, attackSpeedMultiplier, hpMultiplier);
        
        // 기존 총알들의 데미지 업데이트는 다음 발사 시 적용됨
        Debug.Log($"[{gameObject.name}] 일반 터렛 스탯 업그레이드 완료");
    }

    #endregion

    #region 유틸리티 메서드

    /// <summary>
    /// 터렛의 상세 상태 정보를 반환합니다.
    /// </summary>
    public new string GetStatusInfo()
    {
        return base.GetStatusInfo() + 
               $", 사거리: {range:F1}, 타겟: {TargetName}, 풀 사용량: {bulletPool?.CountActive ?? 0}/{bulletPoolMaxSize}";
    }

    /// <summary>
    /// 사거리를 변경합니다.
    /// </summary>
    public void SetRange(float newRange)
    {
        range = Mathf.Max(0, newRange);
    }

    /// <summary>
    /// 회전 속도를 변경합니다.
    /// </summary>
    public void SetTurnSpeed(float newTurnSpeed)
    {
        turnSpeed = Mathf.Max(0, newTurnSpeed);
    }

    #endregion

    #region 기즈모 및 디버그

    void OnDrawGizmosSelected()
    {
        // 사거리 표시
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
        
        // 타겟 연결선 표시
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target.position);
        }
        
        // 발사 포인트 표시
        if (firePoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(firePoint.position, 0.1f);
        }
    }

    #endregion

    #region 리소스 정리

    private void CleanupNormalTurretResources()
    {
        // CancellationToken 정리
        normalTurretCancellationTokenSource?.Cancel();
        normalTurretCancellationTokenSource?.Dispose();
        
        // 오브젝트 풀 정리
        bulletPool?.Dispose();
        
        Debug.Log($"[{gameObject.name}] 일반 터렛 리소스 정리 완료");
    }

    #endregion
}
