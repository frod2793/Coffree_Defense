using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TurretCombinationData", menuName = "ScriptableObjects/Turret Combination Data")]
public class TurretCombinationData : ScriptableObject
{
    public List<TurretCombinationRecipe> recipes;

    /// <summary>
    /// 주어진 터렛과 아이템에 맞는 조합 결과를 찾습니다.
    /// </summary>
    public TurretBase GetCombinationResult(TurretBase turret, ItemA item)
    {
        if (recipes == null || recipes.Count == 0)
        {
            Debug.LogError("레시피 목록이 비어 있습니다!");
            return null;
        }

        Debug.Log($"조합 검색 시작: 레시피 {recipes.Count}개 확인 중...");

        foreach (var recipe in recipes)
        {
            if (recipe == null)
            {
                Debug.LogWarning("레시피 목록에 null 항목이 있습니다!");
                continue;
            }

            Debug.Log($"레시피 확인: {recipe.name}");
            
            if (recipe.Matches(turret, item))
            {
                Debug.Log($"조합 성공! 결과: {recipe.resultTurretPrefab.name}");
                return recipe.resultTurretPrefab;
            }
        }
        
        Debug.Log("일치하는 레시피를 찾지 못했습니다.");
        return null; // 일치하는 레시피가 없음
    }

    // 인스펙터에서 유효성 검사
    private void OnValidate()
    {
        if (recipes == null || recipes.Count == 0)
        {
            Debug.LogError("레시피 목록이 비어 있습니다!");
        }
        else
        {
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i] == null)
                {
                    Debug.LogError($"레시피 목록의 인덱스 {i}에 null 항목이 있습니다!");
                }
            }
        }
    }
}
