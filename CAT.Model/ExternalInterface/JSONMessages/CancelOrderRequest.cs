using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This message is used to cancel orders. A CancelOrderResponse will be replied indicating success.
    /// </summary>
    [DataContract]
    public class CancelOrderRequest : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "CancelOrderRequest";

        /// <summary>
        /// The ID of the Order wich is to be canceled
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string orderId;

        /// <summary>
        /// Electronic Signature
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = false)]
        public string esign;

       
        public CancelOrderRequest()
            : base(CLASSNAME, 1)
        {
                
        }

        public CancelOrderRequest(string orderId, string esign)
            : base(CLASSNAME, 1)
        {
            this.orderId = orderId;
            this.esign = esign;
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<CancelOrderRequest>(this);
        }

        public new static CancelOrderRequest Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<CancelOrderRequest>(serialized);
        }

        public override string ToString()
        {
            return this.Serialize();
        }

    }
}
