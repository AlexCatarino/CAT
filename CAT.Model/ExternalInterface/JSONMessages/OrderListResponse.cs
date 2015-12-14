using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This class creates an Order List Response. 
    /// This message is replied by Quantsis Connection Box when it receives an OrderListRequest signaling if the request was successful or not.
    /// </summary>
    [DataContract]
    public class OrderListResponse : Command
    {
        /// <summary>
        /// Class name (action)
        /// </summary>
        public const string CLASSNAME = "OrderListResponse";

        public OrderListResponse()
            : base(CLASSNAME, 1)
        {
                
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<OrderListResponse>(this);
        }

        public new static OrderListResponse Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<OrderListResponse>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }
        
    }
}
