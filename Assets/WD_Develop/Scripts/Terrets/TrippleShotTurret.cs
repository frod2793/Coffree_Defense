using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 3개의 총구에서 동시에 총알을 발사하는 3열 터렛 클래스입니다.
/// NormalTurret, DoubleShotTurret과 유사한 구조를 따르며, 오브젝트 풀링을 사용하여 성능을 최적화합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TrippleShotTurret : TurretBase
{
    #region 필드 및 속성

    [Header("3열 터렛 전용 설정")]
    [Tooltip("발사할 총알 프리팹입니다.")]
    [SerializeField] private GameObject bulletPrefab;
    
    [Tooltip("총알이 발사될 3개의 총구 위치입니다.")]
    [SerializeField] private Transform[] firePoints = new Transform[3];
    
    [Tooltip("초당 발사 속도입니다.")]
    [SerializeField] private float fireRate = 1f;

    [Header("성능 설정")]
    [Tooltip("총알 오브젝트 풀의 최대 크기입니다.")]
    [SerializeField] private int bulletPoolMaxSize = 60; // 3발씩 발사하므로 충분한 크기 할당
    
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

    protected void Update()
    {
        if (!isTurretInitialized || !ShouldUpdate()) return;
        
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
        if (firePoints == null || firePoints.Length < 3) Debug.LogError($"[{gameObject.name}] Fire Points가 3개 이상 할당되지 않았습니다.", this);
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
            await UniTask.Yield(cancellationToken); // 발사 전 프레임 지연으로 안정성 확보
            if (target == null) return; // 발사 직전 타겟 재확인

            // 3개의 총구에서 동시에 발사
            foreach (var firePoint in firePoints)
            {
                if (cancellationToken.IsCancellationRequested) break;
                FireOneShot(firePoint);
            }
        }
        catch (System.Exception ex)
        {
            if (!(ex is MissingReferenceException))
            {
                Debug.LogError($"[{gameObject.name}] 발사 오류: {ex.Message}");
            }
        }
    }
    
    private void FireOneShot(Transform firePoint)
    {
        if (firePoint == null) return;

        GameObject bulletGo = bulletPool.Get();
        if (bulletGo == null) return;

        EffectManager.Instance.PlayEffect(EffectType.TurretShoot, firePoint.position);
        SoundManager.Instance.PlaySound(AudioMixerType.SFX, "TowerAttack");
        ConfigureBullet(bulletGo, firePoint);
    }

    private void ConfigureBullet(GameObject bulletGo, Transform firePoint)
    {
        // 총알의 방향은 총구의 방향(직선)을 따릅니다.
        Vector3 direction = firePoint.up; 

        bulletGo.transform.position = firePoint.position;
        bulletGo.transform.rotation = firePoint.rotation;
        
        Bullet bullet = bulletGo.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.Seek(direction, attackPower, bulletPool);
        }
        else
        {
            // 총알 스크립트가 없는 경우, 풀로 즉시 반환하여 오류를 방지합니다.
            bulletPool.Release(bulletGo);
        }
    }

    #endregion

    #region 오브젝트 풀링

    private GameObject CreateBullet()
    {
        if (bulletPrefab == null) return null;
        return Instantiate(bulletPrefab);
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
