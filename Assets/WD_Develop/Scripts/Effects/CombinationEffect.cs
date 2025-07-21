using UnityEngine;

/// <summary>
/// 터렛에 부착하여 조합 관련 시각적 효과를 처리하는 컴포넌트입니다.
/// </summary>
public class CombinationEffect : MonoBehaviour
{
    [SerializeField] private GameObject combiningEffectObject;
    [SerializeField] private GameObject successEffectPrefab;
    [SerializeField] private GameObject failEffectPrefab;
    
    private ParticleSystem combiningParticle;
    
    private void Awake()
    {
        if (combiningEffectObject != null)
        {
            combiningParticle = combiningEffectObject.GetComponent<ParticleSystem>();
            combiningEffectObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// 조합 중 효과를 표시합니다.
    /// </summary>
    public void ShowCombiningEffect()
    {
        if (combiningEffectObject != null)
        {
            combiningEffectObject.SetActive(true);
            if (combiningParticle != null)
            {
                combiningParticle.Play();
            }
        }
    }
    
    /// <summary>
    /// 조합 중 효과를 숨깁니다.
    /// </summary>
    public void HideCombiningEffect()
    {
        if (combiningEffectObject != null)
        {
            combiningEffectObject.SetActive(false);
            if (combiningParticle != null)
            {
                combiningParticle.Stop();
            }
        }
    }
    
    /// <summary>
    /// 조합 성공 효과를 재생합니다.
    /// </summary>
    public void PlaySuccessEffect()
    {
        if (successEffectPrefab != null)
        {
            GameObject effect = Instantiate(successEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 2f); // 2초 후 효과 제거
        }
    }
    
    /// <summary>
    /// 조합 실패 효과를 재생합니다.
    /// </summary>
    public void PlayFailEffect()
    {
        if (failEffectPrefab != null)
        {
            GameObject effect = Instantiate(failEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 1.5f); // 1.5초 후 효과 제거
        }
    }
}

