using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DataEditorUI : MonoBehaviour
{
    [Header("재화 표시")]
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private TextMeshProUGUI tpText;
    [SerializeField] private TextMeshProUGUI waterPointText;

    [Header("입력 필드")]
    [SerializeField] private TMP_InputField coinInputField;
    [SerializeField] private TMP_InputField tpInputField;
    [SerializeField] private TMP_InputField waterPointInputField;

    [Header("코인 버튼")]
    [SerializeField] private Button addCoinButton;
    [SerializeField] private Button subtractCoinButton;
    [SerializeField] private Button setCoinButton;

    [Header("TP 버튼")]
    [SerializeField] private Button addTPButton;
    [SerializeField] private Button subtractTPButton;
    [SerializeField] private Button setTPButton;

    [Header("워터 포인트 버튼")]
    [SerializeField] private Button addWaterPointButton;
    [SerializeField] private Button subtractWaterPointButton;
    [SerializeField] private Button setWaterPointButton;

    [Header("데이터 관리 버튼")]
    [SerializeField] private Button resetDataButton;
    [SerializeField] private Button saveDataButton;
    [SerializeField] private Button loadDataButton;

    [Header("게임씬 이동 버튼")]
    [SerializeField] private Button goToGameSceneButton;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        SetupUI();
        SetupEventListeners();
        UpdateUI();
    }

    void SetupUI()
    {
        // DataManager가 없으면 경고
        if (DataManger.Instance == null)
        {
            Debug.LogError("DataManager Instance가 없습니다!");
            return;
        }

        // 기본 입력값 설정
        if (coinInputField != null) coinInputField.text = "100";
        if (tpInputField != null) tpInputField.text = "10";
        if (waterPointInputField != null) waterPointInputField.text = "50";
    }

    void SetupEventListeners()
    {
        if (DataManger.Instance == null) return;

        // DataManager 이벤트 구독
        DataManger.Instance.OnCoinChanged += UpdateCoinDisplay;
        DataManger.Instance.OnTPChanged += UpdateTPDisplay;
        DataManger.Instance.OnWaterPointChanged += UpdateWaterPointDisplay;

        // 코인 버튼 이벤트 설정
        if (addCoinButton != null)
            addCoinButton.onClick.AddListener(() => AddCurrency("coin"));
        
        if (subtractCoinButton != null)
            subtractCoinButton.onClick.AddListener(() => SubtractCurrency("coin"));
        
        if (setCoinButton != null)
            setCoinButton.onClick.AddListener(() => SetCurrency("coin"));

        // TP 버튼 이벤트 설정
        if (addTPButton != null)
            addTPButton.onClick.AddListener(() => AddCurrency("tp"));
        
        if (subtractTPButton != null)
            subtractTPButton.onClick.AddListener(() => SubtractCurrency("tp"));
        
        if (setTPButton != null)
            setTPButton.onClick.AddListener(() => SetCurrency("tp"));

        // 워터 포인트 버튼 이벤트 설정
        if (addWaterPointButton != null)
            addWaterPointButton.onClick.AddListener(() => AddCurrency("waterPoint"));
        
        if (subtractWaterPointButton != null)
            subtractWaterPointButton.onClick.AddListener(() => SubtractCurrency("waterPoint"));
        
        if (setWaterPointButton != null)
            setWaterPointButton.onClick.AddListener(() => SetCurrency("waterPoint"));

        // 데이터 관리 버튼 이벤트 설정
        if (resetDataButton != null)
            resetDataButton.onClick.AddListener(ResetData);
        
        if (saveDataButton != null)
            saveDataButton.onClick.AddListener(SaveData);
        
        if (loadDataButton != null)
            loadDataButton.onClick.AddListener(LoadData);
        
        // 게임씬 이동 버튼 이벤트 설정
        if (goToGameSceneButton != null)
            goToGameSceneButton.onClick.AddListener(GoToGameScene);
    }

    void UpdateUI()
    {
        if (DataManger.Instance == null) return;

        UpdateCoinDisplay(DataManger.Instance.GetCoin());
        UpdateTPDisplay(DataManger.Instance.GetTp());
        UpdateWaterPointDisplay(DataManger.Instance.GetWaterPoint());
    }

    void UpdateCoinDisplay(int amount)
    {
        if (coinText != null)
            coinText.text = $"코인: {amount}";
    }

    void UpdateTPDisplay(int amount)
    {
        if (tpText != null)
            tpText.text = $"TP: {amount}";
    }

    void UpdateWaterPointDisplay(int amount)
    {
        if (waterPointText != null)
            waterPointText.text = $"워터 포인트: {amount}";
    }

    void AddCurrency(string currencyType)
    {
        if (DataManger.Instance == null) return;

        int amount = 0;
        TMP_InputField targetInputField = null;

        switch (currencyType)
        {
            case "coin":
                targetInputField = coinInputField;
                break;
            case "tp":
                targetInputField = tpInputField;
                break;
            case "waterPoint":
                targetInputField = waterPointInputField;
                break;
        }

        if (targetInputField != null && int.TryParse(targetInputField.text, out amount))
        {
            switch (currencyType)
            {
                case "coin":
                    DataManger.Instance.AddCoin(amount);
                    break;
                case "tp":
                    DataManger.Instance.AddTP(amount);
                    break;
                case "waterPoint":
                    DataManger.Instance.AddWaterPoint(amount);
                    break;
            }
        }
    }

    void SubtractCurrency(string currencyType)
    {
        if (DataManger.Instance == null) return;

        int amount = 0;
        TMP_InputField targetInputField = null;

        switch (currencyType)
        {
            case "coin":
                targetInputField = coinInputField;
                break;
            case "tp":
                targetInputField = tpInputField;
                break;
            case "waterPoint":
                targetInputField = waterPointInputField;
                break;
        }

        if (targetInputField != null && int.TryParse(targetInputField.text, out amount))
        {
            switch (currencyType)
            {
                case "coin":
                    DataManger.Instance.SpendCoin(amount);
                    break;
                case "tp":
                    DataManger.Instance.SpendTP(amount);
                    break;
                case "waterPoint":
                    DataManger.Instance.SpendWaterPoint(amount);
                    break;
            }
        }
    }

    void SetCurrency(string currencyType)
    {
        if (DataManger.Instance == null) return;

        int amount = 0;
        TMP_InputField targetInputField = null;

        switch (currencyType)
        {
            case "coin":
                targetInputField = coinInputField;
                break;
            case "tp":
                targetInputField = tpInputField;
                break;
            case "waterPoint":
                targetInputField = waterPointInputField;
                break;
        }

        if (targetInputField != null && int.TryParse(targetInputField.text, out amount))
        {
            var currencyData = DataManger.Instance.GetUserCurrencyData();
            if (currencyData != null)
            {
                int currentCoin = DataManger.Instance.GetCoin();
                int currentTP = DataManger.Instance.GetTp();
                int currentWaterPoint = DataManger.Instance.GetWaterPoint();

                switch (currencyType)
                {
                    case "coin":
                        currencyData.SetCurrency(amount, currentTP, currentWaterPoint);
                        break;
                    case "tp":
                        currencyData.SetCurrency(currentCoin, amount, currentWaterPoint);
                        break;
                    case "waterPoint":
                        currencyData.SetCurrency(currentCoin, currentTP, amount);
                        break;
                }

                DataManger.Instance.SaveUserData();
                UpdateUI();
            }
        }
    }

    void ResetData()
    {
        if (DataManger.Instance != null)
        {
            DataManger.Instance.ResetAllData();
        }
    }

    void SaveData()
    {
        if (DataManger.Instance != null)
        {
            DataManger.Instance.SaveUserData();
            Debug.Log("데이터가 저장되었습니다.");
        }
    }

    void LoadData()
    {
        if (DataManger.Instance != null)
        {
            DataManger.Instance.LoadUserData();
            Debug.Log("데이터가 로드되었습니다.");
        }
    }

    void GoToGameScene()
    {
        // 데이터 저장 후 게임 씬으로 이동
        if (DataManger.Instance != null)
        {
            DataManger.Instance.SaveUserData();
            Debug.Log("게임 씬 이동 전 데이터 저장 완료");
        }
        
        // SceneLoader를 통해 씬 전환
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadScene("InGameTest");
        }
        else
        {
            Debug.LogError("SceneLoader Instance가 없습니다! SceneManager로 직접 씬을 로드합니다.");
            UnityEngine.SceneManagement.SceneManager.LoadScene("InGameTest");
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제
        if (DataManger.Instance != null)
        {
            DataManger.Instance.OnCoinChanged -= UpdateCoinDisplay;
            DataManger.Instance.OnTPChanged -= UpdateTPDisplay;
            DataManger.Instance.OnWaterPointChanged -= UpdateWaterPointDisplay;
        }
    }
}
