using UnityEngine;

//드래그앤 드랍이 될 대상 오브젝트
[RequireComponent(typeof(BoxCollider))]
public class TerretControl : MonoBehaviour
{
    [SerializeField]
    GameObject TerretHead;
    
    [Header("Terret Stats")]
    [SerializeField] private float range = 15f;
    [SerializeField] private float turnSpeed = 10f;

    private Transform target;
    [SerializeField] private string enemyTag = "Enemy";
    
    public GameObject EnemyPrefab;
    
    private Vector3 offset;
    private Plane groundPlane;
    private float initialY;

    void Start()
    {
        // 오브젝트의 시작 높이를 기준으로 바닥 평면을 생성합니다.
        initialY = transform.position.y;
        groundPlane = new Plane(Vector3.up, new Vector3(0, initialY, 0));
        InvokeRepeating("UpdateTarget", 0f, 0.5f);
    }

    void FixedUpdate()
    {
        if (target == null)
            return;

        // 목표물을 향한 방향 벡터 계산
        Vector3 dir = target.position - transform.position;
        Quaternion lookRotation = Quaternion.LookRotation(dir);
        // 부드러운 회전을 위해 Slerp 사용
        Vector3 rotation = Quaternion.Slerp(TerretHead.transform.rotation, lookRotation, Time.deltaTime * turnSpeed).eulerAngles;
        // Y축으로만 회전하도록 설정
        TerretHead.transform.rotation = Quaternion.Euler(0f, rotation.y, 0f);
    }

    #region Drag and Drop Methods

    

    
    void OnMouseDown()
    {
        // 오브젝트의 현재 위치와 마우스 위치 사이의 간격을 계산하고 저장합니다.
        offset = transform.position - GetMouseWorldPos();
    }

    private Vector3 GetMouseWorldPos()
    {
        // 카메라에서 마우스 포인터 위치로 레이를 생성합니다.
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        float distance;
        
        // 레이가 바닥 평면과 교차하는지 확인합니다.
        if (groundPlane.Raycast(ray, out distance))
        {
            // 교차점의 월드 좌표를 반환합니다.
            return ray.GetPoint(distance);
        }

        // 교차하지 않으면 기본 위치를 반환합니다.
        return transform.position;
    }

    void OnMouseDrag()
    {
        // 마우스/터치를 드래그하는 동안 오브젝트의 위치를 업데이트합니다.
        Vector3 newPos = GetMouseWorldPos() + offset;
        // 오브젝트가 원래 높이를 유지하도록 Y좌표를 고정합니다.
        transform.position = new Vector3(newPos.x, initialY, newPos.z);
    }
    
    
    
    #endregion
    
    
    // 터랫의 머리가 적을 바라 보도록 하는 로직 구현 
    void UpdateTarget()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        float shortestDistance = Mathf.Infinity;
        GameObject nearestEnemy = null;

        foreach (GameObject enemy in enemies)
        {
            float distanceToEnemy = Vector3.Distance(transform.position, enemy.transform.position);
            if (distanceToEnemy < shortestDistance)
            {
                shortestDistance = distanceToEnemy;
                nearestEnemy = enemy;
            }
        }

        if (nearestEnemy != null && shortestDistance <= range)
        {
            target = nearestEnemy.transform;
        }
        else
        {
            target = null;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
