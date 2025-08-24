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
    [RequireComponent(typeof(ParticleSystem))]
    public class Iceflame : MonoBehaviour
    {
        private float attackPower;
        private float slowAmount;
        private IObjectPool<Iceflame> pool;
        private ParticleSystem iceflameParticleSystem;
        private List<ParticleCollisionEvent> collisionEvents;

        // 적별 마지막 피격 시간을 기록하여 DPS를 제어
        private Dictionary<EnemyAdvanced, float> lastHitTimes = new Dictionary<EnemyAdvanced, float>();
        private const float DAMAGE_INTERVAL = 1.0f; // 1초마다 데미지 적용

        private void Awake()
        {
            iceflameParticleSystem = GetComponent<ParticleSystem>();
            collisionEvents = new List<ParticleCollisionEvent>();
        }

        /// <summary>
        /// 아이스플레임 효과를 초기화합니다.
        /// </summary>
        public void Initialize(float power, float slow, IObjectPool<Iceflame> objectPool)
        {
            this.attackPower = power;
            this.slowAmount = slow;
            this.pool = objectPool;
            lastHitTimes.Clear(); // 풀에서 재사용될 때 기록 초기화
        }

        /// <summary>
        /// 파티클 시스템 재생을 시작합니다.
        /// </summary>
        public void StartEmitting()
        {
            iceflameParticleSystem.Play();
        }

        /// <summary>
        /// 파티클 방출을 중지하고, 수명이 다하면 풀에 반환합니다.
        /// </summary>
        public async void StopAndRelease()
        {
            iceflameParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            
            try
            {
                // 파티클의 최대 수명만큼 기다린 후 풀에 반환
                await UniTask.Delay(TimeSpan.FromSeconds(iceflameParticleSystem.main.startLifetime.constantMax), cancellationToken: this.GetCancellationTokenOnDestroy());
                pool?.Release(this);
            }
            catch (OperationCanceledException)
            {
                // 오브젝트 파괴 시 발생하는 예외는 정상적인 상황이므로 무시합니다.
            }
        }

        /// <summary>
        /// 파티클이 적과 충돌했을 때 호출됩니다.
        /// </summary>
        private void OnParticleCollision(GameObject other)
        {
            if (!other.TryGetComponent<EnemyAdvanced>(out var enemy))
                return;

            // 마지막 피격 시간 확인하여 DPS 제어
            if (lastHitTimes.TryGetValue(enemy, out float lastHitTime))
            {
                if (Time.time - lastHitTime < DAMAGE_INTERVAL)
                {
                    // 둔화 효과만 짧게 계속 적용
                    enemy.ApplySlowEffect(slowAmount, 0.5f);
                    Debug.Log($"[Iceflame] {enemy.name} 둔화 효과만 적용 (데미지 쿨다운)");
                    return; // 데미지는 아직 적용하지 않음
                }
            }

            // 데미지 적용 및 마지막 피격 시간 기록
            Debug.Log($"[Iceflame] {enemy.name}에게 데미지 {attackPower} 및 둔화 효과 적용");
            enemy.TakeDamage(attackPower);
            enemy.ApplySlowEffect(slowAmount, 0.5f);
            lastHitTimes[enemy] = Time.time;
        }
    }
}
