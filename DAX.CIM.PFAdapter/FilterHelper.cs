using DAX.CIM.PhysicalNetworkModel;
using DAX.CIM.PhysicalNetworkModel.FeederInfo;
using DAX.CIM.PhysicalNetworkModel.Traversal;
using DAX.CIM.PhysicalNetworkModel.Traversal.Extensions;
using DAX.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.CIM.PFAdapter
{
    public static class FilterHelper
    {
        public static List<IdentifiedObject> Filter(CimContext context, FilterRule rule)
        {
            //List<IdentifiedObject> result = new List<IdentifiedObject>();
            Dictionary<string, IdentifiedObject> result = new Dictionary<string, IdentifiedObject>();

            HashSet<string> assetRefs = new HashSet<string>();
            HashSet<string> assetInfoRefs = new HashSet<string>();
            HashSet<string> assetModelRefs = new HashSet<string>();
            HashSet<string> manufacturerRefs = new HashSet<string>();

            HashSet<PhysicalNetworkModel.ConnectivityNode> cnAlreadyWritten = new HashSet<PhysicalNetworkModel.ConnectivityNode>();

            FeederInfoContext feederContext = new FeederInfoContext(context);
            feederContext.CreateFeederObjects();



            foreach (var cimObject in context.GetAllObjects())
            {
                if ((cimObject is PhysicalNetworkModel.ConductingEquipment 
                    && ((PhysicalNetworkModel.ConductingEquipment)cimObject).BaseVoltage >= rule.MinVoltageLevel) 
                    || !(cimObject is PhysicalNetworkModel.ConductingEquipment) 
                    || cimObject is PhysicalNetworkModel.PowerTransformer
                    || cimObject is PhysicalNetworkModel.ExternalNetworkInjection
                    || (cimObject is EnergyConsumer && ((PowerSystemResource)cimObject).PSRType == "Aftagepunkt_fællesmaaling")
                    )
                {

                    if (
                    cimObject is PhysicalNetworkModel.ACLineSegment ||
                    cimObject is PhysicalNetworkModel.BusbarSection ||
                    cimObject is PhysicalNetworkModel.LoadBreakSwitch ||
                    cimObject is PhysicalNetworkModel.Breaker ||
                    cimObject is PhysicalNetworkModel.Disconnector ||
                    cimObject is PhysicalNetworkModel.Fuse ||
                    cimObject is PhysicalNetworkModel.Substation ||
                    cimObject is PhysicalNetworkModel.VoltageLevel ||
                    cimObject is PhysicalNetworkModel.Bay ||
                    cimObject is PhysicalNetworkModel.PowerTransformer ||
                    cimObject is PhysicalNetworkModel.PowerTransformerEnd ||
                    cimObject is PhysicalNetworkModel.ExternalNetworkInjection ||
                    cimObject is PhysicalNetworkModel.PetersenCoil ||
                    cimObject is PhysicalNetworkModel.CurrentTransformer ||
                    cimObject is PhysicalNetworkModel.PotentialTransformer ||
                    cimObject is PhysicalNetworkModel.EnergyConsumer ||
                    cimObject is PhysicalNetworkModel.RatioTapChanger ||
                    cimObject is PhysicalNetworkModel.LinearShuntCompensator/* ||
                    cimObject is PhysicalNetworkModel.Asset ||
                    cimObject is PhysicalNetworkModel.AssetInfo ||
                    cimObject is PhysicalNetworkModel.ProductAssetModel ||
                    cimObject is PhysicalNetworkModel.Manufacturer */
                    )

                    {
                        // Find substation
                        Substation partOfSt = null;

                        if (cimObject is Substation)
                            partOfSt = (Substation)cimObject;

                        if (cimObject.IsInsideSubstation(context))
                            partOfSt = cimObject.GetSubstation(true, context);

                        if (cimObject is ExternalNetworkInjection)
                        {
                            var eni = cimObject as ExternalNetworkInjection;
                            var neighbors = eni.GetNeighborConductingEquipments();

                            if (neighbors.Exists(c => c.IsInsideSubstation() && (rule.IncludeSpecificSubstations == null || rule.IncludeSpecificSubstations.Contains(c.GetSubstation().name))))
                            {
                                var ce = neighbors.First(c => c.IsInsideSubstation() && (rule.IncludeSpecificSubstations == null || rule.IncludeSpecificSubstations.Contains(c.GetSubstation().name)));
                                
                                // put injection inside substation
                                eni.BaseVoltage = ce.BaseVoltage;
                                if (ce is BusbarSection)
                                {
                                    eni.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = context.GetObject<VoltageLevel>(ce.EquipmentContainer.@ref).mRID };
                                }
                                else
                                {
                                    eni.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = context.GetObject<Bay>(ce.EquipmentContainer.@ref).VoltageLevel.@ref };
                                }
                            }
                        }

                        // Tap changer
                        if (cimObject is RatioTapChanger)
                        {
                            var tap = cimObject as RatioTapChanger;

                            if (!(tap.TransformerEnd != null && tap.TransformerEnd.@ref != null && result.ContainsKey(tap.TransformerEnd.@ref)))
                                continue;

                            var ptEnd = context.GetObject<PowerTransformerEnd>(tap.TransformerEnd.@ref);
                        }


                        //  AuxiliaryEquipment - transfer the one pointing to 60 kV breakers only
                        if (cimObject is AuxiliaryEquipment)
                        {
                            var aux = cimObject as AuxiliaryEquipment;

                            // If not connected to terminal, then skip
                            if (aux.Terminal == null)
                                continue;

                            var swTerminal = context.GetObject<Terminal>(aux.Terminal.@ref);
                            var ctObj = context.GetObject<IdentifiedObject>(swTerminal.ConductingEquipment.@ref);

                            if (ctObj is Switch)
                            {
                                Switch ctSw = ctObj as Switch;

                            // If not connedted to breaker, then skip
                                if (!(ctSw is Breaker))
                                    continue;

                                // if not 60000 volt, then skip
                                if (ctSw.BaseVoltage != 60000)
                                    continue;

                                // If bay name contains transformer, skup
                                var swBayName = ctSw.GetBay(true, context).name;
                                if (swBayName.ToLower().Contains("transformer"))
                                    continue;
                            }
                        }


                        // Generel voltage check
                        if (!(cimObject is ConductingEquipment)
                            || rule.MinVoltageLevel == 0
                            || ((ConductingEquipment)cimObject).BaseVoltage == 0
                            || ((ConductingEquipment)cimObject).BaseVoltage >= rule.MinVoltageLevel
                            || (cimObject is EnergyConsumer && ((PowerSystemResource)cimObject).PSRType == "Aftagepunkt_fællesmaaling"))
                        {
                            // Add high voltage measured customer, even if lv modelled
                            if (cimObject is EnergyConsumer && ((EnergyConsumer)cimObject).PSRType == "Aftagepunkt_fællesmaaling" && ((EnergyConsumer)cimObject).BaseVoltage == 400)
                            {
                                var ec = cimObject as EnergyConsumer;


                                if (ec.name == "571313124501138119")
                                {

                                }

                                var ecAclsNeighbors = context.GetNeighborConductingEquipments(ec).Where(c => c is ACLineSegment).ToList();

                                if (ecAclsNeighbors.Count == 1)
                                {
                                    var ecTerminal = context.GetConnections(ec)[0].Terminal;

                                    var aclsTrafoConnections = context.GetNeighborConductingEquipments(ecAclsNeighbors[0]).Where(c => c is PowerTransformer).ToList();

                                    if (aclsTrafoConnections.Count == 1)
                                    {
                                        var trafo = aclsTrafoConnections[0];
                                        var trafoCn = context.GetConnections(trafo).Where(c => c.Terminal.sequenceNumber == "2").ToList();

                                        if (trafoCn.Count == 1)
                                        {
                                            var trafoTerminal2 = trafoCn[0].Terminal;
                                            var trafoTerminal2Cn = trafoCn[0].ConnectivityNode;

                                            context.ConnectTerminalToAnotherConnectitityNode(ecTerminal, trafoTerminal2Cn);

                                            var ptSt = trafo.GetSubstation(false, context);

                                            if (ptSt != null)
                                            {
                                                var stVls = context.GetSubstationVoltageLevels(ptSt);

                                                var vl = stVls.Find(o => o.BaseVoltage == ec.BaseVoltage);

                                                if (vl != null)
                                                {
                                                    ec.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = vl.mRID };
                                                }

                                            }

                                            //ec.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = trafo.EquipmentContainer.@ref };

                                        }

                                    }
                                }
                                else if (ecAclsNeighbors.Count > 1)
                                {
                                    Logger.Log(LogLevel.Warning, "Cannot convert: " + ec.name + " multiply cables connected to customer. Must be modelled in PF.");
                                    continue;
                                }
                            }


                            // If part of substation, check if we should filter away
                            if (partOfSt != null && (rule.IncludeSpecificSubstations == null ||
                                (rule.IncludeSpecificSubstations.Count > 1
                                && !rule.IncludeSpecificSubstations.Contains(partOfSt.name))))
                            {
                                // Don't filter anything away, if not specific substations specified
                                if (rule.IncludeSpecificSubstations != null)
                                {
                                    bool skip = true;

                                    // Check if substation is feeded from included primary substation

                                    if (partOfSt.PSRType == "SecondarySubstation" &&
                                        partOfSt.InternalFeeders != null &&
                                        partOfSt.InternalFeeders.Count > 0 &&
                                        partOfSt.InternalFeeders[0].ConnectionPoint != null &&
                                        partOfSt.InternalFeeders[0].ConnectionPoint.Substation != null &&
                                        partOfSt.InternalFeeders[0].ConnectionPoint.Substation.name != null)
                                    {
                                        var feededStName = partOfSt.InternalFeeders[0].ConnectionPoint.Substation.name;

                                        if (rule.IncludeSpecificSubstations == null || rule.IncludeSpecificSubstations.Contains(feededStName))
                                            skip = false;
                                    }

                                    if (skip)
                                        continue;
                                }
                            }

                            // If acls, check if we should filter away
                            if (cimObject is ACLineSegment && (rule.IncludeSpecificLines == null || rule.IncludeSpecificLines.Count > 1))
                            {
                                bool continueToCheck = true;
                                
                                var acls = cimObject as ACLineSegment;

                                // check if feeded from primary substation
                                var aclsFeeders = feederContext.GeConductingEquipmentFeeders(acls);

                                if (aclsFeeders != null && aclsFeeders.Count > 0)
                                {
                                    if (rule.IncludeSpecificSubstations == null)
                                        continueToCheck = false;
                                    else
                                    {
                                        var feededStName = aclsFeeders[0].ConnectionPoint.Substation.name;

                                        if (rule.IncludeSpecificSubstations == null || rule.IncludeSpecificSubstations.Contains(feededStName))
                                            continueToCheck = false;
                                    }
                                }

                                if (continueToCheck)
                                {
                                    if (acls.PSRType == "InternalCable")
                                    {
                                        var aclsSt = acls.GetSubstation(false, context);

                                        if (!(aclsSt != null && (rule.IncludeSpecificSubstations == null || rule.IncludeSpecificSubstations.Contains(aclsSt.name))))
                                            continue;
                                    }
                                    else if (acls.name != null && acls.name.Contains("#"))
                                    {
                                        var nameSplit = acls.name.Split('#');

                                        var nameWithoutDelStr = nameSplit[0].ToUpper();

                                        if (rule.IncludeSpecificLines != null && !rule.IncludeSpecificLines.Contains(nameWithoutDelStr))
                                            continue;
                                    }
                                    else
                                        continue;
                                }
                            }

                            // If min voltagelevel > 400, don't include cable boxes and stuff inside cable boxes
                            if (rule.MinVoltageLevel > 400
                                && partOfSt != null
                                && 
                                (partOfSt.PSRType == "CableBox" || partOfSt.PSRType == "T-Junction"))
                            {
                                continue;
                            }

                            if (rule.MinVoltageLevel > 400
                               && partOfSt != null
                               && partOfSt.PSRType == "Tower"
                               && partOfSt.GetPrimaryVoltageLevel(context) < 1000)
                            {
                                continue;
                            }



                            // don't add voltage level, we do this later
                            if (cimObject is VoltageLevel)
                            {
                                continue;
                            }

                            result.Add(cimObject.mRID, cimObject);

                            // Add terminals if conducting equipment
                            if (cimObject is ConductingEquipment)
                            {
                                var ci = cimObject as PhysicalNetworkModel.ConductingEquipment;

                                foreach (var tc in context.GetConnections(ci))
                                {
                                    string stName = "";

                                    if (partOfSt != null && partOfSt.name != null)
                                        stName = partOfSt.name + "_";

                                    //tc.Terminal.phases = PhysicalNetworkModel.PhaseCode.ABCN;
                                    tc.Terminal.name = stName + ci.name + "_T" + tc.Terminal.sequenceNumber;
                                    result.Add(tc.Terminal.mRID, tc.Terminal);


                                    // add connectivity node, if not already added
                                    if (!cnAlreadyWritten.Contains(tc.ConnectivityNode))
                                        result.Add(tc.ConnectivityNode.mRID, tc.ConnectivityNode);

                                    cnAlreadyWritten.Add(tc.ConnectivityNode);
                                }
                            }

                            // Add location
                            if (cimObject is PowerSystemResource)
                            {
                                var psrObj = cimObject as PowerSystemResource;

                                if (psrObj.PSRType != "InternalCable")
                                {
                                    if (psrObj.Location != null && psrObj.Location.@ref != null)
                                    {
                                        var loc = context.GetObject<PhysicalNetworkModel.LocationExt>(psrObj.Location.@ref);
                                        result.Add(loc.mRID, loc);
                                    }
                                }
                            }

                            // Add substation voltage levels
                            if (cimObject is Substation)
                            {
                                var psrObj = cimObject as Substation;

                                var voltageLevels = context.GetSubstationVoltageLevels(psrObj);

                                foreach (var vl in voltageLevels)
                                {
                                    result.Add(vl.mRID,vl);
                                }

                            }
                        }
                    }
                }
            }

            // Add protective equipment (relays) 
            foreach (var cimObject in context.GetAllObjects())
            {
                if (cimObject is ProtectionEquipmentExt)
                {
                    var pe = cimObject as ProtectionEquipmentExt;

                    if (pe.ProtectedSwitches != null && pe.ProtectedSwitches.Length > 0 && result.ContainsKey(pe.ProtectedSwitches[0].@ref))
                        result.Add(cimObject.mRID, cimObject);
                }
            }

            //////////////////////////////////////////////////////////////////////////////////7
            // Add Asset stuff
            //////////////////////////////////////////////////////////////////////////////////7

            // Asset ref
            foreach (var cimObject in result.Values)
            {
                if (cimObject is PowerSystemResource)
                {
                    var psr = cimObject as PowerSystemResource;

                    if (psr.Assets != null && psr.Assets.@ref != null)
                        assetRefs.Add(psr.Assets.@ref);
                }
            }

            // Asset
            foreach (var cimObject in context.GetAllObjects())
            {
                if (cimObject is PhysicalNetworkModel.Asset)
                {
                    var asset = cimObject as PhysicalNetworkModel.Asset;

                    if (!assetRefs.Contains(asset.mRID))
                        continue;

                    if (asset.AssetInfo != null && asset.AssetInfo.@ref != null && !assetInfoRefs.Contains(asset.AssetInfo.@ref))
                        assetInfoRefs.Add(asset.AssetInfo.@ref);

                    result.Add(asset.mRID, asset);
                }
            }

            // Asset info
            foreach (var cimObject in context.GetAllObjects())
            {
                if (cimObject is PhysicalNetworkModel.AssetInfo)
                {

                    var assetInfo = cimObject as PhysicalNetworkModel.AssetInfo;

                    if (!assetInfoRefs.Contains(assetInfo.mRID))
                        continue;


                    if (!(cimObject is PhysicalNetworkModel.CurrentTransformerInfoExt ||
                        cimObject is PhysicalNetworkModel.CableInfoExt ||
                        cimObject is PhysicalNetworkModel.OverheadWireInfoExt))
                    {

                    }

                    if (assetInfo.AssetModel != null && assetInfo.AssetModel.@ref != null && !assetModelRefs.Contains(assetInfo.AssetModel.@ref))
                        assetModelRefs.Add(assetInfo.AssetModel.@ref);

                    result.Add(assetInfo.mRID, assetInfo);
                }
            }

            // Asset model
            foreach (var cimObject in context.GetAllObjects())
            {
                if (cimObject is PhysicalNetworkModel.ProductAssetModel)
                {
                    var assetModel = cimObject as PhysicalNetworkModel.ProductAssetModel;

                    if (!assetModelRefs.Contains(assetModel.mRID))
                        continue;

                    if (assetModel.Manufacturer != null && assetModel.Manufacturer.@ref != null && !manufacturerRefs.Contains(assetModel.Manufacturer.@ref))
                        manufacturerRefs.Add(assetModel.Manufacturer.@ref);

                    result.Add(assetModel.mRID, assetModel);
                }
            }

            // Manufacturer
            foreach (var cimObject in context.GetAllObjects())
            {
                if (cimObject is PhysicalNetworkModel.Manufacturer)
                {
                    var manu = cimObject as PhysicalNetworkModel.Manufacturer;

                    if (!manufacturerRefs.Contains(manu.mRID))
                        continue;

                    result.Add(manu.mRID, manu);
                }
            }

            return result.Values.ToList();
        }
    }

    public class FilterRule
    {
        public double MinVoltageLevel { get; set; }
        public HashSet<string> IncludeSpecificSubstations { get; set; }
        public HashSet<string> IncludeSpecificLines { get; set; }
    }
}
