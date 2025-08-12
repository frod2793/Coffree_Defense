using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using System;
using UnityEngine.InputSystem;

public enum AudioMixerType { Master, BGM, SFX }

[Serializable]
public struct SoundSources
{
    public AudioMixerType audioType;
    public string audioName;
    public AudioClip soundSource;
}

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;
    [SerializeField] private AudioMixer audioMixer;

    [SerializeField] private bool[] isMute = new bool[3];
    [SerializeField] private float[] soundVolume = new float[3];
    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;
    [SerializeField] private AudioSource sfx2AudioSource;

    [SerializeField] private List<SoundSources> soundSources = new List<SoundSources>();

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

    public void PlaySound(AudioMixerType audioType, string audioName, bool loop = false, int index = 1)
    {
        SoundSources soundSource = soundSources.Find(a => a.audioType == audioType && a.audioName == audioName);
        if (soundSource.soundSource != null)
        {
            AudioSource source = audioType == AudioMixerType.SFX ? index == 2 ? sfx2AudioSource : sfxAudioSource : bgmAudioSource;
            source.clip = soundSource.soundSource;
            source.loop = loop;
            source.Play();
        }
        else
        {
            Debug.LogWarning($"Audio source not found for {audioType} with name {audioName}");
        }
    }

}
