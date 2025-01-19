using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.SharedDataProvider.Providers
{
    public class ReactorProvider : TemplateProvider<string, ReactorData>
    {
        public ReactorProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }

        public override IDictionary<string, ReactorData> LoadAll()
        {
            var ret = new Dictionary<string, ReactorData>();


            foreach (var l0Prop in FileSystem.GetProperty("Reactor", "Reactor.img").PropertyChildren)
            {
                // stuff like etc, sandyBeach

                foreach (var l1Prop in l0Prop.PropertyChildren)
                {
                    // eg nature, artificiality (wtf??)

                    foreach (var l2Prop in l1Prop.PropertyChildren)
                    {
                        // eg vFlowerItem0, boss

                        var info = l2Prop.GetProperty("info");

                        var reactor = new ReactorData
                        {
                            L0 = l0Prop.Name,
                            L1 = l1Prop.Name,
                            L2 = l2Prop.Name,
                            RemoveInFieldSet = info?.GetBool("removeInFieldSet") ?? false,
                            ReqHitCount = info?.GetInt32("hitCount") ?? 0,
                        };

                        if (ret.ContainsKey(reactor.Name))
                        {
                            throw new Exception($"Already loaded a reactor with the name {reactor.Name}");
                        }

                        ret[reactor.Name] = reactor;


                        reactor.States = SelectOverIndexed(l2Prop, (currentState, stateProp) =>
                        {
                            var hitDelay = ReactorData.GetSumDelay(stateProp);

                            var state = new ReactorData.StateData
                            {
                                HitDelay = hitDelay,
                            };

                            var hitProp = stateProp.GetProperty("hit");
                            if (hitProp != null)
                                state.ChangeStateDelay = ReactorData.GetSumDelay(hitProp);
                            else
                                state.ChangeStateDelay = 1000;

                            
                            state.End = stateProp.GetBool("end") ?? false;

                            var eventsProp = stateProp.GetProperty("event");
                            if (eventsProp != null)
                            {
                                state.Timeout = eventsProp.GetInt32("timeOut") ?? 0;

                                state.Events = SelectOverIndexed(eventsProp, (id, eventProp) =>
                                {
                                    var type = (ReactorData.EventData.Types) (eventProp.GetInt32("type") ?? -1);

                                    ReactorData.EventData _event;
                                    if (type == ReactorData.EventData.Types.FindItemUpdate)
                                    {
                                        _event = new ReactorData.FindItemUpdateEventData();
                                    }
                                    else
                                    {
                                        _event = new ReactorData.EventData();
                                    }

                                    _event.ID = (byte) id;
                                    _event.Type = (ReactorData.EventData.Types) (eventProp.GetInt32("type") ?? -1);
                                    _event.StateToBe = eventProp.GetUInt8("state") ?? 0; // defaults to -1 on BMS
                                    _event.Args = ReactorData.LoadArgs<int>(eventProp).ToArray();
                                    _event.HitDelay = state.HitDelay;

                                    var lt = eventProp.Get<WzVector2D>("lt");
                                    var rb = eventProp.Get<WzVector2D>("rb");

                                    if (lt != null && rb != null)
                                    {
                                        _event.CheckArea = Rectangle.FromLTRB(lt.X, lt.Y, rb.X, rb.Y);
                                    }

                                    _event.Load();

                                    return _event;
                                }).ToArray();
                            }

                            return state;
                        }).ToArray();

                        reactor.StateCount = reactor.States.Length;


                        var actionsProp = l2Prop.GetProperty("action");
                        if (actionsProp != null)
                        {
                            reactor.Actions = SelectOverIndexed(actionsProp, (_, actionProp) =>
                            {
                                var type = (ReactorData.ActionData.Types) (actionProp.GetInt32("type") ?? -1);

                                ReactorData.ActionData ret = type switch
                                {
                                    ReactorData.ActionData.Types.Reward => new ReactorData.RewardActionData(),
                                    ReactorData.ActionData.Types.SummonMob => new ReactorData.SummonActionData(),
                                    ReactorData.ActionData.Types.SummonNPC => new ReactorData.SummonNpcActionData(),
                                    ReactorData.ActionData.Types.TransferPlayer => new ReactorData.TransferActionData(),
                                    ReactorData.ActionData.Types.TogglePortal => new ReactorData.TogglePortalActionData(),
                                    _ => throw new NotImplementedException($"{type} not implemented"),
                                };
                                
                                ret.Type = (ReactorData.ActionData.Types) (actionProp.GetInt32("type") ?? -1);
                                ret.State = actionProp.GetInt32("state") ?? -1;
                                ret.Message = actionProp.GetString("message");
                                ret.Args = ReactorData.LoadArgs(actionProp).ToArray();

                                if (ret is ReactorData.TogglePortalActionData tpad)
                                {
                                    tpad.PortalName = actionProp.GetString("pn");
                                }

                                return ret;
                            }).ToArray();
                        }
                    }
                }
            }

            return ret;
        }
    }
}