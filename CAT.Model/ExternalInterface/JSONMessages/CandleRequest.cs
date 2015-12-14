using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This message is used to subscribe to candles and get historical data
    /// The Historical Data will be retrieved in CandleResponse message and is currently retrieving:
    /// - Until 5 (default, it may be different for each feeder) days back with the data of the days 
    ///   that exchange was opened for Intraday 1 minute graph.
    /// - Until 30 (default, it may be different for each feeder) days back with the data of the days
    ///   that exchange was opened for Daily graph.
    /// </summary>
    [DataContract]
    public class CandleRequest : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "CandleRequest";

        /// <summary>
        /// Bovespa exchange
        /// </summary>
        public const string EXCHANGE_BOVESPA = "BOVESPA";

        /// <summary>
        /// BMF exchange
        /// </summary>
        public const string EXCHANGE_BMF = "BMF";

        /// <summary>
        /// Timeframe - intraday 1 minute
        /// </summary>
        public const string TIMEFRAME_INTRADAY_1_MIN = "1";

        /// <summary>
        /// Timeframe - daily
        /// </summary>
        public const string TIMEFRAME_DAILY = "D";

        /// <summary>
        /// Security to be subscribed
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public Security security;

        /// <summary>
        /// Timeframe of Request (Intraday - 1 min or Daily)
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string timeframe;

        public CandleRequest()
            : base(CLASSNAME, 1)
        {

        }

        public CandleRequest(Security security, string timeframe)
            : base(CLASSNAME, 1)
        {
            this.security = security;
            this.timeframe = timeframe;
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<CandleRequest>(this);
        }

        public new static CandleRequest Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<CandleRequest>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }

    }
}
