using DAX.CIM.PFAdapter;
using DAX.Cson;
using DAX.IO;
using DAX.IO.CIM;
using DAX.IO.CIM.DataModel;
using DAX.IO.CIM.Processing;
using DAX.IO.CIM.Serialize;
using DAX.IO.Writers;
using DAX.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NRGi.Gis2PowerFactoryBatchRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 5)
            {
                System.Console.Out.WriteLine("Usage: Gis2PowerFactoryBatchRunner.exe inputAdapterConfigFileName outputCimArchiveFolder outputCimArchiveName outputLogFile extent");
                return;
            }

            try
            {
                string cimAdapterConfig = args[0];
                string cimArchiveFolder = args[1];
                string cimArchiveName = args[2];
                string logFileName = args[3];
                string extent = args[4];

                var cimFileName = cimArchiveName + ".jsonl";

                Log.Logger = new LoggerConfiguration()
                .WriteTo.File(logFileName, rollingInterval: RollingInterval.Day)
                .WriteTo.Console()
                .WriteTo.EventLog("NRGi.Gis2PowerFactoryBatchRunner", manageEventSource: true, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error)
                .CreateLogger();

                Logger.WriteToConsole = true;

                var config = new TransformationConfig().LoadFromFile(cimAdapterConfig);

                // Sæt extent
                if (extent != null)
                    config.DataReaders[0].ConfigParameters.Add(new ConfigParameter() { Name = "Extent", Value = extent });

                var transformer = config.InitializeDataTransformer("test");

                // Log ikke til database tabel, da vi køre på prod
                ((CIMGraphWriter)transformer.GetFirstDataWriter()).DoNotLogToTable();
                ((CIMGraphWriter)transformer.GetFirstDataWriter()).DoNotRunPreCheckConnectivity();

                transformer.TransferData();

                CIMGraphWriter writer = transformer.GetFirstDataWriter() as CIMGraphWriter;
                CIMGraph graph = writer.GetCIMGraph();

                // Serialize
                var serializer = config.InitializeSerializer("DAX") as IDAXSerializeable;

                var cimObjects = ((DAXCIMSerializer)serializer).GetIdentifiedObjects(CIMMetaDataManager.Repository, graph.CIMObjects, true, true, true);

                var pfWriter = new KonstantCimArchiveWriter(cimObjects, cimArchiveFolder, cimArchiveName);

                Logger.Log(LogLevel.Info, "Export to Power Factory CIM Archive: " + cimArchiveFolder + "\\" + cimArchiveName + ".zip finished.");

            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }
}

