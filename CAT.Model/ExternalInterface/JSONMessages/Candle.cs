using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// Represents a Candle used in CandleResponse and CandleUpdate messages
    /// </summary>
    [DataContract]
    public class Candle
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "Candle";

        /// <summary>
        /// Formatter for time field (date and time)
        /// </summary>
        public const string DATETIME_FORMATTER = "dd/MM/yyyy - hh:mm:ss";

        /// <summary>
        ///  Candle DateTime dd/MM/yyyy - hh:mm:ss.
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public string date { get; set; }

        /// <summary>
        ///  Candle Open - If it is zero should be ignored as it didn't have change
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public decimal open { get; set; }

        /// <summary>
        ///  Candle High - If it is zero should be ignored as it didn't have change
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public decimal high { get; set; }

        /// <summary>
        ///  Candle Low - If it is zero should be ignored as it didn't have change
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public decimal low { get; set; }

        /// <summary>
        ///  Candle Close
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public decimal close { get; set; }

        /// <summary>
        ///  Quantity Volume - If it is zero should be ignored as the 3rd party provider can't provide this information
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public decimal qttyVol { get; set; }

        public Candle(DateTime date, decimal open, decimal high, decimal low, decimal close, decimal qttyVol)
        {
            this.date = date.ToString(DATETIME_FORMATTER);
            this.open = open;
            this.high = high;
            this.low = low;
            this.close = close;
            this.qttyVol = qttyVol;            
        }


        public string Serialize()
        {
            return JSONSerializer.Serialize<Candle>(this).Replace("\\/", "/");
        }

        public static Candle Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<Candle>(serialized);
        }

        public override string ToString()
        {
            return this.Serialize();
        }
    }
}
