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
    public class TransformerCableMerger : IPreProcessor
    {

        MappingContext _mappingContext;

        public TransformerCableMerger(MappingContext mappingContext)
        {
            _mappingContext = mappingContext;
        }

        public IEnumerable<IdentifiedObject> Transform(CimContext context, IEnumerable<IdentifiedObject> input)
        {
            HashSet<PhysicalNetworkModel.IdentifiedObject> dropList = new HashSet<IdentifiedObject>();

            List<PhysicalNetworkModel.IdentifiedObject> addList = new List<IdentifiedObject>();


            foreach (var inputCimObject in input)
            {
                if (inputCimObject is ACLineSegment && ((ACLineSegment)inputCimObject).PSRType == "InternalCable" && ((ACLineSegment)inputCimObject).BaseVoltage > 1000)
                {
                    var acls = inputCimObject as ACLineSegment;

                    // acls terminal connections
                    var aclsTerminalConnections = context.GetConnections(acls).ToArray();

                    // Only remove cables attached to power transformers
                    if (acls.GetNeighborConductingEquipments(context).Exists(n => n is PowerTransformer))
                    {

                        if (aclsTerminalConnections.Length == 2)
                        {
                            var cn1 = aclsTerminalConnections[0].ConnectivityNode;
                            var cn2 = aclsTerminalConnections[1].ConnectivityNode;

                            // Move everything that is connected to c1 to c2

                            var cn1equipments = context.GetConnections(cn1).ToArray();

                            foreach (var cn1Eq in cn1equipments)
                            {
                                context.ConnectTerminalToAnotherConnectitityNode(cn1Eq.Terminal, cn2);
                            }

                            // remove cable and it's terminals
                            dropList.Add(acls);
                            dropList.Add(aclsTerminalConnections[0].Terminal);
                            dropList.Add(aclsTerminalConnections[1].Terminal);
                        }
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

       
    }
}
