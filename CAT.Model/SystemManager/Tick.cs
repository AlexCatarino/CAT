namespace CAT.Model
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class Tick
    {
        public Tick()
        {

        }
        public Tick(string symbol, DateTime time, decimal value)
        {
            this.Symbol = symbol;
            this.Time = time;
            this.Value = value;
        }
        public Tick(string NEG)
        {
            var data = NEG.Split(';');
            if (data.Length < 5)
            {
                this.Symbol = "XXXXX";
                return;
            }
            this.Symbol = data[1].Trim();
            this.Time = DateTime.Parse(data[0] + " " + data[5]);
            this.Value = decimal.Parse(data[3].Replace('.', ','));
            this.Qnty = long.Parse(data[4]);
        }
        public Tick(AzureTick azuretick)
        {
            this.Symbol = azuretick.Symbol;
            this.Time = azuretick.Time;
            this.Value = azuretick.Value;
            this.Qnty = azuretick.Qnty;
        }
        public string Symbol { get; set; }
        public long Qnty { get; set; }
        public decimal Value { get; set; }
        public DateTime Time { get; set; }

        public string GetClass()
        {
            if(this.Symbol.Contains("FUT")) return "BMF.FUT";
            
            if ((int)this.Symbol[4] > 64) return "BVSP.OPC";

            var str = this.Symbol.Substring(0, 3);
            if (str == "IND" || str == "WIN" || str == "DOL" || str == "WDO") return "BMF.FUT";

            return "BVSP.VIS";
        }
        public decimal GetMinVar()
        {
            if (this.GetClass().Contains("BVSP.")) return 0.01m;
          
            return new Dictionary<string, decimal>
            {
                { "IND", 5m },
                { "WIN", 5m },
                { "DOL", .5m },
                { "WDO", .5m }
            }[this.Symbol.Substring(0, 3)];
        }
        public decimal[] GetCommissions() 
        {
            var assetclass = this.GetClass();
            var str = assetclass == "BMF.FUT" ? this.Symbol.Substring(0, 3): assetclass.Substring(5, 3);
            
            return new Dictionary<string, decimal[]>
            {                          // 0        1        2
                { "VIS", new decimal[]{ 25.21m, 0.005m, 0.00045m } },   // 0: Broker Fixed Commission
                { "OPC", new decimal[]{ 25.21m, 0.005m, 0.00045m } },   // 1: Broker Variable Commission
                { "IND", new decimal[]{ 01.18m, 0.000m, 0.00000m } },   // 2: Stock Exchange Commission
                { "WIN", new decimal[]{ 01.18m, 0.000m, 0.00000m } },
                { "DOL", new decimal[]{ 02.50m, 0.000m, 0.00000m } },
                { "WDO", new decimal[]{ 02.50m, 0.000m, 0.00000m } }    // Fixo
            }[str];    
        }
        public decimal GetUnit()
        {
            if (this.GetClass().Contains("BVSP.")) return 1m;
            return new Dictionary<string, decimal>
            {
                { "IND", 1m },
                { "WIN", .2m },
                { "DOL", 10m },
                { "WDO", 10m }
            }[this.Symbol.Substring(0, 3)];
        }

        public string ToShortString()
        {
            return this.Symbol + this.Time.ToString(" HH:mm:ss ");
        }
        public string ToFile()
        {
            return ToString().Replace(' ', ';');
        }
        public override string ToString()
        {
            return Symbol + " " + Time + " " + Value + " " + Qnty;
        }    
    }

    public class AzureTick : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        public AzureTick() { }

        public AzureTick(Tick tick)
        {
            this.PartitionKey = tick.Time.ToString("yyyyMMdd"); ;
            this.RowKey = tick.Symbol + "-" + tick.Time.ToString("HHmmssfffff");
      
            this.Symbol = tick.Symbol;
            this.Qnty = tick.Qnty;
            this.Value = tick.Value;
            this.Time = tick.Time;
        }
        public string Symbol { get; set; }
        public long Qnty { get; set; }
        public decimal Value { get; set; }
        public DateTime Time { get; set; }
        public override string ToString()
        {
            return this.Symbol + " " + this.Time + " " + this.Value + " " + this.Qnty;
        }
    }
}
