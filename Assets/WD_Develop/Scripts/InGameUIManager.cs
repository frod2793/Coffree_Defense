using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// 인게임 UI 관리자 - UniTask 기반 비동기 처리로 최적화
/// 포탑 조합, 드래그앤드롭, 재화 관리를 담당합니다.
/// </summary>
public class InGameUIManager : MonoBehaviour
{
    #region 필드 및 속성
    
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
    
    // 드래그 관련
    private bool isDraggingTurret = false;
    private TerretControl terretControl;

    [Header("In-Game UI Manager")]
    [SerializeField] private GameObject inGameUI;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private List<Image> images;
    [SerializeField] private Button addTurretButton;
    [SerializeField] private List<GameObject> PrefabList;

    [Header("사용자 데이터")]
    [SerializeField] private TMPro.TextMeshProUGUI coinText;
    [SerializeField] private TMPro.TextMeshProUGUI tpText;
    [SerializeField] private TMPro.TextMeshProUGUI waterPointText;

    [Header("성능 설정")]
    [SerializeField] private float currencyUpdateInterval = 0.5f;
    [SerializeField] private int eventTriggerBatchSize = 5; // 이벤트 트리거 설정 시 프레임 분산 크기

    [Header("게임 상태")]
    [SerializeField] private TMPro.TextMeshProUGUI GameCountDownText; // 게임 카운트다운 텍스트
    [SerializeField] private float countdownDuration = 10f; // 카운트다운 시간 (초)
    [SerializeField] private bool enableCountdown = true; // 카운트다운 활성화 여부
    
    // 게임 상태 관리
    private bool isGameStarted = false;
    private bool isCountdownActive = false;

    
    // UniTask 관련
    private CancellationTokenSource cancellationTokenSource;
    private bool isInitialized = false;
    
    // 성능 최적화를 위한 캐시
    private DataManger.CurrencyInfo lastCurrencyInfo;
    private bool hasDataManagerEvents = false;

    #endregion

    #region 유니티 생명주기

    async void Start()
    {
        cancellationTokenSource = new CancellationTokenSource();
        await InitializeAsync(cancellationTokenSource.Token);
    }

    void OnDestroy()
    {
        CleanupResources();
    }

    #endregion

    #region 초기화

    private async UniTask InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 컴포넌트 찾기
            await FindRequiredComponentsAsync(cancellationToken);
            
            // 이벤트 시스템 설정
            await SetupEventSystemAsync(cancellationToken);
            
            // 시스템 검증
            await ValidateSystemsAsync(cancellationToken);
            
            // 재화 UI 초기화
            await InitializeCurrencyUIAsync(cancellationToken);
            
            // 게임 카운트다운 시작
            if (enableCountdown && GameCountDownText != null)
            {
                await StartGameCountdownAsync(cancellationToken);
            }
            
            // 주기적 업데이트 시작
            StartPeriodicUpdateAsync(cancellationToken).Forget();
            
            isInitialized = true;
            Debug.Log("[InGameUIManager] 초기화 완료");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[InGameUIManager] 초기화 실패: {ex.Message}");
        }
    }

    private async UniTask FindRequiredComponentsAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        if (terretControl == null)
        {
            terretControl = FindFirstObjectByType<TerretControl>();
            if (terretControl == null)
            {
                Debug.LogError("[InGameUIManager] TerretControl을 찾을 수 없습니다.");
            }
        }
    }

    private async UniTask SetupEventSystemAsync(CancellationToken cancellationToken)
    {
        await SetupEventTriggersAsync(cancellationToken);
        await SetupButtonEventsAsync(cancellationToken);
    }

    private async UniTask ValidateSystemsAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        CheckLayerSetup();
    }

    #endregion

    #region 이벤트 시스템 설정

    private async UniTask SetupEventTriggersAsync(CancellationToken cancellationToken)
    {
        if (images?.Count == 0)
        {
            Debug.LogWarning("[InGameUIManager] 드래그 가능한 이미지가 설정되지 않았습니다.");
            return;
        }

        for (int i = 0; i < images.Count; i++)
        {
            var image = images[i];
            if (image == null) continue;

            SetupImageEventTrigger(image, i);

            // 프레임 분산 처리 - 성능 최적화
            if (i % eventTriggerBatchSize == 0)
            {
                await UniTask.Yield(cancellationToken);
            }
        }
    }

    private void SetupImageEventTrigger(Image image, int index)
    {
        var trigger = image.GetComponent<EventTrigger>() ?? image.gameObject.AddComponent<EventTrigger>();
        
        trigger.triggers.Clear(); // 기존 트리거 정리

        // Begin Drag 이벤트
        var beginDragEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.BeginDrag
        };
        beginDragEntry.callback.AddListener((data) => OnBeginDrag(index));
        trigger.triggers.Add(beginDragEntry);

        // End Drag 이벤트
        var endDragEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.EndDrag
        };
        endDragEntry.callback.AddListener((data) => OnEndDrag());
        trigger.triggers.Add(endDragEntry);
    }

    private async UniTask SetupButtonEventsAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);

        if (addTurretButton != null)
        {
            addTurretButton.onClick.RemoveAllListeners(); // 기존 리스너 정리
            addTurretButton.onClick.AddListener(OnAddTurretButtonClicked);
        }
    }

    #endregion

    #region 버튼 이벤트 핸들러

    private void OnAddTurretButtonClicked()
    {
        if (terretControl == null)
        {
            Debug.LogWarning("[InGameUIManager] TerretControl이 없습니다.");
            return;
        }

        // TP 확인 및 소모 - 성능 최적화된 체크
        if (!DataManger.IsAvailable())
        {
            Debug.LogWarning("[InGameUIManager] DataManager를 사용할 수 없습니다.");
            return;
        }

        var currentTP = DataManger.Instance.GetTP();
        if (currentTP <= 0)
        {
            Debug.LogWarning("[InGameUIManager] TP가 부족하여 터렛을 추가할 수 없습니다.");
            return;
        }

        terretControl.SetAddTurret();
        DataManger.Instance.SpendTP(1);
    }

    #endregion

    #region 드래그 앤 드롭 시스템

    /// <summary>
    /// TerretControl에서 터렛 드래그 상태를 설정합니다.
    /// </summary>
    public void SetDraggingTurret(bool isDragging)
    {
        isDraggingTurret = isDragging;
    }

    /// <summary>
    /// UI 이미지에서 드래그를 시작할 때 호출됩니다.
    /// </summary>
    public void OnBeginDrag(int prefabIndex)
    {
        if (isDraggingTurret || terretControl == null) return;

        if (IsValidPrefabIndex(prefabIndex))
        {
            var selectedPrefab = PrefabList[prefabIndex];
            if (selectedPrefab != null)
            {
                terretControl.StartItemDrag(selectedPrefab);
            }
        }
    }

    /// <summary>
    /// 드래그를 놓았을 때 호출됩니다.
    /// </summary>
    public void OnEndDrag()
    {
        terretControl?.EndItemDrag();
    }

    private bool IsValidPrefabIndex(int index)
    {
        return PrefabList?.Count > 0 && index >= 0 && index < PrefabList.Count;
    }

    #endregion

    #region 재화 UI 시스템

    /// <summary>
    /// 재화 UI를 초기화합니다.
    /// </summary>
    private async UniTask InitializeCurrencyUIAsync(CancellationToken cancellationToken)
    {
        if (DataManger.IsAvailable())
        {
            await SubscribeToDataManagerEventsAsync(cancellationToken);
            await UpdateAllCurrencyDisplaysAsync(cancellationToken);
            
            Debug.Log("[InGameUIManager] 재화 UI 초기화 완료");
        }
        else
        {
            Debug.LogWarning("[InGameUIManager] DataManager를 찾을 수 없어 기본 재화 정보를 표시합니다.");
            SetDefaultCurrencyDisplay();
        }
    }

    private async UniTask SubscribeToDataManagerEventsAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        if (!hasDataManagerEvents && DataManger.Instance != null)
        {
            DataManger.Instance.OnCoinChanged += UpdateCoinDisplay;
            DataManger.Instance.OnTPChanged += UpdateTpDisplay;
            DataManger.Instance.OnWaterPointChanged += UpdateWaterPointDisplay;
            hasDataManagerEvents = true;
        }
    }

    private async UniTask UpdateAllCurrencyDisplaysAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);
        
        var currencyInfo = DataManger.Instance.GetAllCurrencyInfo();
        lastCurrencyInfo = currencyInfo;
        
        UpdateCoinDisplay(currencyInfo.coin);
        UpdateTpDisplay(currencyInfo.tp);
        UpdateWaterPointDisplay(currencyInfo.waterPoint);
    }

    private void SetDefaultCurrencyDisplay()
    {
        UpdateCoinDisplay(0);
        UpdateTpDisplay(0);
        UpdateWaterPointDisplay(0);
    }

    #endregion

    #region 재화 표시 업데이트

    /// <summary>
    /// 주기적으로 재화 정보를 업데이트합니다.
    /// </summary>
    private async UniTask StartPeriodicUpdateAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && this != null)
        {
            try
            {
                await UpdateCurrencyDisplayAsync(cancellationToken);
                await UniTask.Delay(System.TimeSpan.FromSeconds(currencyUpdateInterval),
                    DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[InGameUIManager] 주기적 업데이트 오류: {ex.Message}");
                await UniTask.Delay(1000, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }

    private async UniTask UpdateCurrencyDisplayAsync(CancellationToken cancellationToken)
    {
        if (!DataManger.IsAvailable()) return;

        await UniTask.Yield(cancellationToken);

        var currencyInfo = DataManger.Instance.GetAllCurrencyInfo();

        // 성능 최적화: 값이 변경된 경우에만 업데이트
        if (!CurrencyInfoEquals(currencyInfo, lastCurrencyInfo))
        {
            UpdateChangedCurrencyDisplays(currencyInfo);
            lastCurrencyInfo = currencyInfo;
        }
    }

    private bool CurrencyInfoEquals(DataManger.CurrencyInfo info1, DataManger.CurrencyInfo info2)
    {
        return info1.coin == info2.coin && 
               info1.tp == info2.tp && 
               info1.waterPoint == info2.waterPoint;
    }

    private void UpdateChangedCurrencyDisplays(DataManger.CurrencyInfo currencyInfo)
    {
        if (currencyInfo.coin != lastCurrencyInfo.coin)
            UpdateCoinDisplay(currencyInfo.coin);

        if (currencyInfo.tp != lastCurrencyInfo.tp)
            UpdateTpDisplay(currencyInfo.tp);

        if (currencyInfo.waterPoint != lastCurrencyInfo.waterPoint)
            UpdateWaterPointDisplay(currencyInfo.waterPoint);
    }

    private void UpdateCoinDisplay(int amount)
    {
        if (coinText != null)
        {
            coinText.text = $"코인: {amount:N0}";
            coinText.color = amount > 0 ? Color.black : Color.red;
        }
        else
        {
            Debug.LogWarning("[InGameUIManager] 코인 텍스트 UI가 할당되지 않았습니다!");
        }
    }

    private void UpdateTpDisplay(int amount)
    {
        if (tpText != null)
        {
            tpText.text = $"TP: {amount:N0}";
            tpText.color = amount > 0 ? Color.black : Color.red;

            // 터렛 추가 버튼 상태 업데이트
            if (addTurretButton != null)
            {
                addTurretButton.interactable = amount > 0;
            }
        }
        else
        {
            Debug.LogWarning("[InGameUIManager] TP 텍스트 UI가 할당되지 않았습니다!");
        }
    }

    private void UpdateWaterPointDisplay(int amount)
    {
        if (waterPointText != null)
        {
            waterPointText.text = $"워터포인트: {amount:N0}";
            waterPointText.color = amount > 0 ? Color.black : Color.red;
        }
        else
        {
            Debug.LogWarning("[InGameUIManager] 워터포인트 텍스트 UI가 할당되지 않았습니다!");
        }
    }

    #endregion

    #region 재화 관리

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
    /// 재화를 소모합니다. (���동기 버전)
    /// </summary>
    public async UniTask<bool> SpendCurrencyAsync(int coinCost, int tpCost, int waterPointCost,
        CancellationToken cancellationToken = default)
    {
        if (!DataManger.IsAvailable()) return false;

        if (!CanAffordCost(coinCost, tpCost, waterPointCost))
        {
            Debug.LogWarning($"[InGameUIManager] 재화 부족! 필요: 코인({coinCost}), TP({tpCost}), 워터포인트({waterPointCost})");
            return false;
        }

        bool success = await ProcessCurrencySpendingAsync(coinCost, tpCost, waterPointCost, cancellationToken);

        if (success)
        {
            Debug.Log($"[InGameUIManager] 재화 소모 성공! 코인(-{coinCost}), TP(-{tpCost}), 워터포인트(-{waterPointCost})");
        }

        return success;
    }

    /// <summary>
    /// 재화를 소모합니다. (동기 버전 - 하위 호환성)
    /// </summary>
    public bool SpendCurrency(int coinCost, int tpCost, int waterPointCost)
    {
        if (!DataManger.IsAvailable()) return false;

        if (!CanAffordCost(coinCost, tpCost, waterPointCost))
        {
            Debug.LogWarning($"[InGameUIManager] 재화 부족! 필요: 코인({coinCost}), TP({tpCost}), 워터포인트({waterPointCost})");
            return false;
        }

        bool success = ProcessCurrencySpending(coinCost, tpCost, waterPointCost);

        if (success)
        {
            Debug.Log($"[InGameUIManager] 재화 소모 성공! 코인(-{coinCost}), TP(-{tpCost}), 워터포인트(-{waterPointCost})");
        }

        return success;
    }

    private async UniTask<bool> ProcessCurrencySpendingAsync(int coinCost, int tpCost, int waterPointCost, CancellationToken cancellationToken)
    {
        bool success = true;
        
        if (coinCost > 0) success &= await DataManger.Instance.SpendCoinAsync(coinCost, cancellationToken);
        if (tpCost > 0) success &= await DataManger.Instance.SpendTPAsync(tpCost, cancellationToken);
        if (waterPointCost > 0) success &= await DataManger.Instance.SpendWaterPointAsync(waterPointCost, cancellationToken);
        
        return success;
    }

    private bool ProcessCurrencySpending(int coinCost, int tpCost, int waterPointCost)
    {
        bool success = true;
        
        if (coinCost > 0) success &= DataManger.Instance.SpendCoin(coinCost);
        if (tpCost > 0) success &= DataManger.Instance.SpendTP(tpCost);
        if (waterPointCost > 0) success &= DataManger.Instance.SpendWaterPoint(waterPointCost);
        
        return success;
    }

    #endregion

    #region 게임 카운트다운 시스템

    /// <summary>
    /// 게임 시작 전 카운트다운��� 실행합니다.
    /// </summary>
    private async UniTask StartGameCountdownAsync(CancellationToken cancellationToken)
    {
        if (isCountdownActive || GameCountDownText == null)
        {
            Debug.LogWarning("[InGameUIManager] 카운트다운이 이미 실행 중이거나 텍스트가 없습니다.");
            return;
        }

        isCountdownActive = true;
        float remainingTime = countdownDuration;

        Debug.Log($"[InGameUIManager] 게임 카운트다운 시작: {countdownDuration}초");

        try
        {
            // 카운트다운 UI 활성화
            if (GameCountDownText.gameObject != null)
            {
                GameCountDownText.gameObject.SetActive(true);
            }

            while (remainingTime > 0 && !cancellationToken.IsCancellationRequested)
            {
                // 카운트다운 텍스트 업데이트
                UpdateCountdownDisplay(remainingTime);

                // 1초 대기
                await UniTask.Delay(1000, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
                remainingTime -= 1f;
            }

            // 카운트다운 완료
            await OnCountdownCompleteAsync(cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("[InGameUIManager] 카운트다운이 취소되었습니다.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[InGameUIManager] 카운트다운 오류: {ex.Message}");
        }
        finally
        {
            isCountdownActive = false;
        }
    }

    /// <summary>
    /// 카운트다운 텍스트를 업데이트합니다.
    /// </summary>
    private void UpdateCountdownDisplay(float remainingTime)
    {
        if (GameCountDownText == null) return;

        int seconds = Mathf.CeilToInt(remainingTime);
        
        // 시간에 따른 다른 메시지 표시
        if (seconds > 5)
        {
            GameCountDownText.text = $"게임 시작까지\n{seconds}초";
            GameCountDownText.color = Color.black;
        }
        else if (seconds > 0)
        {
            GameCountDownText.text = $"{seconds}";
            GameCountDownText.color = Color.red;
            
            // 마지막 5초는 크기 효과 추가
            float scale = 1f + (6 - seconds) * 0.2f;
            GameCountDownText.transform.localScale = Vector3.one * scale;
        }
        else
        {
            GameCountDownText.text = "START!";
            GameCountDownText.color = Color.green;
            GameCountDownText.transform.localScale = Vector3.one * 1.5f;
        }
    }

    /// <summary>
    /// 카운트다운 완료 시 호출됩니다.
    /// </summary>
    private async UniTask OnCountdownCompleteAsync(CancellationToken cancellationToken)
    {
        Debug.Log("[InGameUIManager] 카운트다운 완료! 게임 시작");

        // "START!" 메시지를 잠시 표시
        UpdateCountdownDisplay(0);
        await UniTask.Delay(1000, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);

        // 카운트다운 텍스트 비활성화
        if (GameCountDownText != null && GameCountDownText.gameObject != null)
        {
            GameCountDownText.gameObject.SetActive(false);
        }

        // 게임 시작 상태로 변경
        isGameStarted = true;

        // GameManager에 게임 시작 알림
        await NotifyGameStartAsync(cancellationToken);
    }

    /// <summary>
    /// GameManager에 게임 시작을 알립니다.
    /// </summary>
    private async UniTask NotifyGameStartAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);

        if (gameManager != null)
        {
            // GameManager에 게임 시작 메서드가 있다면 호출
            var startGameMethod = gameManager.GetType().GetMethod("StartGame");
            if (startGameMethod != null)
            {
                startGameMethod.Invoke(gameManager, null);
                Debug.Log("[InGameUIManager] GameManager에 게임 시작 알림 완료");
            }
            else
            {
                Debug.LogWarning("[InGameUIManager] GameManager에 StartGame 메서드가 없습니다.");
            }
        }
        else
        {
            Debug.LogWarning("[InGameUIManager] GameManager가 할당되지 않았습니다.");
        }
    }

    /// <summary>
    /// 카운트다운을 수동으로 시작합니다.
    /// </summary>
    public void StartCountdown()
    {
        if (!isCountdownActive && enableCountdown)
        {
            StartGameCountdownAsync(cancellationTokenSource.Token).Forget();
        }
    }

    /// <summary>
    /// 카운트다운을 중지합니다.
    /// </summary>
    public void StopCountdown()
    {
        if (isCountdownActive)
        {
            cancellationTokenSource?.Cancel();
            
            if (GameCountDownText != null && GameCountDownText.gameObject != null)
            {
                GameCountDownText.gameObject.SetActive(false);
            }
            
            isCountdownActive = false;
            Debug.Log("[InGameUIManager] 카운트다운이 중지되었습니다.");
        }
    }

    /// <summary>
    /// 카운트다운 시간을 설정합니다.
    /// </summary>
    public void SetCountdownDuration(float duration)
    {
        if (duration > 0)
        {
            countdownDuration = duration;
            Debug.Log($"[InGameUIManager] 카운트다운 시간이 {duration}초로 설정되었습니다.");
        }
    }

    #endregion

    #region 시스템 검증

    /// <summary>
    /// 필요한 레이어가 존재하는지 확인합니다.
    /// </summary>
    private void CheckLayerSetup()
    {
        var requiredLayers = new[] { "Turret", "Ground", "Preview" };
        var missingLayers = new List<string>();

        foreach (var layerName in requiredLayers)
        {
            if (LayerMask.NameToLayer(layerName) == -1)
            {
                missingLayers.Add(layerName);
            }
        }

        if (missingLayers.Count > 0)
        {
            Debug.LogError($"[InGameUIManager] 다음 레이어가 누락되었습니다: {string.Join(", ", missingLayers)}");
        }
    }

    #endregion

    #region 리소스 정리

    /// <summary>
    /// 리소스 정리
    /// </summary>
    private void CleanupResources()
    {
        // CancellationToken 정리
        cancellationTokenSource?.Cancel();
        cancellationTokenSource?.Dispose();

        // 이벤트 구독 해제
        if (hasDataManagerEvents && DataManger.Instance != null)
        {
            DataManger.Instance.OnCoinChanged -= UpdateCoinDisplay;
            DataManger.Instance.OnTPChanged -= UpdateTpDisplay;
            DataManger.Instance.OnWaterPointChanged -= UpdateWaterPointDisplay;
            hasDataManagerEvents = false;
        }

        Debug.Log("[InGameUIManager] 리소스 정리 완료");
    }

    #endregion

    #region 공개 속성

    /// <summary>
    /// UI 매니저가 초기화되었는지 확인합니다.
    /// </summary>
    public bool IsInitialized => isInitialized;

    /// <summary>
    /// 현재 터렛을 드래그 중인지 확인합니다.
    /// </summary>
    public bool IsDraggingTurret => isDraggingTurret;

    /// <summary>
    /// 게임이 시작되었는지 확인합니다.
    /// </summary>
    public bool IsGameStarted => isGameStarted;

    /// <summary>
    /// 카운트다운이 활성화되어 있는지 확인합니다.
    /// </summary>
    public bool IsCountdownActive => isCountdownActive;

    /// <summary>
    /// 남은 카운트다운 시간을 반환합니다.
    /// </summary>
    public float CountdownDuration => countdownDuration;

    #endregion
}
