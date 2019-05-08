﻿using System;
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
using DAX.CIM.PhysicalNetworkModel;
using DAX.CIM.PFAdapter.Asset;
using DAX.CIM.PhysicalNetworkModel.LineInfo;
using DAX.IO.CIM;
using DAX.CIM.PhysicalNetworkModel.FeederInfo;
using DAX.CIM.PFAdapter.Protection;

namespace DAX.CIM.PFAdapter.Tests
{
    [TestClass]
    public class KonstantHspTests : FixtureBase
    {
        //string eqTempFileName = @"c:\temp\cim\pf\pf_test_eq.xml";
        string folder = @"\\SHOBJPOW01V\c$\DAX\export\hsp_test";
        string eqTempFileName = @"\\SHOBJPOW01V\c$\DAX\export\hsp_test\files\byg_hat_krl_test_eq.xml";
        string glTempFileName = @"\\SHOBJPOW01V\c$\DAX\export\hsp_test\files\byg_hat_krl_test_gl.xml";
        string aiTempFileName = @"\\SHOBJPOW01V\c$\DAX\export\hsp_test\files\byg_hat_krl_test_ai.xml";
        string peTempFileName = @"\\SHOBJPOW01V\c$\DAX\export\hsp_test\files\byg_hat_krl_test_pe.xml";

        CimContext _initialContext;

        protected override void SetUp()
        {
            var reader = new CimJsonFileReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"c:\temp\cim\hat_area_hsp_test.jsonl"));
            var cimObjects = reader.Read().ToList();
            _initialContext = CimContext.Create(cimObjects);
            Using(_initialContext);
        }

        [TestMethod]
        public void TestHatBygKrlArea()
        {
            Dictionary<string, string> assetToEqRefs = new Dictionary<string, string>();

            // luftledning without connections
            var luft_dis = _initialContext.GetObject<ACLineSegment>("c599e9a1-189b-4ebd-ba03-ac9aa14ffb1f");
            var luft_dis_neighbors = luft_dis.GetNeighborConductingEquipments();

            // cable with 1 phase
            var luft_1phase = _initialContext.GetObject<ACLineSegment>("76c96252-b533-44b1-aac2-9efb184dc9e7");
            var luft_1phase_terminals = _initialContext.GetConnections(luft_1phase);

            // slukke spole ved lokal trafo
            var spole1 = _initialContext.GetObject<PetersenCoil>("a9cfa63f-6df3-4c29-a9a2-0fde4960b3e5");

            var testHvKunde = _initialContext.GetObject<EnergyConsumer>("a92ed975-ace5-4824-bccc-5ae8bc63fead");

            // linieadskiller som pludselig ikke har forbindelse mere
            var dis = _initialContext.GetObject<Disconnector>("adac6a70-f5fc-46ba-97ae-8c65e256a222");
            var disTest = _initialContext.GetNeighborConductingEquipments(dis);


            var filtered = FilterHelper.Filter(_initialContext, new FilterRule() {
                MinVoltageLevel = 10000,
                IncludeSpecificSubstations = new HashSet<string> { "HAT", "BYG", "KRL"},
                IncludeSpecificLines = new HashSet<string> { "BYG-HAT", "HAT-KRL" }
            });

            var mappingContext = new MappingContext();

            // Reinitialize cim context to filtered objects
            CimContext _context = CimContext.Create(filtered);


            var converter = new PNM2PowerFactoryConverter(filtered,
               new List<IPreProcessor> {
                    new ACLSMerger(mappingContext),
                    new TransformerCableMerger(mappingContext),
                    new BigEnergyConsumerHandler(mappingContext),
                    new PowerFactoryDataPrepareAndFix(mappingContext)
               });

            // Reinitialize cim context to converted objects
            var outputCimObjects = converter.GetCimObjects().ToList();


            // We need to reinitialize context, because converter has modified objects
            _context = CimContext.Create(outputCimObjects);

            var ec = _context.GetObject<EnergyConsumer>("8f5f0b7b-9699-4ab1-97b2-276f273e172c");
            var ecCons = _context.GetConnections(ec);


            var eqWriter = new EQ_Writer(eqTempFileName, _context, mappingContext);
            eqWriter.ForceThreePhases = true;

            var glWriter = new GL_Writer(glTempFileName);
            var aiWriter = new AI_Writer(aiTempFileName, _context, mappingContext);
            var peWriter = new PE_Writer(peTempFileName, _context, mappingContext);


            //////////////////////
            // do the lines
            var lineContext = new LineInfoContext(_context);
            //lineContext.CreateLineInfo();
            Dictionary<SimpleLine, string> lineToGuid = new Dictionary<SimpleLine, string>();


            foreach (var line in lineContext.GetLines())
            {
                var lineGuid = GUIDHelper.CreateDerivedGuid(Guid.Parse(line.Children[0].Equipment.mRID), 678, true).ToString();
                lineToGuid.Add(line, lineGuid);

                //eqWriter.AddLine(lineGuid, line.Name);

            }
            

            //////////////////////
            // do the general cim objects
            foreach (var cimObject in _context.GetAllObjects())
            {
                if (!(cimObject is Location) && !(cimObject is VoltageLevel && ((VoltageLevel)cimObject).BaseVoltage < 400))
                {
                    if (cimObject is ACLineSegment)
                    {
                        var acls = cimObject as ACLineSegment;

                        var lines = lineContext.GetLines().Where(l => l.Children.Exists(c => c.Equipment == acls)).ToList();

                        if (lines.Count == 1)
                        {
                            var line = lines[0];
                            eqWriter.AddPNMObject(acls, lineToGuid[line]);
                        }
                        else
                            eqWriter.AddPNMObject((dynamic)cimObject);

                    }
                    else
                    {
                        // Don't add things that goes into asset and protectionn file
                        if (!(
                            (cimObject is PhysicalNetworkModel.Asset) ||
                            (cimObject is PhysicalNetworkModel.AssetInfo) ||
                            (cimObject is PhysicalNetworkModel.ProductAssetModel) ||
                            (cimObject is PhysicalNetworkModel.Manufacturer) ||
                            (cimObject is CurrentTransformerExt) ||
                            (cimObject is PotentialTransformer) ||
                            (cimObject is ProtectionEquipmentExt) 
                            ))
                        eqWriter.AddPNMObject((dynamic)cimObject);
                    }
                }

                if (cimObject is PowerSystemResource)
                {
                    var psrObj = cimObject as PowerSystemResource;

                    if (psrObj.Location != null && psrObj.Location.@ref != null)
                    {
                        var loc = _context.GetObject<PhysicalNetworkModel.LocationExt>(psrObj.Location.@ref);
                        glWriter.AddLocation(Guid.Parse(psrObj.mRID), loc);
                    }
                    if (psrObj.Assets != null && psrObj.Assets.@ref != null)
                        assetToEqRefs.Add(psrObj.Assets.@ref, psrObj.mRID);
                }
            }



            // Association: cim:Terminal.ConnectivityNode refers to object: _aa2161c0-1ab4-7756-833e-20b8f8f9afa3 which cannot be found in referenced models.
            var pfCnError1 = _context.GetObject<IdentifiedObject>("aa2161c0-1ab4-7756-833e-20b8f8f9afa3");
            var pfTerminalError1 = _context.GetObject<IdentifiedObject>("7f1f36be-efb2-4c54-8113-1e8df6cead78") as Terminal;
            var pfTerminalError1Ce = _context.GetObject<IdentifiedObject>(pfTerminalError1.ConductingEquipment.@ref);

            // Association: cim:ConnectivityNode.ConnectivityNodeContainer refers to object: _e4bd1ea6-1902-4fc7-9849-cf99341aa929 which cannot be found in referenced models.
            //var pfCnContainerError = _context.GetObject<IdentifiedObject>("e4bd1ea6-1902-4fc7-9849-cf99341aa929");

            // _43ad242e-30e2-7049-bd2a-696a3f850578


            // Write acls asset to asset file

            //////////////////////
            // do the asset object
            foreach (var cimObject in _context.GetAllObjects())
            {
                if (cimObject is PhysicalNetworkModel.Asset)
                {
                    if (assetToEqRefs.ContainsKey(cimObject.mRID))
                    {
                        var eqMrid = assetToEqRefs[cimObject.mRID];
                        aiWriter.AddPNMObject((dynamic)cimObject, eqMrid);
                    }
                }

                if (cimObject is PhysicalNetworkModel.AssetInfo)
                    aiWriter.AddPNMObject((dynamic)cimObject);

                if (cimObject is PhysicalNetworkModel.ProductAssetModel)
                    aiWriter.AddPNMObject((dynamic)cimObject);

                if (cimObject is PhysicalNetworkModel.Manufacturer)
                    aiWriter.AddPNMObject((dynamic)cimObject);

            }

            //////////////////////
            // do the projection object
            foreach (var cimObject in _context.GetAllObjects())
            {
                if (cimObject is PhysicalNetworkModel.ProtectionEquipment)
                {
                    peWriter.AddPNMObject((dynamic)cimObject);
                }
                if (cimObject is PhysicalNetworkModel.PotentialTransformer)
                {
                    peWriter.AddPNMObject((dynamic)cimObject);
                }
                if (cimObject is PhysicalNetworkModel.CurrentTransformer)
                {
                    peWriter.AddPNMObject((dynamic)cimObject);
                }
            }


            /*

            foreach (var cimObj in _context.GetAllObjects().Where(o => o is ACLineSegment))
            {
                var acls = cimObj as ACLineSegment;

                if (acls.PSRType == "OverheadLine")
                {
                }

                if (acls.mRID == "61d60dd0-5283-45cb-8511-1a948b33e862")
                {
                }

                if (acls.Assets.@ref != null)
                {
                    var asset = _initialContext.GetObject<DAX.CIM.PhysicalNetworkModel.Asset>(acls.Assets.@ref);

                    asset.name = acls.name + " " + asset.name;

                    aiWriter.AddPNMObject(asset, acls.mRID);

                    if (asset.AssetInfo != null)
                    {
                        var assetInfo = _initialContext.GetObject<DAX.CIM.PhysicalNetworkModel.AssetInfo>(asset.AssetInfo.@ref);

                        aiWriter.AddPNMObject(assetInfo);
                    }
                }
            }
            */

            /*
            // Create line measurment asset
            var assetInfo2 = new PhysicalNetworkModel.CableInfoExt();
            assetInfo2.mRID = Guid.NewGuid().ToString();
            assetInfo2.name = "BYG-HAT measured line test data";
            assetInfo2.x = new Reactance() { Value = 0.247};
            assetInfo2.x0 = new Reactance() { Value = 2.563};
            assetInfo2.r = new Resistance() { Value = 0.131};
            assetInfo2.r0 = new Resistance() { Value = 0.684 };
            aiWriter.AddPNMObject(assetInfo2);

            var asset2 = new PhysicalNetworkModel.Asset();
            asset2.mRID = Guid.NewGuid().ToString();
            asset2.name = "BYG-HAT measured line test data";
            asset2.type = "Measured line";
            asset2.AssetInfo = new AssetAssetInfo() { @ref = assetInfo2.mRID };
            aiWriter.AddPNMObject(asset2, lineToGuid.First(o => o.Key.Name == "BYG-HAT").Value);
            */
            eqWriter.Close();
            glWriter.Close();
            aiWriter.Close();
            peWriter.Close();

            //File.Delete(aiTempFileName);

            string startPath = folder + "\\files";
            string zipPath = folder + "\\konstant_byg_hat_krl_test.zip";

            File.Delete(zipPath);

            ZipFile.CreateFromDirectory(startPath, zipPath);

        }

        /*
        [TestMethod]
        public void TestEngumEQWriter()
        {
            bool includeAll = false;

            var mappingContext = new MappingContext();

            var converter = new PNM2PowerFactoryConverter(_context.GetAllObjects(), 
                new List<IPreProcessor> {
                    new ACLSMerger(mappingContext),
                    new NetSamConverter(mappingContext)
                });

            var outputCimObjects = converter.GetCimObjects().ToList();

            // We need to reinitialize context, because converter has modified objects
            _context = CimContext.Create(outputCimObjects);

            var eqWriter = new EQ_Writer(eqTempFileName, _context, mappingContext);
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

*/

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

            var converter = new EQ_Writer(eqTempFileName, null, null);

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