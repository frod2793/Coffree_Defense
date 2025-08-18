using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace WD_Develop.Scripts.Terrets
{
    /// <summary>
    /// 화염방사 터렛입니다.
    /// LaserTurret과 NormalTurret과 DoubleShotTurret의 구조를 참고하여 구현되었습니다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FlameTurret : TurretBase
    {
        [Header("화염방사 터렛 설정")]
        [Tooltip("발사할 화염 파티클 프리팹입니다. Flame 스크립트를 포함해야 합니다.")]
        [SerializeField] private Flame flamePrefab;
        [Tooltip("화염이 발사될 위치입니다.")]
        [SerializeField] private Transform firePoint;

        [Header("성능 설정")]
        [SerializeField] private int flamePoolMaxSize = 5;

        private bool isFiring;
        private CancellationTokenSource turretCancellationTokenSource;
        private bool isTurretInitialized;
        private Flame currentFlameInstance;
        private ObjectPool<Flame> flamePool;

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
            UpdateAttackLogic();
        }

        void OnDestroy()
        {
            turretCancellationTokenSource?.Cancel();
            turretCancellationTokenSource?.Dispose();
            flamePool?.Dispose();
        }

        #endregion

        #region 초기화

        private void ValidateComponents()
        {
            if (firePoint == null) Debug.LogError($"[{gameObject.name}] Fire Point가 할당되지 않았습니다.", this);
            if (flamePrefab == null) Debug.LogError($"[{gameObject.name}] Flame Prefab이 할당되지 않았습니다.", this);
        }

        private async UniTask InitializeTurretAsync(CancellationToken cancellationToken)
        {
            await UniTask.Yield(cancellationToken);
            InitializeFlamePool();
            isTurretInitialized = true;
        }

        private void InitializeFlamePool()
        {
            flamePool = new ObjectPool<Flame>(
                CreateFlame,
                OnGetFromPool,
                OnReleaseToPool,
                OnDestroyFlame,
                maxSize: flamePoolMaxSize
            );
        }

        #endregion

        #region 공격 로직

        private void UpdateAttackLogic()
        {
            if (target != null)
            {
                ChangeState(TerretState.Active);
                if (!isFiring)
                {
                    StartFiring();
                }
            }
            else
            {
                ChangeState(TerretState.Idle);
                if (isFiring)
                {
                    StopFiring();
                }
            }
        }

        #endregion

        #region 화염 발사 및 풀링 관리

        private void StartFiring()
        {
            isFiring = true;
            if (flamePool != null && currentFlameInstance == null)
            {
                currentFlameInstance = flamePool.Get();
                currentFlameInstance.Initialize(attackPower, flamePool);
            }
        }

        private void StopFiring()
        {
            isFiring = false;
            if (currentFlameInstance != null)
            {
                currentFlameInstance.StopAndRelease();
                currentFlameInstance = null;
            }
        }

        // --- 오브젝트 풀 콜백 함수 ---

        private Flame CreateFlame()
        {
            Flame flame = Instantiate(flamePrefab, firePoint.position, firePoint.rotation, firePoint);
            return flame;
        }

        private void OnGetFromPool(Flame flame)
        {
            flame.gameObject.SetActive(true);
            flame.transform.SetPositionAndRotation(firePoint.position, firePoint.rotation);
            flame.GetComponent<ParticleSystem>().Play();
        }

        private void OnReleaseToPool(Flame flame)
        {
            flame.gameObject.SetActive(false);
        }

        private void OnDestroyFlame(Flame flame)
        {
            Destroy(flame.gameObject);
        }

        #endregion
    }
}
