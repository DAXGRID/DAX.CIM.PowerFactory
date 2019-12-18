using DAX.CIM.PhysicalNetworkModel.Traversal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.CIM.PFAdapter
{
    public class PNM2PowerFactoryConverter
    {
        private IEnumerable<PhysicalNetworkModel.IdentifiedObject> _inputCimObjects;
        private List<IPreProcessor> _preProcessors = new List<IPreProcessor>();
        private CimContext _context;

        public PNM2PowerFactoryConverter(IEnumerable<PhysicalNetworkModel.IdentifiedObject> cimObjects, List<IPreProcessor> preProcessors = null)
        {
            _inputCimObjects = cimObjects;
            _context = CimContext.Create(_inputCimObjects);

            if (preProcessors != null)
                _preProcessors = preProcessors;
        }

        public IEnumerable<PhysicalNetworkModel.IdentifiedObject> GetCimObjects()
        {
            var input = _inputCimObjects.ToList();
            var output = _inputCimObjects.ToList();

            foreach (var preProcessor in _preProcessors)
            {
                output = preProcessor.Transform(_context, input).ToList();

                var ecTest = output.Find(c => c.name == "571313124501006982" && c is PhysicalNetworkModel.EnergyConsumer);

                input = output;
            }

            foreach (var obj in output)
                yield return obj;
        }
    }
}
