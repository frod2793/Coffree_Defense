using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;

namespace WD_Develop.Scripts.Managers
{
    public class LobbyUiManager : MonoBehaviour
    {
        [SerializeField]
        Button playButton;// 플레이버튼 

        [SerializeField]
        Button settingButton;// 셋팅버튼 
        
        [Header("아이템 구매 창")]
        [SerializeField] 
        private GameObject buyPopUpPanel; // 플레이버튼 클릭시 나올 플레이전 아이템 구매 창

        [SerializeField] 
        private GameObject settingPopUpPanel; // 셋팅 창
        [SerializeField]
        private Button gameStartButton; // 아이템 구매 창에서 게임 시작 버튼
        [SerializeField]
        private Button closeButton; // 아이템 구매 창에서 닫기 버튼
        [SerializeField]
        private Button buyTpButton; // 포탑 포인트 구매 버튼 
        [SerializeField]
        private Button buyWwButton; // 워터 포인트 구매 버튼

        [SerializeField]
        private Button sellTpButton; // 포탑 포인트 판매 버튼 
        [SerializeField]
        private Button sellWwButton; // 워터 포인트 판매 버튼

        public TMPro.TextMeshProUGUI coinText; // 코인 표시용 텍스트
        public TMPro.TextMeshProUGUI turretPointsText; // 포탑 포인트 표시용 텍스트
        public TMPro.TextMeshProUGUI waterPointsText; // 워터 포인트 표시용
        public int turretPointsCost = 100; // 포탑 포인트 구매 비용
        public int waterPointsCost = 150; // 워터 포인트 구매 비용
        public int boughtTurretPoints = 10; // 포탑 포인트 구매시 획득량 / 판매시 소비량
        public int boughtWaterPoints = 10; // 워터 포인트 구매시 획득량 / 판매시 소비량
        public float salesRatio = 0.8f; // 판매시 코인 획득 비율

        [Header("애니메이션 설정")]
        [SerializeField] private float animationDuration = 0.5f;
        [SerializeField] private float scaleAmount = 1.1f;

        // 플레이어 포인트
        private int userCoins;
        private int turretPoints;
        private int waterPoints;

        private void Start()
        {
            InitializeButtons();
            InitializePopup();

            UserPointUpdate();
        }

        private void UserPointUpdate()
        {
            userCoins = DataManger.Instance.GetCoin();
            turretPoints = DataManger.Instance.GetTp();
            waterPoints = DataManger.Instance.GetWaterPoint();

            // Coin 텍스트 업데이트
            if (coinText != null)
            {
                coinText.text = "Coin : " + userCoins;
            }
            // TP 텍스트 업데이트
            if (turretPointsText != null)
            {
                turretPointsText.text = "Turret Points : " + turretPoints;
            }
            // WP 텍스트 업데이트
            if (waterPointsText != null)
            {
                waterPointsText.text = "Water Points : " + waterPoints;
            }
        }

        private void InitializeButtons()
        {
            // 플레이 버튼 이벤트 연결
            playButton.onClick.AddListener(OnPlayButtonClicked);
            // 셋팅 버튼 이벤트 연결
            settingButton.onClick.AddListener(OnSettingButtonClicked);

            // 팝업 내 버튼들 이벤트 연결
            gameStartButton.onClick.AddListener(OnGameStartButtonClicked);
            closeButton.onClick.AddListener(OnCloseButtonClicked);
            buyTpButton.onClick.AddListener(OnBuyTurretPointsClicked);
            buyWwButton.onClick.AddListener(OnBuyWaterPointsClicked);
            sellTpButton.onClick.AddListener(OnSellTurretPointsClicked);
            sellWwButton.onClick.AddListener(OnSellWaterPointsClicked);
        }

        private void InitializePopup()
        {
            // 팝업을 초기에는 비활성화하고 스케일을 0으로 설정
            if (buyPopUpPanel != null)
            {
                buyPopUpPanel.SetActive(false);
                buyPopUpPanel.transform.localScale = Vector3.zero;
            }
        }

        #region 버튼 이벤트 핸들러

        private void OnPlayButtonClicked()
        {
            ShowBuyPopup();
        }
        
        private void OnSettingButtonClicked()
        {
            ShowSettingPopup();
        }

        private void OnGameStartButtonClicked()
        {
            // 게임 시작 로직
            Debug.Log("게임을 시작합니다!");

            // 게임 씬으로 이동 (실제 게임 씬 이름으로 변경 필요)
            // SceneManager.LoadScene("YourGameSceneName");

            // 현재는 팝업을 닫기만 함
            HideBuyPopup();
        }

        private void OnCloseButtonClicked()
        {
            HideBuyPopup();
        }
        

        private void OnBuyTurretPointsClicked()
        {
            // 포탑 포인트 구매 로직
            if (CanAffordCost(turretPointsCost))
            {
                // turretPoints += 10;
                DataManger.Instance.SpendCoin(turretPointsCost);
                DataManger.Instance.AddTP(boughtTurretPoints);
                UserPointUpdate();

                Debug.Log($"포탑 포인트 구매 완료! 현재 포탑 포인트: {turretPoints}");
                // 구매 성공 애니메이션
                AnimateButtonPress(buyTpButton.gameObject);
            }
            else
            {
                Debug.Log("포탑 포인트를 구매할 수 없습니다. 코인이 부족합니다.");
                // 구매 실패 애니메이션
                AnimateButtonShake(buyTpButton.gameObject);
            }
        }

        private void OnBuyWaterPointsClicked()
        {
            // 워터 포인트 구매 로직
            
            if (CanAffordCost(waterPointsCost))
            {
                // waterPoints += 10;
                DataManger.Instance.SpendCoin(waterPointsCost);
                DataManger.Instance.AddWaterPoint(boughtWaterPoints);
                UserPointUpdate();

                Debug.Log($"워터 포인트 구매 완료! 현재 워터 포인트: {waterPoints}");
                
                // 구매 성공 애니메이션
                AnimateButtonPress(buyWwButton.gameObject);
            }
            else
            {
                Debug.Log("워터 포인트를 구매할 수 없습니다. 코인이 부족합니다.");
                // 구매 실패 애니메이션
                AnimateButtonShake(buyWwButton.gameObject);
            }
        }

        private void OnSellTurretPointsClicked()
        {
            if (CanAffordTP(boughtTurretPoints))
            {
                DataManger.Instance.SpendTP(boughtTurretPoints);
                DataManger.Instance.AddCoin((int)(turretPointsCost * salesRatio)); // 판매시 판매 비율 적용
                UserPointUpdate();

                Debug.Log($"포탑 포인트 판매 완료! 현재 포탑 포인트: {turretPoints}");
            }
            else
            {
                Debug.Log("포탑 포인트를 판매할 수 없습니다. 포탑 포인트가 부족합니다.");
            }
        }

        private void OnSellWaterPointsClicked()
        {
            if (CanAffordWP(boughtWaterPoints))
            {
                DataManger.Instance.SpendWaterPoint(boughtWaterPoints);
                DataManger.Instance.AddCoin((int)(waterPointsCost * salesRatio)); // 판매시 판매 비율 적용
                UserPointUpdate();

                Debug.Log($"워터 포인트 판매 완료! 현재 워터 포인트: {waterPoints}");
            }
            else
            {
                Debug.Log("워터 포인트를 판매할 수 없습니다. 워터 포인트가 부족합니다.");
            }
        }

        #endregion

        #region 팝업 애니메이션

        private void ShowBuyPopup()
        {
            if (buyPopUpPanel != null)
            {
                buyPopUpPanel.SetActive(true);

                // 팝업 등장 애니메이션 (스케일 + 바운스 효과)
                buyPopUpPanel.transform.localScale = Vector3.zero;
                buyPopUpPanel.transform.DOScale(Vector3.one, animationDuration)
                    .SetEase(Ease.OutBack);
            }
        }

        private void HideBuyPopup()
        {
            if (buyPopUpPanel != null)
            {
                // 팝업 사라지는 애니메이션
                buyPopUpPanel.transform.DOScale(Vector3.zero, animationDuration)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => buyPopUpPanel.SetActive(false));
            }
        }
        
        private void ShowSettingPopup()
        {
            if (settingPopUpPanel != null)
            {
                settingPopUpPanel.SetActive(true);

                // 팝업 등장 애니메이션 (스케일 + 바운스 효과)
                settingPopUpPanel.transform.localScale = Vector3.zero;
                settingPopUpPanel.transform.DOScale(Vector3.one, animationDuration)
                    .SetEase(Ease.OutBack);
            }
        }

        #endregion

        #region 버튼 애니메이션

        private void AnimateButtonPress(GameObject button)
        {
            // 버튼 눌림 효과 애니메이션
            button.transform.DOScale(scaleAmount, 0.1f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    button.transform.DOScale(1f, 0.1f)
                        .SetEase(Ease.InQuad);
                });
        }

        private void AnimateButtonShake(GameObject button)
        {
            // 버튼 흔들림 효과 (구매 실패시)
            button.transform.DOShakePosition(0.5f, strength: 10f, vibrato: 20, randomness: 90f, fadeOut: true);
        }

        #endregion

        #region 유틸리티 메서드

        private bool CanAffordCost(int cost)
        {
            // 실제 코인 시스템과 연동하여 구현
            // 현재는 예시로 cost에 따른 간단한 로직
            // int currentCoins = 1000; // 예시 코인 수량
            return userCoins >= cost;
        }

        private bool CanAffordTP(int point)
        {
            return turretPoints >= point;
        }

        private bool CanAffordWP(int point)
        {
            return waterPoints >= point;
        }

        private void OnDestroy()
        {
            // 메모리 누수 방지를 위한 DOTween 킬
            buyPopUpPanel?.transform.DOKill();
            playButton?.transform.DOKill();
            buyTpButton?.transform.DOKill();
            buyWwButton?.transform.DOKill();
        }

        #endregion
    }
}
