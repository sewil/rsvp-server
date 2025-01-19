using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using log4net;
using WvsBeta.Common;
using WvsBeta.SharedDataProvider.Providers;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.Game.GameObjects
{
    public class MapProvider : TemplateProvider<Map>
    {
        private new static ILog _log = LogManager.GetLogger(typeof(MapProvider));

        public static IDictionary<int, Map> Maps { get; private set; }

        public static void Load()
        {
            var fileSystem = new WzFileSystem();
            fileSystem.Init(Path.Combine(Environment.CurrentDirectory, "..", "DataSvr"));

            Maps = new MapProvider(fileSystem).LoadAll();
            _log.Info($"Maps: {Maps.Count}");

            UpdateFM();
        }

        const int HenesysFM = 100000110;
        const int PerionFM = 102000100;
        const int LudiFM = 220000200;
        const int ElNathFM = 211000110;

        public const int CurrentFM = HenesysFM;
        private static void UpdateFM()
        {
            var otherMaps = new[]
            {
                HenesysFM,
                PerionFM,
                LudiFM,
                ElNathFM,
            };

            foreach (var oldMapID in otherMaps)
            {
                if (oldMapID == CurrentFM) continue;

                for (var i = 0; i <= 12; i++)
                {
                    if (!Maps.TryGetValue(oldMapID + i, out var oldMap)) break;

                    _log.Info($"[UpdateFM] Patching {oldMap.ID} to point to {CurrentFM}");
                    oldMap.ForcedReturn = CurrentFM;
                    oldMap.OnBanishAllUsers();
                }
            }
        }

        public static void FinishLoading()
        {
            // Cleanup map life

            foreach (var map in Maps.Values)
            {
                var mg = map.MobGen.ToList();
                foreach (var mgi in mg)
                {
                    if (DataProvider.Mobs.ContainsKey(mgi.ID)) continue;
                    _log.Info($"Removing mob {mgi.ID} from map {map}, as it does not exist");
                    map.MobGen.Remove(mgi);
                }

                var npcs = map.NPCs.ToList();
                foreach (var npc in npcs)
                {
                    if (DataProvider.NPCs.ContainsKey(npc.ID)) continue;
                    _log.Info($"Removing NPC {npc.ID} from map {map}, as it does not exist");
                    map.NPCs.Remove(npc);
                }


                foreach (var portal in map.Portals)
                {
                    if (portal.Value.ToMapID == Constants.InvalidMap) continue;
                    if (!Maps.TryGetValue(portal.Value.ToMapID, out var otherMap))
                    {
                        _log.Warn($"Portal {portal.Key} in map {map} points to an unknown map ({portal.Value.ToMapID})");
                    }
                    else if (!otherMap.Portals.ContainsKey(portal.Value.ToName))
                    {
                        _log.Warn($"Portal {portal.Key} in map {map} points to an unknown portal (in other map) ({portal.Value.ToMapID}, {portal.Value.ToName})");
                    }
                }

                map.Reset(false);
            }
        }

        public MapProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }

        
        internal enum FieldType
        {
            Default = 0,
            Snowball = 1,
            Contimove = 2,
            Tournament = 3,
            Coconut = 4,
            OXQuiz = 5,
            PersonalTimeLimit = 6,
            WaitingRoom = 7,

            LimitedView = 9,


            // Custom

            AlienHunt = 100,
            Elimination = 101,
        }

        public override IDictionary<int, Map> LoadAll()
        {
            var mapNames = new Dictionary<int, (string name, string streetName)>();

            foreach (var categoryNode in FileSystem.GetProperty("String/Map.img").PropertyChildren)
            {
                foreach (var mapNode in categoryNode.PropertyChildren)
                {
                    if (!int.TryParse(mapNode.Name, out var mapID)) continue;

                    var mapName = mapNode.GetString("mapName");
                    var streetName = mapNode.GetString("streetName");

                    mapNames[mapID] = (mapName, streetName);
                }
            }

            // Get all .img's in Map/Map with numeric names
            var properties = FileSystem.GetPropertiesInDirectory("Map/Map");

            return IterateAllToDict(properties, property =>
            {
                if (!int.TryParse(property.Name.Replace(".img", ""), out var ID)) return null;

                var infoNode = property.GetProperty("info");

                var fieldType = infoNode.HasChild("fieldType") ? infoNode.GetUInt8("fieldType") ?? 0 : 0;

                if (ID == 109100000) fieldType = 100;
                else if (ID == 109100001) fieldType = 7;

                Map map;
                switch ((FieldType)fieldType)
                {
                    case FieldType.Default:
                    case FieldType.LimitedView: // TODO: LimitedView
                        map = new Map(ID);
                        break;

                    case FieldType.Snowball: // Snowball 109060000
                        map = new Map_Snowball(ID, property);
                        break;

                    case FieldType.Contimove: // Contimove 101000300
                        map = new Map(ID);
                        break;

                    case FieldType.Tournament: // Tournament 109070000
                        map = new Map_Tournament(ID);
                        break;

                    case FieldType.Coconut: // Coconut harvest 109080000
                        map = new Map_Coconut(ID, property);
                        break;

                    case FieldType.OXQuiz:
                        map = new Map_OXQuiz(ID);
                        break;

                    case FieldType.PersonalTimeLimit: // JQ maps and such
                        map = new Map_PersonalTimeLimit(ID);
                        break;

                    case FieldType.WaitingRoom: // Snowball entry map 109060001
                        map = new Map_WaitingRoom(ID);
                        break;

                    case FieldType.AlienHunt:
                        map = new Map_AlienHunt(ID);
                        break;

                    case FieldType.Elimination:
                        map = new Map_Elimination(ID);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException($"Unknown FieldType found!!! {fieldType}");
                }

                if (mapNames.TryGetValue(ID, out var kvp))
                {
                    map.Name = kvp.name;
                    map.StreetName = kvp.streetName;
                }
                else
                {
                    map.Name = "???";
                    map.StreetName = "???";
                }

                map.HasClock = property.HasChild("clock");

                int VRLeft = infoNode.GetInt32("VRLeft") ?? 0;
                int VRTop = infoNode.GetInt32("VRTop") ?? 0;
                int VRRight = infoNode.GetInt32("VRRight") ?? 0;
                int VRBottom = infoNode.GetInt32("VRBottom") ?? 0;

                if (infoNode.HasChild("decHP"))
                {
                    map.DecreaseHP = infoNode.GetInt16("decHP") ?? 0;
                }
                else if (infoNode.HasChild("recovery"))
                {
                    map.DecreaseHP = (short)-(infoNode.GetInt16("recovery") ?? 0);
                }

                map.TimeLimit = infoNode.GetInt16("timeLimit") ?? 0;
                map.Swim = infoNode.GetBool("swim") ?? false;
                map.ForcedReturn = infoNode.GetInt32("forcedReturn") ?? 0;
                map.ReturnMap = infoNode.GetInt32("returnMap") ?? 0;
                map.Town = infoNode.GetBool("town") ?? false;
                map.AcceptPersonalShop = infoNode.GetBool("personalShop") ?? false;
                map.DisableScrolls = infoNode.GetBool("scrollDisable") ?? false;
                map.EverlastingDrops = infoNode.GetBool("everlast") ?? false;
                map.DisableGoToCashShop = infoNode.GetBool("bUnableToShop") ?? false;
                map.DisableChangeChannel = infoNode.GetBool("bUnableToChangeChannel") ?? false;
                map.MobRate = infoNode.GetFloat("mobRate") ?? 1.0f;
                map.Limitations = (FieldLimit)(infoNode.GetInt32("fieldLimit") ?? 0);
                map.ProtectItem = infoNode.GetInt32("protectItem") ?? 0;
                map.HideRewardInfo = infoNode.GetBool("hideRewardInfo") ?? false;

                // Invert personal shop setting, because we pretty much accept them anywhere.
                map.AcceptPersonalShop = !map.AcceptPersonalShop;

                // todo: log unhandled nodes

                if (map.ReturnMap == Constants.InvalidMap)
                {
                    _log.Debug($"No return map for {map}");
                    if (map.ForcedReturn == Constants.InvalidMap)
                    {
                        _log.Debug($"Also no forced return map for {map}");
                    }
                }

                var footholds = property.GetProperty("foothold").PropertyChildren //layers
                    .SelectMany(c => c.PropertyChildren) // platform
                    .SelectMany(c => c.PropertyChildren) // footholds
                    .Select(p => new Foothold
                    {
                        ID = Convert.ToInt16(p.Name),
                        NextIdentifier = p.GetInt16("next") ?? 0,
                        PreviousIdentifier = p.GetInt16("prev") ?? 0,
                        X1 = p.GetInt16("x1") ?? 0,
                        X2 = p.GetInt16("x2") ?? 0,
                        Y1 = p.GetInt16("y1") ?? 0,
                        Y2 = p.GetInt16("y2") ?? 0
                    })
                    .ToList();

                map.MapAreas.AddRange(property.GetProperty("area")?
                    .PropertyChildren
                    .Select(areaProperty => new MapArea
                    {
                        Name = areaProperty.Name,
                        Area = Rectangle.FromLTRB(
                            areaProperty.GetInt32("x1") ?? 0,
                            areaProperty.GetInt32("y1") ?? 0,
                            areaProperty.GetInt32("x2") ?? 0,
                            areaProperty.GetInt32("y2") ?? 0
                        )
                    }) ?? Array.Empty<MapArea>());

                map.SetFootholds(footholds);

                map.GenerateMBR(Rectangle.FromLTRB(VRLeft, VRTop, VRRight, VRBottom));

                ReadLife(property, map);
                ReadPortals(property, map);
                ReadSeats(property, map);
                ReadReactors(property, map, FileSystem);

                map.LoadExtraData(property, infoNode);

                return map;
            }, map => map.ID);
        }

        private static void ReadReactors(WzProperty property, Map map, WzFileSystem FileSystem)
        {
            if (!property.HasChild("reactor")) return;

            foreach (var kvp in property.GetProperty("reactor").PropertyChildren)
            {
                var idx = short.Parse(kvp.Name);
                var layer = kvp.GetInt16("pageIdx");
                var objIdx = kvp.GetInt16("pieceIdx");

                if (layer == null || objIdx == null)
                {
                    _log.Error($"Found a reactor in map {map}, idx {idx}, that is missing either pageIdx or pieceIdx!");
                    continue;
                }

                // Validate if this is actually a prop inside the map

                var objInLayer = property.GetProperty(layer.ToString())?.GetProperty("obj")?.GetProperty(objIdx.ToString());
                if (objInLayer == null)
                {
                    _log.Error($"Found a reactor in map {map}, idx {idx}, that does not have a corresponding obj in a layer! Check {layer}/obj/{objIdx}");
                    continue;
                }

                // Check if its a reactor
                var reactorProp = objInLayer.GetInt16("reactor");
                var oS = objInLayer.GetString("oS");
                if (reactorProp == null ||
                    reactorProp.Value != 1 ||
                    oS == null ||
                    oS != "Reactor")
                {
                    _log.Error($"Found a reactor in map {map}, idx {idx}, that points to a non-reactor obj! Check {layer}/obj/{objIdx}");
                    continue;
                }

                map.UsableReactorIDs.Add(idx);

                var l0 = objInLayer.GetString("l0");
                var l1 = objInLayer.GetString("l1");
                var l2 = objInLayer.GetString("l2");
                var reactorPath = $"Reactor/Reactor.img/{l0}/{l1}/{l2}";
                var reactorNode = FileSystem.GetProperty(reactorPath);

                if (reactorNode == null)
                {
                    _log.Error($"Unable to find {reactorPath} for reactor at {map} idx {idx}!");
                    continue;
                }

                var reactor = new Reactor(
                    map,
                    idx,
                    0,
                    objInLayer.GetInt16("x") ?? 0,
                    objInLayer.GetInt16("y") ?? 0,
                    objInLayer.GetUInt8("z") ?? 0,
                    objInLayer.GetUInt8("zM") ?? 0,
                    l2
                );

                reactor.PieceID = objIdx.Value;
                reactor.PageID = layer.Value;

                reactor.RegenInterval = (objInLayer.GetInt32("reactorTime") ?? 0) * 1000;

                if (reactor.RegenInterval > 0)
                {
                    var baseTime = reactor.RegenInterval / 10;
                    reactor.RegenAfter = baseTime + Rand32.Next() % (baseTime * 6);
                    reactor.RegenAfter += MasterThread.CurrentTime;
                }

                if (false)
                {
                    if (reactor.Template.Actions.Any(x => x is ReactorData.SummonActionData))
                    {
                        Trace.WriteLine($"Found map with summoning reactor {reactor}");
                    }
                    else if (reactor.Template.Actions.Any(x => x is ReactorData.SummonNpcActionData))
                    {
                        Trace.WriteLine($"Found map with npc summoning reactor {reactor}");
                    }
                    else if (reactor.Template.Actions.Any(x => x is ReactorData.TransferActionData))
                    {
                        Trace.WriteLine($"Found map with transfer reactor {reactor}");
                    }
                }

                map.AddReactor(reactor);
            }
        }

        private static void ReadSeats(WzProperty property, Map map)
        {
            if (!property.HasChild("seat")) return;

            foreach (var seatProperty in property.GetProperty("seat"))
            {
                var point = (WzVector2D)seatProperty.Value;

                map.AddSeat(new Seat
                (
                    (byte)Utils.ConvertNameToID(seatProperty.Key),
                    (short)point.X,
                    (short)point.Y
                ));
            }
        }

        private static void ReadPortals(WzProperty property, Map map)
        {
            if (!property.HasChild("portal")) return;

            byte idx = 0;
            var portalProperties = property
                .GetProperty("portal")
                .Select(pair => pair.Value)
                .OfType<WzProperty>();

            foreach (var portalProperty in portalProperties)
            {
                var portal = new Portal
                {
                    ID = idx++,
                    Enabled = true,
                    Type = portalProperty.GetUInt8("pt") ?? 0,
                    X = portalProperty.GetInt16("x") ?? 0,
                    Y = portalProperty.GetInt16("y") ?? 0,
                    Name = portalProperty.GetString("pn"),
                    ToMapID = portalProperty.GetInt32("tm") ?? 0,
                    ToName = portalProperty.GetString("tn"),
                    Script = portalProperty.GetString("script")
                };

                if (portal.Type == 4 || portal.Type == 5)
                {
                    portal.Enabled = false;
                }

                map.AddPortal(portal);
            }
        }

        private static void ReadLife(WzProperty property, Map map)
        {
            if (!property.HasChild("life")) return;

            var lifeProperties = property
                .GetProperty("life")
                .Select(pair => pair.Value)
                .OfType<WzProperty>();

            foreach (var lifeProperty in lifeProperties)
            {
                map.AddLife(new Life
                {
                    RespawnTime = lifeProperty.GetInt32("mobTime") ?? 0,
                    FacesLeft = lifeProperty.GetBool("f") ?? false,
                    X = lifeProperty.GetInt16("x") ?? 0,
                    Y = lifeProperty.GetInt16("y") ?? 0,
                    Cy = lifeProperty.GetInt16("cy") ?? 0,
                    Rx0 = lifeProperty.GetInt16("rx0") ?? 0,
                    Rx1 = lifeProperty.GetInt16("rx1") ?? 0,
                    Foothold = lifeProperty.GetUInt16("fh") ?? 0,
                    ID = (int)Utils.ConvertNameToID(lifeProperty.GetString("id")),
                    Type = (char)(lifeProperty.HasChild("type") ? char.Parse(lifeProperty.GetString("type")) : 0)
                }, lifeProperty);
            }
        }
    }
}
