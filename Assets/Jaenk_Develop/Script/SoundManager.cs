using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using System;
using System.Collections;
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

    /// <summary>
    /// 사운드를 재생합니다. BGM의 경우 페이드 효과와 함께 전환됩니다.
    /// </summary>
    /// <param name="audioType">오디오 타입 (BGM 또는 SFX)</param>
    /// <param name="audioName">재생할 오디오의 이름</param>
    /// <param name="loop">루프 여부</param>
    /// <param name="index">SFX를 재생할 AudioSource 인덱스 (1 또는 2)</param>
    /// <param name="fadeDuration">BGM 전환 시 페이드 시간</param>
    public void PlaySound(AudioMixerType audioType, string audioName, bool loop = false, int index = 1, float fadeDuration = 1.0f)
    {
        SoundSources soundSource = soundSources.Find(a => a.audioType == audioType && a.audioName == audioName);
        if (soundSource.soundSource == null)
        {
            Debug.LogWarning($"Audio source not found for {audioType} with name {audioName}");
            return;
        }

        if (audioType == AudioMixerType.BGM)
        {
            StartCoroutine(ChangeBGMCoroutine(soundSource.soundSource, fadeDuration));
        }
        else
        {
            AudioSource source = index == 2 ? sfx2AudioSource : sfxAudioSource;
            if (loop)
            {
                source.clip = soundSource.soundSource;
                source.loop = true;
                source.Play();
            }
            else
            {
                source.PlayOneShot(soundSource.soundSource);
            }
        }
    }

    // 사운드가 자연스럽게 변경되는 로직 추가
    private IEnumerator ChangeBGMCoroutine(AudioClip newClip, float fadeDuration)
    {
        if (bgmAudioSource.clip == newClip && bgmAudioSource.isPlaying) yield break;

        float startVolume = bgmAudioSource.volume;
        float timer = 0f;

        if (bgmAudioSource.isPlaying)
        {
            while (timer < fadeDuration / 2)
            {
                bgmAudioSource.volume = Mathf.Lerp(startVolume, 0, timer / (fadeDuration / 2));
                timer += Time.deltaTime;
                yield return null;
            }
        }

        bgmAudioSource.clip = newClip;
        bgmAudioSource.loop = true;
        bgmAudioSource.Play();

        timer = 0f;
        while (timer < fadeDuration / 2)
        {
            bgmAudioSource.volume = Mathf.Lerp(0, startVolume, timer / (fadeDuration / 2));
            timer += Time.deltaTime;
            yield return null;
        }
        bgmAudioSource.volume = startVolume;
    }
}
