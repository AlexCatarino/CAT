using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This class creates a new Stop Gain Loss Order. A NewStopGainLossOrderResponse will be replied indicating success
    /// </summary>
    [DataContract]
    public class NewStopGainLossOrderRequest : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "NewStopGainLossOrderRequest";

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
               
        /// <summary>
        /// Constructs a new Stop Gain Loss Order Request
        /// </summary>
        /// <param name="symbol">Security Symbol</param>
        /// <param name="side">The order Side</param>
        /// <param name="qtty">Quantity of the order</param>
        /// <param name="limitLoss">limit price of the Stop Loss</param>
        /// <param name="triggerLoss">trigger price of the Stop Loss</param>
        /// <param name="limitGain">limit price of the Stop Gain</param>
        /// <param name="triggerGain">trigger price of the Stop Gain</param>
        /// <param name="market">Security Market</param>
        /// <param name="esign">Electronic Signature</param>
        public NewStopGainLossOrderRequest(string symbol, string side, string qtty, string limitLoss, string triggerLoss,
            string limitGain, string triggerGain, string market, string esign)
            : base(CLASSNAME, 1)
        {
            this.symbol = symbol;
            this.side = side;
            this.qtty = qtty;
            this.limitLossPrice = limitLoss;
            this.triggerLossPrice = triggerLoss;
            this.limitGainPrice = limitGain;
            this.triggerGainPrice = triggerGain;
            this.market = market;
            this.esign = esign;
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<NewStopGainLossOrderRequest>(this);
        }

        public new static NewStopGainLossOrderRequest Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<NewStopGainLossOrderRequest>(serialized);
        }

        public override string ToString()
        {
            return this.Serialize();
        }

    }
}
