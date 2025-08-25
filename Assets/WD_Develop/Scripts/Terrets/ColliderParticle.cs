using UnityEngine;

namespace WD_Develop.Scripts.Terrets
{
    /// <summary>
    /// 자식 파티클 시스템의 충돌을 감지하여 부모의 Iceflame 스크립트로 전달합니다.
    /// </summary>
    public class ColliderParticle : MonoBehaviour
    {
        private Iceflame parentIceflame;

        private void Awake()
        {
            // 부모 오브젝트에서 Iceflame 컴포넌트를 찾습니다.
            parentIceflame = GetComponentInParent<Iceflame>();
            if (parentIceflame == null)
            {
                Debug.LogError("[ColliderParticle] 부모에서 Iceflame 컴포넌트를 찾을 수 없습니다.", gameObject);
            }
        }

        /// <summary>
        /// 파티클이 다른 오브젝트와 충돌할 때 호출됩니다.
        /// </summary>
        private void OnParticleCollision(GameObject other)
        {
            if (parentIceflame != null)
            {
                // 충돌 이벤트를 부모의 Iceflame 스크립트로 전달합니다.
                parentIceflame.HandleParticleCollision(other);
            }
        }
    }
}
