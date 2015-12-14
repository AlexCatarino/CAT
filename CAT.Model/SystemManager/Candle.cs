using System;
using System.Collections.Generic;

namespace CAT.Model
{
    [Serializable]
    public class Candle
    {
        public Candle() { }
        public string Period { get; set; }
        public string Symbol { get; set; }
        public string Market { get; set; }
        public uint? Term { get; set; }
        public DateTime OpenTime { get; set; }
        public decimal OpenValue { get; set; }
        public decimal MaxValue { get; set; }
        public decimal MinValue { get; set; }
        public decimal AveValue { get; set; }
        public decimal CloseValue { get; set; }
        public decimal AdjClValue { get; set; }
        public decimal Trades { get; set; }
        public decimal Quantity { get; set; }
        public decimal Volume { get; set; }
        public decimal? Strike { get; set; }
        public string Indicators { get; set; }

        public Candle(Tick tick)
        {
            this.Symbol = tick.Symbol;
            this.OpenTime = tick.Time;
            this.OpenValue = tick.Value;
            this.MaxValue = tick.Value;
            this.MinValue = tick.Value;
            this.CloseValue = tick.Value;
            this.AdjClValue = tick.Value;
            this.Quantity = tick.Qnty;
        }

        public Candle(string syn, string period, string time, string abe, string max, string min, string ult, string vol, string qua)
        {
            this.Symbol = syn;
            this.Period = period;
            this.OpenTime = DateTime.Parse(time);
            this.OpenValue = decimal.Parse(abe) / 100;
            this.MaxValue = decimal.Parse(max) / 100;
            this.MinValue = decimal.Parse(min) / 100;
            this.CloseValue = decimal.Parse(ult) / 100;
            this.AdjClValue = decimal.Parse(ult) / 100;
            this.Quantity = decimal.Parse(qua);
            this.Volume = decimal.Parse(vol);
        }

        public Candle(string COTAHIST)
        {
            this.Period = "D";

            this.Symbol = COTAHIST.Substring(12, 12).Trim();
            this.OpenTime = ReadDateTime(COTAHIST.Substring(2, 8));
            
            this.OpenValue = ReadDecimal(COTAHIST.Substring(56, 13), 100);
            this.MaxValue = ReadDecimal(COTAHIST.Substring(69, 13), 100);
            this.MinValue = ReadDecimal(COTAHIST.Substring(82, 13), 100);
            this.AveValue = ReadDecimal(COTAHIST.Substring(95, 13), 100);
            this.CloseValue = ReadDecimal(COTAHIST.Substring(108, 13), 100);
            this.Trades = ReadDecimal(COTAHIST.Substring(147, 5), 1);
            this.Quantity = ReadDecimal(COTAHIST.Substring(152, 18), 1000);
            this.Volume = ReadDecimal(COTAHIST.Substring(170, 16), 1000000);
            this.AdjClValue = this.CloseValue;
            
            this.Market = GetMarket(COTAHIST.Substring(24, 3));
            this.Term = ReadUint(COTAHIST.Substring(49, 3));
            this.Strike = ReadDecimal(COTAHIST.Substring(188, 13), 100);
        }

        public Candle(AzureCandle azurecandle)
        {
            this.Symbol = azurecandle.Symbol;
            this.Period = azurecandle.Period;
            this.OpenTime = azurecandle.OpenTime;
            this.OpenValue = azurecandle.OpenValue;
            this.MaxValue = azurecandle.MaxValue;
            this.MinValue = azurecandle.MinValue;
            this.CloseValue = azurecandle.CloseValue;
            this.AdjClValue = azurecandle.AdjClValue;
            this.Quantity = azurecandle.Quantity;
            this.Volume = azurecandle.Volume;
        }
        
        public void Update(Tick tick)
        {
            this.Quantity += tick.Qnty;
            this.CloseValue = tick.Value;
            this.AdjClValue = tick.Value;
            this.MaxValue = Math.Max(this.MaxValue, tick.Value);
            this.MinValue = Math.Min(this.MinValue, tick.Value);
        }

        private DateTime ReadDateTime(string str)
        {
            return DateTime.ParseExact(str, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        }
        private uint ReadUint(string str)
        {
            if (this.Market != "TERMO") return 0;

            uint result;
            if (!uint.TryParse(str, out result) || result == 0) return 0;
            return result;
        }
        private decimal ReadDecimal(string str, decimal divisor)
        { 
            decimal result;
            if (!decimal.TryParse(str, out result) || result == 0) return 0;
            return result / divisor;
        }

        private string GetMarket(string code)
        {
            return new Dictionary<string, string>()
            {
                { "010", "VISTA" },
                { "012", "EXERCÍCIO DE OPÇÕES DE COMPRA" },
                { "013", "EXERCÍCIO DE OPÇÕES DE VENDA" },
                { "017", "LEILÃO" },
                { "020", "FRACIONÁRIO" },
                { "030", "TERMO" },
                { "050", "FUTURO COM RETENÇÃO DE GANHO" },
                { "060", "FUTURO COM MOVIMENTAÇÃO CONTÍNUA" },
                { "070", "OPÇÕES DE COMPRA" },
                { "080", "OPÇÕES DE VENDA" },
            }[code];
        }

        public override string ToString()
        {
            var format = this.Period == "D" ? "dd-MM-yyyy;" : "dd-MM-yyyy HH:mm;";
            return this.Period + ";" + this.OpenTime.ToString(format) + this.Symbol + ";" +
                this.OpenValue + ";" + this.MinValue + ";" + this.MaxValue + ";" + this.AveValue + ";" +
                this.CloseValue + ";" + this.AdjClValue + ";" + this.Trades + ";" + this.Quantity + ";" +
                this.Volume + ";" + this.Indicators;
        }
    }
    public class AzureCandle : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        public AzureCandle() { }
        public AzureCandle(Candle candle)
        {
            this.Symbol = candle.Symbol;
            this.Period = candle.Period;
            this.OpenTime = candle.OpenTime;
            this.PartitionKey = this.OpenTime.ToString("yyyyMMdd");
            this.RowKey = this.Symbol + "-" + this.Period + "-" + this.OpenTime.ToString("HHmmssfffff");
            this.OpenValue = candle.OpenValue;
            this.MaxValue = candle.MaxValue;
            this.MinValue = candle.MinValue;
            this.CloseValue = candle.CloseValue;
            this.AdjClValue = candle.AdjClValue;
            this.Quantity = candle.Quantity;
            this.Volume = candle.Volume;
        }
        public override string ToString()
        {
            return this.OpenTime.ToString("dd-MM-yyyy HH:mm;") + this.Symbol + ";" + this.OpenValue + ";" + this.MinValue + ";" +
                this.MaxValue + ";" + ";" + this.CloseValue + ";" + this.AdjClValue + ";" + this.Volume + ";" + this.Quantity + ";" + this.Indicators;
        }

        public string Symbol { get; set; }
        public string Period { get; set; }
        public DateTime OpenTime { get; set; }
        public decimal OpenValue { get; set; }
        public decimal MaxValue { get; set; }
        public decimal MinValue { get; set; }
        public decimal CloseValue { get; set; }
        public decimal AdjClValue { get; set; }
        public decimal Quantity { get; set; }
        public decimal Volume { get; set; }
        public string Indicators { get; set; }
    }
}