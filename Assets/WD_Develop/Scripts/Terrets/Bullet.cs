using UnityEngine;
using UnityEngine.Pool;

public class Bullet : MonoBehaviour
{
    private Transform target;
    private ObjectPool<GameObject> pool;

    public float speed = 70f;
    public float damage = 10f; // 이 값은 터렛에서 설정해 줄 수 있습니다.

    public void Seek(Transform _target, ObjectPool<GameObject> _pool)
    {
        target = _target;
        pool = _pool;
    }

    void Update()
    {
        if (target == null)
        {
            // 목표물이 사라지면 풀로 반환
            ReleaseBullet();
            return;
        }

        Vector3 dir = target.position - transform.position;
        float distanceThisFrame = speed * Time.deltaTime;

        // 목표물에 도달했는지 확인
        if (dir.magnitude <= distanceThisFrame)
        {
            HitTarget();
            return;
        }

        transform.Translate(dir.normalized * distanceThisFrame, Space.World);
    }

    void HitTarget()
    {
        // 여기에 적에게 데미지를 주는 로직을 추가할 수 있습니다.
        // if(target.TryGetComponent<Enemy>(out var enemy))
        // {
        //     enemy.TakeDamage(damage);
        // }
        Debug.Log(target.name + " 에게 " + damage + " 데미지!");
        
        ReleaseBullet();
    }

    private void ReleaseBullet()
    {
        // 오브젝트가 활성화 상태이고, 풀이 존재할 때만 반환
        if (gameObject.activeInHierarchy && pool != null)
        {
            pool.Release(gameObject);
        }
        else if (pool == null)
        {
            // 풀이 없는 경우(예: 테스트용으로 씬에 직접 배치) 그냥 파괴
            Destroy(gameObject);
        }
    }

    // 카메라의 시야에서 벗어나면 호출되는 함수
    void OnBecameInvisible()
    {
        ReleaseBullet();
    }
}
