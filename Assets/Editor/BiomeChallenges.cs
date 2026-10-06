using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// One authored transition challenge per biome, edited in place. The remaining
// campaign ladder retains its authored content until it has been playtested.
public static class BiomeChallenges
{
  public static List<Vector2Int> Path(int biome)
  {
    Vector2Int P(int x,int y)=>new Vector2Int(x,y);
    if(biome==2)return OpeningSequence.Path(2);
    if(biome==3)return new List<Vector2Int>{P(0,1),P(1,1),P(2,1),P(2,2),P(2,3),P(3,3),P(4,3),P(4,2),P(4,1),P(5,1),P(6,1),P(6,2),P(6,3),P(7,3),P(8,3),P(9,3)};
    if(biome==4){var cells=Path(3);for(int i=0;i<cells.Count;i++)cells[i]=P(cells[i].x,4-cells[i].y);return cells;}
    if(biome==5){var cells=OpeningSequence.Path(2);for(int i=0;i<cells.Count;i++)cells[i]=P(cells[i].x,4-cells[i].y);return cells;}
    if(biome==6)return new List<Vector2Int>{P(0,3),P(1,3),P(2,3),P(2,2),P(2,1),P(3,1),P(4,1),P(4,2),P(4,3),P(5,3),P(6,3),P(6,2),P(6,1),P(7,1),P(8,1),P(9,1)};
    return new List<Vector2Int>{P(0,2),P(1,2),P(2,2),P(2,1),P(2,0),P(3,0),P(4,0),P(5,0),P(6,0),P(7,0),P(7,1),P(7,2),P(7,3),P(8,3),P(9,3)};
  }
  public static WaveConfig.Wave[] Waves(int biome)
  {
    EnemyConfig Load(string name)=>AssetDatabase.LoadAssetAtPath<EnemyConfig>($"Assets/Settings/Enemies/{name}.asset");
    var basic=Load("BasicEnemy");var fast=Load("FastEnemy");var armor=Load("ArmoredEnemy");
    var swarm=Load("SwarmEnemy");var shield=Load("ShieldedEnemy");var split=Load("SplitterEnemy");var heal=Load("HealerEnemy");
    WaveConfig.WaveEnemyGroup G(EnemyConfig cfg,int count,float hp=1)=>new WaveConfig.WaveEnemyGroup{enemyConfig=cfg,count=count,healthMultiplier=hp,rewardMultiplier=1+(hp-1)*.5f};
    WaveConfig.Wave W(float spacing,int reward,string hint,params WaveConfig.WaveEnemyGroup[] groups)
      =>new WaveConfig.Wave{enemyGroups=groups,timeBetweenSpawns=spacing,timeToNextWave=12,waveGoldReward=reward,planningHint=hint};
    if(biome==2)return new[]{
      W(1.4f,35,"Cover both bends. Upgraded Archers hit harder and reach further.",G(basic,5,1.1f)),
      W(.8f,45,"Runners overtake the pack. Ice and FIRST keep the exit safe.",G(basic,3,1.2f),G(fast,4,1)),
      W(.7f,60,"Archers on a bend cover the most road. Give runners enough damage.",G(fast,5,1.15f),G(basic,4,1.3f),G(armor,1,1))};
    if(biome==3)return new[]{
      W(1.3f,40,"Poison bypasses armor. STRONG keeps it on the toughest bacteria.",G(basic,5,1.3f)),
      W(1.1f,50,"An armored front needs sustained damage; Poison or a supported Archer helps.",G(armor,3,1.1f),G(basic,3,1.3f)),
      W(.9f,55,"Healers restore nearby allies. Finish enemies before the next heal pulse.",G(heal,1,.8f),G(armor,2,1.2f),G(basic,4,1.3f)),
      W(.8f,70,"Poison tackles armor; FIRST catches runners. Keep damage covering the last bend.",G(heal,1,1),G(armor,3,1.3f),G(fast,4,1.2f))};
    if(biome==4)return new[]{
      W(.7f,45,"Small swarms arrive close together. Shock or Inferno hits the whole pack.",G(swarm,6,1.2f),G(basic,3,1.5f)),
      W(.75f,55,"Splitters leave two children. Keep splash or chain damage on their route.",G(split,2,1.2f),G(swarm,6,1.3f)),
      W(.9f,65,"Shields need continuous hits to stop regeneration. Keep the road covered.",G(shield,2,1.1f),G(basic,4,1.6f)),
      W(.65f,80,"The final clear includes every Splitter child. Combine control with pack damage.",G(split,3,1.3f),G(swarm,6,1.4f),G(fast,3,1.3f))};
    if(biome==5)return new[]{
      W(.65f,50,"Inferno bursts and Shock chains punish dense groups.",G(swarm,7,1.4f),G(basic,3,1.7f)),
      W(.8f,60,"Shielded bacteria regenerate after a break in damage. Sustain your fire.",G(shield,3,1.25f),G(swarm,5,1.4f)),
      W(.7f,70,"Aura links add damage to adjacent attackers. Protect your strongest coverage.",G(armor,3,1.4f),G(fast,4,1.4f)),
      W(.6f,85,"Mix burst for shields with area damage for the swarm.",G(shield,3,1.4f),G(split,2,1.4f),G(swarm,7,1.5f))};
    if(biome==6)return new[]{
      W(1,55,"Build overlapping coverage across the bends. Support links strengthen adjacent fungi.",G(basic,6,1.8f),G(fast,3,1.4f)),
      W(.85f,65,"Healers protect the pack. Sustained damage and Poison help finish it.",G(heal,1,1.2f),G(armor,3,1.5f),G(basic,4,1.8f)),
      W(.7f,75,"Chains reach through a dense swarm; leave room for Ice near the final bend.",G(swarm,8,1.6f),G(shield,3,1.4f)),
      W(1.2f,95,"Colony Boss warns before armor and rush phases. Poison ignores armor; Ice slows the rush.",G(Phase4Encounter.BossConfig(),1,1.4f),G(fast,4,1.5f),G(basic,4,1.9f))};
    return new[]{
      W(.9f,60,"Snipers reach across bends; Archers work best close to the road.",G(basic,6,2),G(fast,4,1.5f)),
      W(.8f,70,"Shield and armor need different answers: sustained hits and Poison.",G(shield,3,1.5f),G(armor,3,1.7f)),
      W(.65f,85,"Healers and Splitters punish gaps in coverage. Keep pack damage near the exit.",G(heal,1,1.3f),G(split,3,1.5f),G(swarm,7,1.7f)),
      W(1.1f,105,"Mixed finale: watch the boss warning, catch runners, and finish healed armor.",G(Phase4Encounter.BossConfig(),1,1.7f),G(heal,1,1.4f),G(fast,4,1.6f),G(armor,2,1.8f))};
  }
  [MenuItem("Tools/Levels/Apply Biome Challenges")]
  public static void Apply()
  {
    for(int biome=2;biome<=7;biome++)
    {
      var level=AssetDatabase.LoadAssetAtPath<LevelConfig>($"Assets/Resources/Levels/Environment{biome}/Level01.asset");
      level.pathConfig.pathGridCoordinates=Path(biome);level.pathConfig.description=EnvironmentInfo.DisplayName(level.environmentName)+": authored counter challenge.";
      level.waveConfig.waves=Waves(biome);EditorUtility.SetDirty(level.pathConfig);EditorUtility.SetDirty(level.waveConfig);
    }

    AssetDatabase.SaveAssets();Debug.Log("BIOME CHALLENGES: six biome openings and Archer specializations applied in place; save identities retained.");
  }
}
