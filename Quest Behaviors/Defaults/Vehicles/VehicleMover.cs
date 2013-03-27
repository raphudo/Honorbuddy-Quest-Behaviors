﻿// Behavior originally contributed by HighVoltz / revamp by Chinajade
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.


#region Summary and Documentation
// VEHICLEMOVER performs the following functions:
// * Looks for an identified vehicle in the nearby area and mounts it
//      The behavior seeks vehicles that have no players nearby.
// * Drives the vehicle to the given destination location or destination NPC
// * Optionally, casts a spell upon arrival at the destination.
//
// BEHAVIOR ATTRIBUTES:
// Basic Attributes:
//      VehicleIdN [REQUIRED Count: 1]
//          Specifies the vehicle that should be mounted and driven back
//          This is the minimum distance the AvoidMobId must be from our safe spot.
//          On occasion, the 'same' vehicle can have multiple IDs.  These numbers
//          identify all the vehicles that can fulfill the need.
//      AuraId_ProxyVehicle [optional; Default: none]
//          If this value is specified, then then VehicleIdN uses a "Eye of Acherus"-like
//          mechanic (http://wowhead.com/npc=28511), instead of the normal WoWclient
//          "vehicle" mechanic.  For a "proxy vehicle like the Eye, there will be
//          no 'eject' button as you find in a normal WoWclient vehicle.
//          A proxy vehicle has the following characteristics:
//              + You are not 'mounted' or 'in a vehicle'--instead you have a particular aura
//              + The vehicle's location is calculated differently
//          The behavior will not find and enter proxy vehicles for you.  You must
//          arrange to use another behavior (e.g., InteractWith).
//      MobId [optional; Default: none]
//          Specifies an NPC to which the vehicle should be delivered.
//      SpellId [optional; Default: none]
//          This is the SpellId of the spell that should be cast when the vehicle
//          has been delivered to the destination.  The spell will be located
//          on the action bar provided by the vehicle.  But please note that
//          this is the _SpellId_, not the _ActionBarIndex_.
//      X/Y/Z [REQUIRED]
//          Specifies the destination location where the vehicle should be delivered.
//          If the vehicle is to be delivered to an NPC instead, this should specify
//          a location within 50 yards or so of where the NPC can be found.
//
// Quest binding:
//      QuestId [optional; Default: none]:
//      QuestCompleteRequirement [Default:NotComplete]:
//      QuestInLogRequirement [Default:InLog]:
//          A full discussion of how the Quest* attributes operate is described in
//          http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
//
// Tunables (ideally, the profile would _never_ provide these arguments):
//      CastNum [optional; Default: 1]
//          This is the number of times we should cast SpellId once we arrive
//          at the destination.
//      CastTime [optional; Default: 1500ms]
//          The number of milliseconds we should wait after casting SpellId,
//          before any other actions are taken.
//      Hop [optional; Default: false]
//          This value serves as an 'unstuck' mechanic.  It forces the bot to jump
//          in the vehicle on its way to the destination.
//      IgnoreCombat [optional; Default: true]
//          If set to true, if we get in combat on the way to acquiring or delivering
//          the vehicle, it will be ignored, and we will doggedly pursue vehicle
//          acquisition and delivery.
//      NonCompeteDistance [optional; Default: 25.0]
//          When we acquire vehicles, we look for vehicles with no competing players
//          nearby.  This value determines the range at which a player will be considered
//          to be in competition for the vehicle of interest to us.
//      Precision [optional; Default 4.0]
//          As we move from waypoint to waypoint in our travel to the destination,
//          this value determines when we've reached the current waypoint, and can
//          proceed to the next.
//
// THINGS TO KNOW:
// * The vehicle may provide an action bar with spells on it.
//      The SpellId is the Id of the spell (as you would look it up on WoWHead). 
//      The SpellId is _not_ the ActionBarIndex value (1-12).
// * An X/Y/Z must always be provided, even if the destination is an NPC (i.e. MobId).
//      We cannot "see" mobs if they are located too far away.  If the destination
//      is ultimately a mob, the X/Y/Z should be in an area within 50 yards or so
//      of the destination Mob.
// * All looting and harvesting is turned off while the event is in progress.
// * The PullDistance is set to 1 while this behavior is in progress.
// * This behavior consults the Quest Behaviors/Data/AuraIds_OccupiedVehicle.xml
//      file for a list of auras that identified which vehicles are occupied
//      and which are available for taking.
#endregion


#region FAQs
// * Do I need a separate InteractWith behavior to mount the vehicle?
//      No, VehicleMover is smart enough to pick and mount an appropriate vehicle
//      for return to the destination.
//
#endregion


#region Examples
// "Death Comes from On High" (http://wowhead.com/quest=12641)
// Drive the Eye of Acherus (http://wowhead.com/npc=28511) to each of four locations,
// and use the Siphon of Acherus spell (http://wowhead.com/spell=51859) upon arriving
// at the location.
// Since the Eye is a "proxy" vehicle, we must use the InteractWith behavior
// on the Eye of Acherus control mechanism (http://wowhead.com/object=191609).
//      <If Condition="!Me.HasAura(51852)"> <!-- HB start/stop protection -->
//          <CustomBehavior File="InteractWith" MobId = "191609" QuestId="12641"
//              ObjectType="GameObject" NumOfTimes = "1" CollectionDistance = "4"
//              X="2345.848" Y="-5696.338" Z="426.0303" />
//          <CustomBehavior File="WaitTimer" WaitTime="25000" />
//      </If>
//      <CustomBehavior File="Vehicles\VehicleMover" QuestId="12641" QuestObjectiveIndex="3"
//          VehicleId="28511" AuraId_ProxyVehicle="51852" SpellId="51859" CastTime="9000"
//          UseNavigator="false" X="1654.104" Y="-5996.521" Z="183.0229"/>
//      <CustomBehavior File="Vehicles\VehicleMover" QuestId="12641" QuestObjectiveIndex="1"
//          VehicleId="28511" AuraId_ProxyVehicle="51852" SpellId="51859" CastTime="9000"
//          UseNavigator="false" X="1799.286" Y="-6003.341" Z="170.4593"/>
//      <CustomBehavior File="Vehicles\VehicleMover" QuestId="12641" QuestObjectiveIndex="2"
//          VehicleId="28511" AuraId_ProxyVehicle="51852" SpellId="51859" CastTime="9000"
//          UseNavigator="false" X="1592.047" Y="-5735.208" Z="196.1772"/>
//      <CustomBehavior File="Vehicles\VehicleMover" QuestId="12641" QuestObjectiveIndex="4"
//          VehicleId="28511" AuraId_ProxyVehicle="51852" SpellId="51859" CastTime="9000"
//          UseNavigator="false" X="1384.774" Y="-5701.124" Z="199.2797"/>
//
// "The Hungry Ettin": Worgen starter quest (http://wowhead.com/quest=14416)
// Steal Mountain Horses (http://wowhead.com/npc=36540) and return them back
// to Lorna Crowley (http://wowhead.com/npc=36457).
//
//      <CustomBehavior File="Vehicles\VehicleMover" QuestId="14416"
//          VehicleID="36540" Precision="2" MobId="36457" X="-2093.622" Y="2259.525" Z="20.98417" />
//
// "Grand Theft Palomino": Death Knight starter quest (http://www.wowhead.com/quest=12680)
// Steal a Havenshire Stallion (http://wowhead.com/npc=28605), Havenshire Mare (http://wowhead.com/npc=28606),
// or Havenshire Colt (http://wowhead.com/npc=28607), and return it to Salanar the Horseman (http://wowhead.com/npc=28653).
// To complete the quest, we have to summon Salanar the Horseman by casting a spell when we arrive at
// the destination.  The spell is provided by the horse vehicle.
//
//      <CustomBehavior File="Vehicles\VehicleMover" VehicleId="28605" VehicleId2="28606" VehicleId3="28607"
//          MobId="28653" SpellId="52264" X="2347.104" Y="-5695.789" Z="155.9568" />
// 
#endregion


#region Usings
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using CommonBehaviors.Actions;
using Styx;
using Styx.Common;
using Styx.CommonBot;
using Styx.CommonBot.Profiles;
using Styx.CommonBot.Routines;
using Styx.Helpers;
using Styx.Pathing;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;
using System.Xml.Linq;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.Quest_Behaviors.Vehicles.VehicleMover
{
    [CustomBehaviorFileName(@"Vehicles\VehicleMover")]
    public class VehicleMover : CustomForcedBehavior
    {
        #region Consructor and Argument Processing
        public VehicleMover(Dictionary<string, string> args)
            : base(args)
        {
            try
            {
                // Primary attributes...
                AuraId_ProxyVehicle = GetAttributeAsNullable<int>("AuraId_ProxyVehicle", false, ConstrainAs.SpellId, null) ?? 0;
                MobIds = GetNumberedAttributesAsArray<int>("MobId", 0, ConstrainAs.MobId, new[] { "MobID", "NpcId" });
                SpellId = GetAttributeAsNullable<int>("SpellId", false, ConstrainAs.SpellId, new[] { "SpellID" }) ?? 0;
                VehicleIds = GetNumberedAttributesAsArray<int>("VehicleId", 1, ConstrainAs.VehicleId, new[] { "VehicleID" });
                Destination = GetAttributeAsNullable<WoWPoint>("", true, ConstrainAs.WoWPointNonEmpty, null) ?? WoWPoint.Empty;

                // Tunables...
                CastNum = GetAttributeAsNullable<int>("CastNum", false, ConstrainAs.RepeatCount, null) ?? 1;
                CastTime = GetAttributeAsNullable<int>("CastTime", false, new ConstrainTo.Domain<int>(0, 30000), null) ?? 1500;
                Hop = GetAttributeAsNullable<bool>("Hop", false, null, null) ?? false;
                IgnoreCombat = GetAttributeAsNullable<bool>("IgnoreCombat", false, null, null) ?? true;
                NonCompeteDistance = GetAttributeAsNullable<double>("NonCompeteDistance", false, new ConstrainTo.Domain<double>(1.0, 40.0), null) ?? 25.0;
                Precision = GetAttributeAsNullable<double>("Precision", false, new ConstrainTo.Domain<double>(2.0, 100.0), null) ?? 4.0;
                PreferNavigator = GetAttributeAsNullable<bool>("UseNavigator", false, null, null) ?? true;

                // QuestRequirement* attributes are explained here...
                //    http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_QuestId_for_Custom_Behaviors
                // ...and also used for IsDone processing.
                QuestId = GetAttributeAsNullable<int>("QuestId", false, ConstrainAs.QuestId(this), null) ?? 0;
                QuestRequirementComplete = GetAttributeAsNullable<QuestCompleteRequirement>("QuestCompleteRequirement", false, null, null) ?? QuestCompleteRequirement.NotComplete;
                QuestRequirementInLog = GetAttributeAsNullable<QuestInLogRequirement>("QuestInLogRequirement", false, null, null) ?? QuestInLogRequirement.InLog;
                QuestObjectiveIndex = GetAttributeAsNullable<int>("QuestObjectiveIndex", false, new ConstrainTo.Domain<int>(1, 5), null) ?? 0;

                // These attributes are no longer used, but here for backward compatibility (it prevents 'complaining' if profiles supply them)...


                // Semantic coherency / covariant dependency checks --

                // For backward compatibility, we do not error off on an invalid SpellId, but merely warn the user...
                if ((1 <= SpellId) && (SpellId <= 12))
                {
                    LogError("SpellId of {0} is not valid--did you accidently provde an ActionBarIndex instead?", SpellId);
                }
            }

            catch (Exception except)
            {
                // Maintenance problems occur for a number of reasons.  The primary two are...
                // * Changes were made to the behavior, and boundary conditions weren't properly tested.
                // * The Honorbuddy core was changed, and the behavior wasn't adjusted for the new changes.
                // In any case, we pinpoint the source of the problem area here, and hopefully it
                // can be quickly resolved.
                LogError("[MAINTENANCE PROBLEM]: " + except.Message
                        + "\nFROM HERE:\n"
                        + except.StackTrace + "\n");
                IsAttributeProblem = true;
            }
        }

        // Attributes provided by caller
        public int AuraId_ProxyVehicle { get; private set; }
        public int CastNum { get; private set; }
        public int CastTime { get; private set; }
        public bool Hop { get; private set; }
        public bool IgnoreCombat { get; private set; }
        public WoWPoint Destination { get; private set; }
        public int[] MobIds { get; private set; }
        public double NonCompeteDistance { get; private set; }
        public double Precision { get; private set; }
        public bool PreferNavigator { get; private set; }
        public int QuestId { get; private set; }
        public int QuestObjectiveIndex { get; private set; }
        public QuestCompleteRequirement QuestRequirementComplete { get; private set; }
        public QuestInLogRequirement QuestRequirementInLog { get; private set; }
        public int SpellId { get; private set; }
        public int[] VehicleIds { get; private set; }

        // DON'T EDIT THESE--they are auto-populated by Subversion
        public override string SubversionId { get { return ("$Id$"); } }
        public override string SubversionRevision { get { return ("$Revision$"); } }
        #endregion


        #region Private and Convenience variables
        public delegate WoWPoint LocationDelegate(object context);
        public delegate string MessageDelegate(object context);
        public delegate double RangeDelegate(object context);

        private IEnumerable<int> AuraIds_OccupiedVehicle { get; set; }
        private readonly TimeSpan Delay_WoWClientMovementThrottle = TimeSpan.FromMilliseconds(250);
        private WoWPoint FinalDestination { get; set; }
        private string FinalDestinationName { get; set; }
        private LocalPlayer Me { get { return StyxWoW.Me; } }
        private bool DidSuccessfullyMount { get; set; }
        private WoWUnit VehicleUnoccupied { get; set; }

        private Composite _behaviorTreeHook_CombatMain = null;
        private Composite _behaviorTreeHook_CombatOnly = null;
        private Composite _behaviorTreeHook_DeathMain = null;
        private Composite _behaviorTreeHook_Main = null;
        private int _castCounter = 0;
        private ConfigMemento _configMemento = null;
        private bool _isBehaviorDone = false;
        private bool _isDisposed;
        private int _pathIndex;
        private WoWPoint _previousLocation = WoWPoint.Empty;
        private Stopwatch _stuckTimer = new Stopwatch();
        #endregion


        #region Destructor, Dispose, and cleanup
        ~VehicleMover()
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

                // NB: we don't unhook _behaviorTreeHook_Main
                // This was installed when HB created the behavior, and its up to HB to unhook it

                if (_behaviorTreeHook_CombatMain != null)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Main", _behaviorTreeHook_CombatMain);
                    _behaviorTreeHook_CombatMain = null;
                }

                if (_behaviorTreeHook_CombatOnly != null)
                {
                    TreeHooks.Instance.RemoveHook("Combat_Only", _behaviorTreeHook_CombatOnly);
                    _behaviorTreeHook_CombatOnly = null;
                }

                if (_behaviorTreeHook_DeathMain != null)
                {
                    TreeHooks.Instance.RemoveHook("Death_Main", _behaviorTreeHook_DeathMain);
                    _behaviorTreeHook_DeathMain = null;
                }

                // Restore configuration...
                if (_configMemento != null)
                {
                    _configMemento.Dispose();
                    _configMemento = null;
                }

                TreeRoot.GoalText = string.Empty;
                TreeRoot.StatusText = string.Empty;

                // Call parent Dispose() (if it exists) here ...
                base.Dispose();
            }

            _isDisposed = true;
        }

        public void BotEvents_OnBotStop(EventArgs args)
        {
            Dispose();
        }
        #endregion


        #region Overrides of CustomForcedBehavior

        protected override Composite CreateBehavior()
        {
            return _behaviorTreeHook_Main ?? (_behaviorTreeHook_Main = CreateMainBehavior());
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
                bool isQuestObjectiveComplete = ((QuestId > 0) && (QuestObjectiveIndex > 0))
                    ? IsQuestObjectiveComplete(QuestId, QuestObjectiveIndex)
                    : false;

                return (_isBehaviorDone     // normal completion
                        || isQuestObjectiveComplete
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
                // The ConfigMemento() class captures the user's existing configuration.
                // After its captured, we can change the configuration however needed.
                // When the memento is dispose'd, the user's original configuration is restored.
                // More info about how the ConfigMemento applies to saving and restoring user configuration
                // can be found here...
                //     http://www.thebuddyforum.com/mediawiki/index.php?title=Honorbuddy_Programming_Cookbook:_Saving_and_Restoring_User_Configuration
                _configMemento = new ConfigMemento();

                BotEvents.OnBotStop += BotEvents_OnBotStop;

                // Disable any settings that may interfere with the escort --
                // When we escort, we don't want to be distracted by other things.
                // NOTE: these settings are restored to their normal values when the behavior completes
                // or the bot is stopped.
                CharacterSettings.Instance.HarvestHerbs = false;
                CharacterSettings.Instance.HarvestMinerals = false;
                CharacterSettings.Instance.LootChests = false;
                CharacterSettings.Instance.NinjaSkin = false;
                CharacterSettings.Instance.SkinMobs = false;
                CharacterSettings.Instance.PullDistance = 1;    // don't pull anything unless we absolutely must

                PlayerQuest quest = StyxWoW.Me.QuestLog.GetQuestById((uint)QuestId);

                TreeRoot.GoalText = string.Format(
                    "{0}: \"{1}\"",
                    this.GetType().Name,
                    ((quest != null) ? ("\"" + quest.Name + "\"") : "In Progress (no associated quest)"));

                TreeRoot.StatusText = string.Format("{0}: Returning VehicleId({1}) to {2}",
                                    this.GetType().Name,
                                    VehicleIds[0],
                                    Destination);

                AuraIds_OccupiedVehicle = GetOccupiedVehicleAuraIds();

                _behaviorTreeHook_CombatMain = CreateBehavior_CombatMain();
                TreeHooks.Instance.InsertHook("Combat_Main", 0, _behaviorTreeHook_CombatMain);
                _behaviorTreeHook_CombatOnly = CreateBehavior_CombatOnly();
                TreeHooks.Instance.InsertHook("Combat_Only", 0, _behaviorTreeHook_CombatOnly);
                _behaviorTreeHook_DeathMain = CreateBehavior_DeathMain();
                TreeHooks.Instance.InsertHook("Death_Main", 0, _behaviorTreeHook_DeathMain);
            }
        }
        #endregion

        
        #region Main Behaviors
        private Composite CreateBehavior_CombatMain()
        {
            return new Decorator(context => !IsDone,
                new PrioritySelector(

                    // Update values for this BT node visit...
                    new Action(context =>
                    {
                        VehicleUnoccupied = FindUnoccupiedVehicle();

                        // Figure out our final destination (i.e., a location or a mob)...
                        // NB: this can change as we travel.  If our destination is a mob,
                        // We can't "see" distant mobs until we get within 100 yards or so of them.
                        // Until we close that distance, we'll head towards the provided location.
                        // As soon as we "see" the mob, we'll switch to the mob as the destination.
                        FinalDestination = Destination;
                        FinalDestinationName = "to destination";
                    
                        if (MobIds.Count() > 0)
                        {
                            // If we can see our destination mob, calculate a path to it...
                            WoWUnit nearestMob = FindUnitsFromIds(MobIds).OrderBy(u => u.Distance).FirstOrDefault();
                            if (nearestMob != null)
                            {
                                // Target destination mob as feedback to the user...
                                if (!Me.GotTarget || (Me.CurrentTarget != nearestMob))
                                    { nearestMob.Target(); }

                                FinalDestination = nearestMob.Location;
                                FinalDestinationName = "to " + nearestMob.Name;
                            }
                        }

                        return RunStatus.Failure;   // fall thru
                    }),

                    // Proceed if we're not in combat, or are ignoring it...
                    new Decorator(context => !Me.Combat || IgnoreCombat,
                        new PrioritySelector(
                            // If we were successfully mounted...
                            // and within a few yards of our destination when we were dismounted, we must
                            // assume we were auto-dismounted, and the behavior is complete...
                            new Decorator(context => DidSuccessfullyMount && !IsInVehicle()
                                                        && (Me.Location.Distance(FinalDestination) < 15.0),
                                new Action(context => { _isBehaviorDone = true; })),

                            // If we're not in a vehicle, go fetch one...
                            new Decorator(context => !IsInVehicle() && IsViable(VehicleUnoccupied),
                                new Action(context =>
                                {
                                    LogInfo("Moving to {0} {1}",
                                        VehicleUnoccupied.Name,
                                        Me.Combat ? "(ignoring combat)" : "");

                                    if (!VehicleUnoccupied.WithinInteractRange)
                                        { Navigator.MoveTo(VehicleUnoccupied.Location); }
                                    else
                                        { VehicleUnoccupied.Interact(); }
                                })),

                            // If we successfully mounted the vehicle, record the fact...
                            new Decorator(context => IsInVehicle() && !DidSuccessfullyMount,
                                new Action(context => { DidSuccessfullyMount = true; })),

                            // Move vehicle to destination...
                            UtilityBehavior_MoveTo(context => FinalDestination,
                                                    context => FinalDestinationName,
                                                    context => Precision),
                            new Decorator(context => Me.IsMoving,
                                new Sequence(
                                    new Action(context => { WoWMovement.MoveStop(); }),
                                    new WaitContinue(TimeSpan.FromMilliseconds(CastTime), context => false, new ActionAlwaysSucceed())
                                )),

                            // Arrived at destination, use spell if necessary...
                            new Decorator(context => ((QuestId <= 0) && (_castCounter >= CastNum))
                                                        || IsQuestObjectiveComplete(QuestId, QuestObjectiveIndex),
                                new Action(context =>
                                {
                                    LogInfo("Behavior complete.");
                                    _isBehaviorDone = true;
                                })),
                            CreateSpellBehavior()
                        )),

                    // Squelch combat, if requested...
                    new Decorator(context => IgnoreCombat,
                        new ActionAlwaysSucceed())
                ));
        }


        private Composite CreateBehavior_CombatOnly()
        {
            return new PrioritySelector(
                // empty, for now
                );
        }


        private Composite CreateBehavior_DeathMain()
        {
            return new PrioritySelector(
                // empty, for now
                );
        }


        private Composite CreateMainBehavior()
        {
            return new PrioritySelector(
                // If quest is done, behavior is done...
                new Decorator(context => IsDone,
                    new Action(context =>
                    {
                        LogInfo("Finished");
                        _isBehaviorDone = true;
                    }))
            );
        }
        #endregion

        
        #region Helpers

        private Composite CreateSpellBehavior()
        {
            string LuaCastSpellCommand = string.Format("CastSpellByID({0})", SpellId);
            string LuaCooldownCommand = string.Format("return GetSpellCooldown({0})", SpellId);
            string LuaRetrieveSpellInfoCommand = string.Format("return GetSpellInfo({0})", SpellId);

            // If we have a spell to cast, one or more times...
            // NB: Since the spell we want to cast is associated with the vehicle, if we get auto-ejected
            // from the vehicle after we arrive at our destination, then there is no way to cast the spell.
            // If we get auto-ejected, we don't try to cast.
            return new Decorator(context => IsInVehicle() && (SpellId > 0),
                new PrioritySelector(
                    // Stop moving so we can cast...
                    new Decorator(context => Me.IsMoving,
                        new Sequence(
                            new Action(context => { WoWMovement.MoveStop(); }),
                            new WaitContinue(TimeSpan.FromMilliseconds(CastTime), context => false, new ActionAlwaysSucceed())
                        )),
                        
                    // If we cannot retrieve the spell info, its a bad SpellId...
                    new Decorator(context => string.IsNullOrEmpty(Lua.GetReturnVal<string>(LuaRetrieveSpellInfoCommand, 0)),
                        new Action(context =>
                        {
                            LogWarning("SpellId({0}) is not known--ignoring the cast", SpellId);
                            _castCounter = CastNum; // force 'done'
                        })),

                    // If the spell is on cooldown, we need to wait...
                    new Decorator(context => Lua.GetReturnVal<double>(LuaCooldownCommand, 1) > 0.0,
                        new Action(context => LogInfo("Waiting for cooldown") )),

                    // Cast the required spell...
                    new Sequence(
                        new Action(context =>
                        {
                            WoWSpell wowSpell = WoWSpell.FromId(SpellId);
                            LogInfo("Casting {0}", (wowSpell != null) ? wowSpell.Name : string.Format("SpellId({0})", SpellId));

                            // NB: we use LUA to cast the spell.  As some vehicle abilities cause
                            // a "Spell not learned" error.  Apparently, HB only keeps up with
                            // permanent spells known by the toon, and not transient spells that become
                            // available in vehicles.
                            Lua.DoString(LuaCastSpellCommand);
                            ++_castCounter;
                        }),
                        new WaitContinue(TimeSpan.FromMilliseconds(CastTime), context => false, new ActionAlwaysSucceed())
                    )
                ));
        }


        private IEnumerable<WoWPlayer> FindPlayersNearby(WoWPoint location, double radius)
        {
            return
                from player in ObjectManager.GetObjectsOfType<WoWPlayer>()
                where
                    player.IsAlive
                    && player.Location.Distance(location) < radius
                select player;
        }


        private IEnumerable<WoWUnit> FindUnitsFromIds(params int[] unitIds)
        {
            ContractRequires(unitIds != null, () => "unitIds argument may not be null");

            return
                from unit in ObjectManager.GetObjectsOfType<WoWUnit>()
                where
                    IsViable(unit)
                    && unitIds.Contains((int)unit.Entry)
                    && (unit.TappedByAllThreatLists || !unit.TaggedByOther)
                select unit;
        }


        private WoWUnit FindUnoccupiedVehicle()
        {
            return
                (from vehicle in FindUnitsFromIds(VehicleIds)
                 where
                    !vehicle.Auras.Values.Any(aura => AuraIds_OccupiedVehicle.Contains(aura.SpellId))
                    && (FindPlayersNearby(vehicle.Location, NonCompeteDistance).Count() <= 0)
                 orderby vehicle.Distance
                 select vehicle)
                 .FirstOrDefault();
        }


        private bool IsInVehicle()
        {
            return Me.InVehicle
                || Me.HasAura(AuraId_ProxyVehicle);
        }


        // 24Feb2013-08:11UTC chinajade
        private bool IsQuestObjectiveComplete(int questId, int objectiveIndex)
        {
            // If quest and objective was not specified, obviously its not complete...
            if ((questId <= 0) || (objectiveIndex <= 0))
                { return false; }

            // If quest is not in our log, obviously its not complete...
            if (Me.QuestLog.GetQuestById((uint)questId) == null)
                { return false; }

            int questLogIndex = Lua.GetReturnVal<int>(string.Format("return GetQuestLogIndexByID({0})", questId), 0);

            return
                Lua.GetReturnVal<bool>(string.Format("return GetQuestLogLeaderBoard({0},{1})", objectiveIndex, questLogIndex), 2);
        }


        private bool IsViable(WoWUnit wowUnit)
        {
            return
                (wowUnit != null)
                && wowUnit.IsValid
                && wowUnit.IsAlive
                && !Blacklist.Contains(wowUnit, BlacklistFlags.Combat);
        }

        
        private WoWPoint LocationObserver()
        {
            if (Me.HasAura(AuraId_ProxyVehicle))
            {
                if (VehicleUnoccupied != null)
                    { return VehicleUnoccupied.Location; }
            }

            return Me.Location;
        }
        #endregion


        #region Utility Behaviors
        private Composite UtilityBehavior_MoveTo(LocationDelegate locationDelegate,
                                                    MessageDelegate locationNameDelegate,
                                                    RangeDelegate precisionDelegate = null)
        {
            ContractRequires(locationDelegate != null, () => "locationRetriever may not be null");
            ContractRequires(locationNameDelegate != null, () => "locationNameDelegate may not be null");
            precisionDelegate = precisionDelegate ?? (context => Navigator.PathPrecision);

            return new PrioritySelector(locationContext => locationDelegate(locationContext),
                new Decorator(locationContext => !IsInVehicle() && !Me.Mounted
                                                    && Mount.CanMount()
                                                    && Mount.ShouldMount((WoWPoint)locationContext),
                    new Action(locationContext => { Mount.MountUp(() => (WoWPoint)locationContext); })),

                new Decorator(locationContext => (LocationObserver().Distance((WoWPoint)locationContext) > precisionDelegate(locationContext)),
                    new Sequence(
                        new Action(locationContext =>
                        {
                            WoWPoint destination = locationDelegate(locationContext);
                            string locationName = locationNameDelegate(locationContext) ?? destination.ToString();

                            LogInfo("Moving " + locationName);

                            MoveResult moveResult = PreferNavigator
                                ? Navigator.MoveTo(destination)
                                : MoveResult.Failed;

                            // If Navigator couldn't move us, resort to click-to-move...
                            if (!((moveResult == MoveResult.Moved)
                                    || (moveResult == MoveResult.ReachedDestination)
                                    || (moveResult == MoveResult.PathGenerated)))
                            {
                                WoWMovement.ClickToMove(destination);
                            }

                            // If 'unstuck' facilities enabled, use them as needed...
                            if (Hop && (!_stuckTimer.IsRunning || _stuckTimer.ElapsedMilliseconds > 2000))
                            {
                                _stuckTimer.Restart();

                                if (_previousLocation.Distance(Me.Location) <= 3)
                                {
                                    WoWMovement.Move(WoWMovement.MovementDirection.JumpAscend | WoWMovement.MovementDirection.StrafeLeft);
                                    WoWMovement.MoveStop(WoWMovement.MovementDirection.JumpAscend | WoWMovement.MovementDirection.StrafeLeft);
                                }
                                _previousLocation = Me.Location;
                            }
                        }),
                        new WaitContinue(Delay_WoWClientMovementThrottle, ret => false, new ActionAlwaysSucceed())
                    ))
                );
        }
        #endregion


        #region XML Parsing
        // never returns null, but the returned Queue may be empty
        public IEnumerable<int> GetOccupiedVehicleAuraIds()
        {
            List<int> occupiedVehicleAuraIds = new List<int>();
            string auraDataFileName = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                                                    GlobalSettings.Instance.QuestBehaviorsPath,
                                                    "DATA",
                                                    "AuraIds_OccupiedVehicle.xml");

            if (!File.Exists(auraDataFileName))
            {
                LogWarning("Unable to locate Occupied Vehicle Aura database (in {0}).  Vehicles will be unqualified"
                    + "--this may cause us to follow vehicles occupied by other players.",
                    auraDataFileName);
                return occupiedVehicleAuraIds;
            }

            XDocument xDoc = XDocument.Load(auraDataFileName);

            foreach (XElement aura in xDoc.Descendants("Auras").Elements())
            {
                string elementAsString = aura.ToString();

                XAttribute spellIdAttribute = aura.Attribute("SpellId");
                if (spellIdAttribute == null)
                {
                    LogError("Unable to locate SpellId attribute for {0}", elementAsString);
                    continue;
                }

                int auraSpellId;
                if (!int.TryParse(spellIdAttribute.Value, out auraSpellId))
                {
                    LogError("Unable to parse SpellId attribute for {0}", elementAsString);
                    continue;
                }

                occupiedVehicleAuraIds.Add(auraSpellId);
            }

            return occupiedVehicleAuraIds;
        }
        #endregion


        #region Diagnostic Methods
                // These are needed by a number of the pre-supplied methods...
        public delegate bool    ContractPredicateDelegate();
        public delegate string  StringProviderDelegate();

        /// <summary>
        /// <para>This is an efficent poor man's mechanism for reporting contract violations in methods.</para>
        /// <para>If the provided ISCONTRACTOKAY evaluates to true, no action is taken.
        /// If ISCONTRACTOKAY is false, a diagnostic message--given by the STRINGPROVIDERDELEGATE--is emitted to the log, along with a stack trace.</para>
        /// <para>This emitted information can then be used to locate and repair the code misusing the interface.</para>
        /// <para>For convenience, this method returns the evaluation if ISCONTRACTOKAY.</para>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * The interface is built in terms of a StringProviderDelegate,
        /// so we don't pay a performance penalty to build an error message that is not used
        /// when ISCONTRACTOKAY is true.</para></description></item>
        /// <item><description><para> * The .NET 4.0 Contract support is insufficient due to the way Buddy products
        /// dynamically compile parts of the project at run time.</para></description></item>
        /// </list></para>
        /// </summary>
        /// <param name="isContractOkay"></param>
        /// <param name="stringProviderDelegate"></param>
        /// <returns>the evaluation of the provided ISCONTRACTOKAY predicate delegate</returns>
        ///  30Jun2012-15:58UTC chinajade
        ///  NB: We could provide a second interface to ContractRequires() that is slightly more convenient for static string use.
        ///  But *please* don't!  If helps maintainers to not make mistakes if they see the use of this interface consistently
        ///  throughout the code.
        public bool ContractRequires(bool isContractOkay, StringProviderDelegate stringProviderDelegate)
        {
            if (!isContractOkay)
            {
                // TODO: (Future enhancement) Build a string representation of isContractOkay if stringProviderDelegate is null
                string      message = stringProviderDelegate() ?? "NO MESSAGE PROVIDED";
                StackTrace  trace   = new StackTrace(1);

                LogError("[CONTRACT VIOLATION] {0}\nLocation:\n{1}", message, trace.ToString());
            }

            return isContractOkay;
        }


        /// <summary>
        /// <para>Returns the name of the method that calls this function. If SHOWDECLARINGTYPE is true,
        /// the scoped method name is returned; otherwise, the undecorated name is returned.</para>
        /// <para>This is useful when emitting log messages.</para>
        /// </summary>
        /// <para>Notes:<list type="bullet">
        /// <item><description><para> * This method uses reflection--making it relatively 'expensive' to call.
        /// Use it with caution.</para></description></item>
        /// </list></para>
        /// <returns></returns>
        ///  7Jul2012-20:26UTC chinajade
        public static string    GetMyMethodName(bool  showDeclaringType   = false)
        {
            var method  = (new StackTrace(1)).GetFrame(0).GetMethod();

            if (showDeclaringType)
                { return (method.DeclaringType + "." + method.Name); }

            return (method.Name);
        }


        /// <summary>
        /// <para>For DEBUG USE ONLY--don't use in production code! (Almost exclusively used by DebuggingTools methods.)</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogDeveloperInfo(string message, params object[] args)
        {
            LogMessage("debug", message, args);
        }
        
        
        /// <summary>
        /// <para>Error situations occur when bad data/input is provided, and no corrective actions can be taken.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogError(string message, params object[] args)
        {
            LogMessage("error", message, args);
        }
        
        
        /// <summary>
        /// <para>Normal information to keep user informed.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogInfo(string message, params object[] args)
        {
            LogMessage("info", message, args);
        }
        
        
        /// <summary>
        /// MaintenanceErrors occur as a result of incorrect code maintenance.  There is usually no corrective
        /// action a user can perform in the field for these types of errors.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        ///  30Jun2012-15:58UTC chinajade
        public void LogMaintenanceError(string message, params object[] args)
        {
            string          formattedMessage    = string.Format(message, args);
            StackTrace      trace               = new StackTrace(1);

            LogMessage("error", "[MAINTENANCE ERROR] {0}\nLocation:\n{1}", formattedMessage, trace.ToString());
        }


        /// <summary>
        /// <para>Used to notify of problems where corrective (fallback) actions are possible.</para>
        /// </summary>
        /// <param name="message"></param>
        /// <param name="args"></param>
        public void LogWarning(string message, params object[] args)
        {
            LogMessage("warning", message, args);
        }
        #endregion
    }
}
