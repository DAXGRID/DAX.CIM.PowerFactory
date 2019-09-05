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
    /// Used to process cim data to PF requirements
    /// Konstant specific "hacks"
    /// </summary>
    public class PowerFactoryDataPrepareAndFix : IPreProcessor
    {
        int _guidOffset = 1000;

        MappingContext _mappingContext;

        public PowerFactoryDataPrepareAndFix(MappingContext mappingContext)
        {
            _mappingContext = mappingContext;
        }

        public IEnumerable<IdentifiedObject> Transform(CimContext context, IEnumerable<IdentifiedObject> input)
        {
            HashSet<PhysicalNetworkModel.IdentifiedObject> dropList = new HashSet<IdentifiedObject>();

            List<PhysicalNetworkModel.IdentifiedObject> addList = new List<IdentifiedObject>();


            // AssetInfo to Asset ref dictionary
            Dictionary<string, string> assetInfoToAssetRef = new Dictionary<string, string>();

            foreach (var inputCimObject in input)
            {
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

            foreach (var inputCimObject in input)
            {
                // Set busbar names to station + voltagelevel + bay
                if (inputCimObject is BusbarSection)
                {
                    var bus = inputCimObject as BusbarSection;

                    var vl = context.GetObject<VoltageLevel>(bus.EquipmentContainer.@ref);

                    var st = bus.GetSubstation(true, context);

                    bus.name = st.name + "_" + GetVoltageLevelStr(vl.BaseVoltage) + "_" + bus.name;
                }
            }

            // Fix and check objects
            foreach (var inputCimObject in input)
            {

                // Aux equipment
                if (inputCimObject is CurrentTransformerInfoExt)
                {
                    var ctInfo = inputCimObject as CurrentTransformerInfoExt;

                    if (ctInfo.primaryCurrent == null || ctInfo.secondaryCurrent == null)
                    {
                        var assetMrid = assetInfoToAssetRef[ctInfo.mRID];
                        var eq = assetToEquipmentRef[assetMrid];

                        var ct = context.GetObject<CurrentTransformerExt>(eq);

                        var stName = ct.GetSubstation(true, context).name;
                        var bayName = ct.GetBay(true, context).name;

                        System.Diagnostics.Debug.WriteLine("CT Missing Info: " + stName + " " + bayName);
                        
                    }
                }

                if (inputCimObject is PotentialTransformerInfoExt)
                {
                    var vtInfo = inputCimObject as PotentialTransformerInfoExt;

                    if (vtInfo.primaryVoltage == null || vtInfo.secondaryVoltage == null)
                    {
                        var assetMrid = assetInfoToAssetRef[vtInfo.mRID];
                        var eq = assetToEquipmentRef[assetMrid];

                        var ct = context.GetObject<PotentialTransformer>(eq);

                        var stName = ct.GetSubstation(true, context).name;
                        var bayName = ct.GetBay(true, context).name;

                        System.Diagnostics.Debug.WriteLine("VT Missing Info: " + stName + " " + bayName);

                    }
                }


                // Set relay names to station + bay
                if (inputCimObject is ProtectionEquipment)
                {
                    var pe = inputCimObject as ProtectionEquipment;

                    // check if relay is
                    var peSw = context.GetObject<Switch>(pe.ProtectedSwitches[0].@ref);
                    var bay = peSw.GetBay(true, context);
                    var st = peSw.GetSubstation(true, context);

                    pe.name = st.name + " " + bay.name;
                }

                // Tap changer
                if (inputCimObject is RatioTapChanger)
                {
                    // Set tap neutralU til winding voltage
                    var tap = inputCimObject as RatioTapChanger;

                    var ptEnd = context.GetObject<PowerTransformerEnd>(tap.TransformerEnd.@ref);
                    var pt = context.GetObject<PowerTransformer>(ptEnd.PowerTransformer.@ref);

                    tap.name = pt.GetSubstation(true, context).name + " " + pt.name + "_TAB";

                    tap.neutralU = new Voltage() { Value = ptEnd.ratedU.Value };
                   
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


                // Peterson coil konstant hacks (must be cleaned up in GIS!)
                if (inputCimObject is PetersenCoil)
                {
                    var coil = inputCimObject as PetersenCoil;

                    // hack because voltage level is wrong
                    if (coil.nominalU.Value < 1000)
                    {
                        coil.nominalU.Value = coil.nominalU.Value * 1000;
                    }

                }


                // Beregn r,x og b på trafo'er
                if (inputCimObject is PowerTransformerEndExt)
                {
                    var ptEnd = inputCimObject as PowerTransformerEndExt;

                    if (ptEnd.endNumber == "1")
                    {
                        if (ptEnd.PowerTransformer.@ref == "a52fb173-f556-4444-98ac-0579896e813d")
                        { }

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
                            ptEnd.r = new Resistance() { Value = ptEnd.loss.Value * Math.Pow((ptEnd.ratedU.Value / ptEnd.ratedS.Value), 2) };

                            // g=LossZero/ratedU^2 
                            double g = ptEnd.lossZero.Value / Math.Pow(ptEnd.ratedU.Value, 2);
                            ptEnd.g = new Conductance() { Value = g };

                            //YOC=(excitingCurrentZero*ratedS)/(100*ratedU^2)
                            double yoc = (ptEnd.excitingCurrentZero.Value * ptEnd.ratedS.Value) / (100 * Math.Pow(ptEnd.ratedU.Value, 2));

                            // b==SQRT(YOC^2-g^2)
                            ptEnd.b = new Susceptance()
                            {
                                Value =
                                Math.Sqrt(Math.Pow(yoc, 2) - Math.Pow(g, 2))
                            };

                            // Zk=(uk*ratedU^2)/(100*ratedS)
                            double zk = (ptEnd.uk.Value * Math.Pow(ptEnd.ratedU.Value, 2)) / (100 * ptEnd.ratedS.Value);


                            //  x=SQRT(Zk^2-r^2)
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

                // Ensure connectivity nodes have names. CGMES / PF wants that.
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
                            // Local trafo secondary side node hack
                            if (pt.name != null && pt.name.ToLower().Contains("lokal"))
                            {
                                // HACK SHOULD BE CLEANED UP
                                // Terminal actual exist in source, but get trown away in filter, because nothing connected to it
                                var ptConnections = context.GetConnections(pt);

                                var ptLvCn = new ConnectivityNode() { mRID = Guid.NewGuid().ToString(), name = pt.name  };
                                addList.Add(ptLvCn);

                                var stVoltageLevels = context.GetSubstationVoltageLevels(pt.Substation);

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
                            if (bus != null  || voltageLevel == 400)
                                cn.name = pt.GetSubstation(true, context).name + "_" + GetVoltageLevelStr(voltageLevel) + "_" +pt.name.Replace("TRF","");
                            // ellers så brug gis/cim trafo navngivning (TRF..), for at undgå dublet i node navn
                            else
                                cn.name = pt.GetSubstation(true, context).name + "_" + GetVoltageLevelStr(voltageLevel) + "_" + pt.name;
                
                        }
                        else if (bus != null && bus.name != null)
                            cn.name = bus.name;
                        else
                            cn.name = "CN";
                    }
                }

            }

            // return objects, except the one dropped
            foreach (var inputObj in input)
            {
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
            else if (voltageLevel == 60000)
                vlStr = "060";
            else
                throw new Exception("Don't know how to handle voltage level: " + voltageLevel);

            return vlStr;
        }
    }
}
