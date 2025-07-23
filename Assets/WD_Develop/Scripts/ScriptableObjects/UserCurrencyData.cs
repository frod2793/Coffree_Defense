using UnityEngine;
using System;

[CreateAssetMenu(fileName = "UserCurrencyData", menuName = "ScriptableObjects/User Currency Data")]
public class UserCurrencyData : ScriptableObject
{
    [Header("유저 재화 정보")]
    [SerializeField] private int coin = 0;
    [SerializeField] private int tp = 0; // Tower Point
    [SerializeField] private int waterPoint = 0;
    
    // 재화 변경 이벤트
    public static event Action<int> OnCoinChanged;
    public static event Action<int> OnTPChanged;
    public static event Action<int> OnWaterPointChanged;
    
    // 프로퍼티로 접근 제어
    public int Coin 
    { 
        get => coin; 
        private set 
        { 
            coin = Mathf.Max(0, value); 
            OnCoinChanged?.Invoke(coin);
        } 
    }
    
    public int TP 
    { 
        get => tp; 
        private set 
        { 
            tp = Mathf.Max(0, value); 
            OnTPChanged?.Invoke(tp);
        } 
    }
    
    public int WaterPoint 
    { 
        get => waterPoint; 
        private set 
        { 
            waterPoint = Mathf.Max(0, value); 
            OnWaterPointChanged?.Invoke(waterPoint);
        } 
    }
    
    /// <summary>
    /// 코인을 추가합니다.
    /// </summary>
    public void AddCoin(int amount)
    {
        if (amount > 0)
        {
            Coin += amount;
            Debug.Log($"코인 추가: +{amount} (총 {Coin})");
        }
    }
    
    /// <summary>
    /// 코인을 사용합니다.
    /// </summary>
    public bool SpendCoin(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("잘못된 코인 사용량입니다.");
            return false;
        }
        
        if (Coin >= amount)
        {
            Coin -= amount;
            Debug.Log($"코인 사용: -{amount} (남은 코인: {Coin})");
            return true;
        }
        
        Debug.LogWarning($"코인이 부족합니다. 필요: {amount}, 보유: {Coin}");
        return false;
    }
    
    /// <summary>
    /// TP를 추가합니다.
    /// </summary>
    public void AddTP(int amount)
    {
        if (amount > 0)
        {
            TP += amount;
            Debug.Log($"TP 추가: +{amount} (총 {TP})");
        }
    }
    
    /// <summary>
    /// TP를 사용합니다.
    /// </summary>
    public bool SpendTP(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("잘못된 TP 사용량입니다.");
            return false;
        }
        
        if (TP >= amount)
        {
            TP -= amount;
            Debug.Log($"TP 사용: -{amount} (남은 TP: {TP})");
            return true;
        }
        
        Debug.LogWarning($"TP가 부족합니다. 필요: {amount}, 보유: {TP}");
        return false;
    }
    
    /// <summary>
    /// 워터 포인트를 추가합니다.
    /// </summary>
    public void AddWaterPoint(int amount)
    {
        if (amount > 0)
        {
            WaterPoint += amount;
            Debug.Log($"워터 포인트 추가: +{amount} (총 {WaterPoint})");
        }
    }
    
    /// <summary>
    /// 워터 포인트를 사용합니다.
    /// </summary>
    public bool SpendWaterPoint(int amount)
    {
        if (amount <= 0)
        {
            Debug.LogWarning("잘못된 워터 포인트 사용량입니다.");
            return false;
        }
        
        if (WaterPoint >= amount)
        {
            WaterPoint -= amount;
            Debug.Log($"워터 포인트 사용: -{amount} (남은 워터 포인트: {WaterPoint})");
            return true;
        }
        
        Debug.LogWarning($"워터 포인트가 부족합니다. 필요: {amount}, 보유: {WaterPoint}");
        return false;
    }
    
    /// <summary>
    /// 특정 재화가 충분한지 확인합니다.
    /// </summary>
    public bool HasEnoughCoin(int amount) => Coin >= amount;
    public bool HasEnoughTP(int amount) => TP >= amount;
    public bool HasEnoughWaterPoint(int amount) => WaterPoint >= amount;
    
    /// <summary>
    /// 모든 재화를 초기화합니다.
    /// </summary>
    public void ResetAllCurrency()
    {
        Coin = 0;
        TP = 0;
        WaterPoint = 0;
        Debug.Log("모든 재화가 초기화되었습니다.");
    }
    
    /// <summary>
    /// 재화 정보를 설정합니다. (에디터 또는 디버그 용도)
    /// </summary>
    [ContextMenu("Debug - Set Test Currency")]
    public void SetTestCurrency()
    {
        Coin = 1000;
        TP = 50;
        WaterPoint = 25;
        Debug.Log("테스트 재화가 설정되었습니다.");
    }
    
    /// <summary>
    /// 현재 재화 상태를 로그로 출력합니다.
    /// </summary>
    [ContextMenu("Debug - Show Currency Status")]
    public void ShowCurrencyStatus()
    {
        Debug.Log($"현재 재화 상태 - 코인: {Coin}, TP: {TP}, 워터 포인트: {WaterPoint}");
    }
    
    // 인스펙터에서 값 변경 시 유효성 검사
    private void OnValidate()
    {
        coin = Mathf.Max(0, coin);
        tp = Mathf.Max(0, tp);
        waterPoint = Mathf.Max(0, waterPoint);
    }
}

