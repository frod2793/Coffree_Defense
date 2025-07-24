using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TurretLayerSetup : EditorWindow
{
    private static readonly string[] requiredLayers = new string[] { "Turret", "Ground", "Preview" };
    
    [MenuItem("도구/레이어 설정 검사")]
    public static void CheckLayers()
    {
        List<string> missingLayers = new List<string>();
        
        foreach (string layerName in requiredLayers)
        {
            if (LayerMask.NameToLayer(layerName) == -1)
            {
                missingLayers.Add(layerName);
            }
        }
        
        if (missingLayers.Count > 0)
        {
            EditorUtility.DisplayDialog("레이어 확인", 
                $"다음 레이어가 없습니다: {string.Join(", ", missingLayers)}\n\n" +
                "Edit > Project Settings > Tags and Layers에서 추가해주세요.", 
                "확인");
        }
        else
        {
            EditorUtility.DisplayDialog("레이어 확인", "모든 필수 레이어가 설정되어 있습니다.", "확인");
        }
    }
    
    [MenuItem("도구/모든 터렛에 레이어 설정")]
    public static void SetTurretLayers()
    {
        int turretLayer = LayerMask.NameToLayer("Turret");
        if (turretLayer == -1)
        {
            EditorUtility.DisplayDialog("오류", "'Turret' 레이어가 존재하지 않습니다! 먼저 레이어를 추가해주세요.", "확인");
            return;
        }
        
        TurretBase[] turrets = FindObjectsByType<TurretBase>(FindObjectsSortMode.None);
        int count = 0;
        
        foreach (TurretBase turret in turrets)
        {
            if (turret.gameObject.layer != turretLayer)
            {
                Undo.RecordObject(turret.gameObject, "Set Turret Layer");
                turret.gameObject.layer = turretLayer;
                count++;
                
                // 자식 오브젝트도 같은 레이어로 설정
                foreach (Transform child in turret.transform)
                {
                    Undo.RecordObject(child.gameObject, "Set Turret Child Layer");
                    child.gameObject.layer = turretLayer;
                }
            }
        }
        
        EditorUtility.DisplayDialog("완료", $"{count}개의 터렛 레이어를 설정했습니다.", "확인");
    }
}

