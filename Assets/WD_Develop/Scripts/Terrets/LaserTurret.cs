using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace WD_Develop.Scripts.Terrets
{
    /// <summary>
    /// 오브젝트 풀링을 사용하여 레이저 프리팹을 생성하고 발사하는 터렛 클래스입니다.
    /// </summary>
    public class LaserTurret : TurretBase
    {
        [Header("레이저 터렛 설정")]
        [Tooltip("발사할 레이저 효과 프리팹입니다. Laser 스크립트를 포함해야 합니다.")]
        [SerializeField] private Laser laserPrefab;
        [Tooltip("레이저가 발사될 위치입니다.")]
        [SerializeField] private Transform firePoint;

        [Header("레이저 발사 사이클")]
        [SerializeField] private float fireDuration = 2f;
        [SerializeField] private float cooldownDuration = 2f;

        [Header("성능 설정")]
        [SerializeField] private int laserPoolMaxSize = 5;

        private bool isFiring;
        private float cycleTimer;
        private CancellationTokenSource laserTurretCancellationTokenSource;
        private bool isLaserTurretInitialized;
        private Laser currentLaserInstance;
        private ObjectPool<Laser> laserPool;

        #region 유니티 생명주기

        protected override void Start()
        {
            base.Start();
            laserTurretCancellationTokenSource = new CancellationTokenSource();
            ValidateComponents();
            InitializeLaserTurretAsync(laserTurretCancellationTokenSource.Token).Forget();
        }

        protected void Update()
        {
            if (!isLaserTurretInitialized || !ShouldUpdate()) return;
            
            UpdateAttackLogic();
        }

        void OnDestroy()
        {
            laserTurretCancellationTokenSource?.Cancel();
            laserTurretCancellationTokenSource?.Dispose();
            laserPool?.Dispose();
        }

        #endregion

        #region 초기화

        private void ValidateComponents()
        {
            if (firePoint == null) Debug.LogError($"[{gameObject.name}] Fire Point가 할당되지 않았습니다.", this);
            if (laserPrefab == null) Debug.LogError($"[{gameObject.name}] Laser Prefab이 할당되지 않았습니다.", this);
        }

        private async UniTask InitializeLaserTurretAsync(CancellationToken cancellationToken)
        {
            await UniTask.Yield(cancellationToken);
            InitializeLaserPool();
            isLaserTurretInitialized = true;
        }

        private void InitializeLaserPool()
        {
            laserPool = new ObjectPool<Laser>(
                CreateLaser,
                OnGetFromPool,
                OnReleaseToPool,
                OnDestroyLaser,
                maxSize: laserPoolMaxSize
            );
        }

        #endregion

        #region 공격 로직

        private void UpdateAttackLogic()
        {
            if (target == null)
            {
                ChangeState(TerretState.Idle);
                if (isFiring) StopFiring();
                return;
            }

            ChangeState(TerretState.Active);
            HandleFiringCycle();
        }

        private void HandleFiringCycle()
        {
            cycleTimer -= Time.deltaTime;

            if (isFiring)
            {
                if (cycleTimer <= 0f)
                {
                    StopFiring();
                    cycleTimer = cooldownDuration;
                }
            }
            else
            {
                if (cycleTimer <= 0f)
                {
                    StartFiring();
                    cycleTimer = fireDuration;
                }
            }
        }

        #endregion

        #region 레이저 발사 및 풀링 관리

        private void StartFiring()
        {
            isFiring = true;
            if (laserPool != null && currentLaserInstance == null)
            {
                currentLaserInstance = laserPool.Get();
                // Initialize 호출 시 오브젝트 풀을 직접 전달합니다.
                currentLaserInstance.Initialize(attackPower, laserPool);
            }
        }

        private void StopFiring()
        {
            isFiring = false;
            if (currentLaserInstance != null)
            {
                currentLaserInstance.StopAndRelease();
                currentLaserInstance = null;
            }
        }

        // --- 오브젝트 풀 콜백 함수 ---

        private Laser CreateLaser()
        {
            Laser laser = Instantiate(laserPrefab, firePoint.position, firePoint.rotation, firePoint);
            return laser;
        }

        private void OnGetFromPool(Laser laser)
        {
            laser.gameObject.SetActive(true);
            laser.transform.SetPositionAndRotation(firePoint.position, firePoint.rotation);
            laser.GetComponent<ParticleSystem>().Play();
        }

        private void OnReleaseToPool(Laser laser)
        {
            laser.gameObject.SetActive(false);
        }

        private void OnDestroyLaser(Laser laser)
        {
            Destroy(laser.gameObject);
        }

        #endregion
    }
}
