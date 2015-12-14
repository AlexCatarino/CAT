namespace CAT.Model
{
    using System;

    [Serializable]
    public class Client : ObservableObject
    {
        private bool _ismini = false;
        private decimal _capital = 0;
        
        public Client() { }
        public Client(string clientid, decimal capital, bool isMini)
        {
            this.ClientID = clientid;
            this.Capital = capital;
            this.IsMini = isMini;
        }
        
        public int Id { get; set; }  
        public string ClientID { get; set; }
        public int Setup { get; set; }
        public decimal Capital 
        {
            get 
            {
                return _capital;
            }
            set 
            {
                _capital = value;
                RaisePropertyChanged(() => this.Capital);
            }
        }
        public bool IsMini {
            get 
            {
                return _ismini;
            }
            set
            {
                _ismini = value;
                RaisePropertyChanged(() => this.IsMini);
            }
        }
        
        public decimal SetQnty(decimal value, string marketid)
        {
            return marketid == "BMF.FUT" ? this.Capital : 100 * Math.Floor(this.Capital / value / 100);
        }
        public override string ToString()
        {
            return ClientID + ";" + Capital + ";" + IsMini.ToString()[0];
        }
    }
}
