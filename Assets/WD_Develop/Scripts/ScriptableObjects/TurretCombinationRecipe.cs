using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "TurretCombinationRecipe", menuName = "ScriptableObjects/Turret Combination Recipe")]
public class TurretCombinationRecipe : ScriptableObject
{
    // 단일 조합법을 정의하는 내부 클래스
    [System.Serializable]
    public class Recipe
    {
        [Tooltip("조합의 기반이 될 터렛 프리팹")]
        public TurretBase baseTurretPrefab;

        [Tooltip("조합에 필요한 재료 아이템 프리팹")]
        public caffeMaterial requiredItemPrefab;

        [Tooltip("조합 결과로 생성될 터렛 프리팹")]
        public TurretBase resultTurretPrefab;
    }

    [Tooltip("모든 터렛 조합법 목록")]
    public List<Recipe> recipes;

    /// <summary>
    /// 주어진 터렛과 아이템에 맞는 조합 결과를 찾습니다.
    /// </summary>
    public TurretBase GetCombinationResult(TurretBase turret, caffeMaterial item)
    {
        if (recipes == null || recipes.Count == 0)
        {
            return null;
        }

        foreach (var recipe in recipes)
        {
            if (recipe == null || recipe.baseTurretPrefab == null || recipe.requiredItemPrefab == null)
            {
                continue;
            }

            // GetType()을 사용하여 정확한 프리팹 타입이 일치하는지 확인
            bool turretMatch = turret.GetType() == recipe.baseTurretPrefab.GetType();
            bool itemMatch = item.GetType() == recipe.requiredItemPrefab.GetType();

            if (turretMatch && itemMatch)
            {
                return recipe.resultTurretPrefab;
            }
        }
        
        return null; // 일치하는 레시피가 없음
    }
}
