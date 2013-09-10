﻿// Originally contributed by Chinajade.
//
// LICENSE:
// This work is licensed under the
//     Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// also known as CC-BY-NC-SA.  To view a copy of this license, visit
//      http://creativecommons.org/licenses/by-nc-sa/3.0/
// or send a letter to
//      Creative Commons // 171 Second Street, Suite 300 // San Francisco, California, 94105, USA.

#region Usings
using System;
using System.Collections.Generic;
using System.Linq;

using CommonBehaviors.Actions;
using Styx;
using Styx.CommonBot;
using Styx.TreeSharp;
using Styx.WoWInternals;
using Styx.WoWInternals.WoWObjects;

using Action = Styx.TreeSharp.Action;
#endregion


namespace Honorbuddy.QuestBehaviorCore
{
    public partial class UtilityBehaviorSeq
    {
        public class UseItem : Sequence
        {
            /// <summary>
            /// <para>Uses item defined by WOWITEMDELEGATE on target defined by SELECTEDTARGETDELEGATE.</para>
            /// <para>Notes:<list type="bullet">
            /// <item><description><para>* It is up to the caller to assure that all preconditions have been met for
            /// using the item (i.e., the target is in range, the item is off cooldown, etc).</para></description></item>
            /// <item><description><para> * If item use was successful, BT is provided with RunStatus.Success;
            /// otherwise, RunStatus.Failure is returned (e.g., item is not ready for use,
            /// item use was interrupted by combat, etc).</para></description></item>
            /// <item><description><para>* It is up to the caller to blacklist the target, or select a new target
            /// after successful item use.</para></description></item>
            /// </list></para>
            /// </summary>
            /// <param name="selectedTargetDelegate">may NOT be null.  The target provided by the delegate should be viable.</param>
            /// <param name="wowItemIdDelegate">may NOT be null.  The item provided by the delegate should be viable, and ready for use.</param>
            /// <param name="actionOnMissingItemDelegate"></param>
            /// <param name="actionOnSuccessfulItemUseDelegate"></param>
            /// <returns></returns>
            public UseItem(ProvideIntDelegate wowItemIdDelegate,
                            ProvideWoWObjectDelegate selectedTargetDelegate,
                            Action<object> actionOnMissingItemDelegate,
                            Action<object, WoWObject> actionOnSuccessfulItemUseDelegate = null)
            {
                Contract.Requires(selectedTargetDelegate != null, context => "selectedTargetDelegate != null");
                Contract.Requires(wowItemIdDelegate != null, context => "wowItemDelegate != null");
                Contract.Requires(actionOnMissingItemDelegate != null, context => "actionOnMissingItemDelegate != null");

                ActionOnMissingItemDelegate = actionOnMissingItemDelegate;
                ActionOnSuccessfulItemUseDelegate = actionOnSuccessfulItemUseDelegate ?? ((context, wowObject) => { /*NoOp*/ });
                WowItemIdDelegate = wowItemIdDelegate;
                SelectedTargetDelegate = selectedTargetDelegate;

                Children = CreateChildren();
            }


            // BT contruction-time properties...
            private Action<object> ActionOnMissingItemDelegate { get; set; }
            private Action<object, WoWObject> ActionOnSuccessfulItemUseDelegate { get; set; }
            private ProvideIntDelegate WowItemIdDelegate { get; set; }
            private ProvideWoWObjectDelegate SelectedTargetDelegate { get; set; }

            // BT visit-time properties...
            private WoWItem CachedItemToUse { get; set; }
            private WoWObject CachedTarget { get; set; }
            private bool IsInterrupted { get; set; }


            private List<Composite> CreateChildren()
            {
                return new List<Composite>()
                {
                    // Cache & qualify...
                    new Action(context =>
                    {
                        CachedTarget = SelectedTargetDelegate(context);
                        if (!Query.IsViable(CachedTarget))
                        {
                            QBCLog.Warning("Target is not viable!");
                            return RunStatus.Failure;
                        }

                        if (!Query.IsViable(CachedItemToUse))
                        {
                            // Cached item was invalid, so look for item in our bags, and cache it...
                            var itemId = WowItemIdDelegate(context);
                            CachedItemToUse = Me.CarriedItems.FirstOrDefault(i => (i.Entry == itemId));
                            if (!Query.IsViable(CachedItemToUse))
                            {
                                QBCLog.Error("{0} is not in our bags.", Utility.GetItemNameFromId(itemId));
                                return RunStatus.Failure;
                            }
                        }

                        return RunStatus.Success;
                    }),

                    // Wait for Item to be usable...
                    // NB: WoWItem.Usable does not account for cooldowns.
                    new DecoratorContinue(context => !CachedItemToUse.Usable || (CachedItemToUse.Cooldown > 0),
                        new ActionFail(context =>
                        {
                            TreeRoot.StatusText = string.Format("{0} is not usable, yet. (cooldown remaining: {1})",
                                CachedItemToUse.Name,
                                Utility.PrettyTime(TimeSpan.FromSeconds((int)CachedItemToUse.CooldownTimeLeft.TotalSeconds)));
                        })),

                    // Need to be facing target...
                    // NB: Not all items require this, but many do.
                    new DecoratorContinue(context => !Me.IsSafelyFacing(CachedTarget),
                        new ActionFail(context => Me.SetFacing(CachedTarget.Guid))),

                    // Waits for global cooldown to end to successfully use the item
                    new WaitContinue(TimeSpan.FromMilliseconds(500), 
                        ret => !SpellManager.GlobalCooldown, 
                        new ActionAlwaysSucceed()),

                    // Use the item...
                    new Action(context =>
                    {
                        // Set up 'interrupted use' detection...
                        // MAINTAINER'S NOTE: Once these handlers are installed, make sure all possible exit paths from the outer
                        // Sequence unhook these handlers.  I.e., if you plan on returning RunStatus.Failure, be sure to call
                        // UtilityBehaviorSeq_UseItemOn_HandlersUnhook() first.
                        InterruptDetection_Hook();

                        // Notify user of intent...
                        var message = string.Format("Attempting use of '{0}' on '{1}'", CachedItemToUse.Name, CachedTarget.SafeName);

                        var selectedTargetAsWoWUnit = CachedTarget as WoWUnit;
                        if (selectedTargetAsWoWUnit != null)
                        {
                            if (selectedTargetAsWoWUnit.IsDead)
                                { message += " (dead)"; }
                            else
                                { message += string.Format(" (health: {0:F1})", selectedTargetAsWoWUnit.HealthPercent); }
                        }

                        QBCLog.DeveloperInfo(message);

                        // Do it...
                        IsInterrupted = false;
                        CachedItemToUse.Use(CachedTarget.Guid);
                    }),
                    new WaitContinue(Delay.AfterItemUse, context => false, new ActionAlwaysSucceed()),

                    // If item use requires a second click on the target (e.g., item has a 'ground target' mechanic)...
                    new DecoratorContinue(context => StyxWoW.Me.CurrentPendingCursorSpell != null,
                        new Sequence(
                            new Action(context => { SpellManager.ClickRemoteLocation(CachedTarget.Location); }),
                            new WaitContinue(Delay.AfterItemUse,
                                context => StyxWoW.Me.CurrentPendingCursorSpell == null,
                                new ActionAlwaysSucceed()),
                            // If we've leftover spell cursor dangling, clear it...
                            // NB: This can happen for "use item on location" type activites where you get interrupted
                            // (e.g., a walk-in mob).
                            new DecoratorContinue(context => StyxWoW.Me.CurrentPendingCursorSpell != null,
                                new Action(context => { Lua.DoString("SpellStopTargeting()"); }))
                        )),

                    // Wait for any casting to complete...
                    // NB: Some interactions or item usages take time, and the WoWclient models this as spellcasting.
                    new WaitContinue(TimeSpan.FromSeconds(15),
                        context => !(Me.IsCasting || Me.IsChanneling),
                        new ActionAlwaysSucceed()),

                    // Were we interrupted in item use?
                    new Action(context => { InterruptDectection_Unhook(); }),
                    new DecoratorContinue(context => IsInterrupted,
                        new Sequence(
                            new Action(context => { QBCLog.Warning("Use of {0} interrupted.", CachedItemToUse.Name); }),
                            // Give whatever issue encountered a chance to settle...
                            // NB: Wait, not WaitContinue--we want the Sequence to fail when delay completes.
                            new Wait(TimeSpan.FromMilliseconds(1500), context => false, new ActionAlwaysFail())
                        )),
                    new Action(context =>
                    {
                        QBCLog.DeveloperInfo("Use of '{0}' on '{1}' succeeded.", CachedItemToUse.Name, CachedTarget.SafeName);
                        ActionOnSuccessfulItemUseDelegate(context, CachedTarget);
                    })
                };
            }

            private void HandleInterrupted(object sender, LuaEventArgs args)
            {
                var unitId = args.Args[0].ToString();

                if (unitId == "player")
                {
                    // If it was a channeled spell, and still casting

                    var spellName = args.Args[1].ToString();
                    //var rank = args.Args[2].ToString();
                    //var lineId = args.Args[3].ToString();
                    var spellId = args.Args[4].ToString();

                    QBCLog.DeveloperInfo("\"{0}\"({1}) interrupted via {2} Event.",
                        spellName, spellId, args.EventName);
                    IsInterrupted = true;
                }
            }


            private void InterruptDetection_Hook()
            {
                Lua.Events.AttachEvent("UNIT_SPELLCAST_FAILED", HandleInterrupted);
                Lua.Events.AttachEvent("UNIT_SPELLCAST_INTERRUPTED", HandleInterrupted);
            }


            private void InterruptDectection_Unhook()
            {
                Lua.Events.DetachEvent("UNIT_SPELLCAST_FAILED", HandleInterrupted);
                Lua.Events.DetachEvent("UNIT_SPELLCAST_INTERRUPTED", HandleInterrupted);
            }
        }
    }
}