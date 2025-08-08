using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 더블 배럴 터렛 클래스 - 두 개의 포신에서 순차적으로 총알을 발사합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DoubleShotTurret : TurretBase
{
    #region 필드 및 속성

    [Header("더블샷 터렛 전용 설정")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform[] firePoints;
    [SerializeField] private float delayBetweenShots = 0.1f;
    [SerializeField] private float fireRate = 1f;

    [Header("성능 설정")]
    [SerializeField] private int bulletPoolMaxSize = 40;
    
    private float fireCountdown;
    private ObjectPool<GameObject> bulletPool;
    private CancellationTokenSource turretCancellationTokenSource;
    private bool isTurretInitialized;

    #endregion

    #region 유니티 생명주기

    protected override void Start()
    {
        base.Start();
        turretCancellationTokenSource = new CancellationTokenSource();
        ValidateComponents();
        InitializeTurretAsync(turretCancellationTokenSource.Token).Forget();
    }

    // TurretBase에는 Update()가 없으므로 override하지 않습니다.
    // DoubleShotTurret의 독립적인 업데이트 로직을 구현합니다.
    protected void Update()
    {
        if (!isTurretInitialized || !ShouldUpdate()) return;
        
        // TurretBase의 LateUpdate에서 회전이 처리되므로, 여기서는 공격 로직만 담당합니다.
        UpdateFireCountdown();
        UpdateAttackLogic();
    }

    void OnDestroy()
    {
        turretCancellationTokenSource?.Cancel();
        turretCancellationTokenSource?.Dispose();
        bulletPool?.Dispose();
    }

    #endregion

    #region 초기화

    private void ValidateComponents()
    {
        if (bulletPrefab == null) Debug.LogError($"[{gameObject.name}] Bullet Prefab이 할당되지 않았습니다.", this);
        if (firePoints == null || firePoints.Length == 0) Debug.LogError($"[{gameObject.name}] Fire Points가 할당되지 않았습니다.", this);
    }

    private async UniTask InitializeTurretAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        InitializeBulletPool();
        isTurretInitialized = true;
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

    private void UpdateFireCountdown()
    {
        if (fireCountdown > 0)
        {
            fireCountdown -= Time.deltaTime;
        }
    }

    private void UpdateAttackLogic()
    {
        if (target != null) // target은 TurretBase에서 관리
        {
            ChangeState(TerretState.Active);
            if (fireCountdown <= 0f)
            {
                ShootAsync(turretCancellationTokenSource.Token).Forget();
                fireCountdown = 1f / fireRate;
            }
        }
        else
        {
            ChangeState(TerretState.Idle);
        }
    }

    #endregion

    #region 발사 시스템

    private async UniTask ShootAsync(CancellationToken cancellationToken)
    {
        if (target == null || currentState != TerretState.Active) return;

        try
        {
            Vector3 targetPosition = target.position;
            Vector3 commonDirection = (targetPosition - turretHead.position).normalized;

            foreach (var firePoint in firePoints)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                FireOneShot(firePoint, commonDirection);

                await UniTask.Delay((int)(delayBetweenShots * 1000), cancellationToken: cancellationToken);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[{gameObject.name}] 발사 오류: {ex.Message}");
        }
    }
    
    private void FireOneShot(Transform firePoint, Vector3 direction)
    {
        if (firePoint == null) return;

        GameObject bulletGo = bulletPool.Get();
        if (bulletGo == null) return;

        EffectManager.Instance.PlayEffect(EffectType.TurretShoot, firePoint.position);
        
        ConfigureBullet(bulletGo, firePoint, direction);
    }

    private void ConfigureBullet(GameObject bulletGo, Transform firePoint, Vector3 direction)
    {
        bulletGo.transform.position = firePoint.position;
        bulletGo.transform.rotation = Quaternion.LookRotation(direction);
        
        Bullet bullet = bulletGo.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.Seek(direction, attackPower, bulletPool);
        }
    }

    #endregion

    #region 오브젝트 풀링

    private GameObject CreateBullet()
    {
        if (bulletPrefab == null) return null;
        GameObject bulletGo = Instantiate(bulletPrefab);
        return bulletGo;
    }

    private void OnGetFromPool(GameObject bullet)
    {
        if (bullet != null) bullet.SetActive(true);
    }

    private void OnReleaseToPool(GameObject bullet)
    {
        if (bullet != null) bullet.SetActive(false);
    }

    private void OnDestroyBullet(GameObject bullet)
    {
        if (bullet != null) Destroy(bullet);
    }

    #endregion
}
