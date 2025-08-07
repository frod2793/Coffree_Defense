using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Generic;

/// <summary>
/// 수압프레스 - 파도처럼 앞으로 나아가며 경로상의 모든 적을 한 번씩 밀어내고 피해를 주는 오브젝트
/// </summary>
public class HydroWaterPress : MonoBehaviour
{
    //todo 이펙트 관련 구성 이펙트 매니져로 편성 하고 
    //적과 충돌시 충돌된 모든 적이 넉백및 대미지가 입도록 할것 (완료 - OnTriggerEnter 기반으로 변경)
    
    #region 필드 및 속성
    
    [Header("수압프레스 설정")]
    [SerializeField] private float moveSpeed = 8f; // 이동 속도
    [SerializeField] private float damage = 50f; // 적에게 주는 데미지
    [SerializeField] private float knockbackForce = 10f; // 넉백 힘
    [SerializeField] private float knockbackDuration = 0.8f; // 넉백 지속시간
    [SerializeField] private float lifeTime = 10f; // 최대 생존시간
    
    [Header("시각적 효과")]
    [SerializeField] private GameObject hitEffect; // 적 공격 시 이펙트
    [SerializeField] private GameObject destroyEffect; // 파괴 시 이펙트
    [SerializeField] private ParticleSystem waterEffect; // 물 이펙트
    
    [Header("오디오")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootSound;
    [SerializeField] private AudioClip hitSound;
    
    // 내부 변수
    private Vector3 moveDirection;
    private float currentLifeTime;
    private CancellationTokenSource cancellationTokenSource;
    private bool isDestroyed = false;
    private List<EnemyAdvanced> _hitEnemies; // 이미 타격한 적 목록 (중복 타격 방지)

    // 상수
    private const string ENEMY_TAG = "Enemy";
    private const float MAP_BOUNDARY = 50f; // 맵 경계 (맵 크기에 따라 조정)
    
    #endregion
    
    #region 유니티 생명주기
    
    private void Awake()
    {
        cancellationTokenSource = new CancellationTokenSource();
        _hitEnemies = new List<EnemyAdvanced>();
        currentLifeTime = lifeTime;
        
        // 앞 방향으로 이동 설정
        moveDirection = transform.forward;
        
        // 오디오 소스 설정
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }
    
    private void Start()
    {
        // 생성 사운드 재생
        PlaySound(shootSound);
        
        // 물 이펙트 시작
        if (waterEffect != null)
            waterEffect.Play();
        
        // 생존시간 체크 시작
        StartLifeTimeCheck(cancellationTokenSource.Token).Forget();
    }
    
    private void Update()
    {
        if (isDestroyed) return;
        
        // 매 프레임 이동
        MoveForward();
        
        // 맵 경계 체크
        CheckMapBoundary();
    }

    /// <summary>
    /// 트리거에 적이 들어왔을 때 한 번만 호출
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (isDestroyed) return;

        if (other.CompareTag(ENEMY_TAG))
        {
            var enemy = other.GetComponent<EnemyAdvanced>();
            
            // 적이 유효하고, 아직 타격한 적이 아니라면 공격
            if (enemy != null && enemy.IsAlive && !_hitEnemies.Contains(enemy))
            {
                _hitEnemies.Add(enemy); // 타격 목록에 추가하여 중복 방지
                AttackEnemy(enemy);
            }
        }
    }

    private void OnDestroy()
    {
        CleanupResources();
    }
    
    #endregion
    
    #region 이동 시스템
    
    /// <summary>
    /// 앞으로 이동
    /// </summary>
    private void MoveForward()
    {
        transform.position += moveDirection * moveSpeed * Time.deltaTime;
    }
    
    /// <summary>
    /// 맵 경계 체크 및 파괴
    /// </summary>
    private void CheckMapBoundary()
    {
        Vector3 pos = transform.position;
        
        // 맵 경계를 벗어나면 파괴
        if (Mathf.Abs(pos.x) > MAP_BOUNDARY || 
            Mathf.Abs(pos.z) > MAP_BOUNDARY || 
            Mathf.Abs(pos.y) > MAP_BOUNDARY)
        {
            Debug.Log($"[{gameObject.name}] 맵 경계 도달로 수압프레스 파괴");
            DestroyHydroPress();
        }
    }
    
    #endregion
    
    #region 적 공격

    /// <summary>
    /// 적을 공격하고 넉백 효과 적용
    /// </summary>
    private void AttackEnemy(EnemyAdvanced enemy)
    {
        if (enemy == null || !enemy.IsAlive) return;
        
        // 데미지 적용
        enemy.TakeDamage(damage);
        
        // 넉백 방향 계산 (수압프레스 이동 방향)
        Vector3 knockbackDirection = moveDirection;
        
        // 넉백 효과 적용
        enemy.ApplyKnockback(knockbackDirection, knockbackForce, knockbackDuration);
        
        // 사운드 재생
        PlaySound(hitSound);
        
        // 타격 이펙트 표시
        ShowHitEffect(enemy.transform.position);
        
        Debug.Log($"[{gameObject.name}] 적 {enemy.name}에게 데미지 {damage}, 넉백 적용");
    }
    
    #endregion
    
    #region 생존시간 관리
    
    /// <summary>
    /// 생존시간 체크
    /// </summary>
    private async UniTask StartLifeTimeCheck(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && !isDestroyed && currentLifeTime > 0)
        {
            try
            {
                currentLifeTime -= Time.deltaTime;
                
                if (currentLifeTime <= 0)
                {
                    Debug.Log($"[{gameObject.name}] 생존시간 만료로 수압프레스 파괴");
                    DestroyHydroPress();
                    break;
                }
                
                await UniTask.Yield(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{gameObject.name}] 생존시간 체크 오류: {ex.Message}");
                await UniTask.Delay(100, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }
    
    #endregion
    
    #region 이펙트 및 사운드
    
    /// <summary>
    /// 타격 이펙트 표시
    /// </summary>
    private void ShowHitEffect(Vector3 position)
    {
        if (hitEffect != null)
        {
            // TODO: 추후 EffectManager를 통해 오브젝트 풀링 방식으로 관리되도록 수정
            GameObject effect = Instantiate(hitEffect, position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }
    
    /// <summary>
    /// 파괴 이펙트 표시
    /// </summary>
    private void ShowDestroyEffect()
    {
        if (destroyEffect != null)
        {
            // TODO: 추후 EffectManager를 통해 오브젝트 풀링 방식으로 관리되도록 수정
            GameObject effect = Instantiate(destroyEffect, transform.position, transform.rotation);
            Destroy(effect, 3f);
        }
    }
    
    /// <summary>
    /// 사운드 재생
    /// </summary>
    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
    
    #endregion
    
    #region 파괴 처리
    
    /// <summary>
    /// 수압프레스 파괴
    /// </summary>
    private void DestroyHydroPress()
    {
        if (isDestroyed) return;
        
        isDestroyed = true;
        
        // 물 이펙트 정지
        if (waterEffect != null)
            waterEffect.Stop();
        
        // 파괴 이펙트 표시
        ShowDestroyEffect();
        
        // 리소스 정리
        CleanupResources();
        
        // 0.5초 후 오브젝트 파괴
        Destroy(gameObject, 0.5f);
    }
    
    /// <summary>
    /// 리소스 정리 (여러 번 호출되어도 안전하도록 수정)
    /// </summary>
    private void CleanupResources()
    {
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
    }
    
    #endregion
    
    #region 공개 메서드
    
    /// <summary>
    /// 수압프레스 설정 (스폰 시 호출)
    /// </summary>
    public void Initialize(Vector3 direction, float speed = -1f, float dmg = -1f)
    {
        moveDirection = direction.normalized;
        
        if (speed > 0)
            moveSpeed = speed;
            
        if (dmg > 0)
            damage = dmg;
        
        // 방향에 맞게 회전
        if (moveDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }
        
        Debug.Log($"[{gameObject.name}] 수압프레스 초기화 완료 - 방향: {moveDirection}, 속도: {moveSpeed}, 데미지: {damage}");
    }
    
    /// <summary>
    /// 현재 수압프레스 상태 정보
    /// </summary>
    public string GetStatusInfo()
    {
        return $"이동속도: {moveSpeed}, 데미지: {damage}, 남은시간: {currentLifeTime:F1}s";
    }
    
    #endregion
    
    #region 기즈모 그리기 (에디터용)
    
    private void OnDrawGizmosSelected()
    {
        // 이동 방향 표시
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, moveDirection * 2f);
    }
    
    #endregion
}