using UnityEngine;
//TODO: Add Damage over time effect (PoisonTower)

[CreateAssetMenu(fileName = "NewTower", menuName = "Tower Defense/Tower")]
public class TowerConfig : ScriptableObject
{
  [Header("Basic Info")]
  public string towerName = "New Tower";
  public GameObject towerPrefab;
  public Sprite towerIcon;
  public int cost = 100;

  [Tooltip("One line, plain language, about what this tower is FOR - the role " +
           "it plays, not its numbers. The stats are shown next to it already. " +
           "Kept short: it has to fit a single bar on a phone in landscape.")]
  [TextArea(2, 3)]
  public string description = "";

  [Header("Attack Properties")]
  public float range = 5f;
  public float fireRate = 1f;
  public int damage = 20;

  [Header("Special Abilities")]
  public bool isAoE = false;
  public float splashRadius = 0f;
  public bool slowsEnemies = false;
  public float slowAmount = 0f;

  [Header("Support")]
  // A support tower never shoots. Instead it raises the damage and fire rate of
  // every attacking tower inside its `range`. Boosts are fractions (0.3 = +30%)
  // and stack additively across overlapping support towers, which is far easier
  // to reason about while balancing than a multiplicative stack.
  public bool isSupport = false;
  [Range(0f, 1f)] public float damageBoost = 0f;
  [Range(0f, 1f)] public float fireRateBoost = 0f;

  // --- Upgrades -------------------------------------------------------------
  //
  // New fields are appended at the END of the class deliberately: Unity
  // re-serialises assets in field order, and several TowerConfig assets have
  // been hand-edited, so inserting above an existing field is how they end up
  // mismatched.
  //
  // The whole point of upgrades here is the structural ceiling in the balance
  // model: player power is capped by buildable cells (~33), so past the mid
  // game the board fills up and gold piles unspent (up to 3,437 at difficulty
  // 67). Upgrades are the only lever that adds power without a free cell.
  //
  // The prices are deliberately BAD value on an open board - maxing a tower
  // costs 9.75x its build price for ~3.4x its output - because the only thing
  // an upgrade buys that a second tower does not is "no cell required". Anything
  // cheaper and the greedy proxy stops building and upgrades instead.
  //
  // These numbers are measured, not guessed. Over five BalanceSim sweeps:
  //   1.0x  -> every level TRIVIAL at 100% health, median towers 24 -> 15.5.
  //           Upgrading a high-coverage cell beat building on the next-best
  //           free one, so the proxy simply stopped filling the board.
  //   2.5x  -> ladder back, but four HARD levels collapsed (d60 to TRIVIAL).
  //   3.5x  -> first upgrade bought at difficulty 48; d1-47 bit-identical to
  //           the pre-upgrade run. THIS IS THE SHIPPED VALUE.
  //   5.0x  -> first upgrade at d53; barely different, because the proxy dumps
  //           its surplus either way. Past ~3.5x the price stops mattering.
  // Growth (1.5 vs 2.0) changed nothing measurable: 92 upgrades spread over 33
  // towers rarely reaches level 3, so the kinder curve was free.
  //
  // What it costs: six verdicts move, ALL at difficulty 60+, all on levels that
  // were sitting on 1,200-3,400 dead gold. Two of the three known losses become
  // winnable; E7L03 (d63) had the smallest surplus and stays a loss. One HARD
  // (d60) softens to TRIVIAL, which is the worst single regression.
  //
  // Read that easing as intended, not as damage: those levels were "hard" only
  // because the player had thousands of gold and nothing to buy with it. And
  // note the real player enters RICHER than the sim models (the wallet carries
  // between levels), so upgrades will do MORE in practice than this shows -
  // which is the argument for having priced them on the conservative side.
  [Header("Upgrades")]
  [Tooltip("1 disables upgrades for this tower.")]
  [Min(1)] public int maxLevel = 3;

  [Tooltip("Compounding per level. 0.6 = +60% damage each level.")]
  public float upgradeDamageStep = 0.6f;
  public float upgradeFireRateStep = 0.15f;
  public float upgradeRangeStep = 0.10f;

  [Tooltip("Compounding per level, for support towers' aura strength.")]
  public float upgradeBoostStep = 0.35f;

  [Tooltip("Cost of the FIRST upgrade, as a fraction of the build cost.")]
  public float upgradeCostScale = 3.5f;
  [Tooltip("Each further upgrade multiplies the previous one by this.")]
  public float upgradeCostGrowth = 1.5f;

  public int MaxLevel => Mathf.Max(1, maxLevel);

  private static float Compound(float step, int level) =>
    Mathf.Pow(1f + Mathf.Max(0f, step), Mathf.Max(0, level - 1));

  public int DamageAt(int level) =>
    Mathf.RoundToInt(damage * Compound(upgradeDamageStep, level));
  public float FireRateAt(int level) =>
    fireRate * Compound(upgradeFireRateStep, level);
  public float RangeAt(int level) =>
    range * Compound(upgradeRangeStep, level);

  // Support auras are the one stat that must stay clamped: TowerBuffs stacks
  // them additively across overlapping supports, and an uncapped aura at level 3
  // under two supports would more than double every attacker on the board.
  public float DamageBoostAt(int level) =>
    Mathf.Min(1f, damageBoost * Compound(upgradeBoostStep, level));
  public float FireRateBoostAt(int level) =>
    Mathf.Min(1f, fireRateBoost * Compound(upgradeBoostStep, level));

  // Gold to go from `level` to `level + 1`. Zero once the tower is maxed.
  public int UpgradeCostFrom(int level)
  {
    if (level >= MaxLevel) return 0;
    float c = cost * Mathf.Max(0f, upgradeCostScale) *
              Mathf.Pow(Mathf.Max(1f, upgradeCostGrowth), Mathf.Max(0, level - 1));
    return Mathf.Max(1, Mathf.RoundToInt(c));
  }

  // Everything the player has put into a tower standing at `level`. The sell
  // value is a fraction of THIS, not of the build cost - otherwise upgrading
  // and selling is a pure loss the player only discovers after the fact.
  public int TotalInvested(int level)
  {
    int total = cost;
    for (int i = 1; i < Mathf.Clamp(level, 1, MaxLevel); i++) total += UpgradeCostFrom(i);
    return total;
  }

  public int SellValueAt(int level) => Mathf.RoundToInt(TotalInvested(level) * 0.7f);

  public int sellValue => SellValueAt(1);
}