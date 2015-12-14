using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// Response for NewOrderRequest
    /// </summary>
    [DataContract]
    public class NewOrderResponse : Command
    {
        /// <summary>
        /// Class name (action)
        /// </summary>
        public const string CLASSNAME = "NewOrderResponse";

        /// <summary>
        /// Buy side constant
        /// </summary>
        public const string BUY = "Buy";

        /// <summary>
        /// Sell side constant
        /// </summary>
        public const string SELL = "Sell";

        /// <summary>
        /// Security symbol
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string symbol;

        /// <summary>
        /// Order Side
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string side;

        /// <summary>
        /// Order Quantity
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string qtty;

        /// <summary>
        /// Limit Price
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string price = "";

        /// <summary>
        /// Security Market
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string market = "";

        /// <summary>
        /// Identification created by client
        /// </summary>
        public string clientOrderID = "";
        
        public new string Serialize()
        {
            return JSONSerializer.Serialize<NewOrderResponse>(this);
        }

        public new static NewOrderResponse Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<NewOrderResponse>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }


    }
}
