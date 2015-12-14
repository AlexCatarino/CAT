using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// Represents a security
    /// </summary>
    [DataContract]
    public class Security
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "Security";

        /// <summary>
        /// Bovespa exchange
        /// </summary>
        public const string EXCHANGE_BOVESPA = "BOVESPA";

        /// <summary>
        /// BMF exchange
        /// </summary>
        public const string EXCHANGE_BMF = "BMF";

        /// <summary>
        /// Security symbol
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string symbol;

        /// <summary>
        /// Security Market
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string market = "";

        /// <summary>
        /// Exchange
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string exchange = "";


        public Security(string symbol, string market, string exchange)            
        {
            this.symbol = symbol;
            this.market = market;
            this.exchange = exchange;
        }

        public string Serialize()
        {
            return JSONSerializer.Serialize<Security>(this);
        }

        public static Security Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<Security>(serialized);
        }

        public override string ToString()
        {
            return this.Serialize();
        }
        
    }
}
