using DAX.CIM.PFAdapter.PreProcessors;
using DAX.CIM.PhysicalNetworkModel.Traversal;
using DAX.CIM.PhysicalNetworkModel.Traversal.Extensions;
using DAX.IO.CIM;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace DAX.CIM.PFAdapter.CGMES
{
    /// <summary>
    /// CGMES equipment (EG) profil RDF/XML builder
    /// Very ugly - just for quick prototyping purpose. Should be refactored to use RDF library.
    /// </summary>
    public class EQ_Writer
    {
        public static DateTime _timeStamp = DateTime.Now;
        public static string _eqModelId = "b8a2ec4d-8337-4a1c-9aec-32b8335435c0";
        public static string _netName = "Konstant";

        public bool ForceThreePhases = false;

        string _fileName = null;
        StreamWriter _writer = null;
        CimContext _cimContext = null;

        private string GetStartContent()
        {
            return (

              @"<?xml version='1.0' encoding='UTF-8'?>
  <rdf:RDF xmlns:cim='http://iec.ch/TC57/2013/CIM-schema-cim16#' xmlns:entsoe='http://entsoe.eu/CIM/SchemaExtension/3/1#' xmlns:md='http://iec.ch/TC57/61970-552/ModelDescription/1#' xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'>
  <md:FullModel rdf:about='" + EQ_Writer._eqModelId.ToString() + @"'>
    <md:Model.scenarioTime>" + _timeStamp.ToString() + @"</md:Model.scenarioTime>
    <md:Model.created>" + _timeStamp.ToString() + @"</md:Model.created>    
    <md:Model.description>DAX Konstant PowerFactory Export</md:Model.description>
    <md:Model.version>4</md:Model.version>
	<md:Model.profile>http://entsoe.eu/CIM/EquipmentCore/3/1</md:Model.profile>
    <md:Model.profile>http://entsoe.eu/CIM/EquipmentOperation/3/1</md:Model.profile>
    <md:Model.modelingAuthoritySet>http://NRGi.dk/Planning/1</md:Model.modelingAuthoritySet>
  </md:FullModel>

  <cim:GeographicalRegion rdf:ID='_0472f5a6-c766-11e1-8775-005056c00008'>
    <cim:IdentifiedObject.name>" + _netName + @"</cim:IdentifiedObject.name>
  </cim:GeographicalRegion>

  <cim:SubGeographicalRegion rdf:ID='_0472a781-c766-11e1-8775-005056c00008'>
    <cim:IdentifiedObject.name>" + _netName + @"</cim:IdentifiedObject.name>
    <cim:SubGeographicalRegion.Region rdf:resource='#_0472f5a6-c766-11e1-8775-005056c00008' />
  </cim:SubGeographicalRegion>
 
  <cim:BaseVoltage rdf:ID='_c6dd6dc7-d8b0-4beb-b78d-9e472b038ffc'>
    <cim:IdentifiedObject.name>0.4</cim:IdentifiedObject.name>
    <cim:BaseVoltage.nominalVoltage>0.4</cim:BaseVoltage.nominalVoltage>
  </cim:BaseVoltage>

  <cim:BaseVoltage rdf:ID='_c63f79cc-7953-4ab6-9fa6-f8c729bf895b'>
    <cim:IdentifiedObject.name>10.00</cim:IdentifiedObject.name>
    <cim:BaseVoltage.nominalVoltage>10.5</cim:BaseVoltage.nominalVoltage>
  </cim:BaseVoltage>

  <cim:BaseVoltage rdf:ID='_c1f24620-610b-41dd-bc75-7f14a7bad90f'>
    <cim:IdentifiedObject.name>15.00</cim:IdentifiedObject.name>
    <cim:BaseVoltage.nominalVoltage>15.2</cim:BaseVoltage.nominalVoltage>
  </cim:BaseVoltage>

  <cim:BaseVoltage rdf:ID='_60ee59f3-5ed7-4551-b623-f4346554b22a'>
    <cim:IdentifiedObject.name>60.00</cim:IdentifiedObject.name>
    <cim:BaseVoltage.nominalVoltage>64</cim:BaseVoltage.nominalVoltage>
  </cim:BaseVoltage>

  <cim:OperationalLimitType rdf:ID='_b05800c4-9744-45d8-8d9e-c1f39562e4fb'>
	<cim:IdentifiedObject.name>PATL</cim:IdentifiedObject.name>
	<entsoe:OperationalLimitType.limitType rdf:resource='http://entsoe.eu/CIM/SchemaExtension/3/1#LimitTypeKind.patl' />
  </cim:OperationalLimitType>

");
        }


        Dictionary<double, string> _baseVoltageIdLookup = new Dictionary<double, string>();

        HashSet<string> _cnAlreadyAdded = new HashSet<string>();

        MappingContext _mappingContext;

        public EQ_Writer(string fileName, CimContext cimContext, MappingContext mappingContext, Guid modelId, string netName)
        {
            _fileName = fileName;
            _cimContext = cimContext;
            _mappingContext = mappingContext;
            _eqModelId = modelId.ToString();
            _netName = netName;

            _baseVoltageIdLookup.Add(400, "c6dd6dc7-d8b0-4beb-b78d-9e472b038ffc");
            _baseVoltageIdLookup.Add(10000, "c63f79cc-7953-4ab6-9fa6-f8c729bf895b");
            _baseVoltageIdLookup.Add(15000, "c1f24620-610b-41dd-bc75-7f14a7bad90f");
            _baseVoltageIdLookup.Add(30000, "4eb4495e-ddff-4fd9-85d6-c09c2486ac9a");
            _baseVoltageIdLookup.Add(60000, "60ee59f3-5ed7-4551-b623-f4346554b22a");

            Open();
        }

        private void Open()
        {
            _writer = new StreamWriter(_fileName, false, Encoding.UTF8);
            _writer.Write(GetStartContent());
            _writer.Write("\r\n\r\n");
        }

        public void Close()
        {
            string xml = "</rdf:RDF>\r\n";
            _writer.Write(xml);
            _writer.Close();
        }

        /* ENTSO-E example
        <cim:ExternalNetworkInjection rdf:ID="_4c3f8092-0e83-9145-b993-765c24aeb0d3">
		    <cim:IdentifiedObject.name>EX_NET_INJ_1</cim:IdentifiedObject.name>
		    <cim:Equipment.EquipmentContainer rdf:resource="#_d0486169-2205-40b2-895e-b672ecb9e5fc"/>
		    <cim:ExternalNetworkInjection.governorSCD>0.99</cim:ExternalNetworkInjection.governorSCD>
		    <cim:ExternalNetworkInjection.maxP>99.99</cim:ExternalNetworkInjection.maxP>
		    <cim:ExternalNetworkInjection.minP>9.99</cim:ExternalNetworkInjection.minP>
		    <cim:ExternalNetworkInjection.maxQ>99.99</cim:ExternalNetworkInjection.maxQ>
		    <cim:ExternalNetworkInjection.minQ>9.99</cim:ExternalNetworkInjection.minQ>
		    <cim:ConductingEquipment.BaseVoltage rdf:resource="#_63893f24-5b4e-407c-9a1e-4ff71121f33c"/>
		    <cim:RegulatingCondEq.RegulatingControl rdf:resource="#_af8df1d5-124e-d64d-8d29-034dc90e7d77"/>
		    <cim:IdentifiedObject.description>EX_NET_INJ_1</cim:IdentifiedObject.description>
		    <cim:IdentifiedObject.mRID>4c3f8092-0e83-9145-b993-765c24aeb0d3</cim:IdentifiedObject.mRID>
		    <entsoe:IdentifiedObject.shortName>EX_NET_INJ_1</entsoe:IdentifiedObject.shortName>
		    <entsoe:IdentifiedObject.energyIdentCodeEic>00X-BE-BE-000547</entsoe:IdentifiedObject.energyIdentCodeEic>
		    <cim:Equipment.aggregate>false</cim:Equipment.aggregate>
	    </cim:ExternalNetworkInjection>


     <cim:Line rdf:ID="_2b659afe-2ac3-425c-9418-3383e09b4b39">
		<cim:IdentifiedObject.name>container of BE-Line_1</cim:IdentifiedObject.name>
		<cim:Line.Region rdf:resource="#_c1d5c0378f8011e08e4d00247eb1f55e"/>
		<cim:IdentifiedObject.description>container of BE-Line_1</cim:IdentifiedObject.description>
		<cim:IdentifiedObject.mRID>2b659afe-2ac3-425c-9418-3383e09b4b39</cim:IdentifiedObject.mRID>
		<entsoe:IdentifiedObject.shortName>BE-Line_1</entsoe:IdentifiedObject.shortName>
		<entsoe:IdentifiedObject.energyIdentCodeEic>00X-BE-BE-000561</entsoe:IdentifiedObject.energyIdentCodeEic>
	 </cim:Line>

     <cim:ACLineSegment rdf:ID="_17086487-56ba-4979-b8de-064025a6b4da">
		<cim:IdentifiedObject.name>BE-Line_1</cim:IdentifiedObject.name>
		<cim:Equipment.EquipmentContainer rdf:resource="#_2b659afe-2ac3-425c-9418-3383e09b4b39"/>
		<cim:ACLineSegment.r>2.200000</cim:ACLineSegment.r>
		<cim:ACLineSegment.x>68.200000</cim:ACLineSegment.x>
		<cim:ACLineSegment.bch>0.0000829380</cim:ACLineSegment.bch>
		<cim:Conductor.length>22.000000</cim:Conductor.length>
		<cim:ACLineSegment.gch>0.0000308000</cim:ACLineSegment.gch>
		<cim:Equipment.aggregate>false</cim:Equipment.aggregate>
		<cim:ConductingEquipment.BaseVoltage rdf:resource="#_7891a026ba2c42098556665efd13ba94"/>
		<entsoe:IdentifiedObject.shortName>BE-L_1</entsoe:IdentifiedObject.shortName>
		<entsoe:IdentifiedObject.energyIdentCodeEic>10T-AT-DE-000061</entsoe:IdentifiedObject.energyIdentCodeEic>
		<cim:IdentifiedObject.description>10T-AT-DE-000061</cim:IdentifiedObject.description>
		<cim:IdentifiedObject.mRID>17086487-56ba-4979-b8de-064025a6b4da</cim:IdentifiedObject.mRID>
	</cim:ACLineSegment>

    <cim:RatioTapChanger rdf:ID="_641b8688-b0bc-49c3-9a49-f129851deb4c">
		<cim:IdentifiedObject.name>BE HVDC TR1</cim:IdentifiedObject.name>
		<cim:TapChanger.neutralU>225.000000</cim:TapChanger.neutralU>
		<cim:TapChanger.lowStep>1</cim:TapChanger.lowStep>
		<cim:TapChanger.highStep>21</cim:TapChanger.highStep>
		<cim:TapChanger.neutralStep>11</cim:TapChanger.neutralStep>
		<cim:TapChanger.normalStep>7</cim:TapChanger.normalStep>
		<cim:RatioTapChanger.stepVoltageIncrement>1.000000</cim:RatioTapChanger.stepVoltageIncrement>
		<cim:TapChanger.ltcFlag>true</cim:TapChanger.ltcFlag>
		<cim:TapChanger.TapChangerControl rdf:resource="#_87e1f736-bde8-499e-abf7-90b310825fb1"/>
		<cim:RatioTapChanger.tculControlMode rdf:resource="http://iec.ch/TC57/2013/CIM-schema-cim16#TransformerControlMode.volt"/>
		<cim:RatioTapChanger.TransformerEnd rdf:resource="#_c9989583-d2c3-4d52-9f3b-7a98a8211fb6"/>
		<cim:RatioTapChanger.RatioTapChangerTable rdf:resource="#_5350c2f2-d5f3-8a46-b181-ff619d3cec9d"/>
		<entsoe:IdentifiedObject.shortName>BE HVDC TR1</entsoe:IdentifiedObject.shortName>
		<cim:IdentifiedObject.description>BE HVDC TR1</cim:IdentifiedObject.description>
		<cim:IdentifiedObject.mRID>641b8688-b0bc-49c3-9a49-f129851deb4c</cim:IdentifiedObject.mRID>
		<entsoe:IdentifiedObject.energyIdentCodeEic>00X-BE-BE-000683</entsoe:IdentifiedObject.energyIdentCodeEic>
	</cim:RatioTapChanger>

        */

        public void AddLine(string mrid, string name)
        {
            string xml = "<cim:Line rdf:ID = '_" + mrid + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(name) + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Line.Region rdf:resource='#_0472a781-c766-11e1-8775-005056c00008'/>\r\n";
            xml += "</cim:Line>\r\n\r\n";

            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.ExternalNetworkInjection eni)
        {
            string xml = "<cim:ExternalNetworkInjection rdf:ID='_" + eni.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(eni.name) + "</cim:IdentifiedObject.name>\r\n";

            if (eni.EquipmentContainer != null)
                xml += "  <cim:Equipment.EquipmentContainer rdf:resource = '#_" + eni.EquipmentContainer.@ref + "'/>\r\n";

            xml += "  <cim:ConductingEquipment.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(eni.BaseVoltage) + "'/>\r\n";

            if (eni.governorSCD != null)
                xml += "  <cim:ExternalNetworkInjection.governorSCD>" + DoubleToString(eni.governorSCD.Value) + "</cim:ExternalNetworkInjection.governorSCD>\r\n";

            if (eni.maxP != null)
                xml += "  <cim:ExternalNetworkInjection.maxP>" + DoubleToString(eni.maxP.Value) + "</cim:ExternalNetworkInjection.maxP>\r\n";

            if (eni.minP != null)
                xml += "  <cim:ExternalNetworkInjection.minP>" + DoubleToString(eni.minP.Value) + "</cim:ExternalNetworkInjection.minP>\r\n";

            if (eni.maxQ != null)
                xml += "  <cim:ExternalNetworkInjection.maxQ>" + DoubleToString(eni.maxQ.Value) + "</cim:ExternalNetworkInjection.maxQ>\r\n";

            if (eni.minQ != null)
                xml += "  <cim:ExternalNetworkInjection.minQ>" + DoubleToString(eni.minQ.Value) + "</cim:ExternalNetworkInjection.minQ>\r\n";

            xml += "  <cim:ExternalNetworkInjection.maxR0ToX0Ratio>" + DoubleToString(eni.maxR0ToX0Ratio) + "</cim:ExternalNetworkInjection.maxR0ToX0Ratio>\r\n";
            xml += "  <cim:ExternalNetworkInjection.maxR1ToX1Ratio>" + DoubleToString(eni.maxR1ToX1Ratio) + "</cim:ExternalNetworkInjection.maxR1ToX1Ratio>\r\n";
            xml += "  <cim:ExternalNetworkInjection.maxZ0ToZ1Ratio>" + DoubleToString(eni.maxZ0ToZ1Ratio) + "</cim:ExternalNetworkInjection.maxZ0ToZ1Ratio>\r\n";

            xml += "  <cim:ExternalNetworkInjection.minR0ToX0Ratio>" + DoubleToString(eni.minR0ToX0Ratio) + "</cim:ExternalNetworkInjection.minR0ToX0Ratio>\r\n";
            xml += "  <cim:ExternalNetworkInjection.minR1ToX1Ratio>" + DoubleToString(eni.minR1ToX1Ratio) + "</cim:ExternalNetworkInjection.minR1ToX1Ratio>\r\n";
            xml += "  <cim:ExternalNetworkInjection.minZ0ToZ1Ratio>" + DoubleToString(eni.minZ0ToZ1Ratio) + "</cim:ExternalNetworkInjection.minZ0ToZ1Ratio>\r\n";

     
            if (eni.minInitialSymShCCurrent != null)
                xml += "  <cim:ExternalNetworkInjection.minInitialSymShCCurrent>" + DoubleToString(eni.minInitialSymShCCurrent.Value * 1000) + "</cim:ExternalNetworkInjection.minInitialSymShCCurrent>\r\n";

            if (eni.maxInitialSymShCCurrent != null)
                xml += "  <cim:ExternalNetworkInjection.maxInitialSymShCCurrent>" + DoubleToString(eni.maxInitialSymShCCurrent.Value * 1000) + "</cim:ExternalNetworkInjection.maxInitialSymShCCurrent>\r\n";

            if (eni.voltageFactor != null)
                xml += "  <cim:ExternalNetworkInjection.voltageFactor>" + DoubleToString(eni.voltageFactor.Value) + "</cim:ExternalNetworkInjection.voltageFactor>\r\n";


            xml += "  <cim:Equipment.aggregate>false</cim:Equipment.aggregate>\r\n";

            xml += "  <cim:ExternalNetworkInjection.governorSCD>0</cim:ExternalNetworkInjection.governorSCD>\r\n";

             xml += "</cim:ExternalNetworkInjection>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.Substation substation)
        {
            string xml = "<cim:Substation rdf:ID = '_" + substation.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(substation.name) + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Substation.Region rdf:resource='#_0472a781-c766-11e1-8775-005056c00008'/>\r\n";
            xml += "</cim:Substation>\r\n\r\n";

            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.VoltageLevel vl)
        {
            string xml = "<cim:VoltageLevel rdf:ID = '_" + vl.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + vl.name + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:VoltageLevel.Substation rdf:resource = '#_" + vl.EquipmentContainer1.@ref + "'/>\r\n";
            xml += "  <cim:VoltageLevel.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(vl.BaseVoltage) + "'/>\r\n";
            xml += "</cim:VoltageLevel>\r\n\r\n";

            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.Bay bay)
        {
            string xml = "<cim:Bay rdf:ID = '_" + bay.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(bay.name) + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Bay.VoltageLevel rdf:resource = '#_" + bay.VoltageLevel.@ref + "'/>\r\n";
            xml += "</cim:Bay>\r\n\r\n";

            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.ACLineSegment acls, string lineMrid = null)
        {
            string xml = "<cim:ACLineSegment rdf:ID='_" + acls.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(acls.name) + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Equipment.aggregate>false</cim:Equipment.aggregate>\r\n";
            xml += "  <cim:ConductingEquipment.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(acls.BaseVoltage) + "'/>\r\n";

            if (lineMrid != null)
            {
                xml += "  <cim:Equipment.EquipmentContainer rdf:resource='#_" + lineMrid + "'/>\r\n";
            }

            // PF CGMES expect km, but vi got m in GIS, so we need to divede by 1000
            xml += "  <cim:Conductor.length>" + DoubleToString(acls.length.Value / 1000) + "</cim:Conductor.length>\r\n";

            
            if (acls.b0ch != null)
                xml += "  <cim:ACLineSegment.b0ch>" + DoubleToString(acls.b0ch.Value / 1000000) + "</cim:ACLineSegment.b0ch>\r\n";
            if (acls.bch != null)
                xml += "  <cim:ACLineSegment.bch>" + DoubleToString(acls.bch.Value / 1000000) + "</cim:ACLineSegment.bch>\r\n";
            if (acls.g0ch != null)
                xml += "  <cim:ACLineSegment.g0ch>" + DoubleToString(acls.g0ch.Value / 1000000) + "</cim:ACLineSegment.g0ch>\r\n";
            if (acls.gch != null)
                xml += "  <cim:ACLineSegment.gch>" + DoubleToString(acls.gch.Value / 1000000) + "</cim:ACLineSegment.gch>\r\n";
            if (acls.r != null)
                xml += "  <cim:ACLineSegment.r>" + DoubleToString(acls.r.Value) + "</cim:ACLineSegment.r>\r\n";
            if (acls.r0 != null)
                xml += "  <cim:ACLineSegment.r0>" + DoubleToString(acls.r0.Value) + "</cim:ACLineSegment.r0>\r\n";
            if (acls.x != null)
                xml += "  <cim:ACLineSegment.x>" + DoubleToString(acls.x.Value) + "</cim:ACLineSegment.x>\r\n";
            if (acls.x0 != null)
                xml += "  <cim:ACLineSegment.x0>" + DoubleToString(acls.x0.Value) + "</cim:ACLineSegment.x0>\r\n";
            

            xml += "</cim:ACLineSegment>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.LoadBreakSwitch ls)
        {
            string xml = "<cim:LoadBreakSwitch rdf:ID='_" + ls.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(ls.name) + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Equipment.EquipmentContainer rdf:resource = '#_" + ls.EquipmentContainer.@ref + "'/>\r\n";
            xml += "  <cim:ConductingEquipment.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(ls.BaseVoltage) + "'/>\r\n";

            string normalOpen = "false";
            if (ls.normalOpen)
                normalOpen = "true";

            xml += "  <cim:Switch.normalOpen>" + normalOpen + "</cim:Switch.normalOpen>\r\n";
            xml += "  <cim:Switch.retained>false</cim:Switch.retained>\r\n";
            xml += "</cim:LoadBreakSwitch>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.Breaker ls)
        {
            string xml = "<cim:Breaker rdf:ID='_" + ls.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(ls.name) + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Equipment.EquipmentContainer rdf:resource = '#_" + ls.EquipmentContainer.@ref + "'/>\r\n";
            xml += "  <cim:ConductingEquipment.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(ls.BaseVoltage) + "'/>\r\n";

            string normalOpen = "false";
            if (ls.normalOpen)
                normalOpen = "true";

            xml += "  <cim:Switch.normalOpen>" + normalOpen + "</cim:Switch.normalOpen>\r\n";
            xml += "  <cim:Switch.retained>false</cim:Switch.retained>\r\n";
            xml += "</cim:Breaker>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.Disconnector dis)
        {
            string xml = "<cim:Disconnector rdf:ID='_" + dis.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(dis.name) + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Equipment.EquipmentContainer rdf:resource = '#_" + dis.EquipmentContainer.@ref + "'/>\r\n";
            xml += "  <cim:ConductingEquipment.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(dis.BaseVoltage) + "'/>\r\n";

            string normalOpen = "false";
            if (dis.normalOpen)
                normalOpen = "true";

            xml += "  <cim:Switch.normalOpen>" + normalOpen + "</cim:Switch.normalOpen>\r\n";
            xml += "  <cim:Switch.retained>false</cim:Switch.retained>\r\n";
            xml += "</cim:Disconnector>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.Fuse fuse)
        {
            // create disconnector, because PF thinks that fuse is not a conducting equipment
            string xml = "<cim:Disconnector rdf:ID='_" + fuse.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(fuse.name) + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Equipment.EquipmentContainer rdf:resource = '#_" + fuse.EquipmentContainer.@ref + "'/>\r\n";
            xml += "  <cim:ConductingEquipment.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(fuse.BaseVoltage) + "'/>\r\n";

            string normalOpen = "false";
            if (fuse.normalOpen)
                normalOpen = "true";

            xml += "  <cim:Switch.normalOpen>" + normalOpen + "</cim:Switch.normalOpen>\r\n";
            xml += "  <cim:Switch.retained>false</cim:Switch.retained>\r\n";
            xml += "</cim:Disconnector>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.PetersenCoil coil)
        {
            string xml = "<cim:PetersenCoil rdf:ID='_" + coil.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(coil.name) + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Equipment.EquipmentContainer rdf:resource = '#_" + coil.EquipmentContainer.@ref + "'/>\r\n";
            xml += "  <cim:ConductingEquipment.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(coil.BaseVoltage) + "'/>\r\n";

            if (coil.nominalU != null)
                xml += "  <cim:PetersenCoil.nominalU>" + DoubleToString(ConvertToEntsoeVoltage(coil.nominalU.Value)) + "</cim:PetersenCoil.nominalU>\r\n";

            if (coil.positionCurrent != null)
                xml += "  <cim:PetersenCoil.positionCurrent>" + DoubleToString(coil.positionCurrent.Value) + "</cim:PetersenCoil.positionCurrent>\r\n";

            if (coil.offsetCurrent != null)
                xml += "  <cim:PetersenCoil.offsetCurrent>" + DoubleToString(coil.offsetCurrent.Value) + "</cim:PetersenCoil.offsetCurrent>\r\n";

            if (coil.r != null)
                xml += "  <cim:EarthFaultCompensator.r>" + DoubleToString(coil.r.Value) + "</cim:EarthFaultCompensator.r>\r\n";

            if (coil.xGroundMin != null)
                xml += "  <cim:PetersenCoil.xGroundMin>" + DoubleToString(coil.xGroundMin.Value) + "</cim:PetersenCoil.xGroundMin>\r\n";

            if (coil.xGroundMax != null)
                xml += "  <cim:PetersenCoil.xGroundMax>" + DoubleToString(coil.xGroundMax.Value) + "</cim:PetersenCoil.xGroundMax>\r\n";

            if (coil.xGroundNominal != null)
                xml += "  <cim:PetersenCoil.xGroundNominal>" + DoubleToString(coil.xGroundNominal.Value) + "</cim:PetersenCoil.xGroundNominal>\r\n";

            xml += "</cim:PetersenCoil>\r\n\r\n";
            _writer.Write(xml);
        }


        public void AddPNMObject(PhysicalNetworkModel.BusbarSection busbar)
        {
            string xml = "<cim:BusbarSection rdf:ID = '_" + busbar.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(busbar.name) + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Equipment.EquipmentContainer rdf:resource = '#_" + busbar.EquipmentContainer.@ref + "'/>\r\n";
            xml += "  <cim:ConductingEquipment.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(busbar.BaseVoltage) + "'/>\r\n";
            xml += "</cim:BusbarSection>\r\n\r\n";

            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.Terminal terminal)
        {

            if (terminal.ConductingEquipment.@ref == "76c96252-b533-44b1-aac2-9efb184dc9e7")
            {

            }

                 
            string xml = "<cim:Terminal rdf:ID = '_" + terminal.mRID + "'>\r\n";
            if (terminal.name == null)
                xml += "  <cim:IdentifiedObject.name>" + "T" + terminal.sequenceNumber + "</cim:IdentifiedObject.name>\r\n";
            else
                xml += "  <cim:IdentifiedObject.name>" + terminal.name + "</cim:IdentifiedObject.name>\r\n";

            xml += "  <cim:ACDCTerminal.sequenceNumber>" + terminal.sequenceNumber + "</cim:ACDCTerminal.sequenceNumber>\r\n";

            // ABCN on everything except cables, to avoid connectivity struggle issues inside substation in PF
            var ci = _cimContext.GetObject<PhysicalNetworkModel.ConductingEquipment>(terminal.ConductingEquipment.@ref);

            if (ci is PhysicalNetworkModel.ACLineSegment)
                xml += "  <cim:Terminal.phases rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#PhaseCode.ABC'/>\r\n";
            else
                xml += "  <cim:Terminal.phases rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#PhaseCode.ABCN'/>\r\n";

            /*
            if (!ForceThreePhases)
                xml += "  <cim:Terminal.phases rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#PhaseCode." + terminal.phases.ToString() + "'/>\r\n";
            else
                xml += "  <cim:Terminal.phases rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#PhaseCode.ABCN'/>\r\n";
            */


            if (terminal.ConnectivityNode != null)
                xml += "  <cim:Terminal.ConnectivityNode rdf:resource = '#_" + terminal.ConnectivityNode.@ref + "'/>\r\n";

            xml += "  <cim:Terminal.ConductingEquipment rdf:resource='#_" + terminal.ConductingEquipment.@ref + "'/>\r\n";
            xml += "</cim:Terminal>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.ConnectivityNode cn)
        {
            if (cn.mRID == "9ecf20a1-980c-74d3-b1ab-67d2fc9180bb")
            {

            }

            if (!_cnAlreadyAdded.Contains(cn.mRID))
            {
                var neighbors = cn.GetNeighborConductingEquipments();

                // Only add cn if we can find a voltage level to ref it to. PF requires this.
                if (_mappingContext.ConnectivityNodeToVoltageLevel.ContainsKey(cn) || neighbors.Exists(o => o is PhysicalNetworkModel.ConductingEquipment && o.BaseVoltage > 0))
                {

                    string xml = "<cim:ConnectivityNode rdf:ID='_" + cn.mRID + "'>\r\n";

                    if (cn.name != null)
                        xml += "  <cim:IdentifiedObject.name>" + cn.name + "</cim:IdentifiedObject.name>\r\n";

                    if (!cn.IsInsideSubstation())
                    {
                        //cn.IsInsideSubstation();
                        //throw new ArgumentException("Connectivity Node with mRID=" + cn.mRID + " has no parent. This will not work in PowerFactory CGMES import.");
                    }
                    string vlMrid = null;

                    if (_mappingContext.ConnectivityNodeToVoltageLevel.ContainsKey(cn))
                    {
                        vlMrid = _mappingContext.ConnectivityNodeToVoltageLevel[cn].mRID;
                    }
                    else
                    {
                        // Ok, we must be in a real substation then. Try find voltage level on neighboor
                        var cnSt = cn.GetSubstation(true, _cimContext);
                      


                        var fistCi = neighbors.First(o => o is PhysicalNetworkModel.ConductingEquipment && o.BaseVoltage > 0);
                        var cnVl = cnSt.GetVoltageLevel(fistCi.BaseVoltage, false);

                        if (cnVl != null)
                            vlMrid = cnVl.mRID;

                    }

                    xml += "  <cim:ConnectivityNode.ConnectivityNodeContainer rdf:resource='#_" + vlMrid + "'/>\r\n";
                    xml += "</cim:ConnectivityNode>\r\n\r\n";
                    _writer.Write(xml);
                }
            }

            _cnAlreadyAdded.Add(cn.mRID);
        }

        public void AddPNMObject(PhysicalNetworkModel.PowerTransformer pt)
        {
            string xml = "<cim:PowerTransformer rdf:ID='_" + pt.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(pt.name) + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Equipment.aggregate>false</cim:Equipment.aggregate>\r\n";
            xml += "  <cim:Equipment.EquipmentContainer rdf:resource = '#_" + pt.EquipmentContainer.@ref + "'/>\r\n";
            xml += "</cim:PowerTransformer>\r\n\r\n";
            _writer.Write(xml);
        }
             
        public void AddPNMObject(PhysicalNetworkModel.PowerTransformerEndExt end)
        {
            string xml = "<cim:PowerTransformerEnd rdf:ID='_" + end.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + end.name + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:TransformerEnd.endNumber>" + end.endNumber + "</cim:TransformerEnd.endNumber>\r\n";
            xml += "  <cim:TransformerEnd.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(end.BaseVoltage) + "'/>\r\n";
            xml += "  <cim:TransformerEnd.Terminal rdf:resource='#_" + end.Terminal.@ref + "'/>\r\n";
            xml += "  <cim:PowerTransformerEnd.PowerTransformer rdf:resource = '#_" + end.PowerTransformer.@ref + "'/>\r\n";

            var pt = _cimContext.GetObject<PhysicalNetworkModel.PowerTransformer>(end.PowerTransformer.@ref);
            
            // HACK som skal fjernes
            if (end.endNumber == "1")
            {
                // Lokal trafoer
                if (pt.name.ToLower().Contains("lokal"))
                {
                    end.grounded = false;
                }
                // Mellemspændings trafo
                else if (end.BaseVoltage < 20000)
                {
                    end.grounded = false;
                }
                else
                {
                    end.grounded = false;
                }
            }
           

            if (end.endNumber == "2")
            {
                // Lokal trafoer
                if (pt.name.ToLower().Contains("lokal"))
                {
                    end.grounded = true;
                }
                // Mellemspændings trafo
                else if (end.BaseVoltage < 1000)
                {
                    end.grounded = true;
                }
                else
                {
                    end.grounded = false;
                }
             }


            if (end.phaseAngleClock != null)
                xml += "  <cim:PowerTransformerEnd.phaseAngleClock>" + end.phaseAngleClock + "</cim:PowerTransformerEnd.phaseAngleClock>\r\n";

            if (end.connectionKind != null)
                xml += "  <cim:PowerTransformerEnd.connectionKind rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#WindingConnection." + end.connectionKind + "'/>\r\n";

            if (end.grounded)
                xml += "  <cim:TransformerEnd.grounded>true</cim:TransformerEnd.grounded>\r\n";
            else
                xml += "  <cim:TransformerEnd.grounded>false</cim:TransformerEnd.grounded>\r\n";

            if (end.b != null)
                xml += "  <cim:PowerTransformerEnd.b>"+ end.b.Value.ToString(CultureInfo.InvariantCulture) + "</cim:PowerTransformerEnd.b>\r\n";

            if (end.b0 != null)
                xml += "  <cim:PowerTransformerEnd.b0>" + end.b0.Value.ToString(CultureInfo.InvariantCulture) + "</cim:PowerTransformerEnd.b0>\r\n";

            if (end.g != null)
                xml += "  <cim:PowerTransformerEnd.g>" + end.g.Value.ToString(CultureInfo.InvariantCulture) + "</cim:PowerTransformerEnd.g>\r\n";

            if (end.g0 != null)
                xml += "  <cim:PowerTransformerEnd.g0>" + end.g0.Value.ToString(CultureInfo.InvariantCulture) + "</cim:PowerTransformerEnd.g0>\r\n";

            if (end.r != null)
                xml += "  <cim:PowerTransformerEnd.r>" + end.r.Value.ToString(CultureInfo.InvariantCulture) + "</cim:PowerTransformerEnd.r>\r\n";

            if (end.r0 != null)
                xml += "  <cim:PowerTransformerEnd.r0>" + end.r0.Value.ToString(CultureInfo.InvariantCulture) + "</cim:PowerTransformerEnd.r0>\r\n";

            if (end.x != null)
                xml += "  <cim:PowerTransformerEnd.x>" + end.x.Value.ToString(CultureInfo.InvariantCulture) + "</cim:PowerTransformerEnd.x>\r\n";

            if (end.x0 != null)
                xml += "  <cim:PowerTransformerEnd.x0>" + end.x0.Value.ToString(CultureInfo.InvariantCulture) + "</cim:PowerTransformerEnd.x0>\r\n";

            if (end.ratedU != null)
                xml += "  <cim:PowerTransformerEnd.ratedU>" + (end.ratedU.Value / 1000).ToString(CultureInfo.InvariantCulture) + "</cim:PowerTransformerEnd.ratedU>\r\n";

            if (end.ratedS != null)
                xml += "  <cim:PowerTransformerEnd.ratedS>" + (end.ratedS.Value / 1000).ToString(CultureInfo.InvariantCulture) + "</cim:PowerTransformerEnd.ratedS>\r\n";


            xml += "</cim:PowerTransformerEnd>\r\n\r\n";
            _writer.Write(xml);

            // Check if rating factor is set, and if so add current limit

            if (end.ratingFactor != null && end.ratingFactor.Value != 1)
            {
                double ratingFactor = end.ratingFactor.Value;
                double limit = ((end.ratedS.Value * 1000) / end.ratedU.Value / Math.Sqrt(3)) * ratingFactor;
                AddCurrentLimit(Guid.Parse(end.Terminal.@ref), end.name, limit);
            }


        }

        public void AddCurrentLimit(Guid terminalMrid, string name, double currentLimitValue)
        {
            // Limit set
            string xml = "<cim:OperationalLimitSet rdf:ID='_" + GUIDHelper.CreateDerivedGuid(terminalMrid, 750, true).ToString() + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + name + "_limitset</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:OperationalLimitSet.Terminal rdf:resource = '#_" + terminalMrid.ToString() + "'/>\r\n";
            xml += "</cim:OperationalLimitSet>\r\n\r\n";

            // Current
            xml += "<cim:CurrentLimit rdf:ID='_" + GUIDHelper.CreateDerivedGuid(terminalMrid, 760, true).ToString() + "'>\r\n";
            xml += "  <cim:OperationalLimit.OperationalLimitType rdf:resource='#_b05800c4-9744-45d8-8d9e-c1f39562e4fb' />\r\n";
            xml += "  <cim:IdentifiedObject.name>" + name + "_limit</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:OperationalLimit.OperationalLimitSet rdf:resource = '#_" + GUIDHelper.CreateDerivedGuid(terminalMrid, 750, true).ToString() + "'/>\r\n";
            xml += "  <cim:CurrentLimit.value>" + DoubleToString(currentLimitValue) + "</cim:CurrentLimit.value>\r\n";
            xml += "</cim:CurrentLimit>\r\n\r\n";


            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.EnergyConsumer ec)
        {
            if (ec.EquipmentContainer != null)
            {
                string xml = "<cim:EnergyConsumer rdf:ID = '_" + ec.mRID + "'>\r\n";
                xml += "  <cim:IdentifiedObject.name>" + ec.name + "</cim:IdentifiedObject.name>\r\n";
                xml += "  <cim:Equipment.EquipmentContainer rdf:resource = '#_" + ec.EquipmentContainer.@ref + "'/>\r\n";
                xml += "  <cim:ConductingEquipment.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(ec.BaseVoltage) + "'/>\r\n";
                xml += "  <cim:Equipment.aggregate>false</cim:Equipment.aggregate>\r\n";
                xml += "</cim:EnergyConsumer>\r\n\r\n";

                _writer.Write(xml);
            }
        }

        public void AddPNMObject(PhysicalNetworkModel.RatioTapChanger tap)
        {
            var controlMrid = GUIDHelper.CreateDerivedGuid(Guid.Parse(tap.mRID), 55);


            string xml = "<cim:RatioTapChanger rdf:ID = '_" + tap.mRID + "'>\r\n";

            xml += "  <cim:RatioTapChanger.TransformerEnd rdf:resource='#_" + tap.TransformerEnd.@ref + "'/>\r\n";
            //xml += "  <cim:TapChanger.TapChangerControl rdf:resource='#_" + controlMrid.ToString() + "'/>\r\n";

            xml += "  <cim:IdentifiedObject.name>TAP</cim:IdentifiedObject.name>\r\n";

            if (tap.neutralU != null)
                xml += "  <cim:TapChanger.neutralU>" + DoubleToString(tap.neutralU.Value / 1000) + "</cim:TapChanger.neutralU>\r\n";

            if (tap.neutralStep != null)
                xml += "  <cim:TapChanger.neutralStep>" + tap.neutralStep + "</cim:TapChanger.neutralStep>\r\n";

            if (tap.normalStep != null && tap.normalStep != "")
                xml += "  <cim:TapChanger.normalStep>" + tap.normalStep + "</cim:TapChanger.normalStep>\r\n";

            xml += "  <cim:TapChanger.lowStep>" + tap.lowStep + "</cim:TapChanger.lowStep>\r\n";
            xml += "  <cim:TapChanger.highStep>" + tap.highStep + "</cim:TapChanger.highStep>\r\n";
            xml += "  <cim:TapChanger.ltcFlag>" + (tap.ltcFlag ? "true" : "false") + "</cim:TapChanger.ltcFlag>\r\n";
            xml += "  <cim:RatioTapChanger.tculControlMode rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#TransformerControlMode.volt'/>\r\n";

            if (tap.stepVoltageIncrement != null)
                xml += "  <cim:RatioTapChanger.stepVoltageIncrement>" + DoubleToString(tap.stepVoltageIncrement.Value) + "</cim:RatioTapChanger.stepVoltageIncrement>\r\n";

            xml += "</cim:RatioTapChanger>\r\n\r\n";

            _writer.Write(xml);

           /*
           // Create regulating control if automatic
           if (tap.ltcFlag)
           {

               var ptEnd = _cimContext.GetObject<PhysicalNetworkModel.PowerTransformerEnd>(tap.TransformerEnd.@ref);

               xml = "<cim:TapChangerControl rdf:ID = '_" + controlMrid.ToString() + "'>\r\n";
               xml += "  <cim:IdentifiedObject.name>Tab Controler</cim:IdentifiedObject.name>\r\n";
               xml += "  <cim:RegulatingControl.mode rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#RegulatingControlModeKind.voltage' />\r\n";
               xml += "  <cim:RegulatingControl.Terminal rdf:resource='#_" + ptEnd.Terminal.@ref + "'/>\r\n";

               xml += "</cim:TapChangerControl>\r\n\r\n";
           }
           */
          
        }

        private string GetBaseVoltageId(double voltageLevel)
        {
            if (!_baseVoltageIdLookup.ContainsKey(voltageLevel))
                throw new Exception("Voltage level: " + voltageLevel + " not defined in lookup dictionary!");

            return _baseVoltageIdLookup[voltageLevel];
        }

        private string DoubleToString(double value)
        {
            return value.ToString(CultureInfo.GetCultureInfo("en-GB"));
        }

        private double ConvertToEntsoeVoltage(double value)
        {
            return value / 1000;
        }
    }
}
