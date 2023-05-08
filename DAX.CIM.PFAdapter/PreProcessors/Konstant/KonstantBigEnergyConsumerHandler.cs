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
    /// Remove cables between power transformer winding and neighbor conduction equipment (i.e. switch or busbar)
    /// </summary>
    public class KonstantBigEnergyConsumerHandler : IPreProcessor
    {

        MappingContext _mappingContext;
        List<IdentifiedObject> _allCimObjects;


        public KonstantBigEnergyConsumerHandler(MappingContext mappingContext, List<IdentifiedObject> allCimObjects)
        {
            _mappingContext = mappingContext;
            _allCimObjects = allCimObjects;
        }

        public IEnumerable<IdentifiedObject> Transform(CimContext context, IEnumerable<IdentifiedObject> cimObjects)
        {
            HashSet<PhysicalNetworkModel.IdentifiedObject> dropList = new HashSet<IdentifiedObject>();

            List<PhysicalNetworkModel.IdentifiedObject> addList = new List<IdentifiedObject>();

            foreach (var inputCimObject in cimObjects)
            {
                if (inputCimObject is EnergyConsumer)
                {
                   

                    // 60 kv aftagepunkt horsens
                    if (inputCimObject.name == "571313124500300517")
                    {
                    }

                    // skejby 15 kv aftagepunkt århus
                    if (inputCimObject.name == "571313115190422986")
                    {
                    }

                        
                    var ec = inputCimObject as EnergyConsumer;

                    var pts = cimObjects.Where(c => c is CurrentTransformer);

                    if (ec.name != null && ec.name.Length == 18 && _allCimObjects.Any(c => c is CurrentTransformer && c.name == ec.name && ((CurrentTransformer)c).PSRType != null && ( ((CurrentTransformer)c).PSRType.Contains("60") || ((CurrentTransformer)c).PSRType.Contains("0,4 kV siden")) ))
                    {
                        dropList.Add(ec);

                        var ecTerminalConnections = context.GetConnections(ec);

                        if (ecTerminalConnections.Count > 0)
                        {
                            var ecTerminal = ecTerminalConnections[0].Terminal;

                            var ecCn = ecTerminalConnections[0].ConnectivityNode;

                            var ecCnConnections = context.GetConnections(ecCn);

                            dropList.Add(ecTerminal);

                            var loc = cimObjects.First(c => c.mRID == ec.Location.@ref);

                            dropList.Add(loc);


                            // fjern stik kabel
                            var aclsCount = ecCnConnections.Count(o => o.ConductingEquipment is ACLineSegment);

                            if (aclsCount == 1)
                            {
                                var acls = ecCnConnections.First(o => o.ConductingEquipment is ACLineSegment).ConductingEquipment;
                                var aclsConnections = context.GetConnections(acls);

                                dropList.Add(acls);

                                dropList.Add(aclsConnections[0].Terminal);
                                dropList.Add(aclsConnections[1].Terminal);
                            }
                        }
                    }
                    else
                    {
                        // ec terminal connections
                        var ecTerminalConnections = context.GetConnections(inputCimObject);

                        if (ecTerminalConnections.Count == 1)
                        {
                            var ecTerminal = ecTerminalConnections[0].Terminal;

                            var ecCn = ecTerminalConnections[0].ConnectivityNode;

                            var ecCnConnections = context.GetConnections(ecCn);

                            var aclsCount = ecCnConnections.Count(o => o.ConductingEquipment is ACLineSegment);

                            if (aclsCount == 1)
                            {
                                var acls = ecCnConnections.First(o => o.ConductingEquipment is ACLineSegment).ConductingEquipment;

                                // find other end of acls
                                var aclsConnections = context.GetConnections(acls);

                                // Can we find another cn (in the other end of the cable)
                                if (aclsConnections.Exists(o => o.ConnectivityNode != ecCn))
                                {
                                    var aclsCn = aclsConnections.First(o => o.ConnectivityNode != ecCn).ConnectivityNode;

                                    context.ConnectTerminalToAnotherConnectitityNode(ecTerminal, aclsCn);

                                    var aclsSt = aclsCn.GetSubstation(false, context);

                                    if (aclsSt != null)
                                    {
                                        var stVls = context.GetSubstationVoltageLevels(aclsSt);

                                        var vl = stVls.Find(o => o.BaseVoltage == ec.BaseVoltage);

                                        if (vl != null)
                                        {
                                            ec.EquipmentContainer = new EquipmentEquipmentContainer() { @ref = vl.mRID };


                                            dropList.Add(acls);

                                            dropList.Add(aclsConnections[0].Terminal);
                                            dropList.Add(aclsConnections[1].Terminal);
                                        }
                                    }
                                }
                            }
                            else if (aclsCount > 1)
                            {
                                foreach (var acls in ecCnConnections.Where(o => o.ConductingEquipment is ACLineSegment))
                                {
                                    if (acls.ConductingEquipment.mRID == "5d8d97aa-ae4c-4d6a-b866-8fd4d61d2e19")
                                    {

                                    }
                                }
                            }
                        }
                    }
                }
            }


            // return objects, except the one dropped
            foreach (var inputObj in cimObjects)
            {
                if (inputObj.name == "571313124501006982")
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

       
    }
}
