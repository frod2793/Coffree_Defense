using UnityEngine;
using System.Collections;

public class TerretBase : MonoBehaviour
{
    // TerretBase 클래스는 테렛의 기본 기능을 정의합니다.
    //기능 1 공격 
    //기능 2 , 배치모드 , 활성화 모드 ,대기 모드
    //드래그중엔 배치모드 , 드래그 완료 후 2초뒤에 활성화모드 후 공격 이벤트 시작
    //변수, 공격범위 , 공격속도 , 공격력, 포탑 hp
    //기능 3 , 공격범위 내 적이 있을 경우 공격모드로 전환 후 공격
    //기능 4 , 공격범위 내 적이 없을 경우 대기모드로 전환
    //기능 5 , 포탑 hp가 0이 되면 파괴
    //기능 6 , 포탑이 파괴되면 해당 오브젝트를 비활성화
    //기능 7 , 포탑이 파괴되면 해당 오브젝트를 비활성화하고 파괴 이펙트 재생
    //기능 8 , 포탑의 머리를 회전시켜 적을 바라보게 함 
    
    public enum TerretState { Placement, Idle, Active, Destroyed }
    public TerretState currentState;

    [Header("Terret Stats")]
    [SerializeField] protected float attackPower = 10f;
    [SerializeField] protected float attackSpeed = 1f;
    [SerializeField] private float hp = 100f;
    
    [Header("Effects")]
    [SerializeField] private GameObject destructionEffect;
    
    void Start()
    {
        currentState = TerretState.Placement;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnMouseUp()
    {
        if (currentState == TerretState.Placement)
        {
            StartCoroutine(ActivateAfterDelay(2.0f));
        }
    }

    private IEnumerator ActivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        currentState = TerretState.Idle; 
        Debug.Log("터렛 활성화!");
        // 활성화 되면 공격 로직 등을 시작할 수 있습니다.
    }

    public void TakeDamage(float damage)
    {
        if (currentState == TerretState.Destroyed) return;

        hp -= damage;
        if (hp <= 0)
        {
            DestroyTerret();
        }
    }

    private void DestroyTerret()
    {
        currentState = TerretState.Destroyed;
        if (destructionEffect != null)
        {
            Instantiate(destructionEffect, transform.position, Quaternion.identity);
        }
        gameObject.SetActive(false);
        Debug.Log("터렛 파괴됨");
    }
}
