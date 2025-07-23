using UnityEngine;
using UnityEngine.Pool;

//역할:
// 터렛 오브젝트에 추가될 클래스 

// TurretBone 오브젝트에 ItemA 오브젝트가 드래그앤 드랍으로 충돌시
// NormalTurret 오브젝트로 변환

[RequireComponent(typeof(BoxCollider))]
public class NormalTurret:TurretBase
{
    [Header("Normal Terret Specifics")]
    [SerializeField] private GameObject bulletPrefab;
    
    [SerializeField] private Transform firePoint; // 총알이 발사되는 위치
    [SerializeField] private Transform terretHead;

    [Header("Terret Stats")]
    [SerializeField] private float range = 15f;
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private string enemyTag = "Enemy";
    
    private Transform target;
    private float fireCountdown = 0f;
    
    private ObjectPool<GameObject> bulletPool;
    
    void Start()
    {
        // 필수 컴포넌트 및 변수가 할당되었는지 확인
        if (bulletPrefab == null)
            Debug.LogError("Bullet Prefab이 할당되지 않았습니다.", this);
        if (firePoint == null)
            Debug.LogError("Fire Point가 할당되지 않았습니다.", this);
        if (terretHead == null)
            Debug.LogError("Terret Head가 할당되지 않았습니다.", this);

        InvokeRepeating(nameof(UpdateTarget), 0f, 0.5f);
        
        bulletPool = new ObjectPool<GameObject>(
            CreateBullet,
            OnGetFromPool,
            OnReleaseToPool,
            OnDestroyBullet,
            maxSize: 20
        );
    }

    protected override void Update()
    {
        // 배치 중이거나 파괴된 상태에서는 아무것도 하지 않음
        if (currentState == TerretState.Placement || currentState == TerretState.Destroyed)
            return;

        // 목표물 조준
        if (target != null)
        {
            Vector3 dir = target.position - transform.position;
            Quaternion lookRotation = Quaternion.LookRotation(dir);
            Vector3 rotation = Quaternion.Slerp(terretHead.rotation, lookRotation, Time.deltaTime * turnSpeed).eulerAngles;
            terretHead.rotation = Quaternion.Euler(0f, rotation.y, 0f);
        }

        // 카운트다운 감소
        if (fireCountdown > 0)
        {
            fireCountdown -= Time.deltaTime;
        }

        // 공격 로직
        if (target != null)
        {
            currentState = TerretState.Active;
            if (fireCountdown <= 0f)
            {
                Shoot();
                fireCountdown = 1f / attackSpeed; // 공격 속도에 따라 다음 발사 시간 설정
            }
        }
        else
        {
            currentState = TerretState.Idle;
        }
    }

    void Shoot()
    {
        
        if (bulletPrefab != null && firePoint != null)
        {
            // 오브젝트 풀에서 총알을 가져옵니다.
            GameObject bulletGO = bulletPool.Get();
            if (bulletGO == null) return;

            bulletGO.transform.position = firePoint.position;
            bulletGO.transform.rotation = firePoint.rotation;
            
            Bullet bullet = bulletGO.GetComponent<Bullet>();
            if (bullet != null)
            {
                bullet.Seek(target, bulletPool);
            }
        }
        Debug.Log("발사!");
    }
    
    // --- Object Pool Methods ---
    private GameObject CreateBullet()
    {
        if (bulletPrefab == null) return null;
        
        GameObject bulletGO = Instantiate(bulletPrefab);
        Bullet bulletComponent = bulletGO.GetComponent<Bullet>();

        if (bulletComponent == null)
        {
            Debug.LogError("Bullet Prefab에 Bullet 스크립트가 없습니다.", bulletGO);
            Destroy(bulletGO); // 잘못된 오브젝트는 파괴
            return null;
        }
        
        bulletComponent.damage = attackPower;
        return bulletGO;
    }

    private void OnGetFromPool(GameObject bullet)
    {
        bullet.SetActive(true);
    }

    private void OnReleaseToPool(GameObject bullet)
    {
        bullet.SetActive(false);
    }

    private void OnDestroyBullet(GameObject bullet)
    {
        Destroy(bullet);
    }
    
    void UpdateTarget()
    {
        if (currentState == TerretState.Placement)
        {
            target = null;
            return;
        }
        
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
