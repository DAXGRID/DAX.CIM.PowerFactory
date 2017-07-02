using DAX.CIM.PhysicalNetworkModel;
using DAX.CIM.PhysicalNetworkModel.Traversal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.CIM.PFAdapter
{
    public interface IPreProcessor
    {
        IEnumerable<IdentifiedObject> Transform(CimContext context, IEnumerable<IdentifiedObject> input);
    }
}
