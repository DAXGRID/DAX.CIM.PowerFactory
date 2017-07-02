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

namespace DAX.CIM.PFAdapter
{
    /// <summary>
    /// Find junctions (connectivity nodes) outside substations. 
    /// If the ACLS have the same charateristic, merge them.
    /// Otherwise create a substation where the junction is.
    /// </summary>
    public class OrphanJunctionProcessor : IPreProcessor
    {
        int _guidOffset = 1000;
        
        public IEnumerable<IdentifiedObject> Transform(CimContext context, IEnumerable<IdentifiedObject> input)
        {
            HashSet<PhysicalNetworkModel.IdentifiedObject> dropList = new HashSet<IdentifiedObject>();
       
            foreach (var inputCimObject in input)
            {
                // Find connectivity nodes outside substations
                if (inputCimObject is ConnectivityNode && !dropList.Contains(inputCimObject) && !inputCimObject.IsInsideSubstation())
                {
                    ConnectivityNode cn = inputCimObject as ConnectivityNode;

                    // Handle that acls cn relationship might have changed
                    var cnNeighborsx = cn.GetNeighborConductingEquipments();
                 
                    if (cnNeighborsx.Count(o => o == null) > 0)
                    {

                    }

                    // If two acls and we are above low voltage, merge them
                    if (cnNeighborsx.Count(o => o.BaseVoltage > 5000) > 0 && cnNeighborsx.Count(o => o is ACLineSegmentExt) == 2)
                    {
                        var acls1 = cnNeighborsx[0] as ACLineSegment;
                        var acls2 = cnNeighborsx[1] as ACLineSegment; 

                        // ACLS 1 will survive, ACLS 2 and the CN will die
                        dropList.Add(cn);
                        dropList.Add(acls2);
                        
                        var loc1 = context.GetObject<LocationExt>(acls1.Location.@ref);
                        var loc2 = context.GetObject<LocationExt>(acls2.Location.@ref);

                        LineMerger lm = new LineMerger();

                        // Convert to NTS geometries
                        lm.Add(GetGeometry(loc1));
                        lm.Add(GetGeometry(loc2));

                        // Merge the two line strings
                        var mergedLineList = lm.GetMergedLineStrings();

                        if (mergedLineList.Count != 1)
                            throw new Exception("Cannot merge ACLS: " + acls1.mRID + " and " + acls2.mRID);

                        // Overwrite loc 1 coordinated with merged strings
                        loc1.coordinates = GetPoints((ILineString)mergedLineList[0]).ToArray();

                        // Sum length
                        acls1.length.Value += acls2.length.Value;

                        // Find cn in the other end of ACLS 2
                        var acls2otherEndCn = context.GetConnections(acls2).Find(o => o.ConnectivityNode != cn);

                        // Get terminal of ACLS 1 that point to ACLS 2
                        var acls1Terminal = acls1.GetTerminal(acls2, true, context);

                        // just checking
                        var acls1n1 = acls1.GetNeighborConductingEquipments(context);
                        var acls2n1 = acls2.GetNeighborConductingEquipments(context);


                        // Disconnect ACLS 2 terminals
                        var acls2connections = context.GetConnections(acls2);

                        List<Terminal> terminalsToDisconnect = new List<Terminal>();

                        foreach (var acls2con in acls2connections)
                        {
                            terminalsToDisconnect.Add(acls2con.Terminal);
                        }

                        foreach (var t2d in terminalsToDisconnect)
                        {
                            context.DisconnectTerminalFromConnectitityNode(t2d);
                        }

                        // Change terminal of ACLS 1 to point to ACLS 2 other end CN
                        context.ConnectTerminalToAnotherConnectitityNode(acls1Terminal, acls2otherEndCn.ConnectivityNode);

                        var acls1n2 = acls1.GetNeighborConductingEquipments(context);
                        var acls2n2 = acls2.GetNeighborConductingEquipments(context);

                        var lbNeighbors = context.GetObject<PhysicalNetworkModel.ConductingEquipment>("15088672-f80c-453c-8bc6-30550ab00780").GetNeighborConductingEquipments(context);

                        var testOtherEndCnNeighboors2 = acls2otherEndCn.ConnectivityNode.GetNeighborConductingEquipments(context);
                                            
                      

                    }
                }
            }

            // return objects, except the one dropped
            foreach (var inputObj in input)
            {
                if (!dropList.Contains(inputObj))
                    yield return inputObj;
            }

        }

        private IGeometry GetGeometry(PhysicalNetworkModel.LocationExt location)
        {
            List<Coordinate> points = new List<Coordinate>();
            foreach (var locPt in location.coordinates)
                points.Add(new Coordinate(locPt.X, locPt.Y));

            return (new LineString(points.ToArray()));
        }

        private List<PhysicalNetworkModel.Point2D> GetPoints(ILineString line)
        {
            List<PhysicalNetworkModel.Point2D> result = new List<Point2D>();

            foreach (var pt in line.Coordinates)
                result.Add(new Point2D() { X = pt.X, Y = pt.Y });

            return result;
        }
    }
}
