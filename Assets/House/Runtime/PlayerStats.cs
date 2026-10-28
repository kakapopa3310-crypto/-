using UnityEngine;

namespace PaperHouse
{
    public enum SkillType
    {
        Strength,
        Speed,
        Efficiency
    }

    /// <summary>Прокачка и деньги за сданную бумагу.</summary>
    public static class PlayerStats
    {
        public static int Money;
        public static int StrengthLevel = 1;
        public static int SpeedLevel = 1;
        public static int EfficiencyLevel = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Reset();
        }

        /// <summary>Сколько бумаги складывается за одно действие: 1 сила = 25%.</summary>
        public static float FoldAmount
        {
            get { return Mathf.Clamp01(StrengthLevel * 0.25f); }
        }

        /// <summary>Каждая единица силы добавляет 0.25 м к высоте прыжка.</summary>
        public static float JumpHeight
        {
            get { return 0.75f + StrengthLevel * 0.25f; }
        }

        public static float SpeedMultiplier
        {
            get { return 1f + (SpeedLevel - 1) * 0.10f; }
        }

        public static int PaperValue
        {
            get
            {
                int foldBonus = Mathf.RoundToInt(FoldAmount * 15f);
                int efficiencyBonus = (EfficiencyLevel - 1) * 2;
                return 5 + foldBonus + efficiencyBonus;
            }
        }

        public static int GetLevel(SkillType skill)
        {
            switch (skill)
            {
                case SkillType.Strength: return StrengthLevel;
                case SkillType.Speed: return SpeedLevel;
                case SkillType.Efficiency: return EfficiencyLevel;
                default: return 1;
            }
        }

        public static int UpgradeCost(SkillType skill)
        {
            return 20 + (GetLevel(skill) - 1) * 15;
        }

        public static bool TryUpgrade(SkillType skill)
        {
            int cost = UpgradeCost(skill);
            if (Money < cost) return false;

            Money -= cost;
            switch (skill)
            {
                case SkillType.Strength: StrengthLevel++; break;
                case SkillType.Speed: SpeedLevel++; break;
                case SkillType.Efficiency: EfficiencyLevel++; break;
            }
            return true;
        }

        public static void Reset()
        {
            Money = 0;
            StrengthLevel = 1;
            SpeedLevel = 1;
            EfficiencyLevel = 1;
        }
    }
}
