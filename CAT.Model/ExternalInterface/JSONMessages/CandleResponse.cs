using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// Response for CandleRequest
    /// It will retrieve all the historical data until:
    /// 5 (default, it may be different for each feeder) days back for Intraday 1 minute graph
    /// 30 (default, it may be different for each feeder) days back for Daily graph
    /// </summary>
    [DataContract]
    public class CandleResponse : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "CandleResponse";

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

        /// <summary>
        /// List of Historical Data Candles
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public List<Candle> candles;

        public CandleResponse()
            : base(CLASSNAME, 1)
        {

        }

        public CandleResponse(Security security, string timeframe)
            : base(CLASSNAME, 1)
        {
            this.security = security;
            this.timeframe = timeframe;
            this.candles = new List<Candle>();
        }

        public CandleResponse(Security security, string timeframe, List<Candle> candles)
            : base(CLASSNAME, 1)
        {
            this.security = security;
            this.timeframe = timeframe;
            this.candles = candles;
        }

        public CandleResponse(Security security, List<Candle> candles)
            : base(CLASSNAME, 1)
        {
            this.security = security;
            this.candles = candles;
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<CandleResponse>(this).Replace("\\/", "/");
        }

        public new static CandleResponse Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<CandleResponse>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }


    }
}
