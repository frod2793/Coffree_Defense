using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace WD_Develop.Scripts.Terrets
{
    [RequireComponent(typeof(BoxCollider))]
    public class FlameTurret : TurretBase
    {
        [Header("화염방사 터렛 설정")]
        [SerializeField] private Flame flamePrefab;
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

        /// <summary>
        /// 최적화: 타겟 유무에 따라 발사 상태를 변경하는 로직을 개선하여 가독성을 높입니다.
        /// </summary>
        private void UpdateAttackLogic()
        {
            bool shouldBeFiring = (target != null);

            if (shouldBeFiring && !isFiring)
            {
                ChangeState(TerretState.Active);
                StartFiring();
            }
            else if (!shouldBeFiring && isFiring)
            {
                ChangeState(TerretState.Idle);
                StopFiring();
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
                currentFlameInstance.StartEmitting();
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
            return Instantiate(flamePrefab, firePoint.position, firePoint.rotation, firePoint);
        }

        private void OnGetFromPool(Flame flame)
        {
            flame.gameObject.SetActive(true);
            flame.transform.SetPositionAndRotation(firePoint.position, firePoint.rotation);
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
