using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InGameUIManager : MonoBehaviour
{
  
    // 포탑 조합에 대한 결과오브젝트는 scriptable object로 관리합니다.
    // 조합 식 클래스를 따로 생성하여 조합에 따라 포탑의 프리펙 을 변경 
    // 포탑의 기본 구동 방식은 TerretBase 클래스를 상속받아 구현합니다.
    // UI 의 재료 칸에서 포탑으로 드래그 앤 드랍시 포탑의 조합 식이 활성화 됩니다 
    // 재료칸에서 가져간 재료를 포탑이랑 겹쳐 놓을시 포탑이 아웃라인으로 표시 됩니다 
    // 재료를 포탑에 드랍시 조합식이 활성화 됩니다 .
    // 조합 식이 활성화 되면 포탑의 작동이 비활성화 되고 포탑의 머리위에 말풍선의 형태로 조합식이 작동 합니다.
    //예 ) 드래그한것 (타피오카) + (아직 드래드안한것 )???? == 포탑의 모양을 실루엣 처리 
    // 조합 식이 활성화 되면 조합 결과 오브젝트가 활성화 됩니다.
    // 2초후 포탑이 활성화 됩니다.
    //포탑 인터페이스를 통해 게임 메니져에 설치된 포탑의 정보를 전달합니다.
    
    
    // 포탑의 드래그앤 드랍이 활성화 되면 포탑이 비활성화 됩니다 (쿨타임 말풍선 형태의 ui 로 표시 )
    // 포탑의 위치가 고정되면 2초의 쿨타임후 포탑이 활성화 됩니다 
    
    //확인해야될 사항 유니티 에셋 에 관한 조항 
    
    
    
    /// <summary>
    /// 선택 ui 에서 드래그 하여 인게임 맵으로 마우스 포인트 이동시에 마우스 포인터 위치에 미리보기 아이템 프리펙을 표시합니다.
    /// </summary>
    
    private GameObject draggedItem; // 현재 드래그 중인 아이템의 프리팹 미리보기
    private GameObject selectedPrefab; // 현재 선택된 원본 프리팹

    [Header( "In-Game UI Manager")]
    [SerializeField] private GameObject inGameUI; // In-Game UI 오브젝트
    
    [SerializeField]
    GameManager gameManager;
    
    [SerializeField]
    List<Image> images; // UI에 표시할 이미지 리스트>
    private Vector3 offset;
    private Plane groundPlane;
    private float initialY = 0f; // 바닥의 Y 좌표, 필요에 따라 조정

    
    [SerializeField]
    List<GameObject> PrefabList; // UI에 표시할 프리팹 리스트
    
    void Start()
    {
        // 바닥 평면을 초기화합니다.
        groundPlane = new Plane(Vector3.up, new Vector3(0, initialY, 0));
        // 이벤트 트리거 에 이벤트 연결을 설정합니다.
        for (int i = 0; i < images.Count; i++)
        {
            Image image = images[i];
            EventTrigger trigger = image.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = image.gameObject.AddComponent<EventTrigger>();
            }

            // Begin Drag 이벤트 설정
            EventTrigger.Entry beginDragEntry = new EventTrigger.Entry();
            beginDragEntry.eventID = EventTriggerType.BeginDrag;
            int index = i; // 클로저 문제를 피하기 위해 인덱스 복사
            beginDragEntry.callback.AddListener((data) => { OnBeginDrag(index); });
            trigger.triggers.Add(beginDragEntry);

            // End Drag 이벤트 설정
            EventTrigger.Entry endDragEntry = new EventTrigger.Entry();
            endDragEntry.eventID = EventTriggerType.EndDrag;
            endDragEntry.callback.AddListener((data) => { OnEndDrag(); });
            trigger.triggers.Add(endDragEntry);
        }
    }

    void Update()
    {
        UpdateItemPreviewPosition();
    }

    /// <summary>
    /// UI 이미지에서 드래그를 시작할 때 호출됩니다. (EventTrigger에 연결 필요)
    /// </summary>
    /// <param name="prefabIndex">PrefabList에 있는 프리팹의 인덱스</param>
    public void OnBeginDrag(int prefabIndex)
    {
        if (prefabIndex >= 0 && prefabIndex < PrefabList.Count)
        {
            selectedPrefab = PrefabList[prefabIndex];
            if (selectedPrefab != null)
            {
                ShowItemPreview(selectedPrefab);
            }
        }
    }

    /// <summary>
    /// 드래그를 놓았을 때 호출됩니다. (EventTrigger에 연결 필요)
    /// </summary>
    public void OnEndDrag()
    {
        if (draggedItem != null && selectedPrefab != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            // "Ground" 또는 타워를 설치할 레이어로 변경해야 합니다.
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, LayerMask.GetMask("Ground")))
            {
                // 유효한 위치에 드롭했으므로 실제 타워를 생성합니다.
                Instantiate(selectedPrefab, hit.point, Quaternion.identity);
            }

            // 미리보기 오브젝트는 항상 파괴합니다.
            Destroy(draggedItem);
            draggedItem = null;
            selectedPrefab = null;
        }
    }

    /// <summary>
    /// 선택한 아이템의 프리팹 미리보기를 마우스 위치에 표시합니다.
    /// </summary>
    /// <param name="itemPrefab">표시할 아이템의 프리팹</param>
    public void ShowItemPreview(GameObject itemPrefab)
    {
        if (itemPrefab != null)
        {
            // 임시 위치에 생성하고 Update에서 위치를 즉시 보정합니다.
            draggedItem = Instantiate(itemPrefab, Vector3.zero, Quaternion.identity);
        }
    }

    /// <summary>
    /// 드래그 중인 아이템 미리보기의 위치를 마우스 포인터 위치로 업데이트합니다.
    /// </summary>
    private void UpdateItemPreviewPosition()
    {
        if (draggedItem != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            float distance;
            if (groundPlane.Raycast(ray, out distance))
            {
                Vector3 point = ray.GetPoint(distance);
                draggedItem.transform.position = new Vector3(point.x, initialY, point.z);
            }
        }
    }
}
