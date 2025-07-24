using System;
using EasyTransition;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static event Action<string> OnSceneLoadStarted;
    
    [SerializeField]
    private TransitionSettings transitionSettings;
    [SerializeField]
    private float startDelay = 0.5f;
    
    [Header("대체 설정")]
    [SerializeField]
    private bool useTransitionIfAvailable = true;
    [SerializeField]
    private bool showTransitionWarnings = true;

    public static SceneLoader Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Inspector에서 TransitionSettings가 할당되지 않은 경우 경고 표시
            if (transitionSettings == null && showTransitionWarnings)
            {
                Debug.LogWarning("[SceneLoader] TransitionSettings가 할당되지 않았습니다. " +
                                "Inspector에서 할당하거나 일반 씬 전환을 사용합니다.");
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadScene(string sceneName)
    {
        // TransitionSettings가 할당되어 있고 TransitionManager가 존재하는 경우 전환 효과 사용
        if (transitionSettings != null && useTransitionIfAvailable && TransitionManager.Instance() != null)
        {
            Debug.Log($"[SceneLoader] 전환 효과와 함께 씬 로드: {sceneName}");
            OnSceneLoadStarted?.Invoke(sceneName);
            TransitionManager.Instance().Transition(sceneName, transitionSettings, startDelay);
        }
        else
        {
            // TransitionSettings가 없거나 TransitionManager가 없는 경우 일반 씬 로드 사용
            if (transitionSettings == null && showTransitionWarnings)
            {
                Debug.LogWarning($"[SceneLoader] TransitionSettings가 없어 일반 씬 전환을 사용합니다: {sceneName}");
            }
            
            Debug.Log($"[SceneLoader] 일반 씬 로드: {sceneName}");
            OnSceneLoadStarted?.Invoke(sceneName);
            
            // 딜레이 후 씬 로드
            if (startDelay > 0)
            {
                StartCoroutine(LoadSceneWithDelay(sceneName, startDelay));
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }
    }
    
    /// <summary>
    /// 지연 시간 후 씬을 로드하는 코루틴
    /// </summary>
    private System.Collections.IEnumerator LoadSceneWithDelay(string sceneName, float delay)
    {
        yield return new UnityEngine.WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }
    
    /// <summary>
    /// TransitionSettings를 런타임에서 설정하는 메서드
    /// </summary>
    public void SetTransitionSettings(TransitionSettings settings)
    {
        transitionSettings = settings;
        Debug.Log("[SceneLoader] TransitionSettings가 런타임에서 설정되었습니다.");
    }
    
    /// <summary>
    /// 현재 TransitionSettings가 할당되어 있는지 확인
    /// </summary>
    public bool HasTransitionSettings()
    {
        return transitionSettings != null;
    }
}