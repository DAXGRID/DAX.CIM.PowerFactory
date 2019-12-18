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

        public KonstantBigEnergyConsumerHandler(MappingContext mappingContext)
        {
            _mappingContext = mappingContext;
        }

        public IEnumerable<IdentifiedObject> Transform(CimContext context, IEnumerable<IdentifiedObject> input)
        {
            HashSet<PhysicalNetworkModel.IdentifiedObject> dropList = new HashSet<IdentifiedObject>();

            List<PhysicalNetworkModel.IdentifiedObject> addList = new List<IdentifiedObject>();

            foreach (var inputCimObject in input)
            {
                if (inputCimObject is EnergyConsumer)
                {
                   if (inputCimObject.name == "571313124501006982")
                    {

                    }

                    var ec = inputCimObject as EnergyConsumer;

                    if (ec.BaseVoltage > 400)
                    {
                        // ec terminal connections
                        var ecTerminalConnections = context.GetConnections(inputCimObject);

                        if (ecTerminalConnections.Count == 1)
                        {
                            var ecTerminal = ecTerminalConnections[0].Terminal;

                            var ecCn = ecTerminalConnections[0].ConnectivityNode;

                            var ecCnConnections = context.GetConnections(ecCn);

                            if (ecCnConnections.Exists(o => o.ConductingEquipment is ACLineSegment))
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
                        }
                    }
                }
            }


            // return objects, except the one dropped
            foreach (var inputObj in input)
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
