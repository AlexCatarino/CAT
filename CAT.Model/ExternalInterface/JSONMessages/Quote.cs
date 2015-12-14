using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// Represents a Quote.
    /// Fields not present means that it haven't been changed on QuoteUpdate message.
    /// On a QuoteResponse message, all available fields (in 3rd party providers) 
    /// will be filled representing the current quote grid line of the security.
    /// </summary>
    [DataContract]
    public class Quote
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "Quote";

        /// <summary>
        /// Formatter for time field (date and time)
        /// </summary>
        public const string DATETIME_FORMATTER = "dd/MM/yyyy - hh:mm:ss";

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

        /// <summary>
        /// Last Security Value
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal last;

        /// <summary>
        /// Percent Change of the Day
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal changePercent;

        /// <summary>
        /// Date and Time of last trade -> dd/MM/yyyy - hh:mm:ss
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string time;               //data e hora do ultimo negocio

        /// <summary>
        /// Current Best Bid
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal bestBid;

        /// <summary>
        /// Current Best Ask
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal bestAsk;

        /// <summary>
        /// Quantity of Best Bid
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public long qtBestBid;

        /// <summary>
        /// Quantity of Best Ask
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public long qtBestAsk;

        /// <summary>
        /// Previous Close
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal prevClose;

        /// <summary>
        /// Open
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal open;

        /// <summary>
        /// Low
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal low;

        /// <summary>
        /// Medium Price 
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal mediumPrice;

        /// <summary>
        /// High
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal high;

        /// <summary>
        /// Volume in Quantity of the day
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public long qtVolume;

        /// <summary>
        /// Trade Volume of the Day
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int tradeVolume;

        /// <summary>
        /// Financial Volume of the Day
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal financialVolume;

        /// <summary>
        /// Quotation By
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int quotationBy;        //como a cotacao eh feita, por acao ou por 1000 acoes

        /// <summary>
        /// Default lot size 
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int lotSize;         //numero minimo para q nao esteja no fracionario

        /// <summary>
        /// Open Interest
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal openInterest;

        /// <summary>
        /// Adjust Price
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public decimal adjustPrice;


        public Quote(
            string exchange,
            string market,
            string symbol,

            decimal? last,
            decimal? changePercent,
            DateTime? time,

            decimal? melhorOfertaBuy,
            decimal? melhorOfertaSell,
            long? QtdMelhorOfertaBuy,
            long? QtdMelhorOfertaSell,

            decimal? prevClose,
            decimal? open,
            decimal? low,
            decimal? precoMedio,
            decimal? high,

            long? volQtd,
            int? volNeg,
            decimal? volFin,

            int? quotationBy,
            int? lotePadrao,

            decimal? openInterest,
            decimal? precoAjuste)
        {
            this.exchange = exchange;
            this.market = market;
            this.symbol = symbol;

            if (last.HasValue)
                this.last = last.Value;

            if (changePercent.HasValue)
                this.changePercent = changePercent.Value;

            if (time.HasValue)
                this.time = time.Value.ToString(DATETIME_FORMATTER);

            if (melhorOfertaBuy.HasValue)
                this.bestBid = melhorOfertaBuy.Value;

            if (melhorOfertaSell.HasValue)
                this.bestAsk = melhorOfertaSell.Value;

            if (QtdMelhorOfertaBuy.HasValue)
                this.qtBestBid = QtdMelhorOfertaBuy.Value;

            if (QtdMelhorOfertaSell.HasValue)
                this.qtBestAsk = QtdMelhorOfertaSell.Value;

            if (prevClose.HasValue)
                this.prevClose = prevClose.Value;

            if (open.HasValue)
                this.open = open.Value;

            if (low.HasValue)
                this.low = low.Value;

            if (precoMedio.HasValue)
                this.mediumPrice = precoMedio.Value;

            if (high.HasValue)
                this.high = high.Value;

            if (volQtd.HasValue)
                this.qtVolume = volQtd.Value;

            if (volNeg.HasValue)
                this.tradeVolume = volNeg.Value;

            if (quotationBy.HasValue)
                this.financialVolume = volFin.Value;

            if (quotationBy.HasValue)
                this.quotationBy = quotationBy.Value;

            if (lotePadrao.HasValue)
                this.lotSize = lotePadrao.Value;

            if (openInterest.HasValue)
                this.openInterest = openInterest.Value;

            if (precoAjuste.HasValue)
                this.adjustPrice = precoAjuste.Value;
        }

        public Quote(string symbol, string market, string exchange)
        {
            this.symbol = symbol;
            this.market = market;
            this.exchange = exchange;
        }

        public string Serialize()
        {
            return JSONSerializer.Serialize<Quote>(this);
        }

        public static Quote Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<Quote>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }

    }
}
