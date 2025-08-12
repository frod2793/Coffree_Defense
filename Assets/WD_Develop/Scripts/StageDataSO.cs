using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지의 웨이브 데이터와 클리어 여부를 저장하는 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "StageData", menuName = "ScriptableObjects/StageDataSO", order = 1)]
public class StageDataSO : ScriptableObject
{

    [Header("Wave Configuration")]
    public List<WaveData> waveDataList = new List<WaveData>();
}
