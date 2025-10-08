using WvsBeta.Common;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.DataValidator
{
    static class MapValidator
    {
        public static void Validate(WzFileSystem fileSystem)
        {
            Console.WriteLine("Validating map data...");

            var properties = fileSystem.GetPropertiesInDirectory("Map/Map");

            foreach (var property in properties)
            {
                if (!int.TryParse(property.Name.Replace(".img", ""), out int mapID)) continue;

                ValidateLayers(property, mapID, fileSystem);
                ValidateLife(property, mapID, fileSystem);
                ValidateReactors(property, mapID, fileSystem);
            }
        }

        /// <summary>
        /// Validates that the map property node contains valid layer data to prevent unexpected client crashes.
        /// </summary>
        /// <param name="property">The property node for the current map.</param>
        /// <param name="mapID">The map ID.</param>
        /// <param name="fileSystem">The WzFileSystem instance.</param>
        private static void ValidateLayers(WzProperty property, int mapID, WzFileSystem fileSystem)
        {
            for (int layer = 0; layer <= 7; layer++)
            {
                var layerNode = property.GetProperty(layer.ToString());
                foreach (var layerSubNode in layerNode.PropertyChildren)
                {
                    switch (layerSubNode.Name)
                    {
                        case "tile":
                            if (layerSubNode.Children.Count == 0) continue;
                            string tS = layerNode.GetProperty("info").GetString("tS");
                            if (tS == null)
                            {
                                Console.WriteLine(string.Format("Map layer tS not found when trying to parse tiles in map {0} at layer {1}.", mapID, layer));
                                continue;
                            }
                            foreach (var tileNode in layerSubNode.PropertyChildren)
                            {
                                int tileIdx = int.Parse(tileNode.Name);
                                string tileName = tileNode.GetString("u");
                                int tileNo = tileNode.GetInt32("no") ?? -1;
                                string tilePath = "Map/Tile/" + tS + ".img/" + tileName + "/" + tileNo;
                                if (!fileSystem.PathExists(tilePath))
                                {
                                    Console.WriteLine(string.Format("Map tile not found at \"{0}\" in map {1} at layer {2}, tile idx {3}.", tilePath, mapID, layer, tileIdx));
                                }
                            }
                            break;
                        case "obj":
                            foreach (var objNode in layerSubNode.PropertyChildren)
                            {
                                int objIdx = int.Parse(objNode.Name);
                                string objectImgName = objNode.GetString("oS") + ".img";
                                string imgSubPath = string.Join("/", objNode.Where(n => n.Key.StartsWith("l")).Select(n => n.Value));
                                string objPath = objectImgName + "/" + imgSubPath;
                                if (!fileSystem.PathExists("Map/Obj/" + objPath))
                                {
                                    Console.WriteLine(string.Format("Map object not found at \"{0}\" in map {1} at layer {2} and object index {3}.", objPath, mapID, layer, objIdx));
                                }
                                if (objNode.GetBool("reactor") == true && !fileSystem.PathExists($"Reactor/{objPath}"))
                                {
                                    Console.WriteLine(string.Format("Reactor object not found at \"{0}\" in map {1} at layer {2} and object index {3}.", objPath, mapID, layer, objIdx));
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            foreach (var backSubNode in property.GetProperty("back").PropertyChildren.Where(i => i.Children.Count > 0))
            {
                string bS = backSubNode.GetString("bS");
                if (string.IsNullOrWhiteSpace(bS)) continue;
                int backIdx = int.Parse(backSubNode.Name);
                string img = bS + ".img";
                int no = backSubNode.GetInt32("no") ?? -1;
                string backPath = "Map/Back/" + img + "/back/" + no;
                if (!fileSystem.PathExists(backPath))
                {
                    Console.WriteLine(string.Format("Map background not found at \"{0}\" in map {1}, back idx {2}.", backPath, mapID, backIdx));
                }
            }
        }

        // <summary>
        /// Validates that the map property node contains valid life data to prevent unexpected client crashes.
        /// </summary>
        /// <param name="property">The property node for the current map.</param>
        /// <param name="mapID">The map ID.</param>
        /// <param name="fileSystem">The WzFileSystem instance.</param>
        private static void ValidateLife(WzProperty property, int mapID, WzFileSystem fileSystem)
        {
            if (!property.HasChild("life")) return;

            var lifeProperties = property
                .GetProperty("life")
                .Select(pair => pair.Value)
                .OfType<WzProperty>();

            foreach (var lifeProperty in lifeProperties)
            {
                int lifeID = (int)Utils.ConvertNameToID(lifeProperty.GetString("id"));
                string lifeType = lifeProperty.GetString("type");

                if (lifeType == "n" && !fileSystem.PathExists($"Npc/{lifeID.ToString().PadLeft(7, '0')}.img"))
                {
                    Console.WriteLine(string.Format("Unknown npc {0} in map {1} at index {2}.", lifeID, mapID, lifeProperty.Name));
                }
                else if (lifeType == "m" && !fileSystem.PathExists($"Mob/{lifeID.ToString().PadLeft(7, '0')}.img"))
                {
                    Console.WriteLine(string.Format("Unknown mob {0} in map {1} at index {2}.", lifeID, mapID, lifeProperty.Name));
                }
            }
        }

        // <summary>
        /// Validates that the map property node contains valid reactor data to prevent unexpected client crashes.
        /// </summary>
        /// <param name="property">The property node for the current map.</param>
        /// <param name="mapID">The map ID.</param>
        /// <param name="fileSystem">The WzFileSystem instance.</param>
        private static void ValidateReactors(WzProperty property, int mapID, WzFileSystem fileSystem)
        {
            if (!property.HasChild("reactor")) return;

            foreach (var kvp in property.GetProperty("reactor").PropertyChildren)
            {
                var idx = short.Parse(kvp.Name);
                var layer = kvp.GetInt16("pageIdx");
                var objIdx = kvp.GetInt16("pieceIdx");

                if (layer == null || objIdx == null)
                {
                    Console.WriteLine($"Found a reactor in map {mapID}, idx {idx}, that is missing either pageIdx or pieceIdx!");
                    continue;
                }

                // Validate if this is actually a prop inside the map

                var objInLayer = property.GetProperty(layer.ToString())?.GetProperty("obj")?.GetProperty(objIdx.ToString());
                if (objInLayer == null)
                {
                    Console.WriteLine($"Found a reactor in map {mapID}, idx {idx}, that does not have a corresponding obj in a layer! Check {layer}/obj/{objIdx}");
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
                    Console.WriteLine($"Found a reactor in map {mapID}, idx {idx}, that points to a non-reactor obj! Check {layer}/obj/{objIdx}");
                    continue;
                }

                var l0 = objInLayer.GetString("l0");
                var l1 = objInLayer.GetString("l1");
                var l2 = objInLayer.GetString("l2");
                var name = objInLayer.GetString("name") ?? l2;
                var reactorPath = $"Reactor/Reactor.img/{l0}/{l1}/{l2}";
                var reactorNode = fileSystem.GetProperty(reactorPath);

                if (reactorNode == null)
                {
                    Console.WriteLine($"Unable to find {reactorPath} for reactor at {mapID} idx {idx}!");
                    continue;
                }
            }
        }
    }
}
