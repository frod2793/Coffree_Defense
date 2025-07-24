using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;

public class InGameUIManager : MonoBehaviour
{
    // 포탑 조합에 대한 결과오브젝트는 scriptable object로 관리합니다.
    // 조합 식 클래스를 따로 생성하여 조합에 따라 포탑의 프리펙 을 변경 
    // 포탑의 기본 구동 방식은 TurretBase 클래스를 상속받아 구현합니다.
    // UI 의 재료 칸에서 포탑으로 드래그 앤 드랍시 포탑의 조합 식이 활성화 됩니다 
    // 재료칸에서 가져간 재료를 포탑이랑 겹쳐 놓을시 포탑이 아웃라인으로 표시 됩니다 
    // 재료를 포탑에 드랍시 조합식이 활성화 됩니다 .
    // 조합 식이 활성화 되면 포탑의 작동이 비활성화 되고 포탑의 머리위에 말풍선의 형태로 조합식이 작동 합니다.
    //예 ) 드래그한것 (타피오카) + (아직 드래드안한것 )???? == 포탑의 모양을 실루엣 처리 
    // 조합 식이 활성화 되면 조합 결과 오브젝트가 활성화 됩니다.
    // 2초후 포탑이 활성화 됩니다.
    //포탑 인터페이스를 통해 게임 메니져에 설치된 포탑의 정보를 전달합니다.
    
    // 포탑의 드래그앤 드랍이 활성화 되면 포탑이 비활성화 됩니다 (쿨타임 말풍선 형태의 ui 로 표시 )
    // 포탑의 위치가 고정되면 2초의 쿨타임후 포탑이 활성화 됩니다 
    
    //확인해야될 사항 유니티 에셋 에 관한 조항 
    
    /// <summary>
    /// 선택 ui 에서 드래그 하여 인게임 맵으로 마우스 포인트 이동시에 마우스 포인터 위치에 미리보기 아이템 프리펙을 표시합니다.
    /// </summary>
    
    private bool isDraggingTurret = false; // TerretControl에서 터렛을 드래그 중인지 여부
    private TerretControl terretControl; // TerretControl 참조

    [Header( "In-Game UI Manager")]
    [SerializeField] private GameObject inGameUI; // In-Game UI 오브젝트
    
    [SerializeField]
    GameManager gameManager;
    
    [SerializeField]
    List<Image> images; // UI에 표시할 이미지 리스트
    
    [SerializeField]
    Button addTurretButton; // 터렛 추가 버튼
    
    [SerializeField]
    List<GameObject> PrefabList; // UI에 표시할 프리팹 리스트
    
    [Header ("user data")]
    [SerializeField] private TMPro.TextMeshProUGUI coinText; // 코인 텍스트 (UI용으로 변경)
    [SerializeField] private TMPro.TextMeshProUGUI tpText; // TP 텍스트 (UI용으로 변경)
    [SerializeField] private TMPro.TextMeshProUGUI waterPointText; // 워터 포인트 텍스트 (UI용으로 변경)
    
    // 재화 정보 업데이트 간격 (초)
    [SerializeField] private float currencyUpdateInterval = 0.5f;
    private float lastCurrencyUpdateTime = 0f;
    
    // UniTask 관련
    private CancellationTokenSource cancellationTokenSource;
    private bool isInitialized = false;
    
    async void Start()
    {
        cancellationTokenSource = new CancellationTokenSource();
        
        // 비동기 초기화 시작
        await InitializeAsync(cancellationTokenSource.Token);
    }
    
    private async UniTask InitializeAsync(CancellationToken cancellationToken)
    {
        // TerretControl 찾기 (비동기)
        await UniTask.Yield(cancellationToken);
        terretControl = FindFirstObjectByType<TerretControl>();
        if (terretControl == null)
        {
            Debug.LogError("TerretControl을 찾을 수 없습니다.");
        }
        
        // 이벤트 트리거 설정을 다음 프레임으로 분산
        await SetupEventTriggersAsync(cancellationToken);
        
        // 버튼 이벤트 설정
        await SetupButtonEventsAsync(cancellationToken);
        
        // 레이어 설정 체크
        await UniTask.Yield(cancellationToken);
        CheckLayerSetup();
        
        // 재화 UI 초기화
        await InitializeCurrencyUIAsync(cancellationToken);
        
        // 주기적 업데이트 시작
        StartPeriodicUpdateAsync(cancellationToken).Forget();
        
        isInitialized = true;
        Debug.Log("[InGameUIManager] 초기화 완료");
    }
    
    private async UniTask SetupEventTriggersAsync(CancellationToken cancellationToken)
    {
        // 이벤트 트리거 에 이벤트 연결을 설정합니다.
        for (int i = 0; i < images.Count; i++)
        {
            Image image = images[i];
            EventTrigger trigger = image.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = image.gameObject.AddComponent<EventTrigger>();
            }

            // Begin Drag 이벤트 설정
            EventTrigger.Entry beginDragEntry = new EventTrigger.Entry();
            beginDragEntry.eventID = EventTriggerType.BeginDrag;
            int index = i; // 클로저 문제를 피하기 위해 인덱스 복사
            beginDragEntry.callback.AddListener((data) => { OnBeginDrag(index); });
            trigger.triggers.Add(beginDragEntry);

            // End Drag 이벤트 설정
            EventTrigger.Entry endDragEntry = new EventTrigger.Entry();
            endDragEntry.eventID = EventTriggerType.EndDrag;
            endDragEntry.callback.AddListener((data) => { OnEndDrag(); });
            trigger.triggers.Add(endDragEntry);
            
            // 프레임 분산을 위해 일정 간격으로 yield
            if (i % 5 == 0) // 5개마다 한 프레임 대기
            {
                await UniTask.Yield(cancellationToken);
            }
        }
    }
    
    private async UniTask SetupButtonEventsAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        addTurretButton.onClick.AddListener(() =>
        {
            // 터렛 추가 버튼 클릭 시 TerretControl에 알림
            if (terretControl != null)
            {
                terretControl.SetAddTurret();
            }
        });
    }
    
    // 주기적 업데이트를 UniTask로 처리
    private async UniTask StartPeriodicUpdateAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && this != null)
        {
            try
            {
                // 재화 정보 주기적 업데이트
                await UpdateCurrencyDisplayAsync(cancellationToken);
                
                // 다음 업데이트까지 대기 - UniTask.Delay 매개변수 수정
                await UniTask.Delay(System.TimeSpan.FromSeconds(currencyUpdateInterval), 
                    DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                // 정상적인 취소
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InGameUIManager] 주기적 업데이트 오류: {ex.Message}");
                // 오류 발생 시 잠시 대기 후 재시도 - UniTask.Delay 매개변수 수정
                await UniTask.Delay(1000, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }
    
    /// <summary>
    /// TerretControl에서 터렛 드래그 상태를 설정합니다.
    /// </summary>
    public void SetDraggingTurret(bool isDragging)
    {
        isDraggingTurret = isDragging;
    }

    /// <summary>
    /// UI 이미지에서 드래그를 시작할 때 호출됩니다. (EventTrigger에 연결 필요)
    /// </summary>
    /// <param name="prefabIndex">PrefabList에 있는 프리팹의 인덱스</param>
    public void OnBeginDrag(int prefabIndex)
    {
        // 터렛 드래그 중일 때는 아이템 드래그 허용하지 않음
        if (isDraggingTurret || terretControl == null) return;
        
        if (prefabIndex >= 0 && prefabIndex < PrefabList.Count)
        {
            GameObject selectedPrefab = PrefabList[prefabIndex];
            if (selectedPrefab != null)
            {
                // TerretControl에 아이템 드래그 시작 알림
                terretControl.StartItemDrag(selectedPrefab);
            }
        }
    }

    /// <summary>
    /// 드래그를 놓았을 때 호출됩니다. (EventTrigger에 연결 필요)
    /// </summary>
    public void OnEndDrag()
    {
        // TerretControl에 아이템 드래그 종료 알림
        if (terretControl != null)
        {
            terretControl.EndItemDrag();
        }
    }
    
    /// <summary>
    /// 재화 UI를 초기화합니다. (비동기)
    /// </summary>
    private async UniTask InitializeCurrencyUIAsync(CancellationToken cancellationToken)
    {
        // DataManager가 사용 가능한지 확인
        if (DataManger.IsAvailable())
        {
            // DataManager 이벤트 구독
            DataManger.Instance.OnCoinChanged += UpdateCoinDisplay;
            DataManger.Instance.OnTPChanged += UpdateTpDisplay;
            DataManger.Instance.OnWaterPointChanged += UpdateWaterPointDisplay;
            
            // 초기 재화 정보 표시 (비동기)
            await UniTask.Yield(cancellationToken);
            var currencyInfo = DataManger.Instance.GetAllCurrencyInfo();
            UpdateCoinDisplay(currencyInfo.coin);
            UpdateTpDisplay(currencyInfo.tp);
            UpdateWaterPointDisplay(currencyInfo.waterPoint);
            
            Debug.Log("[InGameUIManager] 재화 UI 초기화 완료");
        }
        else
        {
            // DataManager가 없는 경우 기본값 표시
            Debug.LogWarning("[InGameUIManager] DataManager를 찾을 수 없어 기본 재화 정보를 표시합니다.");
            UpdateCoinDisplay(0);
            UpdateTpDisplay(0);
            UpdateWaterPointDisplay(0);
        }
    }
    
    /// <summary>
    /// 주기적으로 재화 정보를 업데이트합니다. (비동기)
    /// </summary>
    private async UniTask UpdateCurrencyDisplayAsync(CancellationToken cancellationToken)
    {
        // DataManager가 사용 가능한 경우에만 업데이트
        if (DataManger.IsAvailable())
        {
            await UniTask.Yield(cancellationToken);
            
            var currencyInfo = DataManger.Instance.GetAllCurrencyInfo();
            
            // UI 텍스트가 현재 값과 다른 경우에만 업데이트 (성능 최적화)
            if (coinText != null && coinText.text != $"코인: {currencyInfo.coin:N0}")
            {
                UpdateCoinDisplay(currencyInfo.coin);
            }
            
            if (tpText != null && tpText.text != $"TP: {currencyInfo.tp:N0}")
            {
                UpdateTpDisplay(currencyInfo.tp);
            }
            
            if (waterPointText != null && waterPointText.text != $"워터포인트: {currencyInfo.waterPoint:N0}")
            {
                UpdateWaterPointDisplay(currencyInfo.waterPoint);
            }
        }
    }
    
    /// <summary>
    /// 코인 표시를 업데이트합니다.
    /// </summary>
    private void UpdateCoinDisplay(int amount)
    {
        if (coinText != null)
        {
            coinText.text = $"코인: {amount:N0}";
            // 색상 변경 (선택사항 - 재화 부족 시 빨간색 표시 등)
            coinText.color = amount > 0 ? Color.black : Color.red;
        }
        else
        {
            Debug.LogWarning("[InGameUIManager] 코인 텍스트 UI가 할당되지 않았습니다!");
        }
    }
    
    /// <summary>
    /// TP 표시를 업데이트합니다.
    /// </summary>
    private void UpdateTpDisplay(int amount)
    {
        if (tpText != null)
        {
            tpText.text = $"TP: {amount:N0}";
            // 색상 변경 (선택사항)
            tpText.color = amount > 0 ? Color.black : Color.red;
        }
        else
        {
            Debug.LogWarning("[InGameUIManager] TP 텍스트 UI가 할당되지 않았습니다!");
        }
    }
    
    /// <summary>
    /// 워터포인트 표시를 업데이트합니다.
    /// </summary>
    private void UpdateWaterPointDisplay(int amount)
    {
        if (waterPointText != null)
        {
            waterPointText.text = $"워터포인트: {amount:N0}";
            // 색상 변경 (선택사항)
            waterPointText.color = amount > 0 ? Color.black : Color.red;
        }
        else
        {
            Debug.LogWarning("[InGameUIManager] 워터포인트 텍스트 UI가 할당되지 않았습니다!");
        }
    }
    
    /// <summary>
    /// 재화 사용 가능 여부를 확인합니다.
    /// </summary>
    public bool CanAffordCost(int coinCost, int tpCost, int waterPointCost)
    {
        if (!DataManger.IsAvailable()) return false;
        
        var currencyInfo = DataManger.Instance.GetAllCurrencyInfo();
        return currencyInfo.coin >= coinCost && 
               currencyInfo.tp >= tpCost && 
               currencyInfo.waterPoint >= waterPointCost;
    }
    
    /// <summary>
    /// 재화를 소모합니다. (터렛 구매, 업그레이드 등에 사용) - 비동기 버전
    /// </summary>
    public async UniTask<bool> SpendCurrencyAsync(int coinCost, int tpCost, int waterPointCost, CancellationToken cancellationToken = default)
    {
        if (!DataManger.IsAvailable()) return false;
        
        // 재화 충분한지 확인
        if (!CanAffordCost(coinCost, tpCost, waterPointCost))
        {
            Debug.LogWarning($"[InGameUIManager] 재화 부족! 필요: 코인({coinCost}), TP({tpCost}), 워터포인트({waterPointCost})");
            return false;
        }
        
        // 재화 소모를 비동기로 처리
        bool success = true;
        if (coinCost > 0) success &= await DataManger.Instance.SpendCoinAsync(coinCost, cancellationToken);
        if (tpCost > 0) success &= await DataManger.Instance.SpendTPAsync(tpCost, cancellationToken);
        if (waterPointCost > 0) success &= await DataManger.Instance.SpendWaterPointAsync(waterPointCost, cancellationToken);
        
        if (success)
        {
            Debug.Log($"[InGameUIManager] 재화 소모 성공! 코인(-{coinCost}), TP(-{tpCost}), 워터포인트(-{waterPointCost})");
        }
        
        return success;
    }
    
    // 동기 버전 유지 (하위 호환성)
    public bool SpendCurrency(int coinCost, int tpCost, int waterPointCost)
    {
        if (!DataManger.IsAvailable()) return false;
        
        // 재화 충분한지 확인
        if (!CanAffordCost(coinCost, tpCost, waterPointCost))
        {
            Debug.LogWarning($"[InGameUIManager] 재화 부족! 필요: 코인({coinCost}), TP({tpCost}), 워터포인트({waterPointCost})");
            return false;
        }
        
        // 재화 소모
        bool success = true;
        if (coinCost > 0) success &= DataManger.Instance.SpendCoin(coinCost);
        if (tpCost > 0) success &= DataManger.Instance.SpendTP(tpCost);
        if (waterPointCost > 0) success &= DataManger.Instance.SpendWaterPoint(waterPointCost);
        
        if (success)
        {
            Debug.Log($"[InGameUIManager] 재화 소모 성공! 코인(-{coinCost}), TP(-{tpCost}), 워터포인트(-{waterPointCost})");
        }
        
        return success;
    }
    
    /// <summary>
    /// 필요한 레이어가 존재하는지 확인
    /// </summary>
    private void CheckLayerSetup()
    {
        int turretLayer = LayerMask.NameToLayer("Turret");
        if (turretLayer == -1)
        {
            Debug.LogError("'Turret' 레이어가 존재하지 않습니다! 프로젝트 설정에서 레이어를 추가해주세요.");
        }
        
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer == -1)
        {
            Debug.LogError("'Ground' 레이어가 존재하지 않습니다! 프로젝트 설정에서 레이어를 추가해주세요.");
        }
        
        int previewLayer = LayerMask.NameToLayer("Preview");
        if (previewLayer == -1)
        {
            Debug.LogWarning("'Preview' 레이어가 존재하지 않습니다. 기존 레이어를 사용합니다.");
        }
    }

    /// <summary>
    /// 컴포넌트 파괴 시 정리 작업
    /// </summary>
    void OnDestroy()
    {
        // CancellationToken 정리
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();
        
        // 이벤트 구독 해제
        if (DataManger.Instance != null)
        {
            DataManger.Instance.OnCoinChanged -= UpdateCoinDisplay;
            DataManger.Instance.OnTPChanged -= UpdateTpDisplay;
            DataManger.Instance.OnWaterPointChanged -= UpdateWaterPointDisplay;
        }
    }
}
