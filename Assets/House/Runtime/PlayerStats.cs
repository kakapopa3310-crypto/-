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

        /// <summary>
        /// Качество складки 0..1 — влияет только на визуал сгиба.
        /// На цену бумаги сила не влияет.
        /// </summary>
        public static float FoldQuality
        {
            get { return Mathf.Clamp01(StrengthLevel * 0.25f); }
        }

        /// <summary>Высота прыжка постоянная: сила её больше не прокачивает.</summary>
        public static float JumpHeight
        {
            get { return 1f; }
        }

        /// <summary>Дальность броска: короткая, растёт от силы понемногу.</summary>
        public static float ThrowDistance
        {
            get { return 2.2f + (StrengthLevel - 1) * 0.9f; }
        }

        /// <summary>Высота дуги броска — тоже посильнее с ростом силы.</summary>
        public static float ThrowArc
        {
            get { return 0.25f + (StrengthLevel - 1) * 0.12f; }
        }

        public static float SpeedMultiplier
        {
            get { return 1f + (SpeedLevel - 1) * 0.10f; }
        }

        /// <summary>Цена листа: только эффективность, сила не участвует.</summary>
        public static int PaperValue
        {
            get { return 5 + (EfficiencyLevel - 1) * 2; }
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
