﻿namespace Ensage.Common.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Ensage.Common.AbilityInfo;

    /// <summary>
    /// </summary>
    public static class AbilityExtensions
    {
        #region Static Fields

        private static readonly Dictionary<string, double> CastPointDictionary = new Dictionary<string, double>();

        private static readonly Dictionary<string, AbilityData> DataDictionary = new Dictionary<string, AbilityData>();

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     Checks if given ability can be used
        /// </summary>
        /// <param name="ability"></param>
        /// <returns>returns true in case ability can be used</returns>
        public static bool CanBeCasted(this Ability ability)
        {
            //Console.WriteLine((ability == null) + " " + ability.Level + " " + ability.Cooldown + " " + ((ability.Owner as Hero) == null));
            try
            {
                var owner = ability.Owner as Hero;
                if (owner == null)
                {
                    return ability.Level > 0 && ability.Cooldown <= 0;
                }
                if (ability is Item || owner.ClassID != ClassID.CDOTA_Unit_Hero_Invoker)
                {
                    return ability.AbilityState == AbilityState.Ready && ability.Level > 0;
                }
                var spell4 = owner.Spellbook.Spell4;
                var spell5 = owner.Spellbook.Spell5;
                if (ability.Name != "invoker_invoke" && ability.Name != "invoker_quas" && ability.Name != "invoker_wex"
                    && ability.Name != "invoker_exort" && !ability.Equals(spell4) && !ability.Equals(spell5))
                {
                    return false;
                }
                return ability.AbilityState == AbilityState.Ready && ability.Level > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        ///     Checks if given ability can be used
        /// </summary>
        /// <param name="ability"></param>
        /// <param name="target"></param>
        /// <returns>returns true in case ability can be used</returns>
        public static bool CanBeCasted(this Ability ability, Unit target)
        {
            if (!target.IsValidTarget())
            {
                return false;
            }

            var canBeCasted = ability.CanBeCasted();
            if (!target.IsMagicImmune())
            {
                return canBeCasted;
            }

            var data = AbilityDatabase.Find(ability.Name);
            return data == null ? canBeCasted : data.MagicImmunityPierce;
        }

        /// <summary>
        ///     Uses prediction to cast given skillshot ability
        /// </summary>
        /// <param name="ability"></param>
        /// <param name="target"></param>
        /// <returns>returns true in case of successfull cast</returns>
        public static bool CastSkillShot(this Ability ability, Unit target)
        {
            var data = AbilityDatabase.Find(ability.Name);
            var owner = ability.Owner as Unit;
            var delay = Game.Ping / 1000 + ability.FindCastPoint();
            // Console.WriteLine(ability.FindCastPoint());
            var speed = float.MaxValue;
            var radius = 0f;
            delay += ability.GetChannelTime(ability.Level - 1);
            if (data != null)
            {
                if (data.AdditionalDelay > 0)
                {
                    delay += (float)data.AdditionalDelay;
                }
                if (data.Speed != null)
                {
                    speed = ability.GetAbilityData(data.Speed);
                }
                if (data.Width != null)
                {
                    radius = ability.GetAbilityData(data.Width);
                }
            }
            var xyz = Prediction.SkillShotXYZ(owner, target, (float)(delay * 1000), speed, radius);
            xyz = Prediction.SkillShotXYZ(
                owner,
                target,
                (float)((delay + (float)owner.GetTurnTime(xyz)) * 1000),
                speed,
                radius);
            if (!(owner.Distance2D(xyz) <= (ability.GetCastRange() + radius / 2)))
            {
                return false;
            }
            ability.UseAbility(xyz);
            return true;
        }

        /// <summary>
        ///     Uses given ability in case enemy is not disabled or would be chain stunned.
        /// </summary>
        /// <param name="ability"></param>
        /// <param name="target"></param>
        /// <returns>returns true in case of successfull cast</returns>
        public static bool CastStun(this Ability ability, Unit target, float straightTimeforSkillShot = 0, bool chainStun = true)
        {
            if (!ability.CanBeCasted())
            {
                return false;
            }
            var data = AbilityDatabase.Find(ability.Name);
            var owner = ability.Owner;
            var delay = Game.Ping / 1000 + ability.FindCastPoint();
            var radius = 0f;
            if (!ability.AbilityBehavior.HasFlag(AbilityBehavior.NoTarget))
            {
                delay += (float)owner.GetTurnTime(target);
            }
            if (data != null)
            {
                if (data.AdditionalDelay > 0)
                {
                    delay += (float)data.AdditionalDelay;
                }
                if (data.Speed != null)
                {
                    var speed = ability.GetAbilityData(data.Speed);
                    delay += owner.Distance2D(target) / speed;
                }
                if (data.Radius != 0)
                {
                    radius = data.Radius;
                }
                else if (data.StringRadius != null)
                {
                    radius = ability.GetAbilityData(data.StringRadius);
                }
                else if (data.Width != null)
                {
                    radius = ability.GetAbilityData(data.Width);
                }
            }
            var canUse = Utils.ChainStun(target, delay, null, false);
            if (!canUse && chainStun)
            {
                return false;
            }
            if (ability.AbilityBehavior.HasFlag(AbilityBehavior.UnitTarget))
            {
                ability.UseAbility(target);
            }
            else if ((ability.AbilityBehavior.HasFlag(AbilityBehavior.AreaOfEffect)
                     || ability.AbilityBehavior.HasFlag(AbilityBehavior.Point)))
            {
                if (Prediction.StraightTime(target) > straightTimeforSkillShot*1000 && ability.CastSkillShot(target))
                {
                    Utils.Sleep(delay * 1000 + 100, "CHAINSTUN_SLEEP");
                    return true;
                }
                return false;
            }
            else if (ability.AbilityBehavior.HasFlag(AbilityBehavior.NoTarget))
            {
                if (target.Distance2D(owner) > radius)
                {
                    return false;
                }
                ability.UseAbility();
            }
            Utils.Sleep(delay * 1000 + 100, "CHAINSTUN_SLEEP");
            return true;
        }

        /// <summary>
        ///     Returns castpoint of given ability
        /// </summary>
        /// <param name="ability"></param>
        /// <returns></returns>
        public static double FindCastPoint(this Ability ability)
        {
            if (ability is Item)
            {
                return 0;
            }
            if (ability.OverrideCastPoint != -1)
            {
                return 0.1;
            }

            double castPoint;
            if (CastPointDictionary.TryGetValue(ability.Name + " " + ability.Level, out castPoint))
            {
                return castPoint;
            }
            castPoint = ability.GetCastPoint(ability.Level);
            CastPointDictionary.Add(ability.Name + " " + ability.Level, castPoint);
            return castPoint;
        }

        /// <summary>
        ///     Returns ability data with given name, checks if data are level dependent or not
        /// </summary>
        /// <param name="ability"></param>
        /// <param name="dataName"></param>
        /// <param name="level">Custom level</param>
        /// <returns></returns>
        public static float GetAbilityData(this Ability ability, string dataName, uint level = 0)
        {
            var lvl = ability.Level;
            AbilityData data;
            if (!DataDictionary.TryGetValue(ability.Name + "_" + dataName, out data))
            {
                data = ability.AbilityData.FirstOrDefault(x => x.Name == dataName);
                DataDictionary.Add(ability.Name + "_" + dataName, data);
            }
            if (level > 0)
            {
                lvl = level;
            }
            if (data == null)
            {
                return 0;
            }
            return data.Count > 1 ? data.GetValue(lvl - 1) : data.Value;
        }

        /// <summary>
        /// Returns delay before ability is casted
        /// </summary>
        /// <param name="ability"></param>
        /// <param name="source"></param>
        /// <param name="target"></param>
        /// <param name="usePing"></param>
        /// <returns></returns>
        public static double GetCastDelay(this Ability ability, Hero source, Unit target, bool usePing = false)
        {
            var castPoint = Math.Max(ability.FindCastPoint(),0.05);
            if (usePing)
            {
                castPoint += Game.Ping / 1000;
            }
            if (!ability.AbilityBehavior.HasFlag(AbilityBehavior.NoTarget))
            {
                return castPoint + source.GetTurnTime(target);
            }
            return castPoint;
        }

        /// <summary>
        ///     Returns cast range of ability, if ability is NonTargeted it will return its radius!
        /// </summary>
        /// <param name="ability"></param>
        /// <returns></returns>
        public static float GetCastRange(this Ability ability)
        {
            if (ability.Name == "templar_assassin_meld")
            {
                return (ability.Owner as Hero).GetAttackRange() + 50;
            }
            if (!ability.AbilityBehavior.HasFlag(AbilityBehavior.NoTarget))
            {
                var castRange = ability.CastRange;
                var bonusRange = 0;
                if (castRange <= 0)
                {
                    castRange = 999999;
                }
                if (ability.Name == "dragon_knight_dragon_tail"
                    && (ability.Owner as Hero).Modifiers.Any(x => x.Name == "modifier_dragon_knight_dragon_form")) 
                {
                    bonusRange = 250;
                } 
                else if (ability.Name == "beastmaster_primal_roar" && (ability.Owner as Hero).AghanimState()) 
                {
                    bonusRange = 350;
                }
                return castRange + bonusRange + 100;
            }
            var radius = 0f;
            AbilityInfo data;
            if (!AbilityDamage.DataDictionary.TryGetValue(ability, out data))
            {
                data = AbilityDatabase.Find(ability.Name);
                AbilityDamage.DataDictionary.Add(ability, data);
            }
            if (data == null)
            {
                return ability.CastRange;
            }
            if (data.Width != null)
            {
                radius = ability.GetAbilityData(data.Width);
            }
            if (data.StringRadius != null)
            {
                radius = ability.GetAbilityData(data.StringRadius);
            }
            if (data.Radius > 0)
            {
                radius = data.Radius;
            }
            return radius + 50;
        }

        /// <summary>
        ///     Checks if this ability can be casted by Invoker, if the ability is not currently invoked, it is gonna check for
        ///     both invoke and the ability manacost.
        /// </summary>
        /// <param name="ability">given ability</param>
        /// <param name="invoke">invoker ultimate</param>
        /// <param name="spell4">current spell on slot 4</param>
        /// <param name="spell5">current spell on slot 5</param>
        /// <returns></returns>
        public static bool InvoCanBeCasted(this Ability ability, Ability invoke, Ability spell4, Ability spell5)
        {
            var owner = ability.Owner as Hero;
            if (owner == null)
            {
                return false;
            }
            if (!(ability is Item) && ability.Name != "invoker_invoke" && ability.Name != "invoker_quas"
                && ability.Name != "invoker_wex" && ability.Name != "invoker_exort" && !ability.Equals(spell4)
                && !ability.Equals(spell5))
            {
                return invoke.Level > 0 && invoke.Cooldown <= 0 && ability.Cooldown <= 0
                       && (ability.ManaCost + invoke.ManaCost) <= owner.Mana;
            }
            return ability.AbilityState == AbilityState.Ready;
        }

        #endregion
    }
}
