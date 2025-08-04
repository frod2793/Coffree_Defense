

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 웨이브 정보를 저장하는 데이터 클래스
/// </summary>
[System.Serializable]
public class WaveData
{
    public int waveNumber;
    public Vector3 spawnPoint;
    public List<EnemyGroup> enemyGroups = new List<EnemyGroup>();
}