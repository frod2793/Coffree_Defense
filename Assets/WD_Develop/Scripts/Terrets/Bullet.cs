using UnityEngine;
using UnityEngine.Pool;

public class Bullet : MonoBehaviour
{
    private Transform target;
    private ObjectPool<GameObject> pool;

    private bool isReleased = false;

    public float speed = 70f;
    public float damage = 10f; // 이 값은 터렛에서 설정해 줄 수 있습니다.

    public void Seek(Transform _target, ObjectPool<GameObject> _pool)
    {
        target = _target;
        pool = _pool;
        isReleased = false;
        // 일정 시간 후 자동 반환 (예: 5초)
        AutoReleaseAfterDelay(5f).Forget();
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid AutoReleaseAfterDelay(float delay)
    {
        await Cysharp.Threading.Tasks.UniTask.Delay((int)(delay * 1000));
        if (!isReleased && gameObject.activeInHierarchy)
        {
            ReleaseBullet();
        }
    }

    void Update()
    {
        if (target == null)
        {
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
        if (target != null)
        {
            // 이펙트가 생성될 위치를 미리 저장 (타겟이 즉시 파괴될 수 있으므로)
            Vector3 hitPosition = target.position;

            var enemy = target.GetComponent<EnemyAdvanced>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }
            
            // EffectManager를 사용하여 피격 이펙트 재생
            if (EffectManager.Instance != null)
            {
                EffectManager.Instance.PlayEffect(EffectType.BulletImpact, hitPosition);
            }
        }
        
        Debug.Log(target != null ? target.name + " 에게 " + damage + " 데미지!" : "타겟 없음");
        ReleaseBullet();
    }

    private void ReleaseBullet()
    {
        if (isReleased) return;
        isReleased = true;
        if (gameObject.activeInHierarchy && pool != null)
        {
            pool.Release(gameObject);
        }
        else
        {
            // 풀 미지정 시 비활성화(씬에 남아도 GC 없음)
            gameObject.SetActive(false);
        }
    }

    // 카메라의 시야에서 벗어나면 호출되는 함수
    void OnBecameInvisible()
    {
        ReleaseBullet();
    }
}
