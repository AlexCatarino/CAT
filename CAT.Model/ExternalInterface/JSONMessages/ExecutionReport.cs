using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This is an Execution Report came from Broker
    /// </summary>
    [DataContract]
    public class ExecutionReport : Command
    {
        /// <summary>
        /// The name of this class (action)
        /// </summary>
        public const string CLASSNAME = "ExecutionReport";

        /// <summary>
        /// Buy side constant
        /// </summary>
        public const string SIDE_BUY = "Buy";
        /// <summary>
        /// Sell side constant
        /// </summary>
        public const string SIDE_SELL = "Sell";

        /// <summary>
        /// Order Type - Limited constant
        /// </summary>
        public const string OT_LIMIT = "Limit";
        /// <summary>
        ///  Order Type - Stop Limit constant
        /// </summary>
        public const string OT_STOP_LIMIT = "Stop";
        /// <summary>
        ///  Order Type - Stop Gain Loss constant
        /// </summary>
        public const string OT_STOP_GAIN_LOSS = "Stop Gain Loss";

        /// <summary>
        /// Validity type - Dated constant
        /// </summary>
        public const string VAL_DATED = "Dated";
        /// <summary>
        /// Validity type - Today constant
        /// </summary>
        public const string VAL_TODAY = "Today";
        /// <summary>
        /// Validity type - Until Cancel constant
        /// </summary>
        public const string VAL_UNTIL_CANCEL = "Until Cancel";

        /// <summary>
        /// Order Status - Processed constant
        /// </summary>
        public const string OS_PROCESSED = "Processed";
        /// <summary>
        /// Order Status - New constant
        /// </summary>
        public const string OS_NEW = "New";
        /// <summary>
        /// Order Status - Canceled constant
        /// </summary>
        public const string OS_CANCELED = "Canceled";
        /// <summary>
        /// Order Status - Pending Order constant
        /// </summary>
        public const string OS_PENDING = "Pendente";
        /// <summary>
        /// Order Status - Partially Filled constant
        /// </summary>
        public const string OS_PARTIALLY_FILLED = "Partially Filled";
        /// <summary>
        /// Order Status - Filled constant
        /// </summary>
        public const string OS_FILLED = "Filled";
        /// <summary>
        /// Order Status - Rejected constant
        /// </summary>
        public const string OS_REJECTED = "Rejected";


        /// <summary>
        /// The order ID
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string orderId;

        /// <summary>
        /// Security Symbol
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string symbol;

        /// <summary>
        /// Security Description
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string securityDesc;
        
        /// <summary>
        /// The side of this order - Buy or Sell
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string side;

        /// <summary>
        /// Order Quantity
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int qtty;

        /// <summary>
        /// Order Type.
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string orderType = "";
        
        /// <summary>
        /// Limit Price
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal price;

        /// <summary>
        /// Limit Price of Stop Loss
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal limitLossPrice;

        /// <summary>
        /// Trigger Price of Stop Loss
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal triggerLossPrice;

        /// <summary>
        /// Limit Price of Stop Gain
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal limitGainPrice;

        /// <summary>
        /// Trigger Price of Stop Gain
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal triggerGainPrice;

        /// <summary>
        /// Order Validity
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string validity = "";

        /// <summary>
        /// Order Validity Data
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string validityDate = "";

        /// <summary>
        /// Order Status
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string orderStatus = "";

        /// <summary>
        /// Date and Time of the Order Creation 
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string creationDateTime;

        /// <summary>
        /// Date and Time of the last Transaction
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string transactionDateTime;

        /// <summary>
        /// Quantity still pending
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int pendingQtty;
                
        /// <summary>
        /// Quantity already executed
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int cumQtty;
                
        /// <summary>
        /// Average Price of this Execution
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal averagePrice;

        /// <summary>
        /// The Identification created locally by client
        /// </summary>
        public string clientOrderID = "";

        public new string Serialize()
        {
            string s = JSONSerializer.Serialize<ExecutionReport>(this);
            return s.Replace("\\/", "/");
        }

        public new static ExecutionReport Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<ExecutionReport>(serialized);
        }

        public override string ToString()
        {
            return this.Serialize();            
        }

    }
}
