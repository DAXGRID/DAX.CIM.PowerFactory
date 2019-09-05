using DAX.CIM.PhysicalNetworkModel;
using DAX.CIM.PhysicalNetworkModel.Traversal;
using DAX.CIM.PhysicalNetworkModel.Traversal.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NRGi.MeterExport
{
    class Program
    {
        static void Main(string[] args)
        {
            // Kontrol måler liste til brug for Thue´s 60 kV projekt
            var fileName = @"C:\temp\cim\complete_net.jsonl";

            //var fileName = @"C:\temp\cim\engum.jsonl";
            var cson = new NRGi.Cson.CsonSerializer();

            var cimContext = CimContext.Create(cson.DeserializeObjects(File.OpenRead(fileName)));
         
            foreach (var cimObj in cimContext.GetAllObjects())
            {
                if (cimObj is CurrentTransformer)
                {
                    var ct = cimObj as CurrentTransformer;

                    // If CT har EAN number in name, we are dealing with a meter
                    if (ct.name != null && ct.name.Length == 18)
                    {
                        string line = "";

                        var st = ct.GetSubstation();

                        // Get conducting equipment the CT sits on
                        if (ct.Terminal != null)
                        {
                            var ciTerminal = cimContext.GetObject<Terminal>(ct.Terminal.@ref);
                            var ciEquipment = cimContext.GetObject<ConductingEquipment>(ciTerminal.ConductingEquipment.@ref);

                            // Find power transformer that CT measures
                            var traceResult = ciEquipment.Traverse(ce =>
                                ce.IsInsideSubstation() &&
                                !ce.IsOpen() &&
                                !ce.GetNeighborConductingEquipments().Exists(o => o is BusbarSection)
                                ).ToList();

                            var pt = traceResult.Find(o => o is PowerTransformer);

                            if (pt != null)
                            {
                                // We don't want T and TRF, only the number
                                string trafoNumber = pt.name.ToLower().Replace("trf", "").Replace("t", "");
                                line = "\"" + ct.name + "\";" + st.name + "_0" + ciEquipment.BaseVoltage / 1000 + "_" + trafoNumber;
                            }
                            else
                                line = "\"" + ct.name + "\";" + st.name;
                        }
                        else
                            line = "\"" + ct.name + "\";" + st.name;

                        System.Diagnostics.Debug.WriteLine(line);
                    }
                }
            }
        }
    }
}
