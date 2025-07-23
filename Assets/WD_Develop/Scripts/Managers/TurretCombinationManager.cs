using UnityEngine;
using System.Collections.Generic;

public class TurretCombinationManager : MonoBehaviour
{
    public static TurretCombinationManager Instance { get; private set; }
    
    [SerializeField] private List<TurretCombinationRecipe> combinationRecipes;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public bool CanCombine(TurretBase turret, ItemA item)
    {
        if (turret == null || item == null) return false;
        
        foreach (var recipe in combinationRecipes)
        {
            if (recipe.Matches(turret, item))
            {
                return true;
            }
        }
        return false;
    }
    
    public TurretBase GetCombinationResult(TurretBase turret, ItemA item)
    {
        foreach (var recipe in combinationRecipes)
        {
            if (recipe.Matches(turret, item))
            {
                return recipe.resultTurretPrefab;
            }
        }
        return null;
    }
    
    // 아이템 드래그 시작 시 조합 가능한 터렛들의 아웃라인 활성화
    public void OnItemDragStart(ItemA draggedItem)
    {
        var allTurrets = FindObjectsByType<TurretBase>(FindObjectsSortMode.None);
        foreach (var turret in allTurrets)
        {
            if (CanCombine(turret, draggedItem))
            {
                turret.EnableDragOutline();
            }
        }
    }
    
    // 아이템 드래그 종료 시 모든 터렛의 아웃라인 비활성화
    public void OnItemDragEnd()
    {
        var allTurrets = FindObjectsByType<TurretBase>(FindObjectsSortMode.None);
        foreach (var turret in allTurrets)
        {
            turret.DisableOutline();
        }
    }
    
    // 터렛이 아이템과 조합될 때 호출
    public bool CombineTurretWithItem(TurretBase turret, ItemA item)
    {
        var resultPrefab = GetCombinationResult(turret, item);
        if (resultPrefab == null) return false;
        
        // 조합 시작
        turret.StartCombining();
        
        // 새로운 터렛 생성
        var newTurret = Instantiate(resultPrefab, turret.transform.position, turret.transform.rotation);
        
        // 기존 터렛 제거
        Destroy(turret.gameObject);
        
        Debug.Log($"터렛 조합 성공: {turret.name} + {item.name} = {newTurret.name}");
        return true;
    }
}
