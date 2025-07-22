using UnityEngine;

//역할:
// 터렛들을 드래그앤 드랍하고 제어하는 터랫 컨트롤 매니져 스크립트 매니져 오브젝트에 배치 되어있을예정
// 이 스크립트는 터렛 오브젝트를 드래그하여 배치하고 위치를 변경하는 역할을 합니다.
// 터렛의 드래그는 오직 위치 이동에만 사용됩니다.
// 조합 로직도 이 스크립트에서 처리합니다.

public class TerretControl : MonoBehaviour
{
    private Transform selectedTurret;
    private TurretBase selectedTurretBase;
    private Vector3 offset;
    private Plane groundPlane;
    private float initialY;
    private InGameUIManager uiManager;
    
    // 터렛이 이동 가능한 상태인지 확인하는 변수
    private bool canMoveTurret = true;
    
    // 바닥 평면이 초기화되었는지 여부를 추적하는 변수
    private bool isGroundPlaneInitialized = false;
    
    [Header("Combination")]
    [SerializeField] private TurretCombinationData combinationData; // 조합법 데이터
    private TurretBase highlightedTurret; // 현재 하이라이트된 터렛
    private GameObject draggedItem; // 현재 드래그 중인 아이템의 프리팹 미리보기
    private GameObject selectedItemPrefab; // 현재 선택된 원본 아이템 프리팹
    private bool isDraggingItem = false; // 아이템을 드래그 중인지 여부

    void Start()
    {
        // 초기 바닥 평면 설정
        initialY = 0f; // 기본 바닥 높이
        InitializeGroundPlane();
        
        uiManager = FindObjectOfType<InGameUIManager>();
        if (uiManager == null)
        {
            Debug.LogError("InGameUIManager를 찾을 수 없습니다.");
        }
        
        // 조합 데이터 체크
        CheckCombinationData();
    }
    
    // 바닥 평면 초기화 메서드
    private void InitializeGroundPlane()
    {
        groundPlane = new Plane(Vector3.up, new Vector3(0, initialY, 0));
        isGroundPlaneInitialized = true;
    }
    
    // 조합 데이터 유효성 검사
    private void CheckCombinationData()
    {
        if (combinationData == null)
        {
            Debug.LogError("조합 데이터가 설정되지 않았습니다! Inspector에서 TurretCombinationData를 할당해주세요.");
            return;
        }
        
        if (combinationData.recipes == null || combinationData.recipes.Count == 0)
        {
            Debug.LogError("조합 데이터에 레시피가 없습니다! 레시피를 추가해주세요.");
            return;
        }
        
        Debug.Log($"조합 데이터 로드 완료: {combinationData.recipes.Count}개 레시피");
    }

    void Update()
    {
        // 터렛 드래그 로직
        UpdateTurretDragging();
        
        // 아이템 드래그 로직
        UpdateItemDragging();
    }
    
    // 터렛 드래그 업데이트
    private void UpdateTurretDragging()
    {
        // 터렛 이동이 불가능한 상태면 로직 실행하지 않음
        if (!canMoveTurret) return;
        
        // 마우스 왼쪽 버튼을 눌렀을 때
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // 터렛을 클릭했는지 확인
                if (hit.collider.CompareTag("Turret") && hit.collider.TryGetComponent<TurretBase>(out var terretBase))
                {
                    // 터렛이 이동 가능한 상태일 때만 선택 가능
                    if (terretBase.CanBeMoved())
                    {
                        // 터렛을 배치 모드로 전환
                        terretBase.currentState = TurretBase.TerretState.Placement;
                        
                        selectedTurret = hit.transform;
                        selectedTurretBase = terretBase;
                        
                        initialY = selectedTurret.position.y;
                        // 바닥 평면 업데이트
                        groundPlane = new Plane(Vector3.up, new Vector3(0, initialY, 0));
                        isGroundPlaneInitialized = true;
                        
                        offset = selectedTurret.position - GetMouseWorldPos();
                        
                        // 터렛 선택 시 드래그 아웃라인 활성화
                        selectedTurretBase.SetOutline(true, Color.green, 6f);
                        
                        if(uiManager != null) uiManager.SetDraggingTurret(true);
                    }
                }
            }
        }

        // 마우스를 드래그하는 동안
        if (Input.GetMouseButton(0) && selectedTurret != null)
        {
            Vector3 newPos = GetMouseWorldPos() + offset;
            selectedTurret.position = new Vector3(newPos.x, initialY, newPos.z);
            // 드래그 중 아웃라인 유지 (이미 활성화됨)
        }

        // 마우스 왼쪽 버튼을 뗐을 때
        if (Input.GetMouseButtonUp(0) && selectedTurret != null)
        {
            // 터렛 배치 완료 처리
            selectedTurretBase.OnMouseUp();
            
            // 터렛 아웃라인 비활성화
            selectedTurretBase.SetOutline(false);
            
            selectedTurret = null;
            selectedTurretBase = null;
            
            if(uiManager != null) uiManager.SetDraggingTurret(false);
        }
    }
    
    // 아이템 드래그 업데이트
    private void UpdateItemDragging()
    {
        if (draggedItem != null && isDraggingItem)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            
            // 아이템 미리보기 위치 업데이트
            if (groundPlane.Raycast(ray, out float distance))
            {
                Vector3 point = ray.GetPoint(distance);
                draggedItem.transform.position = new Vector3(point.x, initialY, point.z);
            }

            // 터렛 위 충돌 감지 및 아웃라인 처리
            if (selectedItemPrefab != null && selectedItemPrefab.GetComponent<ItemA>() != null)
            {
                // Turret 레이어 체크
                int turretLayer = LayerMask.NameToLayer("Turret");
                if (turretLayer == -1)
                {
                    Debug.LogError("'Turret' 레이어가 없습니다! 일반 레이캐스트로 대체합니다.");
                    
                    // 모든 콜라이더와 충돌 감지
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        HandleTurretHit(hit);
                    }
                    else
                    {
                        ClearHighlightedTurret();
                    }
                }
                else
                {
                    // Turret 레이어만 대상으로 레이캐스트
                    if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, 1 << turretLayer))
                    {
                        HandleTurretHit(hit);
                    }
                    else
                    {
                        ClearHighlightedTurret();
                    }
                }
            }
        }
    }
    
    // 터렛 히트 처리
    private void HandleTurretHit(RaycastHit hit)
    {
        if (hit.collider.TryGetComponent<TurretBase>(out var turretBase))
        {
            Debug.Log($"터렛 감지: {turretBase.name} ({turretBase.GetType().Name})");
            
            // 조합 가능한지 확인
            ItemA itemComponent = selectedItemPrefab.GetComponent<ItemA>();
            if (itemComponent == null)
            {
                Debug.LogError("선택된 프리팹에 ItemA 컴포넌트가 없습니다!");
                return;
            }
            
            TurretBase resultPrefab = combinationData.GetCombinationResult(turretBase, itemComponent);
            if (resultPrefab != null)
            {
                if (highlightedTurret != turretBase)
                {
                    // 기존 하이라이트 끄기
                    if (highlightedTurret != null) 
                    {
                        highlightedTurret.SetOutline(false);
                        // 조합 모드를 종료
                        highlightedTurret.EndCombining();
                    }
                    
                    // 새 터렛 하이라이트
                    highlightedTurret = turretBase;
                    highlightedTurret.SetOutline(true);
                    
                    // 터렛을 조합 모드로 즉시 변경
                    highlightedTurret.StartCombining();
                    
                    // 조합 가능 시각적 효과 표시
                    ShowCombinationEffect(highlightedTurret);
                    
                    Debug.Log($"조합 가능 터렛 하이라이트: {highlightedTurret.name}");
                }
            }
            else
            {
                Debug.Log($"조합 불가: {turretBase.name} + {selectedItemPrefab.name}");
                ClearHighlightedTurret();
            }
        }
        else
        {
            ClearHighlightedTurret();
        }
    }
    
    // 하이라이트된 터렛 초기화
    private void ClearHighlightedTurret()
    {
        if (highlightedTurret != null)
        {
            highlightedTurret.SetOutline(false);
            // 조합 모드 종료
            highlightedTurret.EndCombining();
            // 조합 효과 제거
            HideCombinationEffect(highlightedTurret);
            highlightedTurret = null;
        }
    }

    private Vector3 GetMouseWorldPos()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return Vector3.zero;
    }
    
    // 터렛 이동 가능 상태 설정 (아이템 드래그 중일 때는 터렛 이동 비활성화)
    public void SetTurretMovable(bool movable)
    {
        canMoveTurret = movable;
    }
    
    /// <summary>
    /// 아���템 드래그를 시작합니다.
    /// </summary>
    public void StartItemDrag(GameObject itemPrefab)
    {
        if (itemPrefab == null) return;
        
        selectedItemPrefab = itemPrefab;
        isDraggingItem = true;
        
        // 바닥 평면 초기화
        if (!isGroundPlaneInitialized)
        {
            InitializeGroundPlane();
        }
        
        // 미리보기 생성
        ShowItemPreview(itemPrefab);
        
        // 터렛 이동 비활성화
        SetTurretMovable(false);
    }
    
    /// <summary>
    /// 아이템 드래그를 종료합니다.
    /// </summary>
    public void EndItemDrag()
    {
        isDraggingItem = false;
        
        // 터렛 이동 다시 활성화
        SetTurretMovable(true);
        
        if (draggedItem != null && selectedItemPrefab != null)
        {
            // 하이라이트된 터렛이 있으면 조합 시도
            if (highlightedTurret != null)
            {
                Debug.Log($"드롭한 위치: 터렛({highlightedTurret.name})");
                
                if (selectedItemPrefab.TryGetComponent<ItemA>(out var item))
                {
                    // 조합 이펙트 제거
                    HideCombinationEffect(highlightedTurret);
                    
                    // 조합 결과 확인
                    TurretBase resultPrefab = combinationData.GetCombinationResult(highlightedTurret, item);
                    if (resultPrefab != null)
                    {
                        // 조합 성공 시 조합 효과 표시
                        PlayCombinationSuccessEffect(highlightedTurret.transform.position);
                        
                        Debug.Log($"조합 성공! {highlightedTurret.name} + {selectedItemPrefab.name} = {resultPrefab.name}");
                        
                        // 조합 성공
                        GameObject newTurretObj = Instantiate(resultPrefab.gameObject, highlightedTurret.transform.position, highlightedTurret.transform.rotation);
                        TurretBase newTurretBase = newTurretObj.GetComponent<TurretBase>();
                        
                        // 새 터렛의 레이어 설정
                        int turretLayer = LayerMask.NameToLayer("Turret");
                        if (turretLayer != -1)
                        {
                            newTurretObj.layer = turretLayer;
                            // 모든 자식 오브젝트의 레이어도 설정
                            foreach (Transform child in newTurretObj.transform)
                            {
                                child.gameObject.layer = turretLayer;
                            }
                        }
                        
                        if (newTurretBase != null)
                        {
                            // 조합된 터렛이 바로 활성화되도록 상태를 변경합니다.
                            newTurretBase.OnMouseUp();
                        }
                        Destroy(highlightedTurret.gameObject); // 기존 터렛 파괴
                    }
                    else
                    {
                        // 조합 실패 시 효과 표시
                        PlayCombinationFailEffect(highlightedTurret.transform.position);
                        
                        // 조합 실패 시 터렛 조합 모드 종료
                        highlightedTurret.EndCombining();
                        
                        Debug.LogWarning($"조합 실패: {highlightedTurret.name} + {selectedItemPrefab.name}");
                    }
                }
                highlightedTurret.SetOutline(false);
                highlightedTurret = null;
            }
            // 바닥에 드롭했는지 확인
            else
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                LayerMask groundMask = (groundLayer != -1) ? (1 << groundLayer) : Physics.DefaultRaycastLayers;
                
                if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, Mathf.Infinity, groundMask))
                {
                    Debug.Log($"드롭한 위치: 바닥({hit.point})");
                    
                    // 뼈대 터렛(TurretBone) 또는 모든 터렛 종류를 바닥에 설치 가능하도록 합니다.
                    if (selectedItemPrefab.GetComponent<TurretBase>() != null)
                    {
                        GameObject newTurret = Instantiate(selectedItemPrefab, hit.point, Quaternion.identity);
                        
                        // 터렛 레이어 설정
                        int turretLayer = LayerMask.NameToLayer("Turret");
                        if (turretLayer != -1)
                        {
                            newTurret.layer = turretLayer;
                            // 모든 자식 오브젝트의 레이어도 설정
                            foreach (Transform child in newTurret.transform)
                            {
                                child.gameObject.layer = turretLayer;
                            }
                        }
                        
                        Debug.Log($"터렛 배치 완료: {newTurret.name}");
                    }
                }
            }

            // 미리보기 오브젝트는 항상 파괴합니다.
            Destroy(draggedItem);
            draggedItem = null;
            selectedItemPrefab = null;
        }
    }
    
    /// <summary>
    /// 선택한 아이템의 프리팹 미리보기를 마우스 위치에 표시합니다.
    /// </summary>
    private void ShowItemPreview(GameObject itemPrefab)
    {
        if (itemPrefab == null) return;
        
        // 이미 미리보기가 있다면 제거
        if (draggedItem != null)
        {
            Destroy(draggedItem);
        }
        
        // 마우스 위치 계산
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Vector3 spawnPosition = Vector3.zero;
        
        if (groundPlane.Raycast(ray, out float distance))
        {
            spawnPosition = ray.GetPoint(distance);
        }
        
        // 미리보기 오브젝트 생성
        draggedItem = Instantiate(itemPrefab, spawnPosition, Quaternion.identity);
        
        // 미리보기 특성 설정
        ConfigurePreviewObject(draggedItem);
        
        Debug.Log($"{itemPrefab.name} 미리보기 생성됨");
    }
    
    /// <summary>
    /// 미리보기 오브젝트의 특성을 설정합니다. (반투명, 콜라이더 비활성화 등)
    /// </summary>
    private void ConfigurePreviewObject(GameObject previewObject)
    {
        if (previewObject == null) return;
        
        // 레이어 설정 (기존 레이어가 없다면 생성 필요)
        previewObject.layer = LayerMask.NameToLayer("Preview") != -1 
            ? LayerMask.NameToLayer("Preview") 
            : previewObject.layer;
        
        // 모든 자식 오브젝트에 대해 처리
        foreach (Renderer renderer in previewObject.GetComponentsInChildren<Renderer>())
        {
            // 반투명 효과 적용
            Color color = renderer.material.color;
            color.a = 0.5f; // 50% 투명도
            renderer.material.color = color;
        }
        
        // 콜라이더가 있다면 비활성화 (드래그 중 물리 충돌 방지)
        foreach (Collider collider in previewObject.GetComponentsInChildren<Collider>())
        {
            collider.enabled = false;
        }
        
        // 게임 로직 관련 컴포넌트 비활성화 (필요에 따라 수정)
        TurretBase turretBase = previewObject.GetComponent<TurretBase>();
        if (turretBase != null)
        {
            turretBase.enabled = false;
        }
    }
    
    /// <summary>
    /// 조합 가능한 터렛에 조합 효과를 표시합니다.
    /// </summary>
    private void ShowCombinationEffect(TurretBase turret)
    {
        if (turret == null) return;
        
        // 터렛에 CombinationEffect 컴포넌트가 있는지 확인하고 효과 표시
        CombinationEffect combinationEffect = turret.GetComponent<CombinationEffect>();
        if (combinationEffect != null)
        {
            combinationEffect.ShowCombiningEffect();
        }
        else
        {
            Debug.LogWarning($"터렛 {turret.name}에 CombinationEffect 컴포넌트가 없습니다.");
        }
        
        Debug.Log($"터렛 {turret.name}에 조합 효과 표시");
    }
    
    /// <summary>
    /// 터렛에서 조합 효과를 제거합니다.
    /// </summary>
    private void HideCombinationEffect(TurretBase turret)
    {
        if (turret == null) return;
        
        // 터렛에 CombinationEffect 컴포넌트가 있는지 확인하고 효과 숨김
        CombinationEffect combinationEffect = turret.GetComponent<CombinationEffect>();
        if (combinationEffect != null)
        {
            combinationEffect.HideCombiningEffect();
        }
        
        Debug.Log($"터렛 {turret.name}의 조합 효과 제거");
    }
    
    /// <summary>
    /// 조합 성공 이펙트를 재생합니다.
    /// </summary>
    private void PlayCombinationSuccessEffect(Vector3 position)
    {
        // 조합 성공 이펙트 구현 (파티클, 사운드 등)
        Debug.Log("조합 성공 이펙트 재생");
        
        // 예시: 파티클 시스템 생성 및 재생
        // GameObject effect = Instantiate(combinationSuccessEffectPrefab, position, Quaternion.identity);
        // Destroy(effect, 2f); // 2초 후 이펙트 제거
    }
    
    /// <summary>
    /// 조합 실패 이펙트를 재생합니다.
    /// </summary>
    private void PlayCombinationFailEffect(Vector3 position)
    {
        // 조합 실패 이펙트 구현 (파티클, 사운드 등)
        Debug.Log("조합 실패 이펙트 재생");
        
        // 예시: 파티클 시스템 생성 및 재생
        // GameObject effect = Instantiate(combinationFailEffectPrefab, position, Quaternion.identity);
        // Destroy(effect, 1.5f); // 1.5초 후 이펙트 제거
    }
}
