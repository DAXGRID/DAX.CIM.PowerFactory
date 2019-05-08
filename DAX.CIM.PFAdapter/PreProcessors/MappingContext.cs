using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAX.CIM.PFAdapter.PreProcessors
{
    /// <summary>
    /// Power Factory specific extra information that needed to be exported
    /// </summary>
    public class MappingContext
    {
        /// <summary>
        /// Power Factory CGMES import requires that all connectivity nodes are inside a voltage level
        /// </summary>
        public Dictionary<PhysicalNetworkModel.ConnectivityNode, PhysicalNetworkModel.VoltageLevel> ConnectivityNodeToVoltageLevel = new Dictionary<PhysicalNetworkModel.ConnectivityNode, PhysicalNetworkModel.VoltageLevel>();
    }
}
