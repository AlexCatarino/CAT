using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// Response for NewStopOrderRequest
    /// </summary>
    [DataContract]
    public class NewStopOrderResponse : Command
    {
        /// <summary>
        /// Class name (action)
        /// </summary>
        public const string CLASSNAME = "NewStopOrderResponse";

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
        /// Preço limite.
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string limitPrice = "";

        /// <summary>
        /// Preço de disparo.
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string triggerPrice = "";

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
            return JSONSerializer.Serialize<NewStopOrderResponse>(this);
        }

        public new static NewStopOrderResponse Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<NewStopOrderResponse>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }      

    }
}
