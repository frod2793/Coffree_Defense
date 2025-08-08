using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(Collider))]
public class Bullet : MonoBehaviour
{
    private ObjectPool<GameObject> pool;
    private Vector3 moveDirection;
    private bool isReleased = false;

    public float speed = 70f;
    public float damage = 10f;

    [SerializeField]
    private Collider collider;
    
    private const string ENEMY_TAG = "Enemy";

    private void Awake()
    {
        // 총알의 콜라이더는 트리거로 설정되어야 합니다.
        collider.isTrigger = true;
    }

    /// <summary>
    /// 총알을 초기화하고 발사 방향을 설정합니다.
    /// </summary>
    public void Seek(Vector3 direction, float newDamage, ObjectPool<GameObject> _pool)
    {
        pool = _pool;
        moveDirection = direction.normalized;
        damage = newDamage;
        isReleased = false;
        
        // 5초 후 자동 반환
        AutoReleaseAfterDelay(5f).Forget();
    }

    private async Cysharp.Threading.Tasks.UniTaskVoid AutoReleaseAfterDelay(float delay)
    {
        await Cysharp.Threading.Tasks.UniTask.Delay((int)(delay * 1000));
        ReleaseBullet();
    }

    void Update()
    {
        // 지정된 방향으로 직진
        float distanceThisFrame = speed * Time.deltaTime;
        transform.Translate(moveDirection * distanceThisFrame, Space.World);
    }

    void OnTriggerEnter(Collider other)
    {
        // 적과 충돌했는지 태그로 확인
        if (other.CompareTag(ENEMY_TAG))
        {
            HitTarget(other.gameObject);
        }
        // 적이 아닌 다른 것에 부딪혀도 총알은 사라지도록 처리 (예: 벽)
        else
        {
            ReleaseBullet();
        }
    }

    void HitTarget(GameObject targetObject)
    {
        if (targetObject != null)
        {
            var enemy = targetObject.GetComponent<EnemyAdvanced>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }
            
            if (EffectManager.Instance != null)
            { 
                // 충돌 지점에서 이펙트 재생
                EffectManager.Instance.PlayEffect(EffectType.BulletImpact, transform.position);
            }
        }
        
        ReleaseBullet();
    }

    private void ReleaseBullet()
    {
        if (isReleased) return;
        isReleased = true;

        if (pool != null && gameObject.activeInHierarchy)
        {
            pool.Release(gameObject);
        }
        else if (gameObject.activeInHierarchy)
        { 
            gameObject.SetActive(false);
        }
    }

    void OnBecameInvisible()
    {
        ReleaseBullet();
    }
}
