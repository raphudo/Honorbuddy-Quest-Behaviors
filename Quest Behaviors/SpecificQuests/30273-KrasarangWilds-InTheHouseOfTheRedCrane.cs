// Behavior originally contributed by Natfoth.
//
// WIKI DOCUMENTATION:
//     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Custom_Behavior:_FireFromTheSky
//
// QUICK DOX:
//      Used for the Dwarf Quest SI7: Fire From The Sky
//
//  Notes:
//      * Make sure to Save Gizmo.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;


namespace Honorbuddy.Quest_Behaviors.SpecificQuests.InTheHouseOfTheRedCrane
{
    [CustomBehaviorFileName(@"SpecificQuests\30273-KrasarangWilds-InTheHouseOfTheRedCrane")]
    public class KrasarangWildsHouseOfTheRedCrane : CustomForcedBehavior
    {
        public KrasarangWildsHouseOfTheRedCrane(Dictionary<string, string> args)
            : base(args)
        {

            try
            {
                QuestId = GetAttributeAsNullable("QuestId", false, ConstrainAs.QuestId(this), null) ?? 30273;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogMessage("error", "BEHAVIOR MAINTENANCE PROBLEM: " + except.Message
                                    + "\nFROM HERE:\n"
                                    + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }


        // Attributes provided by caller
        public int QuestId { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }

        // Private variables for internal state
        private bool _isBehaviorDone;
        private bool _isDisposed;
        private Composite _root;

        // Private properties
        private LocalPlayer Me { get { return (StyxWoW.Me); } }

        public static int[] MobIds = new[] { 59687 };
        public static int RedCraneID = 59653;

        public static WoWPoint ShaLocation = new WoWPoint(-1813.47f, 1052.34f, -31.73f);

        public WoWUnit Echo
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>().FirstOrDefault(u => (MobIds.Contains((int)u.Entry) || u.Name.Contains("Echo")) && u.CanSelect && !u.IsDead));
            }
        }

        public WoWUnit Sha
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == 59651 && !u.IsDead && u.CanSelect)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        public WoWUnit Crane
        {
            get
            {
                return (ObjectManager.GetObjectsOfType<WoWUnit>()
                                     .Where(u => u.Entry == RedCraneID && !u.IsDead)
                                     .OrderBy(u => u.Distance).FirstOrDefault());
            }
        }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id: 30273-KrasarangWilds-InTheHouseOfTheRedCrane.cs 664 2013-07-23 12:44:32Z Dogan $"); } }
        public override string SubversionRevision { get { return ("$Revision: 664 $"); } }


        ~KrasarangWildsHouseOfTheRedCrane()
        {
            Dispose(false);
        }


        public void Dispose(bool isExplicitlyInitiatedDispose)
        {
            if (!_isDisposed)
            {
                // NOTE: we should call any Dispose() method for any managed or unmanaged
                // resource, if that resource provides a Dispose() method.

                // Clean up managed resources, if explicit disposal...
                if (isExplicitlyInitiatedDispose)
                {
                    // empty, for now
                }

                // Clean up unmanaged resources (if any) here...
                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

       
        #region Overrides of CustomForcedBehavior

        public bool IsQuestComplete()
        {
            var quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);
            return quest == null || quest.IsCompleted;
        }


        public Composite DoneYet
        {
            get
            {
                return
                    new Decorator(ret => IsQuestComplete(), new Action(delegate
                    {
                        TreeRoot.StatusText = "Finished!";
                        _isBehaviorDone = true;
                        return RunStatus.Success;
                    }));

            }
        }


        public Composite PreCombatStory
        {
            get
            {
                
                return new Decorator(
                    ret => Sha == null,
                    new PrioritySelector(
                        new Decorator(ret => Crane == null,
                                          new Action(delegate
                                                         {
                                                             TreeRoot.StatusText = "Moving to Start Crane Story";
                                                             Navigator.MoveTo(ShaLocation);

                                                             if (Me.IsDead)
                                                             {
                                                                 Lua.DoString("RetrieveCorpse()");
                                                             }
                                                         }

                                              )

                                          ),

                        new Decorator(ret => Crane != null && !Crane.WithinInteractRange && Math.Abs(Crane.Z - -31.73) < .20,
                                      new Action(ret => Navigator.MoveTo(Crane.Location))
                            ),

                        new Decorator(ret => Crane != null && Crane.WithinInteractRange && Math.Abs(Crane.Z - -31.73) < .20,
                                      new Sequence(
                                          new Action(ret => WoWMovement.MoveStop()),
                                          new Action(ret => Crane.Interact()),
                                          new Sleep(400),
                                          new Action(ret => Lua.DoString("SelectGossipOption(1,\"gossip\", true)"))
                                          ))));
            }
        }


        public WoWUnit Priority
        {
            get { 

                if (Echo != null)
                {
                    return Echo;
                }

                if (Sha != null)
                {
                    return Sha;
                }

                return null;

            }
        }


        public Composite CombatStuff
        {
            get
            {
                return new PrioritySelector(
                    
                    new Decorator(r=> Me.CurrentTarget == null && Priority != null, new Action(r=>Priority.Target())),
                    //new Decorator(r=> Echo != null && Sha != null && Me.CurrentTarget != null && Me.CurrentTarget == Sha, new Action(r=>Echo.Target())),

                    //LevelBot.CreateCombatBehavior()
                    
                    new Decorator(r=> !Me.Combat, RoutineManager.Current.PullBehavior),
                    RoutineManager.Current.HealBehavior,
                    RoutineManager.Current.CombatBuffBehavior,
                   RoutineManager.Current.CombatBehavior
                    
                    );
            }
        }


        protected override Composite CreateBehavior()
        {
            return _root ?? (_root = new Decorator(ret => !_isBehaviorDone, new PrioritySelector(DoneYet, PreCombatStory, CombatStuff, new ActionAlwaysSucceed())));
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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
                if (TreeRoot.Current != null && TreeRoot.Current.Root != null && TreeRoot.Current.Root.LastStatus != RunStatus.Running)
                {
                    var currentRoot = TreeRoot.Current.Root;
                    if (currentRoot is GroupComposite)
                    {
                        var root = (GroupComposite)currentRoot;
                        root.InsertChild(0, CreateBehavior());
                    }
                }


                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = GetType().Name + ": " + ((quest != null) ? quest.Name : "In Progress");
            }
        }

        #endregion
    }
}