/*
 * Author: Thor Tronrud
 * Updated for v1.3.9
 */

using DistinguishedServiceRedux.settings;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DistinguishedServiceRedux
{
    internal class DSBattleLogic : MissionLogic
    {
        public DSBattleLogic()
        {
            if (PromotionManager.__instance == null)
                return;
            PromotionManager.__instance.nominations = new();
            PromotionManager.__instance.killcounts = new();
        }
        
        private int SumKillCountByNonHero()
        {
            int totalKillCount = 0;
            if (Mission.Current?.PlayerTeam?.ActiveAgents == null)
                return 0;
                
            foreach (Agent ag in Mission.Current.PlayerTeam.ActiveAgents)
            {
                if (ag != null && !ag.IsHero)
                {
                    totalKillCount += ag.KillCount;
                }
            }
            return totalKillCount;
        }
        
        private List<float> GetKillCounts()
        {
            List<float> kills = new();
            if (Mission.Current?.PlayerTeam?.ActiveAgents == null)
                return kills;
                
            foreach (Agent ag in Mission.Current.PlayerTeam.ActiveAgents)
            {
                if (ag != null && !ag.IsHero)
                {
                    kills.Add((float)ag.KillCount);
                }
            }
            return kills;
        }
        
        private double GetPercentile(IEnumerable<float> seq, double percentile)
        {
            var elements = seq.ToArray();
            if (elements.Length == 0)
                return 0;
                
            Array.Sort(elements);
            double realIndex = percentile * (elements.Length - 1);
            int index = (int)realIndex;
            double frac = realIndex - index;
            if (index + 1 < elements.Length)
                return elements[index] * (1 - frac) + elements[index + 1] * frac;
            else
                return elements[index];
        }

        public override void ShowBattleResults()
        {
            if (PromotionManager.__instance == null)
                return;
                
            PromotionManager.__instance.nominations.Clear();
            PromotionManager.__instance.killcounts.Clear();
            
            if (Mission.Current == null || Mission.Current.Mode == MissionMode.Conversation || Mission.Current.Mode == MissionMode.StartUp)
                return;
                
            if (Mission.Current.CombatType == Mission.MissionCombatType.ArenaCombat)
                return;
                
            if (SumKillCountByNonHero() <= 0)
                return;
                
            float qKills = (float)GetPercentile(GetKillCounts(), Settings.Instance.EligiblePercentile);
            
            if (Mission.Current?.PlayerTeam?.ActiveAgents == null)
                return;
                
            foreach (Agent ag in Mission.Current.PlayerTeam.ActiveAgents)
            {
                if (ag == null || ag.IsHero || ag.Origin == null)
                    continue;
                    
                PartyBase originParty = ag.Origin.BattleCombatant as PartyBase;
                if (originParty == null || originParty.MobileParty == null || !originParty.MobileParty.IsMainParty)
                    continue;
                    
                CharacterObject co = CharacterObject.Find(ag.Character.StringId);
                if (co == null)
                    continue;
                    
                if (!PartyBase.MainParty.MemberRoster.Contains(co) || !MobileParty.MainParty.MemberRoster.Contains(co))
                    continue;

                int cutoffKills;
                if (co.IsRanged && co.IsMounted)
                {
                    cutoffKills = Settings.Instance.EligibleKillCountMountedArcher;
                }
                else if (co.IsMounted)
                {
                    cutoffKills = Settings.Instance.EligibleKillCountCavalry;
                }
                else if (co.IsRanged)
                {
                    cutoffKills = Settings.Instance.EligibleKillCountRanged;
                }
                else
                {
                    cutoffKills = Settings.Instance.EligibleKillCountInfantry;
                }
                
                bool qualified = (Settings.Instance.EligiblePercentile <= 0 || ag.KillCount > MathF.Ceiling(qKills));
                if (qualified && ag.KillCount >= cutoffKills)
                {
                    if (PromotionManager.IsSoldierQualified(co))
                    {
                        PromotionManager.__instance.nominations.Add(co);
                        PromotionManager.__instance.killcounts.Add(ag.KillCount);
                    }
                }
            }
            PromotionManager.__instance.OnPCBattleEndedResults();
        }
    }

    internal class DSBattleBehavior : CampaignBehaviorBase
    {
        public DSBattleBehavior()
        {
        }
        
        public override void RegisterEvents()
        {
            CampaignEvents.OnMissionStartedEvent.AddNonSerializedListener((object)this, new Action<IMission>(this.FindBattle));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener((object)this, new Action<CampaignGameStarter>(PromotionManager.AddDialogs));
        }
        
        public override void SyncData(IDataStore dataStore)
        {
        }
        
        public void FindBattle(IMission mission)
        {
            if (mission == null || Mission.Current == null)
                return;
                
            Mission currentMission = Mission.Current;
            
            if (currentMission.CombatType > Mission.MissionCombatType.Combat)
                return;
                
            if (currentMission.Scene == null)
                return;
                
            if (currentMission.HasMissionBehavior<DSBattleLogic>())
            {
                return;
            }
            
            currentMission.AddMissionBehavior(new DSBattleLogic());
        }
    }
}