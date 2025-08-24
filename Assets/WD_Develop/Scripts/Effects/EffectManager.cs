using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using WD_Develop.Scripts; // EnemyAdvanced 클래스가 있는 네임스페이스

// EffectType과 프리팹, 지속시간을 인스펙터에서 매핑하기 위한 구조체
[Serializable]
public struct EffectMapping
{
    public EffectType type;
    public GameObject prefab;
    public float duration; // 0 또는 음수이면 지속 효과(Looping)
}

/// <summary>
/// 모든 종류의 이펙트를 관리하는 범용 이펙트 매니저입니다.
/// 일회성 이펙트와 지속 이펙트를 모두 지원하며, 오브젝트 풀링을 사용합니다.
/// </summary>
public class EffectManager : MonoBehaviour
{
    #region Singleton
    public static EffectManager Instance { get; private set; }
    #endregion

    [Header("이펙트 목록")]
    [SerializeField] private List<EffectMapping> effectMappings;

    // 빠른 조회를 위한 딕셔너리
    private Dictionary<EffectType, EffectMapping> effectDictionary = new Dictionary<EffectType, EffectMapping>();
    
    // --- 풀링 시스템 ---
    private Dictionary<GameObject, Queue<GameObject>> effectPool = new Dictionary<GameObject, Queue<GameObject>>();
    
    // --- 지속 효과 관리 ---
    // 소유자(owner)를 키로, (활성 인스턴스, 원본 프리팹)을 값으로 저장
    private Dictionary<object, (GameObject instance, GameObject prefab)> activeLoopingEffects = new Dictionary<object, (GameObject, GameObject)>();

    private void Awake()
    {
        // Singleton 인스턴스 설정
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 인스펙터에서 설정한 리스트를 딕셔너리로 변환하여 빠른 조회 가능하도록 함
        foreach (var mapping in effectMappings)
        {
            if (!effectDictionary.ContainsKey(mapping.type))
            {
                effectDictionary.Add(mapping.type, mapping);
            }
        }
    }

    #region Public API

    /// <summary>
    /// 지정된 타입의 일회성 이펙트를 특정 위치에 재생합니다.
    /// </summary>
    public void PlayEffect(EffectType effectType, Vector3 position)
    {
        if (effectDictionary.TryGetValue(effectType, out EffectMapping mapping))
        {
            if (mapping.prefab == null)
            {
                Debug.LogWarning($"EffectType '{effectType}'에 대한 Prefab이 할당되지 않았습니다.");
                return;
            }
            
            // 지속 효과는 이 메서드로 재생할 수 없음
            if (mapping.duration <= 0)
            {
                Debug.LogWarning($"EffectType '{effectType}'은(는) 지속 효과입니다. PlayLoopingEffect를 사용하세요.");
                return;
            }

            PlayOneShotEffectInternal(mapping.prefab, position, mapping.duration);
        }
        else
        { 
            Debug.LogWarning($"EffectType '{effectType}'에 해당하는 이펙트 매핑을 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 지정된 타입의 지속 이펙트를 특정 오브젝트(owner)에 대해 재생합니다.
    /// 이펙트는 부모(parent)에 부착됩니다.
    /// </summary>
    public void PlayLoopingEffect(EffectType effectType, Transform parent, object owner)
    {
        if (owner == null) {
            Debug.LogError("지속 이펙트의 소유자(owner)는 null일 수 없습니다.");
            return;
        }

        if (activeLoopingEffects.ContainsKey(owner)) return; // 중복 재생 방지

        if (effectDictionary.TryGetValue(effectType, out EffectMapping mapping))
        {
            if (mapping.prefab == null) {
                Debug.LogWarning($"EffectType '{effectType}'에 대한 Prefab이 할당되지 않았습니다.");
                return;
            }
            
            // 일회성 이펙트는 이 메서드로 재생할 수 없음
            if (mapping.duration > 0)
            {
                Debug.LogWarning($"EffectType '{effectType}'은(는) 일회성 효과입니다. PlayEffect를 사용하세요.");
                return;
            }

            GameObject effectInstance = GetFromPool(mapping.prefab);
            effectInstance.transform.SetParent(parent, false); // 월드 포지션 유지 안함
            effectInstance.transform.localPosition = Vector3.zero;
            effectInstance.transform.localRotation = Quaternion.identity;
            
            activeLoopingEffects[owner] = (effectInstance, mapping.prefab);
        }
        else
        {
            Debug.LogWarning($"EffectType '{effectType}'에 해당하는 이펙트 매핑을 찾을 수 없습니다.");
        }
    }

    /// <summary>
    /// 특정 오브젝트(owner)에 대해 재생 중인 지속 이펙트를 중지합니다.
    /// </summary>
    public void StopLoopingEffect(object owner)
    {
        if (owner == null) return;

        if (activeLoopingEffects.TryGetValue(owner, out var effectData))
        {
            ReturnToPool(effectData.instance, effectData.prefab);
            activeLoopingEffects.Remove(owner);
        }
    }

    /// <summary>
    /// 특정 적에게 적용된 둔화 효과(시각적, 기능적)를 중지합니다.
    /// </summary>
    public void StopSlowEffect(EnemyAdvanced enemy)
    {
        if (enemy == null) return;

        // 시각적 이펙트 중지
        StopLoopingEffect(enemy);

        // 기능적 효과(속도 저하) 제거
        enemy.RemoveSlowEffect();
    }

    #endregion

    #region 오브젝트 풀링 로직 (내부)

    private void PlayOneShotEffectInternal(GameObject effectPrefab, Vector3 position, float duration)
    {
        GameObject effectInstance = GetFromPool(effectPrefab);
        effectInstance.transform.position = position;
        effectInstance.transform.rotation = Quaternion.identity;
        
        StartCoroutine(ReturnToPoolAfterDelay(effectInstance, effectPrefab, duration));
    }

    private GameObject GetFromPool(GameObject prefab)
    {
        if (effectPool.TryGetValue(prefab, out Queue<GameObject> pool) && pool.Count > 0)
        {
            GameObject instance = pool.Dequeue();
            instance.SetActive(true);
            return instance;
        }
        return Instantiate(prefab, transform);
    }

    private void ReturnToPool(GameObject instance, GameObject prefab)
    {
        if (!effectPool.TryGetValue(prefab, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            effectPool[prefab] = pool;
        }
        instance.transform.SetParent(transform); 
        instance.SetActive(false);
        pool.Enqueue(instance);
    }

    private IEnumerator ReturnToPoolAfterDelay(GameObject instance, GameObject prefab, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnToPool(instance, prefab);
    }

    #endregion

    private void OnDestroy()
    {
        ClearPools();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #region 풀 관리

    public void ClearPools()
    {
        foreach (var pool in effectPool.Values)
        {
            foreach (var item in pool)
            {
                if(item != null) Destroy(item);
            }
            pool.Clear();
        }
        effectPool.Clear();
        
        foreach(var item in activeLoopingEffects.Values)
        {
            if(item.instance != null) Destroy(item.instance);
        }
        activeLoopingEffects.Clear();
    }

    #endregion
}