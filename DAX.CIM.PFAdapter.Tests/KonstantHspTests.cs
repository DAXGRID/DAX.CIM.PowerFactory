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
        //string folder = @"\\SHOBJPOW01V\c$\DAX\export\hsp_test";
     
        CimContext _initialContext;

        protected override void SetUp()
        {
        }

        [TestMethod]
        public void TestKonstantSouthArea()
        {
            var reader = new CimJsonFileReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"c:\temp\cim\pf_test_syd.jsonl"));

            var cimObjects = reader.Read();

            string folder = @"C:\temp\pf\konstant_syd_test";

            var writer = new KonstantCimArchiveWriter(cimObjects, folder, "konstant_south", Guid.Parse("b8a2ec4d-8337-4a1c-9aec-32b8335435c0"));
        }

        [TestMethod]
        public void TestKonstantNorthArea()
        {
            var reader = new CimJsonFileReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"c:\temp\cim\pf_test_nord.jsonl"));

            var cimObjects = reader.Read();

            string folder = @"\\shokstpow02v\e$\GIS_CIM_EXPORT\\konstant_nord_test";

            var writer = new KonstantCimArchiveWriter(cimObjects, folder, "konstant_north", Guid.Parse("3b697c10-ed30-47bb-98b6-f6960df87a41"));

        }

        [TestMethod]
        public void TestKonstantSouthArea60kVOnly()
        {
            var reader = new CimJsonFileReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"c:\temp\cim\pf_test_syd.jsonl"));

            var cimObjects = reader.Read();

            string folder = @"\\SHOBJPOW01V\c$\gis_cim_export\konstant_syd_60kv_test";

            var writer = new KonstantCimArchiveWriter(cimObjects, folder, "Konstant Syd", Guid.Parse("3527c345-3f5b-4de8-acf4-906b4d0688af"), true);
        }

        [TestMethod]
        public void TestKonstantNorthArea60kVOnly()
        {
            var reader = new CimJsonFileReader(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"c:\temp\cim\pf_test_nord.jsonl"));

            var cimObjects = reader.Read();

            string folder = @"\\SHOBJPOW01V\c$\gis_cim_export\konstant_nord_60kv_test";

            var writer = new KonstantCimArchiveWriter(cimObjects, folder, "Konstant Nord", Guid.Parse("c3cb1ba1-8670-471a-9ba2-9d6d46d1295c"), true);
        }



        [TestMethod]
        public void TestEQWriterACLineSegment()
        {
            //
            string eqTempFileName = "equipment.xml";

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

            var converter = new EQ_Writer(eqTempFileName, null, null, Guid.NewGuid(), "test");

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
