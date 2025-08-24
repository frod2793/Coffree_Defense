using UnityEngine;
using UnityEngine.Pool;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace WD_Develop.Scripts.Terrets
{
    /// <summary>
    /// 화염과 냉기 효과를 결합한 아이스플레임 효과를 발사하는 터렛입니다.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class IceflameTurret : TurretBase
    {
        [Header("아이스플레임 터렛 설정")]
        [SerializeField] private Iceflame iceflamePrefab; // 아이스플레임 효과 프리팹
        
        [SerializeField] private Transform firePoint;       // 발사 지점
        [SerializeField] private float slowAmount = 0.7f;   // 둔화율 (0.3 = 30%)

        [Header("성능 설정")]
        [SerializeField] private int iceflamePoolMaxSize = 5;

        private bool isFiring;
        private CancellationTokenSource turretCancellationTokenSource;
        private bool isTurretInitialized;
        private Iceflame currentIceflameInstance;
        private ObjectPool<Iceflame> iceflamePool;

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
            iceflamePool?.Dispose();
        }

        #endregion

        #region 초기화

        private void ValidateComponents()
        {
            if (firePoint == null) Debug.LogError($"[{gameObject.name}] Fire Point가 할당되지 않았습니다.", this);
            if (iceflamePrefab == null) Debug.LogError($"[{gameObject.name}] Iceflame Prefab이 할당되지 않았습니다.", this);
        }

        private async UniTask InitializeTurretAsync(CancellationToken cancellationToken)
        {
            await UniTask.Yield(cancellationToken);
            InitializeIceflamePool();
            isTurretInitialized = true;
        }

        private void InitializeIceflamePool()
        {
            iceflamePool = new ObjectPool<Iceflame>(
                CreateIceflame,
                OnGetFromPool,
                OnReleaseToPool,
                OnDestroyIceflame,
                maxSize: iceflamePoolMaxSize
            );
        }

        #endregion

        #region 공격 로직

        /// <summary>
        /// 타겟 유무에 따라 발사 상태를 변경하는 로직을 개선하여 가독성을 높입니다.
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

        #region 아이스플레임 발사 및 풀링 관리

        private void StartFiring()
        {
            isFiring = true;
            if (iceflamePool != null && currentIceflameInstance == null)
            {
                currentIceflameInstance = iceflamePool.Get();
                // Iceflame에 공격력과 둔화율, 풀 정보를 전달하여 초기화
                currentIceflameInstance.Initialize(attackPower, slowAmount, iceflamePool);
                currentIceflameInstance.StartEmitting();
            }
        }

        private void StopFiring()
        {
            isFiring = false;
            if (currentIceflameInstance != null)
            {
                currentIceflameInstance.StopAndRelease();
                currentIceflameInstance = null;
            }
        }

        // --- 오브젝트 풀 콜백 함수 ---

        private Iceflame CreateIceflame()
        {
            return Instantiate(iceflamePrefab, firePoint.position, firePoint.rotation, firePoint);
        }

        private void OnGetFromPool(Iceflame iceflame)
        {
            iceflame.gameObject.SetActive(true);
            iceflame.transform.SetPositionAndRotation(firePoint.position, firePoint.rotation);
        }

        private void OnReleaseToPool(Iceflame iceflame)
        {
            iceflame.gameObject.SetActive(false);
        }

        private void OnDestroyIceflame(Iceflame iceflame)
        {
            Destroy(iceflame.gameObject);
        }

        #endregion
    }
}
