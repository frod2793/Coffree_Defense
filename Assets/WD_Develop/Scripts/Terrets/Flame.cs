using UnityEngine;
using UnityEngine.Pool;

namespace WD_Develop.Scripts.Terrets
{
    /// <summary>
    /// 화염 파티클의 로직을 처리하는 클래스입니다.
    /// Laser.cs와 거의 동일하며, 파티클 충돌로 데미지를 처리합니다.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class Flame : MonoBehaviour
    {
        private float damagePerParticle;
        private IObjectPool<Flame> pool;
        private ParticleSystem flameParticles;

        private const string EnemyTag = "Enemy";

        private void Awake()
        {
            flameParticles = GetComponent<ParticleSystem>();
        }

        /// <summary>
        /// FlameTurret의 오브젝트 풀에서 화염을 처음 생성하거나 가져올 때 호출됩니다.
        /// </summary>
        /// <param name="damage">파티클 입자 하나당 입힐 데미지</param>
        /// <param name="objectPool">자신을 관리하는 오브젝트 풀</param>
        public void Initialize(float damage, IObjectPool<Flame> objectPool)
        {
            this.damagePerParticle = damage;
            this.pool = objectPool;
        }

        /// <summary>
        /// 화염 발사를 멈추고, 파티클이 모두 사라진 후 오브젝트 풀에 자신을 반환합니다.
        /// </summary>
        public void StopAndRelease()
        {
            // 파티클 시스템의 재생을 멈춥니다. (이미 재생중인 파티클은 사라질 때까지 유지됩니다)
            flameParticles.Stop();
            
            // 파티클의 최대 수명만큼 기다린 후 풀에 반환합니다.
            float releaseDelay = flameParticles.main.startLifetime.constantMax;
            Invoke(nameof(ReleaseToPool), releaseDelay);
        }

        private void ReleaseToPool()
        {
            // 오브젝트 풀이 할당되어 있을 경우에만 반환 로직을 실행합니다.
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