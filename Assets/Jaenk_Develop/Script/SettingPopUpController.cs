using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.EventSystems;
public class SettingPopUpController : MonoBehaviour
{
    public Slider masterSlider;
    public Slider bgmSlider;
    public Slider sfxSlider;

    [SerializeField]
    private Button settingCloseButton; // 셋팅 창에서 닫기 버튼

    [SerializeField]
    private GameObject settingPopUpPanel; // 셋팅 창

    [SerializeField] private float animationDuration = 0.5f;

    void Start()
    {
        InitializeButtons();
        InitializeSliders();
    }

    private void InitializeButtons()
    {
        settingCloseButton.onClick.AddListener(OnSettingCloseButtonClicked);
    }

    private void InitializeSliders()
    {
        masterSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        bgmSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
    }

    private void OnSettingCloseButtonClicked()
    {
        UIButtonSoundPlay();
        HideSettingPopup();
    }

    private void OnSliderHandleClicked() {
        UIButtonSoundPlay();
    }

    private void HideSettingPopup()
    {
        if (settingPopUpPanel != null)
        {
            // 팝업 사라지는 애니메이션
            settingPopUpPanel.transform.DOScale(Vector3.zero, animationDuration)
                .SetEase(Ease.InBack)
                .OnComplete(() => settingPopUpPanel.SetActive(false));
        }
    }

    void OnMasterVolumeChanged(float value)
    {
        SoundManager.Instance.SetSoundVolume(AudioMixerType.Master, value);
    }
    void OnBgmVolumeChanged(float value)
    {
        SoundManager.Instance.SetSoundVolume(AudioMixerType.BGM, value);
    }

    void OnSfxVolumeChanged(float value)
    {
        SoundManager.Instance.SetSoundVolume(AudioMixerType.SFX, value);
    }
    
    void UIButtonSoundPlay()
    {
        SoundManager.Instance.PlaySound(AudioMixerType.SFX, "UIButton");
    }
}
