using UnityEngine;
using UnityEngine.Pool;

namespace WD_Develop.Scripts.Terrets
{
    /// <summary>
    /// 레이저 빔의 로직을 처리하는 클래스입니다.
    /// Bullet.cs와 동일하게 EnemyAdvanced 컴포넌트에 직접 데미지를 줍니다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class Laser : MonoBehaviour
    {
        private float damagePerParticle;
        private IObjectPool<Laser> pool;
        private ParticleSystem laserParticles;

        private const string EnemyTag = "Enemy";

        private void Awake()
        {
            laserParticles = GetComponent<ParticleSystem>();
        }

        /// <summary>
        /// LaserTurret의 오브젝트 풀에서 레이저를 처음 생성하거나 가져올 때 호출됩니다.
        /// </summary>
        public void Initialize(float damage, IObjectPool<Laser> objectPool)
        {
            this.damagePerParticle = damage;
            this.pool = objectPool;
        }

        /// <summary>
        /// 레이저 발사를 멈추고, 파티클이 모두 사라진 후 오브젝트 풀에 자신을 반환합니다.
        /// </summary>
        public void StopAndRelease()
        {
            laserParticles.Stop();
            float releaseDelay = laserParticles.main.startLifetime.constantMax;
            Invoke(nameof(ReleaseToPool), releaseDelay);
        }

        private void ReleaseToPool()
        {
            pool?.Release(this);
        }

        /// <summary>
        /// 파티클이 다른 콜라이더와 충돌할 때마다 호출됩니다.
        /// </summary>
        void OnParticleCollision(GameObject other)
        {
            if (other.CompareTag(EnemyTag))
            {
                // Bullet.cs와 동일하게 EnemyAdvanced 컴포넌트를 직접 찾아 데미지를 입힙니다.
                var enemy = other.GetComponent<EnemyAdvanced>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damagePerParticle);
                }
            }
        }
    }
}
