using UnityEngine;

public class InGameUIManager : MonoBehaviour
{
  
    // 포탑 조합에 대한 결과오브젝트는 scriptable object로 관리합니다.
    // 조합 식 클래스를 따로 생성하여 조합에 따라 포탑의 프리펩 을 변경 
    // 포탑의 기본 구동 방식은 TerretBase 클래스를 상속받아 구현합니다.
    // UI 의 재료 칸에서 포탑으로 드래그 앤 드랍시 포탑의 조합 식이 활성화 됩니다 
    // 재료칸에서 가져간 재료를 포탑이랑 겹쳐 놓을시 포탑이 아웃라인으로 표시 됩니다 
    // 재료를 포탑에 드랍시 조합식이 활성화 됩니다 .
    // 조합 식이 활성화 되면 포탑의 작동이 비활성화 되고 포탑의 머리위에 말풍선의 형태로 조합식이 작동 합니다.
    //예 ) 드래그한것 (타피오카) + (아직 드래드안한것 )???? == 포탑의 모양을 실루엣 처리 
    // 조합 식이 활성화 되면 조합 결과 오브젝트가 활성화 됩니다.
    // 2초후 포탑이 활성화 됩니다.
    //포탑 인터페이스를 통해 게임 메니져에 설치된 포탑의 정보를 전달합니다.
    
    
    // 포탑의 드래그앤 드랍이 활성화 되면 포탑이 비활성화 됩니다 (쿨타임 말풍선 형태의 ui 로 표시 )
    // 포탑의 위치가 고정되면 2초의 쿨타임후 포탑이 활성화 됩니다 
    
    //확인해야될 사항 유니티 에셋 에 관한 조항 
    
    
    
    [Header( "In-Game UI Manager")]
    [SerializeField] private GameObject inGameUI; // In-Game UI 오브젝트
    
    [SerializeField]
    GameManager gameManager;
    
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
