using UnityEngine;
using UnityEngine.Audio;

public enum AudioMixerType{Master, BGM, SFX}
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;
    [SerializeField] private AudioMixer audioMixer;

    [SerializeField] private bool[] isMute = new bool[3];
    [SerializeField] private float[] soundVolume = new float[3];
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        SetSoundVolume(AudioMixerType.Master, 0.5f);
        SetSoundVolume(AudioMixerType.BGM, 0.5f);
        SetSoundVolume(AudioMixerType.SFX, 0.5f);
    }

    public void SetSoundVolume(AudioMixerType audioType, float volume)
    {
        audioMixer.SetFloat(audioType.ToString(), Mathf.Log10(volume) * 20);
    }

    public void SetAudioMute(AudioMixerType audioType)
    {
        int type = (int)audioType;
        if (!isMute[type])
        {
            isMute[type] = true;
            audioMixer.GetFloat(audioType.ToString(), out float currentVolume);
            soundVolume[type] = currentVolume;
            SetSoundVolume(audioType, 0.001f);
        }
        else
        {
            isMute[type] = false;
            SetSoundVolume(audioType, soundVolume[type]);
        }
    }

}
