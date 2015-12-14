using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This class holds the values of one position item from the array of positions in PositionResponse message.
    /// Some of the fields may not be present according to the broker server availability.
    /// </summary>
    [DataContract]
    public class PositionItem
    {

        /// <summary>
        ///  Class name - not an action
        /// </summary>
        public const string CLASSNAME = "PositionItem";

        /// <summary>
        ///  Security Symbol
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string symbol { get; set; }

        /// <summary>
        ///  Security Description
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string description { get; set; }

        /// <summary>
        ///  Exchange
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string exchange { get; set; }

        /// <summary>
        ///  Market
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string market { get; set; }
         
        /// <summary>
        ///  Total Quantity
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string qtTotal { get; set; }

        /// <summary>
        ///  Pending Quantity
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string qtPending { get; set; }

        /// <summary>
        ///  Confirmed Quantity
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string qtConfirmed { get; set; }

        /// <summary>
        ///  Blocked Quantity
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string qtBlocked { get; set; }

        /// <summary>
        ///  Available Quantity
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string qtAvailable { get; set; }

        /// <summary>
        ///  Available Quantity for Day Trade
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string qtDayTrade { get; set; }
        
        public string Serialize()
        {
            return JSONSerializer.Serialize<PositionItem>(this);
        }

        public static PositionItem Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<PositionItem>(serialized);
        }
               
        public override string ToString()
        {
            return this.Serialize();
        }
    }
}
