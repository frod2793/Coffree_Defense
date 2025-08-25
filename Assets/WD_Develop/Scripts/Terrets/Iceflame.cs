using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System;

namespace WD_Develop.Scripts.Terrets
{
    /// <summary>
    /// 화염과 냉기 효과를 결합하여, 파티클 충돌 시 적에게 지속 피해와 둔화 효과를 적용합니다.
    /// </summary>
    public class Iceflame : MonoBehaviour
    {
        [SerializeField] private ParticleSystem collitderparticle;
        private float attackPower;
        private float slowAmount;
        private IObjectPool<Iceflame> pool;

        /// <summary>
        /// 아이스플레임 효과를 초기화합니다.
        /// </summary>
        public void Initialize(float power, float slow, IObjectPool<Iceflame> objectPool)
        {
            this.attackPower = power;
            this.slowAmount = slow;
            this.pool = objectPool;
        }

        /// <summary>
        /// 파티클 시스템 재생을 시작합니다.
        /// </summary>
        public void StartEmitting()
        {
            collitderparticle.Play();
        }

        /// <summary>
        /// 파티클 방출을 중지하고, 수명이 다하면 풀에 반환합니다.
        /// </summary>
        public async void StopAndRelease()
        {
            collitderparticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            
            try
            {
                // 파티클의 최대 수명만큼 기다린 후 풀에 반환
                await UniTask.Delay(TimeSpan.FromSeconds(collitderparticle.main.startLifetime.constantMax), cancellationToken: this.GetCancellationTokenOnDestroy());
                pool?.Release(this);
            }
            catch (OperationCanceledException)
            {
                // 오브젝트 파괴 시 발생하는 예외는 정상적인 상황이므로 무시합니다.
            }
        }

        /// <summary>
        /// ColliderParticle로부터 충돌 이벤트를 받아 처리합니다.
        /// </summary>
        public void HandleParticleCollision(GameObject other)
        {
            if (!other.TryGetComponent<EnemyAdvanced>(out var enemy))
            {
                // EnemyAdvanced 컴포넌트가 없는 오브젝트는 무시
                return;
            }

            // attackPower를 DPS로 간주하고, 프레임 시간(deltaTime)에 비례한 데미지를 적용합니다.
            // 이렇게 하면 프레임 속도와 관계없이 일정한 DPS를 유지할 수 있습니다.
            enemy.TakeDamage(attackPower * Time.deltaTime);
            
            // 둔화 효과는 짧은 시간 동안 지속적으로 갱신하여 효과가 끊기지 않도록 합니다.
            enemy.ApplySlowEffect(slowAmount, 0.5f);
        }
    }
}
