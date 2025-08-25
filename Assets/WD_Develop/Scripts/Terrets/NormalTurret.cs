using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using System.Threading;

[RequireComponent(typeof(BoxCollider))]
public class NormalTurret : TurretBase
{
    #region 필드 및 속성

    [Header("일반 터렛 전용 설정")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 1f;
    
    [Header("성능 설정")]
    [SerializeField] private int bulletPoolMaxSize = 20;
    
    private float fireCountdown;
    private ObjectPool<GameObject> bulletPool;
    private CancellationTokenSource normalTurretCancellationTokenSource;
    private bool isNormalTurretInitialized;

    #endregion

    #region 유니티 생명주기

    protected override void Start()
    {
        base.Start();
        normalTurretCancellationTokenSource = new CancellationTokenSource();
        ValidateComponents();
        InitializeNormalTurretAsync(normalTurretCancellationTokenSource.Token).Forget();
    }

    // override 키워드를 제거합니다. TurretBase에 virtual Update가 정의되어 있지 않기 때문입니다.
    // 각 터렛은 자신만의 업데이트 로직을 독립적으로 가집니다.
    protected void Update()
    {
        // base.Update(); // 부모 클래스에 Update가 없으므로 호출할 수 없습니다.
        if (!isNormalTurretInitialized || !ShouldUpdate()) return;
        
        // TurretBase.Update()에서 RotateToTarget()이 이미 호출됨
        UpdateFireCountdown();
        UpdateAttackLogic();
    }

    void OnDestroy()
    {
        normalTurretCancellationTokenSource?.Cancel();
        normalTurretCancellationTokenSource?.Dispose();
        bulletPool?.Dispose();
    }

    #endregion

    #region 초기화

    private void ValidateComponents()
    {
        if (bulletPrefab == null) Debug.LogError($"[{gameObject.name}] Bullet Prefab이 할당되지 않았습니다.", this);
        if (firePoint == null) Debug.LogError($"[{gameObject.name}] Fire Point가 할당되지 않았습니다.", this);
    }

    private async UniTask InitializeNormalTurretAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        InitializeBulletPool();
        isNormalTurretInitialized = true;
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
                Shoot();
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

    private void Shoot()
    {
        if (target == null || currentState != TerretState.Active) return;

        try
        {
            GameObject bulletGo = bulletPool.Get();
            if (bulletGo == null) return;

            EffectManager.Instance.PlayEffect(EffectType.TurretShoot, firePoint.position);
            SoundManager.Instance.PlaySound(AudioMixerType.SFX, "TowerAttack");

            ConfigureBullet(bulletGo, target);
        }
        catch (System.Exception ex)
        {
            if (!(ex is MissingReferenceException))
            {
                Debug.LogError($"[{gameObject.name}] 발사 오류: {ex.Message}");
            }
        }
    }

    private void ConfigureBullet(GameObject bulletGo, Transform bulletTarget)
    {
        if (bulletTarget == null)
        {
            bulletPool.Release(bulletGo);
            return;
        }

        Vector3 direction = (bulletTarget.position - firePoint.position).normalized;

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