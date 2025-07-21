using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InGameUIManager : MonoBehaviour
{
    // 포탑 조합에 대한 결과오브젝트는 scriptable object로 관리합니다.
    // 조합 식 클래스를 따로 생성하여 조합에 따라 포탑의 프리펙 을 변경 
    // 포탑의 기본 구동 방식은 TurretBase 클래스를 상속받아 구현합니다.
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
    
    private bool isDraggingTurret = false; // TerretControl에서 터렛을 드래그 중인지 여부
    private TerretControl terretControl; // TerretControl 참조

    [Header( "In-Game UI Manager")]
    [SerializeField] private GameObject inGameUI; // In-Game UI 오브젝트
    
    [SerializeField]
    GameManager gameManager;
    
    [SerializeField]
    List<Image> images; // UI에 표시할 이미지 리스트
    
    [SerializeField]
    List<GameObject> PrefabList; // UI에 표시할 프리팹 리스트
    
    void Start()
    {
        // TerretControl 찾기
        terretControl = FindObjectOfType<TerretControl>();
        if (terretControl == null)
        {
            Debug.LogError("TerretControl을 찾을 수 없습니다.");
        }
        
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
        
        // 레이어 설정 체크
        CheckLayerSetup();
    }

    // 필요한 레이어가 존재하는지 확인
    private void CheckLayerSetup()
    {
        int turretLayer = LayerMask.NameToLayer("Turret");
        if (turretLayer == -1)
        {
            Debug.LogError("'Turret' 레이어가 존재하지 않습니다! 프로젝트 설정에서 레이어를 추가해주세요.");
        }
        
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer == -1)
        {
            Debug.LogError("'Ground' 레이어가 존재하지 않습니다! 프로젝트 설정에서 레이어를 추가해주세요.");
        }
        
        int previewLayer = LayerMask.NameToLayer("Preview");
        if (previewLayer == -1)
        {
            Debug.LogWarning("'Preview' 레이어가 존재하지 않습니다. 기존 레이어를 사용합니다.");
        }
    }

    /// <summary>
    /// TerretControl에서 터렛 드래그 상태를 설정합니다.
    /// </summary>
    public void SetDraggingTurret(bool isDragging)
    {
        isDraggingTurret = isDragging;
    }

    /// <summary>
    /// UI 이미지에서 드래그를 시작할 때 호출됩니다. (EventTrigger에 연결 필요)
    /// </summary>
    /// <param name="prefabIndex">PrefabList에 있는 프리팹의 인덱스</param>
    public void OnBeginDrag(int prefabIndex)
    {
        // 터렛 드래그 중일 때는 아이템 드래그 허용하지 않음
        if (isDraggingTurret || terretControl == null) return;
        
        if (prefabIndex >= 0 && prefabIndex < PrefabList.Count)
        {
            GameObject selectedPrefab = PrefabList[prefabIndex];
            if (selectedPrefab != null)
            {
                // TerretControl에 아이템 드래그 시작 알림
                terretControl.StartItemDrag(selectedPrefab);
            }
        }
    }

    /// <summary>
    /// 드래그를 놓았을 때 호출됩니다. (EventTrigger에 연결 필요)
    /// </summary>
    public void OnEndDrag()
    {
        // TerretControl에 아이템 드래그 종료 알림
        if (terretControl != null)
        {
            terretControl.EndItemDrag();
        }
    }
}
