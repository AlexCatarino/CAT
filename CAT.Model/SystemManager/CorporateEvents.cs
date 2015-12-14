using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CAT.Model
{
    public class CorporateEvents
    {
        public int ID { get; set; }
        public string Symbols { get; set; }
        public string Sector { get; set; }
        public DateTime Date { get; set; }
        public decimal? ProvShare { get; set; }
        public decimal? ProvCashON { get; set; }
        public decimal? ProvCashPN { get; set; }
        public decimal? ProvCashPNA { get; set; }
        public decimal? ProvCashPNB { get; set; }
        public decimal? ProvCashUNT { get; set; }
        public CorporateEvents() { }
        public CorporateEvents(int id, string symbols, string sector)
        { 
            this.ID = id;
            this.Symbols = symbols;
            this.Sector = sector;
        }
        public CorporateEvents(int id, string symbols, string sector, string date, string value, string type)
        {
            this.ID = id;
            this.Symbols = symbols;
            this.Sector = sector;
            this.Date = DateTime.Parse(date);
            if (value.Length == 0)
            {
                this.ProvShare = 1;
                this.ProvCashON = 0;
                this.ProvCashPN = 0;
                this.ProvCashPNA = 0;
                this.ProvCashPNB = 0;
                this.ProvCashUNT = 0;
                return;
            }

            switch (type)
            {
                case "ON": this.ProvCashON = decimal.Parse(value); break;
                case "PN": this.ProvCashPN = decimal.Parse(value); break;
                case "PNA": this.ProvCashPNA = decimal.Parse(value); break;
                case "PNB": this.ProvCashPNB = decimal.Parse(value); break;
                case "UNT": this.ProvCashUNT = decimal.Parse(value); break;
                default:

                    var newshare = value.Split('.', ',');
                    if (newshare.Length > 2) value = newshare[0] + newshare[1] + "," + newshare[2];
                    var inplit = value.Split('/');

                    this.ProvShare = inplit.Length == 1
                        ? 1 + (decimal.Parse(value.Replace(".", ",")) / 100)
                        : decimal.Parse(inplit[0].Replace(".", ",")) / 10;

                    break;
            }
        }

        public override string ToString()
        {
            return this.ID + ";" + this.Symbols.Substring(0, 4) + ";" +
                this.Date.Date.ToShortDateString().Replace("-", "/") + ";" + this.ProvShare + ";" + this.ProvCashON + ";" +
                this.ProvCashPN + ";" + this.ProvCashPNA + ";" + this.ProvCashPNB + ";" + this.ProvCashUNT;
        }
    }
}
