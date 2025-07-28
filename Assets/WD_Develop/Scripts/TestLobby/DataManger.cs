using UnityEngine;
using System;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 유저의 데이터를 관리하는 클래스 
/// 씬 전환 시에도 파괴되지 않고 인게임에서 지속적으로 사용 가능합니다.
/// UniTask를 활용한 비동기 처리로 성능 최적화
/// </summary>
public class DataManger : MonoBehaviour
{
    public static DataManger Instance { get; private set; }

    [Header("User Data")]
    [SerializeField] private UserCurrencyData userCurrencyData;

    // 재화 변경 이벤트
    public event Action<int> OnCoinChanged;
    public event Action<int> OnTPChanged;
    public event Action<int> OnWaterPointChanged;

    // DataManager가 초기화되었는지 확인하는 플래그
    private bool isInitialized = false;
    private CancellationTokenSource cancellationTokenSource;

    private void Awake()
    {
        // 싱글톤 패턴 구현 - 씬 전환 시에도 유지됨
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시 파괴되지 않음
            cancellationTokenSource = new CancellationTokenSource();
            InitializeDataAsync(cancellationTokenSource.Token).Forget();
            
            Debug.Log("[DataManager] 인스턴스 생성 및 영구 보존 설정 완료");
        }
        else
        {
            // 이미 존재하는 인스턴스가 있으면 중복 제거
            Debug.Log("[DataManager] 중복 인스턴스 감지, 현재 오브젝트 파괴");
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        // 중복 생성된 경우 Start 실행 방지
        if (Instance != this) return;
        
        await LoadUserDataAsync(cancellationTokenSource.Token);
        
        // SceneLoader 이벤트 구독
        SceneLoader.OnSceneLoadStarted += OnSceneChangeStarted;
        
        isInitialized = true;
        Debug.Log("[DataManager] 초기화 완료 - 인게임에서 사용 준비됨");
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveUserDataAsync(cancellationTokenSource.Token).Forget();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveUserDataAsync(cancellationTokenSource.Token).Forget();
        }
    }

    /// <summary>
    /// DataManager가 초기화되었는지 확인합니다.
    /// </summary>
    public bool IsInitialized()
    {
        return isInitialized && userCurrencyData != null;
    }

    /// <summary>
    /// 인게임에서 DataManager 인스턴스에 안전하게 접근하기 위한 메서드
    /// </summary>
    public static bool IsAvailable()
    {
        return Instance != null && Instance.IsInitialized();
    }

    /// <summary>
    /// 인게임에서 재화 정보를 한 번에 가져오는 메서드
    /// </summary>
    public CurrencyInfo GetAllCurrencyInfo()
    {
        if (!IsInitialized())
        {
            Debug.LogWarning("[DataManager] 초기화되지 않은 상태에서 재화 정보 요청");
            return new CurrencyInfo();
        }

        return new CurrencyInfo
        {
            coin = userCurrencyData.Coin,
            tp = userCurrencyData.TP,
            waterPoint = userCurrencyData.WaterPoint
        };
    }

    /// <summary>
    /// 인게임에서 사용할 재화 정보 구조체
    /// </summary>
    [System.Serializable]
    public struct CurrencyInfo
    {
        public int coin;
        public int tp;
        public int waterPoint;

        public override string ToString()
        {
            return $"코인: {coin}, TP: {tp}, 워터포인트: {waterPoint}";
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            // CancellationToken 정리
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            
            SaveUserData();
            
            // SceneLoader 이벤트 구독 해제
            SceneLoader.OnSceneLoadStarted -= OnSceneChangeStarted;
            
            Debug.Log("[DataManager] 인스턴스 파괴 시 정리 작업 완료");
        }
    }

    private async UniTask InitializeDataAsync(CancellationToken cancellationToken)
    {
        if (userCurrencyData == null)
        {
            Debug.LogError("[DataManager] UserCurrencyData가 설정되지 않았습니다! Inspector에서 할당해주세요.");
            return;
        }
        
        // 1프레임 대기하여 다른 초기화와 동기화
        await UniTask.Yield(cancellationToken);
        
        // UserCurrencyData가 이미 데이터를 가지고 있으므로 별도의 초기화 불필요
        Debug.Log("[DataManager] UserCurrencyData 초기화 완료");
    }

    // 데이터 로드 (비동기 처리)
    public async UniTask LoadUserDataAsync(CancellationToken cancellationToken = default)
    {
        if (userCurrencyData == null) 
        {
            Debug.LogError("[DataManager] UserCurrencyData가 없어 데이터 로드 실패");
            return;
        }

        // 무거운 작업을 다음 프레임으로 분산
        await UniTask.Yield(cancellationToken);

        int savedCoin = userCurrencyData.Coin;
        int savedTP = userCurrencyData.TP;
        int savedWaterPoint = userCurrencyData.WaterPoint;

        // 메인 스레드에서 이벤트 발생
        OnCoinChanged?.Invoke(savedCoin);
        OnTPChanged?.Invoke(savedTP);
        OnWaterPointChanged?.Invoke(savedWaterPoint);

        Debug.Log($"[DataManager] 데이터 로드 완료 - 코인: {savedCoin}, TP: {savedTP}, 워터포인트: {savedWaterPoint}");
    }

    // 동기 버전 유지 (하위 호환성)
    public void LoadUserData()
    {
        LoadUserDataAsync(cancellationTokenSource.Token).Forget();
    }

    // 데이터 저장 (비동기 처리)
    public async UniTask SaveUserDataAsync(CancellationToken cancellationToken = default)
    {
        if (userCurrencyData == null) 
        {
            Debug.LogError("[DataManager] UserCurrencyData가 없어 데이터 저장 실패");
            return;
        }

        // 파일 I/O를 다음 프레임으로 분산
        await UniTask.Yield(cancellationToken);

        // UserCurrencyData가 자동으로 저장하므로 수동 저장만 호출
        userCurrencyData.Save();

        Debug.Log($"[DataManager] 데이터 저장 완료 - 코인: {userCurrencyData.Coin}, TP: {userCurrencyData.TP}, 워터포인트: {userCurrencyData.WaterPoint}");
    }

    // 동기 버전 유지 (하위 호환성)
    public void SaveUserData()
    {
        SaveUserDataAsync(cancellationTokenSource.Token).Forget();
    }

    // 재화 접근 메서드들
    public UserCurrencyData GetUserCurrencyData()
    {
        return userCurrencyData;
    }

    public int GetCoin() => userCurrencyData?.Coin ?? 0;
    public int GetTp() => userCurrencyData?.TP ?? 0;
    public int GetWaterPoint() => userCurrencyData?.WaterPoint ?? 0;

    // 코인 관련 (비동기 처리)
    public async UniTask<bool> SpendCoinAsync(int amount, CancellationToken cancellationToken = default)
    {
        if (userCurrencyData != null && userCurrencyData.SpendCoin(amount))
        {
            // UI 업데이트를 다음 프레임으로 분산
            await UniTask.Yield(cancellationToken);
            OnCoinChanged?.Invoke(userCurrencyData.Coin);
            return true;
        }
        return false;
    }

    // 동기 버전 유지
    public bool SpendCoin(int amount)
    {
        if (userCurrencyData != null && userCurrencyData.SpendCoin(amount))
        {
            OnCoinChanged?.Invoke(userCurrencyData.Coin);
            return true;
        }
        return false;
    }

    public async UniTask AddCoinAsync(int amount, CancellationToken cancellationToken = default)
    {
        if (userCurrencyData != null)
        {
            userCurrencyData.AddCoin(amount);
            await UniTask.Yield(cancellationToken);
            OnCoinChanged?.Invoke(userCurrencyData.Coin);
        }
    }

    public void AddCoin(int amount)
    {
        if (userCurrencyData != null)
        {
            userCurrencyData.AddCoin(amount);
            OnCoinChanged?.Invoke(userCurrencyData.Coin);
        }
    }

    // TP 관련 (비동기 처리)
    public async UniTask<bool> SpendTPAsync(int amount, CancellationToken cancellationToken = default)
    {
        if (userCurrencyData != null && userCurrencyData.SpendTP(amount))
        {
            await UniTask.Yield(cancellationToken);
            OnTPChanged?.Invoke(userCurrencyData.TP);
            return true;
        }
        return false;
    }

    public bool SpendTP(int amount)
    {
        if (userCurrencyData != null && userCurrencyData.SpendTP(amount))
        {
            OnTPChanged?.Invoke(userCurrencyData.TP);
            return true;
        }
        return false;
    }

    public async UniTask AddTPAsync(int amount, CancellationToken cancellationToken = default)
    {
        if (userCurrencyData != null)
        {
            userCurrencyData.AddTP(amount);
            await UniTask.Yield(cancellationToken);
            OnTPChanged?.Invoke(userCurrencyData.TP);
        }
    }

    public void AddTP(int amount)
    {
        if (userCurrencyData != null)
        {
            userCurrencyData.AddTP(amount);
            OnTPChanged?.Invoke(userCurrencyData.TP);
        }
    }

    public int GetTP()
    {
        return userCurrencyData != null ? userCurrencyData.TP : 0;
    }

    // 워터 포인트 관련 (비동기 처리)
    public async UniTask<bool> SpendWaterPointAsync(int amount, CancellationToken cancellationToken = default)
    {
        if (userCurrencyData != null && userCurrencyData.SpendWaterPoint(amount))
        {
            await UniTask.Yield(cancellationToken);
            OnWaterPointChanged?.Invoke(userCurrencyData.WaterPoint);
            return true;
        }
        return false;
    }

    public bool SpendWaterPoint(int amount)
    {
        if (userCurrencyData != null && userCurrencyData.SpendWaterPoint(amount))
        {
            OnWaterPointChanged?.Invoke(userCurrencyData.WaterPoint);
            return true;
        }
        return false;
    }

    public async UniTask AddWaterPointAsync(int amount, CancellationToken cancellationToken = default)
    {
        if (userCurrencyData != null)
        {
            userCurrencyData.AddWaterPoint(amount);
            await UniTask.Yield(cancellationToken);
            OnWaterPointChanged?.Invoke(userCurrencyData.WaterPoint);
        }
    }

    public void AddWaterPoint(int amount)
    {
        if (userCurrencyData != null)
        {
            userCurrencyData.AddWaterPoint(amount);
            OnWaterPointChanged?.Invoke(userCurrencyData.WaterPoint);
        }
    }

    // 데이터 리셋 (비동기 처리)
    public async UniTask ResetAllDataAsync(CancellationToken cancellationToken = default)
    {
        if (userCurrencyData != null)
        {
            userCurrencyData.ResetData();
            
            // UI 업데이트를 분산 처리
            await UniTask.Yield(cancellationToken);
            
            OnCoinChanged?.Invoke(userCurrencyData.Coin);
            OnTPChanged?.Invoke(userCurrencyData.TP);
            OnWaterPointChanged?.Invoke(userCurrencyData.WaterPoint);
            
            Debug.Log("모든 데이터가 리셋되었습니다.");
        }
    }

    public void ResetAllData()
    {
        ResetAllDataAsync(cancellationTokenSource.Token).Forget();
    }

    private void OnSceneChangeStarted(string sceneName)
    {
        // 씬 전환 시 자동으로 데이터 저장 (비동기)
        SaveUserDataAsync(cancellationTokenSource.Token).Forget();
        Debug.Log($"[DataManager] 씬 전환({sceneName})으로 인한 데이터 자동 저장");
    }

    /// <summary>
    /// 애플리케이션 종료 시 호출 (데이터 보존)
    /// </summary>
    private void OnApplicationQuit()
    {
        if (Instance == this)
        {
            // 동기적으로 즉시 저장 (앱 종료 시)
            SaveUserData();
            Debug.Log("[DataManager] 애플리케이션 종료 시 데이터 저장 완료");
        }
    }
}
