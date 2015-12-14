using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{

    /// <summary>
    /// This class creates a new Stop Loss Order. A NewStopOrderResponse will be replied indicating success
    /// </summary>
    [DataContract]
    public class NewStopOrderRequest : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "NewStopOrderRequest";

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
        /// Limit Price of the Stop Loss
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string limitPrice = "";

        /// <summary>
        /// Trigger Price of the Stop Loss
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string triggerPrice = "";

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
        /// <param name="limitPrice">Limit price of the Stop Loss</param>
        /// <param name="triggerPrice">Trigger price of the Stop Loss</param>
        /// <param name="market">Security Market</param>
        /// <param name="esign">Electronic Signature</param>
        public NewStopOrderRequest(string symbol, string side, string qtty, string limitPrice, string triggerPrice, string market, string esign)
            : base(CLASSNAME, 1)
        {
            this.symbol = symbol;
            this.side = side;
            this.qtty = qtty;
            this.limitPrice = limitPrice;
            this.triggerPrice = triggerPrice;
            this.market = market;
            this.esign = esign;
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<NewStopOrderRequest>(this);
        }

        public new static NewStopOrderRequest Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<NewStopOrderRequest>(serialized);
        }

        public override string ToString()
        {
            return this.Serialize();
        }
        
    }
}
