using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;

namespace WD_Develop.Scripts.Managers
{
    public class LobbyUiManager : MonoBehaviour
    {
        [SerializeField]
        private List<Button> stageButtons;// 스테이지버튼 
        [SerializeField] private List<int> buttonValues; // 버튼에 스테이지 값 할당

        [SerializeField]
        Button settingButton;// 셋팅버튼 

        [SerializeField]
        Button nextButton;// 셋팅버튼 

        [SerializeField]
        Button prevButton;// 셋팅버튼 

        [Header("아이템 구매 창")]
        [SerializeField]
        private GameObject buyPopUpPanel; // 플레이버튼 클릭시 나올 플레이전 아이템 구매 창

        [SerializeField]
        private GameObject settingPopUpPanel; // 셋팅 창
        [SerializeField]
        private GameObject stageScrollView; // 스테이지 버튼 스크롤 뷰

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
        private int highestClearedStage;

        private void Start()
        {

            UserPointUpdate();

            InitializeButtons();
            InitializePopup();

            SoundManager.Instance.PlaySound(AudioMixerType.BGM, "LobbyBgm", true); // 로비 BGM 재생
        }

        private void UserPointUpdate()
        {
            userCoins = DataManger.Instance.GetCoin();
            turretPoints = DataManger.Instance.GetTp();
            waterPoints = DataManger.Instance.GetWaterPoint();
            highestClearedStage = DataManger.Instance.GetHighestClearedStage();

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
            // 스테이지 버튼 이벤트 연결
            for (int i = 0; i < stageButtons.Count; i++)
            {
                // 최고 클리어 스테이지를 기준으로 버튼 활성화 (i가 0부터 시작하므로, i <= highestClearedStage)
                if (i <= highestClearedStage)
                {
                    // 버튼 활성화 + 클릭 이벤트 등록
                    stageButtons[i].gameObject.SetActive(true);
                    int value = buttonValues[i];
                    stageButtons[i].onClick.AddListener(() => OnStageButtonClicked(value));
                }
                else
                {
                    // 버튼 비활성화
                    stageButtons[i].gameObject.SetActive(false);
                }
            }

            if (highestClearedStage >= 8)
            {
                nextButton.gameObject.SetActive(true);
                prevButton.gameObject.SetActive(true);
            }
            else
            {
                nextButton.gameObject.SetActive(false);
                prevButton.gameObject.SetActive(false);
            }

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

        private void OnStageButtonClicked(int value)
        {
            UIButtonSoundPlay();
            ShowBuyPopup();
            DataManger.Instance.SetSelectStage(value);
        }

        private void OnSettingButtonClicked()
        {
            UIButtonSoundPlay();
            ShowSettingPopup();
        }

        private void OnGameStartButtonClicked()
        {
            UIButtonSoundPlay();
            // 게임 시작 로직
            Debug.Log("게임을 시작합니다!");

            // 게임 씬으로 이동 (실제 게임 씬 이름으로 변경 필요)
            SceneLoader.Instance.LoadScene("InGameTest");

            // 현재는 팝업을 닫기만 함
            HideBuyPopup();
        }

        private void OnCloseButtonClicked()
        {
            UIButtonSoundPlay();
            HideBuyPopup();
        }


        private void OnBuyTurretPointsClicked()
        {
            // 포탑 포인트 구매 로직
            if (CanAffordCost(turretPointsCost))
            {
                StoreButtonSoundPlay();
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
                StoreButtonSoundPlay();
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
                StoreButtonSoundPlay();
                DataManger.Instance.SpendTP(boughtTurretPoints);
                DataManger.Instance.AddCoin((int)(turretPointsCost * salesRatio)); // 판매시 판매 비율 적용
                UserPointUpdate();

                Debug.Log($"포탑 포인트 판매 완료! 현재 포탑 포인트: {turretPoints}");
                AnimateButtonPress(sellTpButton.gameObject);
            }
            else
            {
                Debug.Log("포탑 포인트를 판매할 수 없습니다. 포탑 포인트가 부족합니다.");
                AnimateButtonShake(sellTpButton.gameObject);
            }
        }

        private void OnSellWaterPointsClicked()
        {
            if (CanAffordWP(boughtWaterPoints))
            {
                StoreButtonSoundPlay();
                DataManger.Instance.SpendWaterPoint(boughtWaterPoints);
                DataManger.Instance.AddCoin((int)(waterPointsCost * salesRatio)); // 판매시 판매 비율 적용
                UserPointUpdate();

                Debug.Log($"워터 포인트 판매 완료! 현재 워터 포인트: {waterPoints}");
                AnimateButtonPress(sellWwButton.gameObject);
            }
            else
            {
                Debug.Log("워터 포인트를 판매할 수 없습니다. 워터 포인트가 부족합니다.");
                AnimateButtonShake(sellWwButton.gameObject);
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

            if (stageScrollView != null)
            {
                stageScrollView.SetActive(false);
            }
            if(nextButton != null)
            {
                nextButton.gameObject.SetActive(false);
            }
            if (prevButton != null)
            {
                prevButton.gameObject.SetActive(false);
            }
        }

        private void HideBuyPopup()
        {
            if (buyPopUpPanel != null)
            {
                // 팝업 사라지는 애니메이션
                buyPopUpPanel.transform.DOScale(Vector3.zero, animationDuration)
                    .SetEase(Ease.InBack)
                    .OnComplete(() =>
                    {
                        buyPopUpPanel.SetActive(false);
                        if (stageScrollView != null)
                        {
                            stageScrollView.SetActive(true);

                            stageScrollView.transform.localScale = Vector3.zero;
                            stageScrollView.transform.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack);
                        }

                        if (nextButton != null && highestClearedStage >= 8)
                        {
                            nextButton.gameObject.SetActive(true);
                            nextButton.transform.localScale = Vector3.zero;
                            nextButton.transform.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack);
                        }

                        if (prevButton != null && highestClearedStage >= 8)
                        {
                            prevButton.gameObject.SetActive(true);
                            prevButton.transform.localScale = Vector3.zero;
                            prevButton.transform.DOScale(Vector3.one, animationDuration).SetEase(Ease.OutBack);
                        }
                    });
            }
            
        }

        private void ShowSettingPopup()
        {
            Debug.Log("Setting button clicked");
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
            if (buyPopUpPanel != null) buyPopUpPanel.transform.DOKill();
            // if (playButton != null) playButton.transform.DOKill();
            if (buyTpButton != null) buyTpButton.transform.DOKill();
            if (buyWwButton != null) buyWwButton.transform.DOKill();
        }

        #endregion

        #region 사운드용

        private void UIButtonSoundPlay()
        {
            SoundManager.Instance.PlaySound(AudioMixerType.SFX, "UIButton");
        }

        private void StoreButtonSoundPlay()
        {
            SoundManager.Instance.PlaySound(AudioMixerType.SFX, "StoreButton");
        }

        #endregion
    }
}
