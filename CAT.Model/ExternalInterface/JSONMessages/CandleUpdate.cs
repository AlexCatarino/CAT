using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This message is a candle update
    /// </summary>
    [DataContract]
    public class CandleUpdate : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "CandleUpdate";

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
        /// Updated Candle
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public Candle candle;

        public CandleUpdate()
            : base(CLASSNAME, 1)
        {

        }

        public CandleUpdate(Security security, string timeframe)
            : base(CLASSNAME, 1)
        {
            this.security = security;
            this.timeframe = timeframe;
        }

        public CandleUpdate(Security security, string timeframe, Candle candle)
            : base(CLASSNAME, 1)
        {
            this.security = security;
            this.timeframe = timeframe;
            this.candle = candle;
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<CandleUpdate>(this).Replace("\\/", "/");
        }

        public new static CandleUpdate Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<CandleUpdate>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }


    }
}
