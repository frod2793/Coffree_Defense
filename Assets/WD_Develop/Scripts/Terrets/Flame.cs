using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System;

namespace WD_Develop.Scripts.Terrets
{
    [RequireComponent(typeof(ParticleSystem))]
    public class Flame : MonoBehaviour
    {
        [SerializeField]
        private float damagePerParticle;
        private IObjectPool<Flame> pool;
        private ParticleSystem flameParticles;

        private List<ParticleCollisionEvent> collisionEvents;
        private const string EnemyTag = "Enemy";

        private void Awake()
        {
            flameParticles = GetComponent<ParticleSystem>();
            collisionEvents = new List<ParticleCollisionEvent>();
        }

        public void Initialize(float damage, IObjectPool<Flame> objectPool)
        {
            this.damagePerParticle = damage;
            this.pool = objectPool;
        }

        public void StartEmitting()
        {
            flameParticles.Play();
        }

        public async void StopAndRelease()
        {
            flameParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(flameParticles.main.startLifetime.constantMax), cancellationToken: this.GetCancellationTokenOnDestroy());
                pool?.Release(this);
            }
            catch (OperationCanceledException)
            {
                // 작업 취소는 정상적인 상황이므로 예외를 무시합니다.
            }
        }

        /// <summary>
        /// 최종 최적화: 데미지를 일괄 처리하여 TakeDamage() 호출을 프레임당 한 번으로 줄입니다.
        /// </summary>
        private void OnParticleCollision(GameObject other)
        {
            if (!other.CompareTag(EnemyTag) || !other.TryGetComponent<EnemyAdvanced>(out var enemy))
                return;

            int numCollisionEvents = flameParticles.GetCollisionEvents(other, collisionEvents);

            // 해당 프레임에 충돌한 모든 파티클의 총 데미지를 계산합니다.
            if (damagePerParticle == 0 )
            {
                damagePerParticle = 1;
            }
            
            float totalDamage = numCollisionEvents * damagePerParticle;

            // 계산된 총 데미지를 한 번에 전달합니다.
            if (totalDamage > 0)
            {
                enemy.TakeDamage(totalDamage);
            }
        }
    }
}
