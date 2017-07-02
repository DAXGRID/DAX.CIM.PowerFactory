using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DAX.CIM.PFAdapter.CGMES;
using System.Xml;
using System.Linq;
using System.Xml.Linq;
using DAX.CIM.PhysicalNetworkModel.Traversal;
using DAX.CIM.PhysicalNetworkModel.Traversal.Extensions;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace DAX.CIM.PFAdapter.Tests
{
    [TestClass]
    public class BasicTests : FixtureBase
    {
        //string eqTempFileName = @"c:\temp\cim\pf\pf_test_eq.xml";
        string folder = @"\\nrgi.local\dfs\gis\AFD_GIS_Installation\DAX\PF";
        string eqTempFileName = @"\\nrgi.local\dfs\gis\AFD_GIS_Installation\DAX\PF\export\engum_eq.xml";
        string glTempFileName = @"\\nrgi.local\dfs\gis\AFD_GIS_Installation\DAX\PF\export\engum_gl.xml";

        CimContext _context;

        protected override void SetUp()
        {
            //var reader = new CimJsonFileReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Data\engum_anonymized.jsonl"));

            var reader = new CimJsonFileReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"c:\temp\cim\engum.jsonl"));
            var cimObjects = reader.Read().ToList();
            _context = CimContext.Create(cimObjects);
            Using(_context);
        }

        [TestMethod]
        public void TestLineMerging()
        {
            // Breaker in BRB
            var swBRB = _context.GetObject<PhysicalNetworkModel.Switch>("366461ea-69b3-4e55-8459-228d9d33668e");

            // Load Break Switch in 30904
            var sw30904 = _context.GetObject<PhysicalNetworkModel.Switch>("15088672-f80c-453c-8bc6-30550ab00780");
            
            var inputObjects = _context.GetAllObjects().Where(o => (o is PhysicalNetworkModel.ACLineSegment && o.name != null && o.name.Contains("BRB-30904")) || !(o is PhysicalNetworkModel.ACLineSegment) );
            
            var converter = new PNM2PowerFactoryConverter(inputObjects, new List<IPreProcessor> { new OrphanJunctionProcessor() });

            var outputCimObjects = converter.GetCimObjects().ToList();

            // We need to reinitialize context, because converter has modified objects
            _context = CimContext.Create(outputCimObjects);

            // Check that the two switches point to each other via the same ACLS
            Assert.AreEqual(swBRB.GetNeighborConductingEquipments().OfType<PhysicalNetworkModel.ACLineSegment>().First(), sw30904.GetNeighborConductingEquipments().OfType<PhysicalNetworkModel.ACLineSegment>().First());
        }

        [TestMethod]
        public void TestEngumEQWriter()
        {
            bool includeAll = true;

            var converter = new PNM2PowerFactoryConverter(_context.GetAllObjects(), new List<IPreProcessor> { new OrphanJunctionProcessor() });

            var outputCimObjects = converter.GetCimObjects().ToList();

            // We need to reinitialize context, because converter has modified objects
            _context = CimContext.Create(outputCimObjects);

            var eqWriter = new EQ_Writer(eqTempFileName, _context);
            var glWriter = new GL_Writer(glTempFileName);

            HashSet<PhysicalNetworkModel.ConnectivityNode> cnAlreadyWritten = new HashSet<PhysicalNetworkModel.ConnectivityNode>();

            foreach (var cimObject in _context.GetAllObjects())
            {
                if ((cimObject is PhysicalNetworkModel.ConductingEquipment && ((PhysicalNetworkModel.ConductingEquipment)cimObject).BaseVoltage > 5000) ||
                    !(cimObject is PhysicalNetworkModel.ConductingEquipment) ||
                    cimObject is PhysicalNetworkModel.PowerTransformer
                    )
                {

                    if (
                    cimObject is PhysicalNetworkModel.ACLineSegment ||
                    cimObject is PhysicalNetworkModel.BusbarSection ||
                    cimObject is PhysicalNetworkModel.LoadBreakSwitch ||
                    cimObject is PhysicalNetworkModel.Breaker ||
                    cimObject is PhysicalNetworkModel.Disconnector ||
                    cimObject is PhysicalNetworkModel.Fuse ||
                    cimObject is PhysicalNetworkModel.Terminal ||
                    (cimObject is PhysicalNetworkModel.ConnectivityNode && cimObject.IsInsideSubstation()) ||
                    cimObject is PhysicalNetworkModel.Substation ||
                    cimObject is PhysicalNetworkModel.VoltageLevel ||
                    cimObject is PhysicalNetworkModel.Bay ||
                    cimObject is PhysicalNetworkModel.PowerTransformer ||
                    cimObject is PhysicalNetworkModel.PowerTransformerEnd
                    )
                    {
                        // Add substations
                        if (cimObject is PhysicalNetworkModel.Substation && (cimObject.name == "BRB" || cimObject.name == "30904" || includeAll))
                        {
                            var st = cimObject as PhysicalNetworkModel.Substation;

                            if (st.PSRType == "PrimarySubstation" || st.PSRType == "SecondarySubstation")
                            {
                                eqWriter.AddPNMObject((dynamic)cimObject);

                                // Add location
                                var loc = _context.GetObject<PhysicalNetworkModel.LocationExt>(st.Location.@ref);
                                glWriter.AddLocation(Guid.Parse(st.mRID), loc);
                            }
                        }

                        // Add voltage levels
                        if (cimObject is PhysicalNetworkModel.VoltageLevel && (cimObject.GetSubstation().name == "BRB" || cimObject.GetSubstation().name == "30904" || includeAll))
                        {
                            eqWriter.AddPNMObject((dynamic)cimObject);
                        }

                        // Power transformer
                        if (cimObject is PhysicalNetworkModel.PowerTransformer && (cimObject.GetSubstation().name == "BRB" || cimObject.GetSubstation().name == "30904" || includeAll))
                        {
                            eqWriter.AddPNMObject((dynamic)cimObject);

                            // Add terminals
                            var ci = cimObject as PhysicalNetworkModel.ConductingEquipment;
                            foreach (var tc in _context.GetConnections(ci))
                            {
                                eqWriter.AddPNMObject((dynamic)tc.Terminal);
                            }
                        }

                        // Power transformer end
                        if (cimObject is PhysicalNetworkModel.PowerTransformerEnd && (cimObject.GetSubstation().name == "BRB" || cimObject.GetSubstation().name == "30904" || includeAll))
                        {
                            eqWriter.AddPNMObject((dynamic)cimObject);

                        }


                        // Add bays
                        if (cimObject is PhysicalNetworkModel.Bay && (cimObject.GetSubstation().name == "BRB" || cimObject.GetSubstation().name == "30904" || includeAll))
                        {
                            eqWriter.AddPNMObject((dynamic)cimObject);
                        }


                        // Add ACLS
                        if (cimObject is PhysicalNetworkModel.ACLineSegment && cimObject.name != null && (cimObject.name.Contains("BRB-30904") || includeAll))
                        {
                            eqWriter.AddPNMObject((dynamic)cimObject);

                            // Add terminals
                            var ci = cimObject as PhysicalNetworkModel.ConductingEquipment;
                            foreach (var tc in _context.GetConnections(ci))
                            {
                                eqWriter.AddPNMObject((dynamic)tc.Terminal);
                            }

                            // Add location
                            if (ci.PSRType != "InternalCable")
                            {
                                var loc = _context.GetObject<PhysicalNetworkModel.LocationExt>(ci.Location.@ref);
                                glWriter.AddLocation(Guid.Parse(ci.mRID), loc);
                            }
                        }

                        // Add stuff inside substation 
                        if (cimObject.IsInsideSubstation() && (cimObject.GetSubstation().name == "BRB" || cimObject.GetSubstation().name == "30904" || includeAll))
                        {
                            // fix busbar voltage level ref
                            if (cimObject is PhysicalNetworkModel.BusbarSection)
                            {
                                var busbar = cimObject as PhysicalNetworkModel.BusbarSection;
                                busbar.EquipmentContainer.@ref = busbar.GetSubstation().GetVoltageLevel(busbar.BaseVoltage).mRID;
                            }

                            eqWriter.AddPNMObject((dynamic)cimObject);
                            if (cimObject is PhysicalNetworkModel.ConductingEquipment)
                            {


                                var ci = cimObject as PhysicalNetworkModel.ConductingEquipment;
                                foreach (var tc in _context.GetConnections(ci))
                                {
                                    eqWriter.AddPNMObject((dynamic)tc.Terminal);

                                    if (!cnAlreadyWritten.Contains(tc.ConnectivityNode))
                                    {
                                        // PF want's a name
                                        tc.ConnectivityNode.name = "CN " + tc.ConductingEquipment.name;
                                        eqWriter.AddPNMObject((dynamic)tc.ConnectivityNode);
                                    }

                                    cnAlreadyWritten.Add(tc.ConnectivityNode);
                                }
                            }
                        }
                    }
                }
            }

            eqWriter.Close();
            glWriter.Close();

            string startPath = folder + "\\export";
            string zipPath = folder + "\\export.zip";

            File.Delete(zipPath);

            ZipFile.CreateFromDirectory(startPath, zipPath);
        }

        [TestMethod]
        public void TestEQWriterACLineSegment()
        {
            // Create ACLS
            PhysicalNetworkModel.IdentifiedObject acls = new PhysicalNetworkModel.ACLineSegment()
            {
                mRID = Guid.NewGuid().ToString(),
                name = "31-32",
                BaseVoltage = 10000,
                length = new PhysicalNetworkModel.Length() { multiplier = PhysicalNetworkModel.UnitMultiplier.none, unit = PhysicalNetworkModel.UnitSymbol.m, Value = 69.213974 },
                b0ch = new PhysicalNetworkModel.Susceptance() { Value = 0 },
                bch = new PhysicalNetworkModel.Susceptance() {  Value = 0.0001440542 },
                gch = new PhysicalNetworkModel.Conductance() {  Value = 0 },
                g0ch = new PhysicalNetworkModel.Conductance() { Value = 0 },
                r = new PhysicalNetworkModel.Resistance() { Value = 5.192352 },
                r0 = new PhysicalNetworkModel.Resistance { Value = 5.296199 },
                x = new PhysicalNetworkModel.Reactance() {  Value = 17.16264 },
                x0 = new PhysicalNetworkModel.Reactance() { Value = 0}
            };

            var converter = new EQ_Writer(eqTempFileName, null);

            converter.AddPNMObject((dynamic)acls);
            converter.Close();

            // Check generated RDF XML document
            XDocument doc = XDocument.Load(eqTempFileName);

            var test = doc.Descendants();

            Assert.IsNotNull(doc.Descendants().First(o => 
                o.Name.LocalName == "Conductor.length" 
                && o.Value == "69.213974"
              ));

            Assert.IsNotNull(doc.Descendants().First(o =>
             o.Name.LocalName == "ACLineSegment.r"
             && o.Value == "5.192352"
           ));

        }
    }
}
