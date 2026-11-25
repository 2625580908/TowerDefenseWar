using UnityEngine;

public class BaseData : MonoSingleton<BaseData>
{
   [TagSelector][Header("敌方阵营标签")]
   public string EnemyTag = "Enemy";
   [TagSelector][Header("本地阵营标签")]
   public string OneselfTag = "Oneself";
   [TagSelector][Header("地形标签")]
   public string TerrainTag = "Terrain";
   void Start()
   {
   }
}
