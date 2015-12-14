using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// Response for CancelOrderRequest
    /// </summary>
    [DataContract]
    public class CancelOrderResponse : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "CancelOrderResponse";

        /// <summary>
        /// The ID of the Order wich is to be canceled
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string orderId;
                

        public new string Serialize()
        {
            return JSONSerializer.Serialize<CancelOrderResponse>(this);
        }

        public new static CancelOrderResponse Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<CancelOrderResponse>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }

       
    }
}
