namespace CAT.Model
{
    public class Asset : Microsoft.WindowsAzure.Storage.Table.TableEntity
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public double Qnty { get; set; }
        public double Share { get; set; }

        public Asset() { }

        public Asset(string index, string syn, string name, string type, string qnty, string share)
        {
            this.PartitionKey = index;
            this.RowKey = syn;
            this.Name = name;
            this.Type = type;
            this.Qnty = double.Parse(qnty);
            this.Share = double.Parse(share);
        }

        public override string ToString()
        {
            return PartitionKey + ";" + RowKey + ";" + Name + ";" + Qnty + ";" + Share;
        }
    }
}
