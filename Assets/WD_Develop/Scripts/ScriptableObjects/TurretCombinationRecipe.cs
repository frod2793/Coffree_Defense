using UnityEngine;
using System;

[CreateAssetMenu(fileName = "TurretCombinationRecipe", menuName = "ScriptableObjects/Turret Combination Recipe")]
public class TurretCombinationRecipe : ScriptableObject
{
    [Tooltip("조합의 기반이 될 터렛 프리팹")]
    public TurretBase baseTurretPrefab;

    [Tooltip("조합에 필요한 재료 아이템 프리팹")]
    public ItemA requiredItemPrefab;

    [Tooltip("조합 결과로 생성될 터렛 프리팹")]
    public TurretBase resultTurretPrefab;

    /// <summary>
    /// 주어진 터렛과 아이템이 이 레시피와 일치하는지 확인합니다.
    /// </summary>
    public bool Matches(TurretBase turret, ItemA item)
    {
        if (turret == null || item == null)
        {
            Debug.LogWarning("Matches: 터렛 또는 아이템이 null입니다.");
            return false;
        }

        if (baseTurretPrefab == null || requiredItemPrefab == null)
        {
            Debug.LogError($"레시피 '{name}'의 기본 터렛 또는 필요 아이템이 설정되지 않았습니다!");
            return false;
        }

        // GetType() 대신 is 연산자를 사용하여 상속 관계도 확인
        bool turretMatch = turret.GetType() == baseTurretPrefab.GetType() || 
                         turret.GetType().IsSubclassOf(baseTurretPrefab.GetType());
                         
        bool itemMatch = item.GetType() == requiredItemPrefab.GetType() || 
                       item.GetType().IsSubclassOf(requiredItemPrefab.GetType());

        // 디버깅 로그 추가
        Debug.Log($"조합 시도: 터렛({turret.GetType().Name}) + 아이템({item.GetType().Name})");
        Debug.Log($"레시피 필요: 터렛({baseTurretPrefab.GetType().Name}) + 아이템({requiredItemPrefab.GetType().Name})");
        Debug.Log($"매칭 결과: 터렛({turretMatch}) 아이템({itemMatch})");

        return turretMatch && itemMatch;
    }

    // 인스펙터에서 유효성 검사
    private void OnValidate()
    {
        if (baseTurretPrefab == null)
        {
            Debug.LogError($"레시피 '{name}'에 기본 터렛 프리팹이 설정되지 않았습니다!");
        }
        
        if (requiredItemPrefab == null)
        {
            Debug.LogError($"레시피 '{name}'에 필요 아이템 프리팹이 설정되지 않았습니다!");
        }
        
        if (resultTurretPrefab == null)
        {
            Debug.LogError($"레시피 '{name}'에 결과 터렛 프리팹이 설정되지 않았습니다!");
        }
    }
}
