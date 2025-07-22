using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TurretBase))]
public class TurretDropZone : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    private TurretBase turretBase;
    
    void Awake()
    {
        turretBase = GetComponent<TurretBase>();
    }
    
    public void OnDrop(PointerEventData eventData)
    {
        var draggedItem = eventData.pointerDrag?.GetComponent<ItemA>();
        if (draggedItem != null && turretBase != null)
        {
            // 조합 시도
            if (TurretCombinationManager.Instance != null)
            {
                TurretCombinationManager.Instance.CombineTurretWithItem(turretBase, draggedItem);
            }
        }
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        // 드래그 중인 아이템이 있고, 이 터렛과 조합 가능한 경우 아웃라인 강조
        if (eventData.pointerDrag != null)
        {
            var draggedItem = eventData.pointerDrag.GetComponent<ItemA>();
            if (draggedItem != null && turretBase.CanCombineWithItem(draggedItem))
            {
                turretBase.SetOutline(true, Color.white, 8f); // 하이라이트 아웃라인
            }
        }
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        // 마우스가 나가면 기본 드래그 아웃라인으로 복원
        if (eventData.pointerDrag != null)
        {
            var draggedItem = eventData.pointerDrag.GetComponent<ItemA>();
            if (draggedItem != null && turretBase.CanCombineWithItem(draggedItem))
            {
                turretBase.EnableDragOutline(); // 기본 드래그 아웃라인으로 복원
            }
        }
    }
}

