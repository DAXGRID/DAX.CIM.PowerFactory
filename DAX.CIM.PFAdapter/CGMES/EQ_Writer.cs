using DAX.CIM.PhysicalNetworkModel.Traversal;
using DAX.CIM.PhysicalNetworkModel.Traversal.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.CIM.PFAdapter.CGMES
{
    /// <summary>
    /// CGMES equipment (EG) profil RDF/XML builder
    /// Very ugly - just for quick prototyping purpose. Should be refactored to use RDF library.
    /// </summary>
    public class EQ_Writer
    {
        string _fileName = null;
        StreamWriter _writer = null;
        CimContext _cimContext = null;

        string _startContent = @"<?xml version='1.0' encoding='UTF-8'?>
  <rdf:RDF xmlns:cim='http://iec.ch/TC57/2013/CIM-schema-cim16#' xmlns:entsoe='http://entsoe.eu/CIM/SchemaExtension/3/1#' xmlns:md='http://iec.ch/TC57/61970-552/ModelDescription/1#' xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'>
  <md:FullModel rdf:about='urn:uuid:8168421c-c68f-3376-f16c-c2b7198bcb2e'>
    <md:Model.scenarioTime>2030-01-02T09:00:00</md:Model.scenarioTime>
    <md:Model.created>2014-10-22T09:01:25.830</md:Model.created>
    <md:Model.description>DAX PowerFactory RDF Export</md:Model.description>
    <md:Model.version>4</md:Model.version>
	<md:Model.profile>http://entsoe.eu/CIM/EquipmentCore/3/1</md:Model.profile>
    <md:Model.profile>http://entsoe.eu/CIM/EquipmentOperation/3/1</md:Model.profile>
    <md:Model.modelingAuthoritySet>http://NRGi.dk/Planning/1</md:Model.modelingAuthoritySet>
  </md:FullModel>

  <cim:GeographicalRegion rdf:ID='_0472f5a6-c766-11e1-8775-005056c00008'>
    <cim:IdentifiedObject.name>NRGi</cim:IdentifiedObject.name>
  </cim:GeographicalRegion>

  <cim:SubGeographicalRegion rdf:ID='_0472a781-c766-11e1-8775-005056c00008'>
    <cim:IdentifiedObject.name>NRGi</cim:IdentifiedObject.name>
    <cim:SubGeographicalRegion.Region rdf:resource='#_0472f5a6-c766-11e1-8775-005056c00008' />
  </cim:SubGeographicalRegion>
 
  <cim:BaseVoltage rdf:ID='_c6dd6dc7-d8b0-4beb-b78d-9e472b038ffc'>
    <cim:IdentifiedObject.name>0.4</cim:IdentifiedObject.name>
    <cim:BaseVoltage.nominalVoltage>0.4</cim:BaseVoltage.nominalVoltage>
  </cim:BaseVoltage>

  <cim:BaseVoltage rdf:ID='_c63f79cc-7953-4ab6-9fa6-f8c729bf895b'>
    <cim:IdentifiedObject.name>10.00</cim:IdentifiedObject.name>
    <cim:BaseVoltage.nominalVoltage>10</cim:BaseVoltage.nominalVoltage>
  </cim:BaseVoltage>

  <cim:BaseVoltage rdf:ID='_60ee59f3-5ed7-4551-b623-f4346554b22a'>
    <cim:IdentifiedObject.name>60.00</cim:IdentifiedObject.name>
    <cim:BaseVoltage.nominalVoltage>60</cim:BaseVoltage.nominalVoltage>
  </cim:BaseVoltage>

";


        Dictionary<double, string> _baseVoltageIdLookup = new Dictionary<double, string>();

        HashSet<string> _cnAlreadyAdded = new HashSet<string>();

        public EQ_Writer(string fileName, CimContext cimContext)
        {
            _fileName = fileName;
            _cimContext = cimContext;

            _baseVoltageIdLookup.Add(400, "c6dd6dc7-d8b0-4beb-b78d-9e472b038ffc");
            _baseVoltageIdLookup.Add(10000, "c63f79cc-7953-4ab6-9fa6-f8c729bf895b");
            _baseVoltageIdLookup.Add(60000, "60ee59f3-5ed7-4551-b623-f4346554b22a");

            Open();
        }

        private void Open()
        {
            _writer = new StreamWriter(_fileName, false, Encoding.UTF8);
            _writer.Write(_startContent);
            _writer.Write("\r\n\r\n");
        }

        public void Close()
        {
            string xml = "</rdf:RDF>\r\n";
            _writer.Write(xml);
            _writer.Close();
        }
   
        public void AddPNMObject(PhysicalNetworkModel.Substation substation)
        {
            string xml = "<cim:Substation rdf:ID = '_" + substation.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + substation.name + "</cim:IdentifiedObject.name>\r\n";
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
            xml += "  <cim:IdentifiedObject.name>" + bay.name + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Bay.VoltageLevel rdf:resource = '#_" + bay.VoltageLevel.@ref + "'/>\r\n";
            xml += "</cim:Bay>\r\n\r\n";

            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.ACLineSegment acls)
        {
            string xml = "<cim:ACLineSegment rdf:ID='_" + acls.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + acls.name + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Equipment.aggregate>false</cim:Equipment.aggregate>\r\n";
            xml += "  <cim:ConductingEquipment.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(acls.BaseVoltage) + "'/>\r\n";
            xml += "  <cim:Conductor.length>" + DoubleToString(acls.length.Value) + "</cim:Conductor.length>\r\n";
            if (acls.b0ch != null)
                xml += "  <cim:ACLineSegment.b0ch>" + DoubleToString(acls.b0ch.Value) + "</cim:ACLineSegment.b0ch>\r\n";
            if (acls.bch != null)
                xml += "  <cim:ACLineSegment.bch>" + DoubleToString(acls.bch.Value) + "</cim:ACLineSegment.bch>\r\n";
            if (acls.g0ch != null)
                xml += "  <cim:ACLineSegment.g0ch>" + DoubleToString(acls.g0ch.Value) + "</cim:ACLineSegment.g0ch>\r\n";
            if (acls.gch != null)
                xml += "  <cim:ACLineSegment.gch>" + DoubleToString(acls.gch.Value) + "</cim:ACLineSegment.gch>\r\n";
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
            xml += "  <cim:IdentifiedObject.name>" + ls.name + "</cim:IdentifiedObject.name>\r\n";
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
            xml += "  <cim:IdentifiedObject.name>" + ls.name + "</cim:IdentifiedObject.name>\r\n";
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
            xml += "  <cim:IdentifiedObject.name>" + dis.name + "</cim:IdentifiedObject.name>\r\n";
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
            string xml = "<cim:Fuse rdf:ID='_" + fuse.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + fuse.name + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Equipment.EquipmentContainer rdf:resource = '#_" + fuse.EquipmentContainer.@ref + "'/>\r\n";
            xml += "  <cim:ConductingEquipment.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(fuse.BaseVoltage) + "'/>\r\n";

            string normalOpen = "false";
            if (fuse.normalOpen)
                normalOpen = "true";

            xml += "  <cim:Switch.normalOpen>" + normalOpen + "</cim:Switch.normalOpen>\r\n";
            xml += "  <cim:Switch.retained>false</cim:Switch.retained>\r\n";
            xml += "</cim:Fuse>\r\n\r\n";
            _writer.Write(xml);
        }



        public void AddPNMObject(PhysicalNetworkModel.BusbarSection busbar)
        {
            string xml = "<cim:BusbarSection rdf:ID = '_" + busbar.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + busbar.name + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Equipment.EquipmentContainer rdf:resource = '#_" + busbar.EquipmentContainer.@ref + "'/>\r\n";
            xml += "  <cim:ConductingEquipment.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(busbar.BaseVoltage) + "'/>\r\n";
            xml += "</cim:BusbarSection>\r\n\r\n";

            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.Terminal terminal)
        {
            string xml = "<cim:Terminal rdf:ID = '_" + terminal.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + "T" + terminal.sequenceNumber + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:ACDCTerminal.sequenceNumber>" + terminal.sequenceNumber + "</cim:ACDCTerminal.sequenceNumber>\r\n";
            xml += "  <cim:Terminal.phases rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#PhaseCode.ABC'/>\r\n";

            if (terminal.ConnectivityNode != null)
                xml += "  <cim:Terminal.ConnectivityNode rdf:resource = '#_" + terminal.ConnectivityNode.@ref + "'/>\r\n";

            xml += "  <cim:Terminal.ConductingEquipment rdf:resource='#_" + terminal.ConductingEquipment.@ref + "'/>\r\n";
            xml += "</cim:Terminal>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.ConnectivityNode cn)
        {
            if (!_cnAlreadyAdded.Contains(cn.mRID))
            {
                string xml = "<cim:ConnectivityNode rdf:ID='_" + cn.mRID + "'>\r\n";

                if (cn.name != null)
                    xml += "  <cim:IdentifiedObject.name>" + cn.name + "</cim:IdentifiedObject.name>\r\n";

                if (!cn.IsInsideSubstation())
                    throw new ArgumentException("Connectivity Node with mRID=" + cn.mRID + " has no parent. This will not work in PowerFactory CGMES import.");

                // PF want's cn to be put into voltage level container
                var cnSt = cn.GetSubstation(true, _cimContext);
                var fistCi = cn.GetNeighborConductingEquipments().First(o => o is PhysicalNetworkModel.ConductingEquipment && o.BaseVoltage > 0);
                var cnVl = cnSt.GetVoltageLevel(fistCi.BaseVoltage);

                xml += "  <cim:ConnectivityNode.ConnectivityNodeContainer rdf:resource='#_" + cnVl.mRID + "'/>\r\n";
                xml += "</cim:ConnectivityNode>\r\n\r\n";
                _writer.Write(xml);
            }

            _cnAlreadyAdded.Add(cn.mRID);
        }

        public void AddPNMObject(PhysicalNetworkModel.PowerTransformer pt)
        {
            string xml = "<cim:PowerTransformer rdf:ID='_" + pt.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + pt.name + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:Equipment.aggregate>false</cim:Equipment.aggregate>\r\n";
            xml += "  <cim:Equipment.EquipmentContainer rdf:resource = '#_" + pt.EquipmentContainer.@ref + "'/>\r\n";
            xml += "</cim:PowerTransformer>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.PowerTransformerEnd end)
        {
            string xml = "<cim:PowerTransformerEnd rdf:ID='_" + end.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + end.name + "</cim:IdentifiedObject.name>\r\n";
            xml += "  <cim:TransformerEnd.endNumber>" + end.endNumber + "</cim:TransformerEnd.endNumber>\r\n";
            xml += "  <cim:TransformerEnd.BaseVoltage rdf:resource='#_" + GetBaseVoltageId(end.BaseVoltage) + "'/>\r\n";
            xml += "  <cim:TransformerEnd.Terminal rdf:resource='#_" + end.Terminal.@ref + "'/>\r\n";
            xml += "  <cim:PowerTransformerEnd.PowerTransformer rdf:resource = '#_" + end.PowerTransformer.@ref + "'/>\r\n";
            xml += "  <cim:TransformerEnd.grounded>false</cim:TransformerEnd.grounded>\r\n";
            xml += "  <cim:PowerTransformerEnd.connectionKind rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#WindingConnection.Y'/>\r\n";

            if (end.b != null)
                xml += "  <cim:PowerTransformerEnd.b>"+ end.b.Value +"</cim:PowerTransformerEnd.b>\r\n";

            if (end.b0 != null)
                xml += "  <cim:PowerTransformerEnd.b0>" + end.b0.Value + "</cim:PowerTransformerEnd.b0>\r\n";

            if (end.g != null)
                xml += "  <cim:PowerTransformerEnd.g>" + end.g.Value + "</cim:PowerTransformerEnd.g>\r\n";

            if (end.g0 != null)
                xml += "  <cim:PowerTransformerEnd.g0>" + end.g0.Value + "</cim:PowerTransformerEnd.g0>\r\n";

            if (end.r != null)
                xml += "  <cim:PowerTransformerEnd.r>" + end.r.Value + "</cim:PowerTransformerEnd.r>\r\n";

            if (end.r0 != null)
                xml += "  <cim:PowerTransformerEnd.r0>" + end.r0.Value + "</cim:PowerTransformerEnd.r0>\r\n";

            if (end.x != null)
                xml += "  <cim:PowerTransformerEnd.x>" + end.x.Value + "</cim:PowerTransformerEnd.x>\r\n";

            if (end.x0 != null)
                xml += "  <cim:PowerTransformerEnd.x0>" + end.x0 + "</cim:PowerTransformerEnd.x0>\r\n";

            if (end.ratedU != null)
                xml += "  <cim:PowerTransformerEnd.ratedU>" + end.ratedU.Value + "</cim:PowerTransformerEnd.ratedU>\r\n";

            if (end.ratedS != null)
                xml += "  <cim:PowerTransformerEnd.ratedS>" + end.ratedS.Value + "</cim:PowerTransformerEnd.ratedS>\r\n";

            if (end.phaseAngleClock != null)
                xml += "  <cim:PowerTransformerEnd.phaseAngleClock>" + end.phaseAngleClock + "</cim:PowerTransformerEnd.phaseAngleClock>\r\n";

            xml += "</cim:PowerTransformerEnd>\r\n\r\n";
            _writer.Write(xml);
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
    }
}
