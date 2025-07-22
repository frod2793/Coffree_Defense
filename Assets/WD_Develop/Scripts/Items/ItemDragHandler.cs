using UnityEngine;
using UnityEngine.EventSystems;

public class ItemDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private ItemA itemComponent;
    private Canvas canvas;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    
    void Awake()
    {
        itemComponent = GetComponent<ItemA>();
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        canvas = GetComponentInParent<Canvas>();
    }
    
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (itemComponent == null) return;
        
        // 드래그 시작 시 투명도 조절
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
        
        // 조합 가능한 터렛들의 아웃라인 활성화
        if (TurretCombinationManager.Instance != null)
        {
            TurretCombinationManager.Instance.OnItemDragStart(itemComponent);
        }
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;
        
        // 아이템을 마우스 위치로 이동
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }
    
    public void OnEndDrag(PointerEventData eventData)
    {
        // 드래그 종료 시 원래 상태로 복원
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        
        // 모든 터렛의 아웃라인 비활성화
        if (TurretCombinationManager.Instance != null)
        {
            TurretCombinationManager.Instance.OnItemDragEnd();
        }
        
        // 터렛과의 충돌 검사
        CheckTurretCollision(eventData);
    }
    
    private void CheckTurretCollision(PointerEventData eventData)
    {
        // 레이캐스트로 터렛과의 충돌 검사
        if (eventData.pointerCurrentRaycast.gameObject != null)
        {
            var turret = eventData.pointerCurrentRaycast.gameObject.GetComponent<TurretBase>();
            if (turret != null && itemComponent != null)
            {
                // 조합 시도
                if (TurretCombinationManager.Instance != null)
                {
                    bool success = TurretCombinationManager.Instance.CombineTurretWithItem(turret, itemComponent);
                    if (success)
                    {
                        // 조합 성공 시 아이템 제거
                        Destroy(gameObject);
                    }
                }
            }
        }
    }
}

