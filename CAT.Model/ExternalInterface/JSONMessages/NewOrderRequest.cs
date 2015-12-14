using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This message creates a New Limited Order. A NewOrderResponse will be replied indicating success.
    /// </summary>
    [DataContract]
    public class NewOrderRequest : Command
    {
        /// <summary>
        /// Class name (action)
        /// </summary>
        public const string CLASSNAME = "NewOrderRequest";

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
        /// Electronic Signature
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = false)]
        public string esign = "";

        /// <summary>
        /// Identification created by client
        /// </summary>
        public string clientOrderID = "";

        public NewOrderRequest()
            : base(CLASSNAME, 1)
        {
                
        }
        
        /// <summary>
        /// Constructs a new order
        /// </summary>
        /// <param name="symbol">security symbol</param>
        /// <param name="side">order side</param>
        /// <param name="qtty">order quantity</param>
        /// <param name="price">order limit price</param>
        /// <param name="market">security market</param>
        /// <param name="esign">electronic signature</param>
        public NewOrderRequest(string symbol, string side, string qtty, string price, string market, string esign)
            : base(CLASSNAME, 1)
        {
            this.symbol = symbol;
            this.side = side;
            this.qtty = qtty;
            this.price = price;
            this.market = market;
            this.esign = esign;
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<NewOrderRequest>(this);
        }

        public new static NewOrderRequest Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<NewOrderRequest>(serialized);
        }

        public override string ToString()
        {
            return this.Serialize();
        }

    }
}
