using UnityEngine;

[CreateAssetMenu(fileName = "UserCurrencyData", menuName = "Game Data/User Currency Data")]
public class UserCurrencyData : ScriptableObject
{
    [Header("User Currency")]
    [SerializeField] private int coin = 1000;
    [SerializeField] private int tp = 50; // Tower Point
    [SerializeField] private int waterPoint = 100;

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

    // 데이터를 기본값으로 리셋
    public void ResetData()
    {
        coin = 1000;
        tp = 50;
        waterPoint = 100;
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
