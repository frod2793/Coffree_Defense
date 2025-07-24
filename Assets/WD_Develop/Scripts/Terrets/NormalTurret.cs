using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using System.Threading;

//역할:
// 터렛 오브젝트에 추가될 클래스 

// TurretBone 오브젝트에 ItemA 오브젝트가 드래그앤 드랍으로 충돌시
// NormalTurret 오브젝트로 변환

[RequireComponent(typeof(BoxCollider))]
public class NormalTurret:TurretBase
{
    [Header("Normal Terret Specifics")]
    [SerializeField] private GameObject bulletPrefab;
    
    [SerializeField] private Transform firePoint; // 총알이 발사되는 위치
    [SerializeField] private Transform terretHead;

    [Header("Terret Stats")]
    [SerializeField] private float range = 15f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private string enemyTag = "Enemy";
    
    private Transform target;
    private float fireCountdown = 0f;
    
    private ObjectPool<GameObject> bulletPool;
    private CancellationTokenSource cancellationTokenSource;
    
    
    // 
    
    async void Start()
    {
        cancellationTokenSource = new CancellationTokenSource();
        
        // 필수 컴포넌트 및 변수가 할당되었는지 확인
        if (bulletPrefab == null)
            Debug.LogError("Bullet Prefab이 할당되지 않았습니다.", this);
        if (firePoint == null)
            Debug.LogError("Fire Point가 할당되지 않았습니다.", this);
        if (terretHead == null)
            Debug.LogError("Terret Head가 할당되지 않았습니다.", this);

        // 비동기 초기화
        await InitializeAsync(cancellationTokenSource.Token);
        
        // 주기적 타겟 업데이트 시작
        StartPeriodicTargetUpdateAsync(cancellationTokenSource.Token).Forget();
    }
    
    private async UniTask InitializeAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        bulletPool = new ObjectPool<GameObject>(
            CreateBullet,
            OnGetFromPool,
            OnReleaseToPool,
            OnDestroyBullet,
            maxSize: 20
        );
        
        Debug.Log("[NormalTurret] 초기화 완료");
    }
    
    // 주기적 타겟 업데이트를 UniTask로 처리
    private async UniTask StartPeriodicTargetUpdateAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && this != null)
        {
            try
            {
                await UpdateTargetAsync(cancellationToken);
                
                // 0.5초마다 타겟 업데이트 - UniTask.Delay 매개변수 수정
                await UniTask.Delay(500, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                // 정상적인 취소
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NormalTurret] 타겟 업데이트 오류: {ex.Message}");
                // 오류 발생 시 잠시 대기 후 재시도 - UniTask.Delay 매개변수 수정
                await UniTask.Delay(1000, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }

    protected override void Update()
    {
        // 배치 중이거나 파괴된 상태 또는 조합 중인 상태에서는 아무것도 하지 않음
        if (currentState == TerretState.Placement || 
            currentState == TerretState.Destroyed ||
            currentState == TerretState.Combining)
            return;

        // 목표물 조준 (비동기로 처리하지 않는 부분 - 매 프레임 필요)
        if (target != null && currentState != TerretState.Placement)
        {
            Vector3 dir = target.position - transform.position;
            Quaternion lookRotation = Quaternion.LookRotation(dir);
            Vector3 rotation = Quaternion.Slerp(terretHead.rotation, lookRotation, Time.deltaTime * turnSpeed).eulerAngles;
            terretHead.rotation = Quaternion.Euler(0f, rotation.y, 0f);
        }

        // 카운트다운 감소 (드래그 중이 아닐 때만)
        if (fireCountdown > 0 && currentState != TerretState.Placement)
        {
            fireCountdown -= Time.deltaTime;
        }

        // 공격 로직 (드래그 중이 아닐 때만 실행)
        if (target != null && currentState != TerretState.Placement)
        {
            currentState = TerretState.Active;
            if (fireCountdown <= 0f)
            {
                // 비동기 발사
                ShootAsync(cancellationTokenSource.Token).Forget();
                fireCountdown = 1f / attackSpeed; // 공격 속도에 따라 다음 발사 시간 설정
            }
        }
        else if (currentState != TerretState.Placement)
        {
            currentState = TerretState.Idle;
        }
    }

    private async UniTask ShootAsync(CancellationToken cancellationToken)
    {
        if (bulletPrefab != null && firePoint != null)
        {
            // 총알 생성을 다음 프레임으로 분산
            await UniTask.Yield(cancellationToken);
            
            // 오브젝트 풀에서 총알을 가져옵니다.
            GameObject bulletGO = bulletPool.Get();
            if (bulletGO == null) return;

            bulletGO.transform.position = firePoint.position;
            bulletGO.transform.rotation = firePoint.rotation;
            
            Bullet bullet = bulletGO.GetComponent<Bullet>();
            if (bullet != null)
            {
                bullet.Seek(target, bulletPool);
            }
        }
        Debug.Log("발사!");
    }
    
    void Shoot()
    {
        // 동기 버전 유지 (하위 호환성)
        if (bulletPrefab != null && firePoint != null)
        {
            // 오브젝트 풀에서 총알을 가져옵니다.
            GameObject bulletGO = bulletPool.Get();
            if (bulletGO == null) return;

            bulletGO.transform.position = firePoint.position;
            bulletGO.transform.rotation = firePoint.rotation;
            
            Bullet bullet = bulletGO.GetComponent<Bullet>();
            if (bullet != null)
            {
                bullet.Seek(target, bulletPool);
            }
        }
        Debug.Log("발사!");
    }
    
    // --- Object Pool Methods ---
    private GameObject CreateBullet()
    {
        if (bulletPrefab == null) return null;
        
        GameObject bulletGO = Instantiate(bulletPrefab);
        Bullet bulletComponent = bulletGO.GetComponent<Bullet>();

        if (bulletComponent == null)
        {
            Debug.LogError("Bullet Prefab에 Bullet 스크립트가 없습니다.", bulletGO);
            Destroy(bulletGO); // 잘못된 오브젝트는 파괴
            return null;
        }
        
        bulletComponent.damage = attackPower;
        return bulletGO;
    }

    private void OnGetFromPool(GameObject bullet)
    {
        bullet.SetActive(true);
    }

    private void OnReleaseToPool(GameObject bullet)
    {
        bullet.SetActive(false);
    }

    private void OnDestroyBullet(GameObject bullet)
    {
        Destroy(bullet);
    }
    
    private async UniTask UpdateTargetAsync(CancellationToken cancellationToken)
    {
        if (currentState == TerretState.Placement)
        {
            target = null;
            return;
        }
        
        // 적 검색을 비동기로 처리
        await UniTask.Yield(cancellationToken);
        
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float shortestDistance = Mathf.Infinity;
        GameObject nearestEnemy = null;

        foreach (GameObject enemy in enemies)
        {
            float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
            if (distanceToEnemy < shortestDistance)
            {
                shortestDistance = distanceToEnemy;
                nearestEnemy = enemy;
            }
        }

        if (nearestEnemy != null && shortestDistance <= range)
        {
            target = nearestEnemy.transform;
        }
        else
        {
            target = null;
        }
    }
    
    void UpdateTarget()
    {
        // 동기 버전 유지 (하위 호환성)
        if (currentState == TerretState.Placement)
        {
            target = null;
            return;
        }
        
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float shortestDistance = Mathf.Infinity;
        GameObject nearestEnemy = null;

        foreach (GameObject enemy in enemies)
        {
            float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
            if (distanceToEnemy < shortestDistance)
            {
                shortestDistance = distanceToEnemy;
                nearestEnemy = enemy;
            }
        }

        if (nearestEnemy != null && shortestDistance <= range)
        {
            target = nearestEnemy.transform;
        }
        else
        {
            target = null;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
    
    private void OnDestroy()
    {
        // CancellationToken 정리
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
    }
}
