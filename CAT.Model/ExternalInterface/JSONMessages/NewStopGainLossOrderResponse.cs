using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// Response for NewStopGainLossOrderRequest
    /// </summary>
    [DataContract]
    public class NewStopGainLossOrderResponse : Command
    {
        /// <summary>
        /// Nome da Classe
        /// </summary>
        public const string CLASSNAME = "NewStopGainLossOrderResponse";

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
        /// Limit price of the Stop Loss
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string limitLossPrice = "";

        /// <summary>
        /// Trigger Price of Stop Loss
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string triggerLossPrice = "";

        /// <summary>
        /// Limit Price of Stop Gain
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string limitGainPrice = "";

        /// <summary>
        /// Trigger Price of Stop Gain
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string triggerGainPrice = "";

        /// <summary>
        /// Security Market
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string market = "";

        /// <summary>
        /// Electronic Signature
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = false)]
        public string esign = "";

        /// <summary>
        /// Identification created by client
        /// </summary>
        public string clientOrderID = "";
        
        public new string Serialize()
        {
            return JSONSerializer.Serialize<NewStopGainLossOrderResponse>(this);
        }

        public new static NewStopGainLossOrderResponse Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<NewStopGainLossOrderResponse>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }


    }
}
