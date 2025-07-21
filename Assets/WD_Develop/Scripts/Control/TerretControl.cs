using UnityEngine;

//역할:
// 터렛들을 드래그앤 드랍하고 제어하는 터랫 컨트롤 매니져 스크립트 매니져 오브젝트에 배치 되어있을예정
// 이 스크립트는 터렛 오브젝트를 드래그하여 배치하고 , 포탑의 조합단계에 따라 포탑을 변형 시키는 역할을 합니다.


public class TerretControl : MonoBehaviour
{
    private Transform selectedTurret;
    private TerretBase selectedTurretBase;
    private Vector3 offset;
    private Plane groundPlane;
    private float initialY;

    void Update()
    {
        // 마우스 왼쪽 버튼을 눌렀을 때
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // 터렛을 클릭했는지 확인
                if (hit.collider.CompareTag("Terret") && hit.collider.TryGetComponent<TerretBase>(out var terretBase))
                {
                    // 이미 배치된(Idle 또는 Active 상태) 터렛을 클릭하면 배치 모드로 전환합니다.
                    if (terretBase.currentState == TerretBase.TerretState.Idle || terretBase.currentState == TerretBase.TerretState.Active)
                    {
                        terretBase.currentState = TerretBase.TerretState.Placement;
                    }
                    
                    // 터렛이 배치 모드일 때 선택하여 드래그할 수 있도록 합니다.
                    if (terretBase.currentState == TerretBase.TerretState.Placement)
                    {
                        selectedTurret = hit.transform;
                        selectedTurretBase = terretBase;
                        
                        initialY = selectedTurret.position.y;
                        groundPlane = new Plane(Vector3.up, new Vector3(0, initialY, 0));
                        offset = selectedTurret.position - GetMouseWorldPos();
                    }
                }
            }
        }

        // 마우스를 드래그하는 동안
        if (Input.GetMouseButton(0) && selectedTurret != null)
        {
            Vector3 newPos = GetMouseWorldPos() + offset;
            selectedTurret.position = new Vector3(newPos.x, initialY, newPos.z);
        }

        // 마우스 왼쪽 버튼을 뗐을 때
        if (Input.GetMouseButtonUp(0) && selectedTurret != null)
        {
            selectedTurretBase.OnMouseUp();
            selectedTurret = null;
            selectedTurretBase = null;
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
}
