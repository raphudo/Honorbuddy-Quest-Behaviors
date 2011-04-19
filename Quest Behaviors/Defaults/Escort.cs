using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Styx.Helpers;
using Styx.Logic.BehaviorTree;
using Styx.Logic.Combat;
using Styx.Logic.Pathing;
using Styx.Logic.Questing;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using TreeSharp;
using Action = TreeSharp.Action;


namespace Styx.Bot.Quest_Behaviors.Escort
{
    public class Escort : CustomForcedBehavior
    {
        /// <summary>
        /// Escort by Natfoth
        /// Allows you to follow and/or defend an NPC until the quest is completed
        /// ##Syntax##
        /// QuestId: Required, it is what the bot uses to see if you are done.
        /// NpcId: Id of the Mob to interact with.
        /// X,Y,Z: The general location where theese objects can be found
        /// </summary>
        /// 
        public Escort(Dictionary<string, string> args)
            : base(args)
        {
			try
			{
                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                Counter     = 1;
                Location    = GetXYZAttributeAsWoWPoint("", true, null) ?? WoWPoint.Empty;
                MobId       = GetAttributeAsMobId("MobId", true, new [] { "NpcId" }) ?? 0;
                MovedToTarget = false;
                QuestId     = GetAttributeAsQuestId("QuestId", true, null) ?? 0;
                QuestRequirementComplete = GetAttributeAsEnum<QuestCompleteRequirement>("QuestCompleteRequirement", false, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog    = GetAttributeAsEnum<QuestInLogRequirement>("QuestInLogRequirement", false, null) ?? QuestInLogRequirement.InLog;
			}

			catch (Exception except)
			{
				// Maintenance problems occur for a number of reasons.  The primary two are...
				// * Changes were made to the behavior, and boundary conditions weren't properly tested.
				// * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
				// In any case, we pinpoint the source of the problem area here, and hopefully it
				// can be quickly resolved.
				UtilLogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
										+ "\nFROM HERE:\n"
										+ except.StackTrace + "\n");
				IsAttributeProblem = true;
			}
        }


        public int                      Counter { get; set; }
        public WoWPoint                 Location { get; private set; }
        public int                      MobId { get; private set; }
        public bool                     MovedToTarget { get; private set; }
        public int                      QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement    QuestRequirementInLog { get; private set; }

        private ConfigMemento           _configMemento;
        private bool                    _isBehaviorDone;
        private bool                    _isDisposed;
        private Composite               _root;

        private LocalPlayer             Me { get { return (ObjectManager.Me); } }


        ~Escort()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        public void     Dispose(bool    isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    if (_configMemento != null)
                    {
                        _configMemento.Dispose();
                        _configMemento = null;
                    }
                }

                // Clean up unmanaged resources (if any) here...
                BotEvents.OnBotStop -= BotEvents_OnBotStop;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }


        public void    BotEvents_OnBotStop(EventArgs args)
        {
             Dispose(true);
        }


        public List<WoWUnit> mobList
        {
            get
            {
               return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                    .Where(u => u.Entry == MobId && !u.Dead)
                                    .OrderBy(u => u.Distance).ToList());
            }
        }


        WoWSpell RangeSpell
        {
            get
            {
                switch (Me.Class)
                {
                    case Styx.Combat.CombatRoutine.WoWClass.Druid:
                        return SpellManager.Spells["Starfire"];
                    case Styx.Combat.CombatRoutine.WoWClass.Hunter:
                        return SpellManager.Spells["Arcane Shot"];
                    case Styx.Combat.CombatRoutine.WoWClass.Mage:
                        return SpellManager.Spells["Frost Bolt"];
                    case Styx.Combat.CombatRoutine.WoWClass.Priest:
                        return SpellManager.Spells["Shoot"];
                    case Styx.Combat.CombatRoutine.WoWClass.Shaman:
                        return SpellManager.Spells["Lightning Bolt"];
                    case Styx.Combat.CombatRoutine.WoWClass.Warlock:
                        return SpellManager.Spells["Curse of Agony"];
                    default: // should never get to here but adding this since the compiler complains
                        return SpellManager.Spells["Auto Attack"]; ;
                }
            }
        }


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _root ?? (_root =
                new PrioritySelector(

                            new Decorator(ret => Me.QuestLog.GetQuestById((uint)QuestId) != null && Me.QuestLog.GetQuestById((uint)QuestId).IsCompleted,
                                new Sequence(
                                    new Action(ret => TreeRoot.StatusText = "Finished!"),
                                    new WaitContinue(120,
                                        new Action(delegate
                                        {
                                            _isBehaviorDone = true;
                                            return RunStatus.Success;
                                        }))
                                    )),

                           new Decorator(ret => mobList.Count == 0,
                                new Sequence(
                                        new Action(ret => TreeRoot.StatusText = "Moving To Location - X: " + Location.X + " Y: " + Location.Y),
                                        new Action(ret => Navigator.MoveTo(Location)),
                                        new Action(ret => Thread.Sleep(300))
                                    )
                                ),

                           new Decorator(ret => Me.CurrentTarget != null && Me.CurrentTarget.IsFriendly,
                               new Action(ret => Me.ClearTarget())),

                           new Decorator(
                               ret => mobList.Count > 0 && mobList[0].IsHostile,
                               new PrioritySelector(
                                   new Decorator(
                                       ret => Me.CurrentTarget != mobList[0],
                                       new Action(ret =>
                                           {
                                               mobList[0].Target();
                                               StyxWoW.SleepForLagDuration();
                                           })),
                                   new Decorator(
                                       ret => !Me.Combat,
                                       new PrioritySelector(
                                            new Decorator(
                                                ret => RoutineManager.Current.PullBehavior != null,
                                                RoutineManager.Current.PullBehavior),
                                            new Action(ret => RoutineManager.Current.Pull()))))),


                           new Decorator(
                               ret => mobList.Count > 0 && (!Me.Combat || Me.CurrentTarget == null || Me.CurrentTarget.Dead) && 
                                      mobList[0].CurrentTarget == null && mobList[0].DistanceSqr > 5f * 5f,
                                new Sequence(
                                            new Action(ret => TreeRoot.StatusText = "Following Mob - " + mobList[0].Name + " At X: " + mobList[0].X + " Y: " + mobList[0].Y + " Z: " + mobList[0].Z),
                                            new Action(ret => Navigator.MoveTo(mobList[0].Location)),
                                            new Action(ret => Thread.Sleep(100))
                                       )
                                ),

                           new Decorator(ret => mobList.Count > 0 && (Me.Combat || mobList[0].Combat),
                                new PrioritySelector(
                                    new Decorator(
                                        ret => Me.CurrentTarget == null && mobList[0].CurrentTarget != null,
                                        new Sequence(
                                        new Action(ret => mobList[0].CurrentTarget.Target()),
                                        new Action(ret => StyxWoW.SleepForLagDuration()))),
                                    new Decorator(
                                        ret => !Me.Combat,
                                        new PrioritySelector(
                                            new Decorator(
                                                ret => RoutineManager.Current.PullBehavior != null,
                                                RoutineManager.Current.PullBehavior),
                                            new Action(ret => RoutineManager.Current.Pull())))))

                        )
                    );
        }


        public override void   Dispose()
        {
            Dispose(true);
        }


        public override bool IsDone
        {
            get
            {
                return (_isBehaviorDone     // normal completion
                        || !UtilIsProgressRequirementsMet(QuestId, QuestRequirementInLog, QuestRequirementComplete));
            }
        }


        public override void OnStart()
        {
            // This reports problems, and stops BT processing if there was a problem with attributes...
            // We had to defer this action, as the 'profile line number' is not available during the element's
            // constructor call.
            OnStart_HandleAttributeProblem();

            // If the quest is complete, this behavior is already done...
            // So we don't want to falsely inform the user of things that will be skipped.
            if (!IsDone)
            {
                _configMemento = new ConfigMemento();
                BotEvents.OnBotStop  += BotEvents_OnBotStop;

                // Disable any settings that may interfere with the escort --
                // When we escort, we don't want to be distracted by other things.
                // NOTE: these settings are restored to their normal values when the behavior completes
                // or the bot is stopped.
                LevelbotSettings.Instance.HarvestHerbs = false;
                LevelbotSettings.Instance.HarvestMinerals = false;
                LevelbotSettings.Instance.LootChests = false;
                LevelbotSettings.Instance.LootMobs = false;
                LevelbotSettings.Instance.NinjaSkin = false;
                LevelbotSettings.Instance.SkinMobs = false;

                WoWUnit     mob     = ObjectManager.GetObjectsOfType<WoWUnit>()
                                      .Where(unit => unit.Entry == MobId)
                                      .FirstOrDefault();

                TreeRoot.GoalText = "Escorting " + ((mob != null) ? mob.Name : ("Mob(" + MobId + ")"));
            }
        }

        #endregion


        #region Stopgap services (remove when later HBcore drop provides these)

        /// <summary>
        /// <para>This class captures the current Honorbuddy configuration.  When the memento is Dispose'd
        /// the configuration that existed when the memento was created is restored.</para>
        /// <para>More info about how this class applies to saving and restoring user configuration
        /// can be found here...
        ///     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_Saving_and_Restoring_User_Configuration
        /// </para>
        /// </summary>
        public new sealed class ConfigMemento
        {
            /// <summary>
            /// Creating a memento captures the Honorbuddy configuration that exists when the memento
            /// is created.  You can then alter the Honorbuddy configuration as you wish.  To restore
            /// the configuration to its original state, just Dispose of the memento.
            /// </summary>
            public ConfigMemento()
            {
                _characterSettings = CharacterSettings.Instance.GetXML();
                _levelBotSettings  = LevelbotSettings.Instance.GetXML();
                _styxSettings      = StyxSettings.Instance.GetXML();
            }


            ~ConfigMemento()
            {
                Dispose(false);
            }

            /// <summary>
            /// Disposing of a memento restores the Honorbuddy configuration that existed when
            /// the memento was created.
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            public /*virtual*/ void     Dispose(bool    isExplicitlyInitiatedDispose)
            {
                if (!_isDisposed)
                {
                    // NOTE: we should call any Dispose() method for any managed or unmanaged
                    // resource, if that resource provides a Dispose() method.

                    // Clean up managed resources, if explicit disposal...
                    if (isExplicitlyInitiatedDispose)
                    {
                        if (_characterSettings != null)
                            { CharacterSettings.Instance.LoadFromXML(_characterSettings); }
                        if (_levelBotSettings != null)
                            { LevelbotSettings.Instance.LoadFromXML(_levelBotSettings); }
                        if (_styxSettings != null)
                            { StyxSettings.Instance.LoadFromXML(_styxSettings); }

                        _characterSettings = null;
                        _levelBotSettings = null;
                        _styxSettings = null;
                     }

                    // Clean up unmanaged resources (if any) here...

                    // Call parent Dispose() (if it exists) here ...
                    // base.Dispose();
                }

                _isDisposed = true;
            }

   
            public override string  ToString()
            {
                string      outString   = "";

                if (_isDisposed)
                    { throw (new ObjectDisposedException(this.GetType().Name)); }

                if (_characterSettings != null)
                    { outString += (_characterSettings.ToString() + "\n"); }
                if (_levelBotSettings != null)
                    { outString += (_levelBotSettings.ToString() + "\n"); }
                if (_styxSettings != null)
                    { outString += (_styxSettings.ToString() + "\n"); }
   
                return (outString);
            }
   
            private System.Xml.Linq.XElement        _characterSettings;
            bool                                    _isDisposed         = false;
            private System.Xml.Linq.XElement        _levelBotSettings;
            private System.Xml.Linq.XElement        _styxSettings;             
        }

        #endregion
    }
}
