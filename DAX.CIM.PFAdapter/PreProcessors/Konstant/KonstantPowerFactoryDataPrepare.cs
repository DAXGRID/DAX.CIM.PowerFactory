using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DAX.CIM.PhysicalNetworkModel;
using DAX.CIM.PhysicalNetworkModel.Traversal;
using DAX.CIM.PhysicalNetworkModel.Traversal.Extensions;
using DAX.IO.CIM;
using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Linemerge;
using DAX.CIM.PFAdapter.PreProcessors;

namespace DAX.CIM.PFAdapter
{
    /// <summary>
    /// Used to process cim data before exporting to Power Factory CIM archive
    /// Konstant specific "hacks"
    /// </summary>
    public class KonstantPowerFactoryDataPrepareAndFix : IPreProcessor
    {
        int _guidOffset = 1000;

        MappingContext _mappingContext;

        public KonstantPowerFactoryDataPrepareAndFix(MappingContext mappingContext)
        {
            _mappingContext = mappingContext;
        }

        public IEnumerable<IdentifiedObject> Transform(CimContext context, IEnumerable<IdentifiedObject> inputParam)
        {
            List<IdentifiedObject> input = inputParam.ToList();

            HashSet<PhysicalNetworkModel.IdentifiedObject> dropList = new HashSet<IdentifiedObject>();
            List<PhysicalNetworkModel.IdentifiedObject> addList = new List<IdentifiedObject>();

            // AssetInfo to Asset ref dictionary
            Dictionary<string, string> assetInfoToAssetRef = new Dictionary<string, string>();

            foreach (var inputCimObject in input)
            {

                if (inputCimObject.name == "571313124501006982" && inputCimObject is EnergyConsumer)
                {

                }

                if (inputCimObject is PhysicalNetworkModel.Asset)
                {
                    var asset = inputCimObject as PhysicalNetworkModel.Asset;

                    if (asset.AssetInfo != null && !assetInfoToAssetRef.ContainsKey(asset.AssetInfo.@ref))
                        assetInfoToAssetRef.Add(asset.AssetInfo.@ref, asset.mRID);
                }
            }

            // Asset to Equipment ref dictionary
            Dictionary<string, string> assetToEquipmentRef = new Dictionary<string, string>();

            foreach (var inputCimObject in input)
            {
                if (inputCimObject is PhysicalNetworkModel.PowerSystemResource)
                {
                    var psr = inputCimObject as PhysicalNetworkModel.PowerSystemResource;
                    if (psr.Assets != null)
                        assetToEquipmentRef.Add(psr.Assets.@ref, psr.mRID);
                }
            }

            // Set busbar names to station + voltagelevel + bay
            foreach (var inputCimObject in input)
            {
                if (inputCimObject is BusbarSection)
                {
                    var bus = inputCimObject as BusbarSection;

                    var vl = context.GetObject<VoltageLevel>(bus.EquipmentContainer.@ref);

                    var st = bus.GetSubstation(true, context);

                    bus.name = st.name + "_" + GetVoltageLevelStr(vl.BaseVoltage) + "_" + bus.name;
                }
            }

            // Set peterson coil name to station + gis name
            foreach (var inputCimObject in input)
            {
                if (inputCimObject is PetersenCoil)
                {
                    var coil = inputCimObject as PetersenCoil;
                                     
                    var st = coil.GetSubstation(true, context);

                    if (coil.Asset != null && coil.Asset.AssetInfo != null && coil.Asset.AssetInfo.@ref != null)
                    {
                        var coilInfo = context.GetObject<PetersenCoilInfoExt>(coil.Asset.AssetInfo.@ref);
                        coil.name = st.name + " " + coil.name + " " + (int)coilInfo.minimumCurrent.Value + "-" + (int)coilInfo.maximumCurrent.Value;
                    }
                    else
                        coil.name = st.name + " " + coil.name + " 0-0";
                }
            }

            // Remove injection > 50 kV
            foreach (var inputCimObject in input)
            {
                if (inputCimObject is ExternalNetworkInjection)
                {
                    var inj = inputCimObject as ExternalNetworkInjection;

                    if (inj.BaseVoltage > 50000)
                    {
                        dropList.Add(inj);

                        var injConnections = context.GetConnections(inj);
                        foreach (var injCon in injConnections)
                            dropList.Add(injCon.Terminal);
                    }
                }
            }

            // Fix and check objects
            foreach (var inputCimObject in input)
            {
                // Prefix energy consumers with "l_"
                if (inputCimObject is EnergyConsumer)
                {
                    if (inputCimObject.name == "571313124501006982")
                    {

                    }

                    if (inputCimObject.name != null)
                        inputCimObject.name = "l_" + inputCimObject.name;
                }

                // Check that current transformer has currents 
                if (inputCimObject is CurrentTransformerInfoExt)
                {
                    var ctInfo = inputCimObject as CurrentTransformerInfoExt;
                    var assetMrid = assetInfoToAssetRef[ctInfo.mRID];
                    var eq = assetToEquipmentRef[assetMrid];

                    var ct = context.GetObject<CurrentTransformerExt>(eq);

                    if (ct.PSRType != null && ct.PSRType == "StromTransformer")
                    {
                        // Make sure primary and secondary current is set, because otherwise PF import fails
                        if (ctInfo.primaryCurrent == null )
                        {
                            var stName = ct.GetSubstation(true, context).name;
                            var bayName = ct.GetBay(true, context).name;

                            System.Diagnostics.Debug.WriteLine("CT Missing primary current: " + stName + " " + bayName);
                            ctInfo.primaryCurrent = new CurrentFlow() { Value = 0, unit = UnitSymbol.A };
                        }

                        if (ctInfo.secondaryCurrent == null)
                        {
                            var stName = ct.GetSubstation(true, context).name;
                            var bayName = ct.GetBay(true, context).name;

                            System.Diagnostics.Debug.WriteLine("CT Missing secondary current: " + stName + " " + bayName);
                            ctInfo.secondaryCurrent = new CurrentFlow() { Value = 0, unit = UnitSymbol.A };
                        }

                    }
                    else
                    {
                        dropList.Add(inputCimObject);
                        dropList.Add(ct);
                    }
                }

                // Check that potential transformer has voltages
                if (inputCimObject is PotentialTransformerInfoExt)
                {
                    var vtInfo = inputCimObject as PotentialTransformerInfoExt;

                    var assetMrid = assetInfoToAssetRef[vtInfo.mRID];
                    var eq = assetToEquipmentRef[assetMrid];

                    var ct = context.GetObject<PotentialTransformer>(eq);

                    // Make sure primary and secondary voltage is set, because otherwise PF import fails
                    if (vtInfo.primaryVoltage == null)
                    {
                        vtInfo.primaryVoltage = new Voltage() { Value = 0, unit = UnitSymbol.V };

                        var stName = ct.GetSubstation(true, context).name;
                        var bayName = ct.GetBay(true, context).name;

                        System.Diagnostics.Debug.WriteLine("VT Missing primary voltage: " + stName + " " + bayName);

                    }

                    if (vtInfo.secondaryVoltage == null)
                    {
                        vtInfo.secondaryVoltage = new Voltage() { Value = 0, unit = UnitSymbol.V };

                        var stName = ct.GetSubstation(true, context).name;
                        var bayName = ct.GetBay(true, context).name;

                        System.Diagnostics.Debug.WriteLine("VT Missing secondary voltage: " + stName + " " + bayName);

                    }

                }


                // Set relay names to station + bay
                if (inputCimObject is ProtectionEquipment)
                {
                    var relay = inputCimObject as ProtectionEquipment;

                    // get relay station and bay via the switch it is connected to
                    if (relay.ProtectedSwitches != null && relay.ProtectedSwitches.Length > 0)
                    {
                        try
                        {
                            var peSw = context.GetObject<PowerSystemResource>(relay.ProtectedSwitches[0].@ref);
                            var bay = peSw.GetBay(true, context);
                            var st = peSw.GetSubstation(true, context);

                            relay.name = st.name + " " + bay.name;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("Cannot find switch: " + relay.ProtectedSwitches[0].@ref + " connected to replay: " + inputCimObject.mRID);
                        }
                    }
                    else
                        dropList.Add(inputCimObject);
                }

                // Set electrical values on internal substation cables to 0
                if (inputCimObject is ACLineSegment)
                {
                    var acls = inputCimObject as ACLineSegment;

                    if (acls.PSRType == "InternalCable")
                    {
                        // Set name to internal cable
                        acls.name = "Internal Cable";

                        // Set length to 1 meter
                        acls.length.Value = 1;

                        // Set value to zero
                        acls.r = new Resistance() { Value = 0 };
                        acls.r0 = new Resistance() { Value = 0 };
                        acls.x = new Reactance() { Value = 0 };
                        acls.x0 = new Reactance() { Value = 0 };
                        acls.bch = new Susceptance() { Value = 0 };
                        acls.b0ch = new Susceptance() { Value = 0 };
                        acls.gch = new Conductance() { Value = 0 };
                        acls.g0ch = new Conductance() { Value = 0 };
                    }
                }
             
                // Beregn r,x,b og g på trafo'er jf. opskrift fra DigSILENT/Anja (se dokument 
                if (inputCimObject is PowerTransformerEndExt)
                {
                    var ptEnd = inputCimObject as PowerTransformerEndExt;

                    if (ptEnd.endNumber == "1")
                    {
                        if (ptEnd.ratedU == null ||
                            ptEnd.ratedS == null ||
                            ptEnd.excitingCurrentZero == null ||
                            ptEnd.loss == null ||
                            ptEnd.lossZero == null ||
                            ptEnd.uk == null)
                        {
                            // FIX: burde måske logge fejl, men PF skal nok brokke sig
                        }
                        else
                        {
                            // Beregn r
                            ptEnd.r = new Resistance() { Value = ptEnd.loss.Value * Math.Pow((ptEnd.ratedU.Value / (ptEnd.ratedS.Value * 1000)), 2) };

                            // Beregn g: (LossZero/ratedU^2)
                            double g = ptEnd.lossZero.Value / Math.Pow(ptEnd.ratedU.Value, 2);
                            ptEnd.g = new Conductance() { Value = g };

                            // Beregn YOC: (excitingCurrentZero*ratedS)/(100*ratedU^2)
                            double yoc = (ptEnd.excitingCurrentZero.Value * (ptEnd.ratedS.Value * 1000)) / (100 * Math.Pow(ptEnd.ratedU.Value, 2));

                            // Beregn b: SQRT(YOC^2-g^2)
                            ptEnd.b = new Susceptance()
                            {
                                Value = Math.Sqrt(Math.Pow(yoc, 2) - Math.Pow(g, 2))
                            };

                            // Beregn Zk: (uk*ratedU^2)/(100*ratedS)
                            double zk = (ptEnd.uk.Value * Math.Pow(ptEnd.ratedU.Value, 2)) / (100 * (ptEnd.ratedS.Value * 1000));

                            // Beregn x: SQRT(Zk^2-r^2)
                            ptEnd.x = new Reactance()
                            {
                                Value = Math.Sqrt(
                                    Math.Pow(zk, 2) - Math.Pow(ptEnd.r.Value, 2)
                               )
                            };
                        }
                    }

                    if (ptEnd.endNumber == "2")
                    {
                        // Set value to zero
                        ptEnd.r = new Resistance() { Value = 0 };
                        ptEnd.r0 = new Resistance() { Value = 0 };
                        ptEnd.x = new Reactance() { Value = 0 };
                        ptEnd.x0 = new Reactance() { Value = 0 };
                        ptEnd.b = new Susceptance() { Value = 0 };
                        ptEnd.b0 = new Susceptance() { Value = 0 };
                        ptEnd.g = new Conductance() { Value = 0 };
                        ptEnd.g0 = new Conductance() { Value = 0 };
                    }
                }

                // Remove trf from transformer name
                if (inputCimObject is PowerTransformer)
                {
                    inputCimObject.name = inputCimObject.name.Replace("TRF", "");
                }

                // Ensure bay name is max 32 charaters
                if (inputCimObject is Bay && inputCimObject.name != null && inputCimObject.name.Length > 32)
                {
                    inputCimObject.name = inputCimObject.name.Substring(0, 32);
                }

                // Ensure connectivity nodes / busbars have proper names. 
                // Needed by Konstant to support result extracts etc. PF uses the name of the node/busbar in reports.
                // Also needed to support time series import (Jakob skinne navngivning)
                if (inputCimObject is ConnectivityNode)
                {
                    var cn = inputCimObject as ConnectivityNode;

                    if (cn.name == null || cn.name.Length == 0)
                    {
                        var neighboords = cn.GetNeighborConductingEquipments(context);

                        var pt = neighboords.Find(o => o is PowerTransformer) as PowerTransformer;
                        var bus = neighboords.Find(o => o is BusbarSection);
                       
                        if (pt != null)
                        {
                            var stVoltageLevels = context.GetSubstationVoltageLevels(pt.Substation);
                  
                            // Local trafo secondary side node hack
                            if (pt.name != null && pt.name.ToLower().Contains("lokal"))
                            {
                                // HACK SHOULD BE CLEANED UP
                                // Terminal actual exist in source, but get trown away in filter, because nothing connected to it
                                var ptConnections = context.GetConnections(pt);

                                var ptLvCn = new ConnectivityNode() { mRID = Guid.NewGuid().ToString(), name = pt.name  };
                                addList.Add(ptLvCn);
                                
                                if (stVoltageLevels.Exists(o => o.BaseVoltage == 400))
                                {
                                    var vl = stVoltageLevels.First(o => o.BaseVoltage == 400);

                                    _mappingContext.ConnectivityNodeToVoltageLevel.Add(ptLvCn, vl);
                                }

                                var ptLvTerminal = new Terminal()
                                {
                                    mRID = Guid.NewGuid().ToString(),
                                    phases = PhaseCode.ABCN,
                                    phasesSpecified = true,
                                    sequenceNumber = "2",
                                    ConnectivityNode = new TerminalConnectivityNode() { @ref = ptLvCn.mRID },
                                    ConductingEquipment = new TerminalConductingEquipment { @ref = pt.mRID }
                                };

                                var ptEnd = context.GetPowerTransformerEnds(pt).First(p => p.endNumber == "2");
                                ptEnd.Terminal = new TransformerEndTerminal() {  @ref = ptLvTerminal.mRID };

                                addList.Add(ptLvTerminal);
                            }

                            var voltageLevels = context.GetSubstationVoltageLevels(pt.Substation);

                            double voltageLevel = 400;

                            if (neighboords.Exists(o => !(o is PowerTransformer) && o.BaseVoltage > 0.0))
                            {
                                voltageLevel = neighboords.First(o => !(o is PowerTransformer) && o.BaseVoltage > 0.0).BaseVoltage;

                                var ptConnections = context.GetConnections(pt);
                                var ptTerminal = ptConnections.First(c => c.ConnectivityNode == cn);

                                if (voltageLevels.Exists(o => o.BaseVoltage == voltageLevel))
                                {
                                    var vl = voltageLevels.First(o => o.BaseVoltage == voltageLevel);

                                    _mappingContext.ConnectivityNodeToVoltageLevel.Add(cn, vl);
                                }
                            }
                            // Set to 400 volt winding, if exists
                            else
                            {
                                if (voltageLevels.Exists(o => o.BaseVoltage == 400))
                                {
                                    var vl = voltageLevels.First(o => o.BaseVoltage == 400);
                                    _mappingContext.ConnectivityNodeToVoltageLevel.Add(cn, vl);
                                }
                            }

                            // Hvis trafo deler node med skinne, eller node er 400 volt, så brug jakob navngivning
                            if (bus != null || voltageLevel == 400)
                                cn.name = pt.GetSubstation(true, context).name + "_" + GetVoltageLevelStr(voltageLevel) + "_" +pt.name.Replace("TRF","");
                            else
                                cn.name = "CN";

                            // JL: Fjernet fordi Thue vil ikke have 10/15 kV trafo noder navngivet
                            // ellers så brug gis/cim trafo navngivning (TRF..), for at undgå dublet i node navn
                            //else
                            //    cn.name = pt.GetSubstation(true, context).name + "_" + GetVoltageLevelStr(voltageLevel) + "_" + pt.name;

                       }
                        else if (bus != null && bus.name != null)
                            cn.name = bus.name;
                        else
                            cn.name = "CN";
                    }
                }
            }

            // Change trafo names
            /*
             * Virker ikke fordi det smadre PF's navgivning af load og generator som de sætter ind
            foreach (var inputCimObject in input)
            {
                if (inputCimObject is PowerTransformer)
                {
                    var pt = inputCimObject as PowerTransformer;

                    var ptEnds = context.GetPowerTransformerEnds(pt);

                    pt.name = pt.GetSubstation(true, context).name + "_" + GetVoltageLevelStr(ptEnds[0].BaseVoltage) + "_" + GetVoltageLevelStr(ptEnds[1].BaseVoltage) + "_" + pt.name.Replace("TRF", "");
                }
            }
            */

            var ecTest = input.Find(c => c.name == "571313124501006982" && c is PhysicalNetworkModel.EnergyConsumer);


            // return objects, except the one dropped
            foreach (var inputObj in input)
            {
                if (inputObj.name == "571313124501006982" && inputObj is EnergyConsumer)
                {

                }

                if (!dropList.Contains(inputObj))
                    yield return inputObj;
            }

            // yield added objects,
            foreach (var inputObj in addList)
            {
                yield return inputObj;
            }

        }

        private string GetVoltageLevelStr(double voltageLevel)
        {
            string vlStr = "";
            if (voltageLevel == 400)
                vlStr = "004";
            else if (voltageLevel == 10000)
                vlStr = "010";
            else if (voltageLevel == 15000)
                vlStr = "015";
            else if (voltageLevel == 30000)
                vlStr = "030";
            else if (voltageLevel == 60000)
                vlStr = "060";
            else
                throw new Exception("Don't know how to handle voltage level: " + voltageLevel);

            return vlStr;
        }
    }
}
