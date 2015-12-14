using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace CAT.Model
{
    [Serializable]
    public class Setup : ObservableObject
    {
        public Setup() { }

        public Setup(int id, string name)
        {
            this.Online = false;
            this.SetupId = id;
            this.How2Trade = 0;
            this.Name = name;
            this.TimeFrame = 0;
            this.Allow = "A";
            this.Capital = 100000m;
            this.DayTradeDuration = 0;
            this.Slippage = 0;
            this.Discount = 100;
            this.OfflineStartTime = new DateTime(2010, 1, 1);
        }

        #region Fields
        private bool _online = false;
        public bool Online
        {
            get
            {
                return _online;
            }
            set
            {
                _online = value;
                RaisePropertyChanged(() => this.Online);
            }
        }
        public int Id { get; set; }
        [XmlAttribute("ID")]
        public int How2Trade { get; set; }
        public int SetupId { get; set; }
        public string Name { get; set; }
        public double TimeFrame { get; set; }
        public string Symbol { get; set; }
        public string Allow { get; set; }
        public decimal? Capital { get; set; }
        public double? DynamicLoss { get; set; }
        public double? StaticLoss { get; set; }
        public double? StaticGain { get; set; }
        public string Parameters { get; set; }
        public double DayTradeDuration { get; set; }
        public decimal Slippage { get; set; }
        public decimal Discount { get; set; }
        public DateTime OfflineStartTime { get; set; }
        public int? TradesCount { get; set; }
        public decimal? TotalNetProfit { get; set; }
        public decimal? DailyNetProfit { get; set; }
        public decimal? MaxDrawndown { get; set; }
        public decimal? MDDPOT { get; set; }
        public decimal? SharpeRatio { get; set; }
        public decimal? PositiveTrades { get; set; }
        public decimal? WinLossRatio { get; set; }
        public decimal? TotalCosts { get; set; }
        public string Description { get; set; }     
        public List<Client> Basket { get; set; }
        #endregion
        public override string ToString()
        {
            var units = Symbol == null || Symbol.Contains("FUT") ? " (R$): " : " (%): ";

            var outstring = ";" + SetupId.ToString("000-") + Name + "\r\n";
            outstring += DayTradeDuration == 0 ? ";Swing-trade de " + Symbol : ";Day-trade de " + Symbol;
            outstring += TimeFrame > 0 ? ". Tempo Gráfico: " + TimeFrame + ". " : " Gráfico diário.";
            if (DayTradeDuration > 0) outstring += " (Bell: " + Math.Round(DayTradeDuration, 2) + ")";
            outstring +=
                Allow == "C" ? "\r\n;Só compras. Slippage: " + Slippage :
                Allow == "V" ? "\r\n;Só vendas. Slippage: " + Slippage : "\r\n;Slippage: " + Slippage;

            if (StaticGain.HasValue) outstring += "\r\n;Objetivo fixo inicial: " + StaticGain.Value;
            if (StaticLoss.HasValue) outstring += "\r\n;Stop fixo inicial: " + StaticLoss.Value;
            if (DynamicLoss.HasValue) outstring += "\r\n;Trailing Stop: " + DynamicLoss.Value;
            if (!string.IsNullOrWhiteSpace(Parameters)) outstring += "\r\n;Parâmetros: " + Parameters;

            outstring += "\r\n;Simulação de " + OfflineStartTime.ToShortDateString();
            
            if (TradesCount.HasValue)
                outstring += "\r\n;Operações: " + TradesCount;
            if (TotalCosts.HasValue)
                outstring += " (R$ " + Math.Floor(TotalCosts.Value / 100) / 10 + " k/a.m. em custos. Desconto: " + Discount + "%)";
            if (PositiveTrades.HasValue)
                outstring += "\r\n;" + Math.Round(PositiveTrades.Value, 2) + "% W";
            if (WinLossRatio.HasValue)
                outstring += " (Rácio W/L " + Math.Round(WinLossRatio.Value, 2) + ":1)";
            if (TotalNetProfit.HasValue)
                outstring += "\r\n;Lucro líquido" + units + Math.Round(TotalNetProfit.Value, 2);
            if (DailyNetProfit.HasValue)
                outstring += " (" + Math.Round(DailyNetProfit.Value, 2) + " mensal)";
            if (MaxDrawndown.HasValue)
                outstring += "\r\n;Drawdown máximo" + units + Math.Round(MaxDrawndown.Value, 2);
            if (SharpeRatio.HasValue)
                outstring += "\r\n;Sharpe Ration: " + Math.Round(SharpeRatio.Value, 5);
            if (!string.IsNullOrWhiteSpace(Description)) outstring += "\r\n;" + Description;
            return outstring + "\r\n";
        }
        public string ToShortString()
        {
            var outstring = ";" + SetupId.ToString("000-") + Name;
            outstring += DayTradeDuration == 0 ? ";S;" : ";D;";
            outstring += Symbol + ";" + Allow + ";";
            outstring += TimeFrame > 0 ? TimeFrame.ToString() + ";" : "EOD;";
            outstring += DynamicLoss.HasValue ? DynamicLoss.Value + ";" : ";";
            outstring += StaticLoss.HasValue ? StaticLoss.Value + ";" : ";";
            outstring += StaticGain.HasValue ? StaticGain.Value + ";" : ";";
            outstring += Parameters + ";";
            outstring += Discount + ";";
            outstring += OfflineStartTime.ToShortDateString() + ";";
            outstring += TradesCount.HasValue ? TradesCount + ";" : ";";
            outstring += SharpeRatio.HasValue ? Math.Round(SharpeRatio.Value, 5) + ";" : ";";
            outstring += TotalNetProfit.HasValue ? Math.Round(TotalNetProfit.Value, 5) + ";" : ";";
            outstring += MaxDrawndown.HasValue ? Math.Round(MaxDrawndown.Value, 5) + ";" : ";";
            outstring += Description;
            return !outstring.Contains("\t") ? outstring : outstring.Substring(0, outstring.IndexOf('\t'));
        }
    }
}
