using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This class creates a new Order List Request to retrieve updated Execution Reports from current user orders
    /// An OrderListResponse will be replied by Quantsis Connection Box signaling if this request was successfull or not
    /// </summary>
    [DataContract]
    public class OrderListRequest : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "OrderListRequest";

        public OrderListRequest()
            : base(CLASSNAME, 1)
        {
                
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<OrderListRequest>(this);
        }

        public new static OrderListRequest Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<OrderListRequest>(serialized);
        }

        public override string ToString()
        {
            return this.Serialize();
        }       
    }
}
