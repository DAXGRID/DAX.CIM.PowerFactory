using DAX.CIM.PFAdapter.CGMES;
using DAX.CIM.PFAdapter.PreProcessors;
using DAX.CIM.PFAdapter.Protection;
using DAX.CIM.PhysicalNetworkModel.Traversal;
using DAX.CIM.PhysicalNetworkModel.Traversal.Extensions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace DAX.CIM.PFAdapter.Asset
{
    /// <summary>
    /// Asset information writer.
    /// Very ugly - just for quick prototyping purpose. Should be refactored to use RDF library.
    /// </summary>
    public class AI_Writer
    {
        string _fileName = null;
        StreamWriter _writer = null;
        CimContext _cimContext = null;

        HashSet<string> _objectAlreadyAdded = new HashSet<string>();

        public static string _assetModelId = Guid.NewGuid().ToString();
        public static DateTime _timeStamp = DateTime.Now;

        static string _modelVersion = "http://konstant.dk/CIM/AssetExtension/1/4";
        
        string _startContent = @"<?xml version='1.0' encoding='UTF-8'?>
  <rdf:RDF xml:base='http://iec.ch/TC57/2013/CIM-schema-cim16' xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#' xmlns:md='http://iec.ch/TC57/61970-552/ModelDescription/1#' xmlns:rdfs='http://www.w3.org/2000/01/rdf-schema#' xmlns:xsd='http://www.w3.org/2001/XMLSchema#' xmlns:kon='" + _modelVersion + @"#' xmlns:cim='http://iec.ch/TC57/2013/CIM-schema-cim16#' xmlns:cims='http://iec.ch/TC57/1999/rdf-schema-extensions-19990926#' xmlns:entsoe='http://entsoe.eu/CIM/SchemaExtension/3/1#'>
  <md:FullModel rdf:about='" + _assetModelId.ToString() + @"'>
    <md:Model.DependentOn rdf:resource='" + EQ_Writer._eqModelId.ToString() + @"' />
    <md:Model.DependentOn rdf:resource='" + PE_Writer._protectionModelId.ToString() + @"' />
    <md:Model.scenarioTime>" + EQ_Writer._timeStamp.ToString() + @"</md:Model.scenarioTime>
    <md:Model.created>" + EQ_Writer._timeStamp.ToString() + @"</md:Model.created>
    <md:Model.description>DAX Konstant PowerFactory Export</md:Model.description>
    <md:Model.version>1</md:Model.version>
    <md:Model.profile>" + _modelVersion + @"</md:Model.profile>
    <md:Model.modelingAuthoritySet>http://NRGi.dk/Planning/1</md:Model.modelingAuthoritySet>
  </md:FullModel>
";

     
        MappingContext _mappingContext;

        public AI_Writer(string fileName, CimContext cimContext, MappingContext mappingContext)
        {
            _fileName = fileName;
            _cimContext = cimContext;
            _mappingContext = mappingContext;

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

        public void AddPNMObject(PhysicalNetworkModel.Asset asset, string psrId)
        {
            string xml = "<cim:Asset rdf:ID='_" + asset.mRID + "'>\r\n";

            if (asset.name != null)
                xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(asset.name) + "</cim:IdentifiedObject.name>\r\n";

            if (asset.type != null)
                xml += "  <cim:Asset.type>" + HttpUtility.HtmlEncode(asset.type) + "</cim:Asset.type>\r\n";

            xml += "  <cim:Asset.PowerSystemResources rdf:resource = '#_" + psrId + "'/>\r\n";

            if (asset.AssetInfo != null && asset.AssetInfo.@ref != null)
            {
                xml += "  <cim:Asset.AssetInfo rdf:resource = '#_" + asset.AssetInfo.@ref + "'/>\r\n";
            }
            xml += "</cim:Asset>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.Manufacturer manufacturer)
        {
            string xml = "<cim:Manufacturer rdf:ID='_" + manufacturer.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(manufacturer.name) + "</cim:IdentifiedObject.name>\r\n";
          
            xml += "</cim:Manufacturer>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.ProductAssetModel model)
        {
            string xml = "<cim:ProductAssetModel rdf:ID='_" + model.mRID + "'>\r\n";
            xml += "  <cim:IdentifiedObject.name>" + HttpUtility.HtmlEncode(model.name) + "</cim:IdentifiedObject.name>\r\n";

            if (model.Manufacturer != null)
                xml += "  <cim:ProductAssetModel.Manufacturer rdf:resource = '#_" + model.Manufacturer.@ref + "'/>\r\n";

            xml += "</cim:ProductAssetModel>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.AssetInfo assetInfo)
        {
            if (!_objectAlreadyAdded.Contains(assetInfo.mRID))
            {
                if (assetInfo is PhysicalNetworkModel.CableInfoExt)
                    AddPNMObject((PhysicalNetworkModel.CableInfoExt)assetInfo);

                else if (assetInfo is PhysicalNetworkModel.OverheadWireInfoExt)
                    AddPNMObject((PhysicalNetworkModel.OverheadWireInfoExt)assetInfo);

                else if (assetInfo is PhysicalNetworkModel.PotentialTransformerInfoExt)
                    AddPNMObject((PhysicalNetworkModel.PotentialTransformerInfoExt)assetInfo);

                else if (assetInfo is PhysicalNetworkModel.CurrentTransformerInfoExt)
                    AddPNMObject((PhysicalNetworkModel.CurrentTransformerInfoExt)assetInfo);

                else if (assetInfo is PhysicalNetworkModel.PetersenCoilInfoExt)
                    AddPNMObject((PhysicalNetworkModel.PetersenCoilInfoExt)assetInfo);

                else 
                {
                    string xml = "<cim:AssetInfo rdf:ID='_" + assetInfo.mRID + "'>\r\n";

                    xml = AddIdentifiedObjectStuff(assetInfo, xml);

                    if (assetInfo.AssetModel != null)
                        xml += "  <cim:AssetInfo.AssetModel rdf:resource = '#_" + assetInfo.AssetModel.@ref + "'/>\r\n";

                    xml += "</cim:AssetInfo>\r\n\r\n";
                    _writer.Write(xml);
                }

                _objectAlreadyAdded.Add(assetInfo.mRID);
            }
        }

        public void AddPNMObject(PhysicalNetworkModel.PetersenCoilInfoExt assetInfo)
        {
            string xml = "<cim:PetersenCoilInfoExt rdf:ID='_" + assetInfo.mRID + "'>\r\n";

            xml = AddIdentifiedObjectStuff(assetInfo, xml);

            if (assetInfo.minimumCurrent != null)
                xml += "  <cim:PetersenCoilInfoExt.minimumCurrent>" + assetInfo.minimumCurrent.Value + "</cim:PetersenCoilInfoExt.minimumCurrent>\r\n";

            if (assetInfo.maximumCurrent != null)
                xml += "  <cim:PetersenCoilInfoExt.maximumCurrent>" + assetInfo.maximumCurrent.Value + "</cim:PetersenCoilInfoExt.maximumCurrent>\r\n";

            if (assetInfo.actualCurrent != null)
                xml += "  <cim:PetersenCoilInfoExt.actualCurrent>" + assetInfo.actualCurrent.Value + "</cim:PetersenCoilInfoExt.actualCurrent>\r\n";


            xml += "</cim:PetersenCoilInfoExt>\r\n\r\n";
            _writer.Write(xml);
        }


        public void AddPNMObject(PhysicalNetworkModel.CurrentTransformerInfoExt assetInfo)
        {
            string xml = "<cim:CurrentTransformerInfoExt rdf:ID='_" + assetInfo.mRID + "'>\r\n";

            xml = AddIdentifiedObjectStuff(assetInfo, xml);

            if (assetInfo.primaryCurrent != null)
                xml += "  <cim:CurrentTransformerInfoExt.primaryCurrent>" + assetInfo.primaryCurrent.Value + "</cim:CurrentTransformerInfoExt.primaryCurrent>\r\n";

            if (assetInfo.secondaryCurrent != null)
                xml += "  <cim:CurrentTransformerInfoExt.secondaryCurrent>" + assetInfo.secondaryCurrent.Value + "</cim:CurrentTransformerInfoExt.secondaryCurrent>\r\n";


            xml += "</cim:CurrentTransformerInfoExt>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.PowerTransformerInfoExt assetInfo)
        {
            string xml = "<cim:PowerTransformerInfoExt rdf:ID='_" + assetInfo.mRID + "'>\r\n";

            xml = AddIdentifiedObjectStuff(assetInfo, xml);

            if (assetInfo.thermalRatedS != null)
                xml += "  <cim:PowerTransformerInfoExt.thermalRatedS>" +  assetInfo.thermalRatedS.Value + "</cim:PowerTransformerInfoExt.thermalRatedS>\r\n";

            if (assetInfo.lowerBound != null)
                xml += "  <cim:PowerTransformerInfoExt.lowerBound>" + DoubleToString(assetInfo.lowerBound.Value) + "</cim:PowerTransformerInfoExt.lowerBound>\r\n";

            if (assetInfo.upperBound != null)
                xml += "  <cim:PowerTransformerInfoExt.upperBound>" + DoubleToString(assetInfo.upperBound.Value) + "</cim:PowerTransformerInfoExt.upperBound>\r\n";

            if (assetInfo.hasInternalDeltaWinding)
                xml += "  <cim:PowerTransformerInfoExt.hasInternalDeltaWinding>true</cim:PowerTransformerInfoExt.hasInternalDeltaWinding>\r\n";
            else
                xml += "  <cim:PowerTransformerInfoExt.hasInternalDeltaWinding>false</cim:PowerTransformerInfoExt.hasInternalDeltaWinding>\r\n";
           
            xml += "</cim:PowerTransformerInfoExt>\r\n\r\n";
            _writer.Write(xml);
        }



        public void AddPNMObject(PhysicalNetworkModel.PotentialTransformerInfoExt assetInfo)
        {
            string xml = "<cim:PotentialTransformerInfoExt rdf:ID='_" + assetInfo.mRID + "'>\r\n";

            xml = AddIdentifiedObjectStuff(assetInfo, xml);

            if (assetInfo.primaryVoltage != null)
                xml += "  <cim:PotentialTransformerInfoExt.primaryVoltage>" + assetInfo.primaryVoltage.Value + "</cim:PotentialTransformerInfoExt.primaryVoltage>\r\n";

            if (assetInfo.secondaryVoltage != null)
                xml += "  <cim:PotentialTransformerInfoExt.secondaryVoltage>" + assetInfo.secondaryVoltage.Value + "</cim:PotentialTransformerInfoExt.secondaryVoltage>\r\n";


            xml += "</cim:PotentialTransformerInfoExt>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.CableInfoExt assetInfo)
        {
            string xml = "<cim:CableInfoExt rdf:ID='_" + assetInfo.mRID + "'>\r\n";

            xml = AddIdentifiedObjectStuff(assetInfo, xml);
            xml = AddWireInfoStuff(assetInfo, xml);
            xml = AddWireInfoExtStuff(assetInfo, xml);

            // Add cable stuff
            if (assetInfo.outerJacketKindSpecified)
                xml += "  <cim:CableInfo.outerJacketKind rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#CableOuterJacktKind."  + assetInfo.outerJacketKind.ToString() + "'/>\r\n";

            if (assetInfo.shieldMaterialSpecified)
                xml += "  <cim:CableInfo.shieldMaterial rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#CableShieldMaterialKind." + assetInfo.shieldMaterial.ToString() + "'/>\r\n";

            if (assetInfo.shieldCrossSectionalAreaSpecified)
                xml += "  <cim:CableInfoExt.shieldCrossSectionalArea>" + DoubleToString(assetInfo.shieldCrossSectionalArea) + "</cim:CableInfoExt.shieldCrossSectionalArea>\r\n";

            if (assetInfo.conductorCount > 0)
                xml += "  <cim:CableInfoExt.conductorCount>" + assetInfo.conductorCount + "</cim:CableInfoExt.conductorCount>\r\n";


            xml += "</cim:CableInfoExt>\r\n\r\n";
            _writer.Write(xml);
        }

        public void AddPNMObject(PhysicalNetworkModel.OverheadWireInfoExt assetInfo)
        {
            string xml = "<cim:OverheadWireInfoExt rdf:ID='_" + assetInfo.mRID + "'>\r\n";

            xml = AddIdentifiedObjectStuff(assetInfo, xml);
            xml = AddWireInfoStuff(assetInfo, xml);
            xml = AddWireInfoExtStuff(assetInfo, xml);

            xml += "</cim:OverheadWireInfoExt>\r\n\r\n";
            _writer.Write(xml);
        }


        private static string AddIdentifiedObjectStuff(PhysicalNetworkModel.IdentifiedObject assetInfo, string xml)
        {
            if (assetInfo.name != null)
                xml += "  <cim:IdentifiedObject.name>" + assetInfo.name + "</cim:IdentifiedObject.name>\r\n";
            return xml;
        }

        private string AddWireInfoStuff(PhysicalNetworkModel.WireInfo assetInfo, string xml)
        {
            if (assetInfo.insulationMaterialSpecified)
                xml += "  <cim:WireInfo.insulationMaterial rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#WireInsulationKind." + assetInfo.insulationMaterial.ToString() + "'/>\r\n";

            if (assetInfo.materialSpecified)
                xml += "  <cim:WireInfo.material rdf:resource='http://iec.ch/TC57/2013/CIM-schema-cim16#WireMaterialKind." + assetInfo.material.ToString() + "'/>\r\n";

            if (assetInfo.ratedCurrent != null)
                xml += "  <cim:WireInfo.ratedCurrent>" + DoubleToString(assetInfo.ratedCurrent.Value / 1000) + "</cim:WireInfo.ratedCurrent>\r\n";
            return xml;
        }

        private string AddWireInfoExtStuff(PhysicalNetworkModel.WireInfoExt assetInfo, string xml)
        {
            if (assetInfo.ratedVoltage != null)
                xml += "  <cim:WireInfoExt.ratedVoltage>" + DoubleToString(assetInfo.ratedVoltage.Value) + "</cim:WireInfoExt.ratedVoltage>\r\n";

            if (assetInfo.b0ch != null)
                xml += "  <cim:WireInfoExt.b0ch>" + DoubleToString(assetInfo.b0ch.Value) + "</cim:WireInfoExt.b0ch>\r\n";
            if (assetInfo.bch != null)
                xml += "  <cim:WireInfoExt.bch>" + DoubleToString(assetInfo.bch.Value) + "</cim:WireInfoExt.bch>\r\n";
            if (assetInfo.g0ch != null)
                xml += "  <cim:WireInfoExt.g0ch>" + DoubleToString(assetInfo.g0ch.Value) + "</cim:WireInfoExt.g0ch>\r\n";
            if (assetInfo.gch != null)
                xml += "  <cim:WireInfoExt.gch>" + DoubleToString(assetInfo.gch.Value) + "</cim:WireInfoExt.gch>\r\n";
            if (assetInfo.r != null)
                xml += "  <cim:WireInfoExt.r>" + DoubleToString(assetInfo.r.Value) + "</cim:WireInfoExt.r>\r\n";
            if (assetInfo.r0 != null)
                xml += "  <cim:WireInfoExt.r0>" + DoubleToString(assetInfo.r0.Value) + "</cim:WireInfoExt.r0>\r\n";
            if (assetInfo.x != null)
                xml += "  <cim:WireInfoExt.x>" + DoubleToString(assetInfo.x.Value) + "</cim:WireInfoExt.x>\r\n";
            if (assetInfo.x0 != null)
                xml += "  <cim:WireInfoExt.x0>" + DoubleToString(assetInfo.x0.Value) + "</cim:WireInfoExt.x0>\r\n";

            if (assetInfo.ratedWithstandCurrent1sec != null)
                xml += "  <cim:WireInfoExt.ratedWithstandCurrent1sec>" + DoubleToString(assetInfo.ratedWithstandCurrent1sec.Value) + "</cim:WireInfoExt.ratedWithstandCurrent1sec>\r\n";

            if (assetInfo.conductorCrossSectionalArea > 0)
                xml += "  <cim:WireInfoExt.conductorCrossSectionalArea>" + DoubleToString(assetInfo.conductorCrossSectionalArea) + "</cim:WireInfoExt.conductorCrossSectionalArea>\r\n";

            return xml;
        }

     



        private string DoubleToString(double value)
        {
            return value.ToString(CultureInfo.GetCultureInfo("en-GB"));
        }

        private string DoubleToString(Decimal value)
        {
            return value.ToString(CultureInfo.GetCultureInfo("en-GB"));
        }

        private double ConvertToEntsoeVoltage(double value)
        {
            return value / 1000;
        }
    }
}
