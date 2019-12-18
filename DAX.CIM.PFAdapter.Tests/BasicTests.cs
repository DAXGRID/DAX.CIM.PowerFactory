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
using DAX.CIM.PFAdapter.PreProcessors;

namespace DAX.CIM.PFAdapter.Tests
{
    [TestClass]
    public class BasicTests : FixtureBase
    {
        //string eqTempFileName = @"c:\temp\cim\pf\pf_test_eq.xml";
        string folder = @"\\SHOBJPOW01V\c$\DAX\export";
        string eqTempFileName = @"\\SHOBJPOW01V\c$\DAX\export\files\engum_eq.xml";
        string glTempFileName = @"\\SHOBJPOW01V\c$\DAX\export\files\engum_gl.xml";

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
            
            var converter = new PNM2PowerFactoryConverter(inputObjects, new List<IPreProcessor> { new ACLSMerger(new MappingContext()) });

            var outputCimObjects = converter.GetCimObjects().ToList();

            // Check that the two switches point to each other via the same ACLS
            Assert.AreEqual(swBRB.GetNeighborConductingEquipments().OfType<PhysicalNetworkModel.ACLineSegment>().First(), sw30904.GetNeighborConductingEquipments().OfType<PhysicalNetworkModel.ACLineSegment>().First());
        }

        [TestMethod]
        public void TestEngumEQWriter()
        {
            bool includeAll = false;

            var mappingContext = new MappingContext();

            var converter = new PNM2PowerFactoryConverter(_context.GetAllObjects(), 
                new List<IPreProcessor> {
                    new ACLSMerger(mappingContext),
                    new KonstantPowerFactoryDataPrepareAndFix(mappingContext)
                });

            var outputCimObjects = converter.GetCimObjects().ToList();

            // We need to reinitialize context, because converter has modified objects
            _context = CimContext.Create(outputCimObjects);

            var eqWriter = new EQ_Writer(eqTempFileName, _context, mappingContext, Guid.Parse("48acc999-f45c-475a-b61c-09e7d1001fc1"), "Engum");

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

                            if (st.PSRType == "PrimarySubstation" || st.PSRType == "SecondarySubstation" || st.PSRType == "Junction")
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
                                tc.Terminal.phases = PhysicalNetworkModel.PhaseCode.ABCN;
                                tc.Terminal.name = ci.name + "_T" + tc.Terminal.sequenceNumber;
                                eqWriter.AddPNMObject((dynamic)tc.Terminal);
                            }
                        }

                        // Power transformer end
                        if (cimObject is PhysicalNetworkModel.PowerTransformerEnd && (cimObject.GetSubstation().name == "BRB" || cimObject.GetSubstation().name == "30904" || includeAll))
                        {
                            PhysicalNetworkModel.PowerTransformerEnd ptend = cimObject as PhysicalNetworkModel.PowerTransformerEnd;


                            ptend.r = new PhysicalNetworkModel.Resistance() { Value = 0.1 };
                            ptend.r0 = new PhysicalNetworkModel.Resistance() { Value = 0.2 };
                            ptend.x = new PhysicalNetworkModel.Reactance() { Value = 0.3 };
                            //ptend.x0 = new PhysicalNetworkModel.Reactance() { Value = 0.4 };
                            ptend.b = new PhysicalNetworkModel.Susceptance { Value = 0.5 };
                            ptend.b0 = new PhysicalNetworkModel.Susceptance { Value = 0.6 };
                            ptend.g = new PhysicalNetworkModel.Conductance { Value = 0.7 };
                            ptend.g0 = new PhysicalNetworkModel.Conductance { Value = 0.8 };
                            ptend.rground = new PhysicalNetworkModel.Resistance { Value = 0.9 };
                            ptend.xground = new PhysicalNetworkModel.Reactance { Value = 0.91 };
                            ptend.phaseAngleClock = "11";
                            ptend.grounded = false;

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
                        if (!(cimObject is PhysicalNetworkModel.PowerTransformer) && cimObject.IsInsideSubstation() && (cimObject.GetSubstation().name == "BRB" || cimObject.GetSubstation().name == "30904" || includeAll))
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
                                        eqWriter.AddPNMObject((dynamic)tc.ConnectivityNode);

                                    cnAlreadyWritten.Add(tc.ConnectivityNode);
                                }
                            }
                        }
                    }
                }
            }

            // Add peterson coil

            var coil = new PhysicalNetworkModel.PetersenCoil();

            var priTrafo = outputCimObjects.Find(o => o is PhysicalNetworkModel.PowerTransformer && o.GetSubstation().name == "BRB");
            var coilSt = priTrafo.GetSubstation();

            coil.mRID = Guid.NewGuid().ToString();
            coil.name = "Test coil";
            coil.BaseVoltage = 10000;
            coil.EquipmentContainer = new PhysicalNetworkModel.EquipmentEquipmentContainer() { @ref = coilSt.GetVoltageLevel(10000).mRID };
            coil.mode = PhysicalNetworkModel.PetersenCoilModeKind.@fixed;
            coil.nominalU = new PhysicalNetworkModel.Voltage() { Value = 10000 };
            coil.positionCurrent = new PhysicalNetworkModel.CurrentFlow() { Value = 99.99 };
            coil.offsetCurrent = new PhysicalNetworkModel.CurrentFlow { Value = 9.99 };
            coil.r = new PhysicalNetworkModel.Resistance() { Value = 9.99 };
            coil.xGroundMin = new PhysicalNetworkModel.Reactance { Value = 0.99 };
            coil.xGroundMax = new PhysicalNetworkModel.Reactance { Value = 9.99 };
            coil.xGroundNominal = new PhysicalNetworkModel.Reactance { Value = 4.99 };
 


            var trafoCons = _context.GetConnections(priTrafo);

            var secTer = trafoCons.Find(o => o.Terminal.sequenceNumber == "1");

            var coilTer = new PhysicalNetworkModel.Terminal();
            coilTer.mRID = Guid.NewGuid().ToString();
            coilTer.ConductingEquipment = new PhysicalNetworkModel.TerminalConductingEquipment() { @ref = coil.mRID };
            coilTer.ConnectivityNode = new PhysicalNetworkModel.TerminalConnectivityNode() { @ref = secTer.ConnectivityNode.mRID };
            coilTer.phases = PhysicalNetworkModel.PhaseCode.N;

            

            eqWriter.AddPNMObject(coil);
            eqWriter.AddPNMObject(coilTer);

            eqWriter.Close();
            glWriter.Close();

            string startPath = folder + "\\files";
            string zipPath = folder + "\\export.zip";

            File.Delete(zipPath);

            ZipFile.CreateFromDirectory(startPath, zipPath);


        }

        /*
         
-- ens
select * from dbo.CIM_Kabel
where GIS_straekningsid like 'BRB-30904%'

-- forskellig
select * from dbo.CIM_Kabel
where GIS_straekningsid like '30249-30466%'

          
         * */

        [TestMethod]
        public void TestEQWriterACLineSegment()
        {
            // Create ACLS
            PhysicalNetworkModel.IdentifiedObject acls = new PhysicalNetworkModel.ACLineSegment()
            {
                mRID = Guid.NewGuid().ToString(),
                name = "31-32",
                BaseVoltage = 10000,
                length = new PhysicalNetworkModel.Length() { multiplier = PhysicalNetworkModel.UnitMultiplier.none, unit = PhysicalNetworkModel.UnitSymbol.m, Value = 2500 },
                b0ch = new PhysicalNetworkModel.Susceptance() { Value = 0 },
                bch = new PhysicalNetworkModel.Susceptance() {  Value = 0.0001440542 },
                gch = new PhysicalNetworkModel.Conductance() {  Value = 0 },
                g0ch = new PhysicalNetworkModel.Conductance() { Value = 0 },
                r = new PhysicalNetworkModel.Resistance() { Value = 5.192352 },
                r0 = new PhysicalNetworkModel.Resistance { Value = 5.296199 },
                x = new PhysicalNetworkModel.Reactance() {  Value = 17.16264 },
                x0 = new PhysicalNetworkModel.Reactance() { Value = 0}
            };

            var converter = new EQ_Writer(eqTempFileName, null, null, Guid.NewGuid(),"test");


            converter.AddPNMObject((dynamic)acls);
            converter.Close();

            // Check generated RDF XML document
            XDocument doc = XDocument.Load(eqTempFileName);

            var test = doc.Descendants();

            // Must be in km now
            Assert.IsNotNull(doc.Descendants().First(o => 
                o.Name.LocalName == "Conductor.length" 
                && o.Value == "2.5"
              ));

            Assert.IsNotNull(doc.Descendants().First(o =>
             o.Name.LocalName == "ACLineSegment.r"
             && o.Value == "5.192352"
           ));

        }
    }
}
