using UnityEngine;

[CreateAssetMenu(fileName = "UserCurrencyData", menuName = "Game Data/User Currency Data")]
public class UserCurrencyData : ScriptableObject
{
    [Header("User Currency")]
    [SerializeField] private int coin = 1000;
    [SerializeField] private int tp = 50; // Tower Point
    [SerializeField] private int waterPoint = 100;
    
    [Header("Game Progress")]
    // 플레이어가 선택한 스테이지
    [SerializeField] private int selectStage;
    // 플레이어가 달성한 가장 높은 클리어 스테이지
    [SerializeField] private int highestClearedStage;

    public int Coin
    {
        get => coin;
        private set => coin = Mathf.Max(0, value);
    }
    
    public int TP 
    { 
        get => tp; 
        private set => tp = Mathf.Max(0, value); 
    }
    
    public int WaterPoint 
    { 
        get => waterPoint; 
        private set => waterPoint = Mathf.Max(0, value); 
    }

    public int SelectStage
    {
        get => selectStage;
        private set => selectStage = value;
    }

    public int HighestClearedStage
    {
        get => highestClearedStage;
        private set => highestClearedStage = value;
    }

    private void OnEnable()
    {
        // HideFlags가 잘못 설정되는 것을 방지
        this.hideFlags = HideFlags.None;
    }

    // 코인 관련 메서드
    public bool CanAfford(int amount)
    {
        return coin >= amount;
    }

    public bool SpendCoin(int amount)
    {
        if (CanAfford(amount))
        {
            Coin = coin - amount;
            SaveData(); // 자동 저장
            return true;
        }
        return false;
    }

    public void AddCoin(int amount)
    {
        Coin = coin + amount;
        SaveData(); // 자동 저장
    }

    // TP 관련 메서드
    public bool CanAffordTP(int amount)
    {
        return tp >= amount;
    }

    public bool SpendTP(int amount)
    {
        if (CanAffordTP(amount))
        {
            TP = tp - amount;
            SaveData(); // 자동 저장
            return true;
        }
        return false;
    }

    public void AddTP(int amount)
    {
        TP = tp + amount;
        SaveData(); // 자동 저장
    }

    // 워터 포인트 관련 메서드
    public bool CanAffordWaterPoint(int amount)
    {
        return waterPoint >= amount;
    }

    public bool SpendWaterPoint(int amount)
    {
        if (CanAffordWaterPoint(amount))
        {
            WaterPoint = waterPoint - amount;
            SaveData(); // 자동 저장
            return true;
        }
        return false;
    }

    public void AddWaterPoint(int amount)
    {
        WaterPoint = waterPoint + amount;
        SaveData(); // 자동 저장
    }

    // 모든 재화를 한 번에 설정하는 메서드
    public void SetCurrency(int newCoin, int newTP, int newWaterPoint)
    {
        coin = Mathf.Max(0, newCoin);
        tp = Mathf.Max(0, newTP);
        waterPoint = Mathf.Max(0, newWaterPoint);
        SaveData(); // 자동 저장
    }

    public void SetSelectStage(int stage)
    {
        SelectStage = stage;
        SaveData();
    }

    /// <summary>
    /// 최고 클리어 스테이지를 갱신합니다. 기존 기록보다 높을 때만 저장됩니다.
    /// </summary>
    public void UpdateHighestClearedStage(int clearedStage)
    {
        if (clearedStage > highestClearedStage)
        {
            HighestClearedStage = clearedStage;
            SaveData();
        }
    }

    // 데이터를 기본값으로 리셋
    public void ResetData()
    {
        coin = 1000;
        tp = 50;
        waterPoint = 100;
        selectStage = 0;
        highestClearedStage = 0;
        SaveData(); // 자동 저장
    }

    // ScriptableObject 데이터 저장
    private void SaveData()
    {
#if UNITY_EDITOR
        // 에디터에서는 에셋을 dirty로 마킹하여 저장
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
#endif
    }

    // 수동 저장 메서드 (외부에서 호출 가능)
    public void Save()
    {
        SaveData();
    }
}
