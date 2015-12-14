using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Globalization;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Table;
using System.Configuration;
using System.Net;
using System.Xml.Serialization;
using CAT.Model;

namespace CAT.BuildDatabase
{
    class Program
    {
        private static ConcurrentBag<Tick> _bag;
        private static string _connectionString;
        private static string _codefile;
        private static string[] _iBrA;
        private static ConcurrentDictionary<int, string> _codes;
        private static string _root = @"C:\Users\alex\Documents\IBOV\";
        private static FileInfo fileprov = new FileInfo(_root + @"\Stock\Proventos_1998_A_2014.xml");
        //private static FileInfo filecota = new FileInfo(_root + @"\Stock\COTAHIST_1998_A_2014.xml");
        private static FileInfo filecota = new FileInfo(_root + @"\Stock\COTAHIST_A2014.xml");
        private static FileInfo fileadjc = new FileInfo(_root + @"\Stock\ADJCLOSE_1998_A_2014.xml");
        
        private static CloudTableClient TableClient
        {
            get
            {
                return CloudStorageAccount.Parse(
                    "DefaultEndpointsProtocol=https;" +
                    "AccountName=catarino;" +
                    "AccountKey=pQFN//7LQ5L0ki0AyUKcwTBUz9i9F07zoX5iFezs9Q4Fm4mm/WsF8MmKmOwZVmNdOpOGr7NZnJs5Jy4ZX6RmsQ==")
                    .CreateCloudTableClient();
            }
        }
        
        static void Main(string[] args)
        {
            
            //_iBrA = File.ReadAllLines(@"C:\Users\alex\Documents\IBOV\IBrA.csv");
            //if (table.CreateIfNotExists()) Console.WriteLine("Daily table created!");

            //foreach (var i in _iBrA)
            //{
            //    var data = i.Split(';');
            //    var result = table.Execute(TableOperation.InsertOrReplace(new Asset("IBrA", data[0], data[1], data[2], data[3], data[4])));
            //}
            var cutoff = new DateTime(2008, 1, 1);

            _iBrA = TableClient.GetTableReference("Market").ExecuteQuery(new TableQuery<Asset>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "IBrA")))
                .Select(a=> a.RowKey).ToArray();

            char key;
            do
            {
                key = Console.ReadKey().KeyChar;

                switch (key)
                { 
                    case '1':
                        Send2Azure();
                        break;
                    case '2':
                        GetLastFromCOTAHIST();
                        break;
                    case '3':
                        GetDaily();
                        break;
                    case '4':
                        GetEvents();
                        break;
                    case '5':
                        ReadCOTAHIST();
                        //COTAHIST2CSV();
                        break;
                    case '6':
                        //AdjustedPrice();
                        //CreatePriceMatrix();
                        AdjustedPrice("VALE5");    
                        break;
                    case '7':
                        ReadNEG(true, cutoff);
                        break;
                    case '8':
                        ReadNEG(false, cutoff);
                        break;
                    case '9':

                        break;
                    default:
                        break;
                }

            } while (key != '0');

        }

        private static async void Send2Azure()
        {
            var table = TableClient.GetTableReference("Daily");
            if (table.CreateIfNotExists()) Console.WriteLine("Daily table created!");
            foreach (var cloudTable in TableClient.ListTables()) Console.WriteLine(cloudTable.Name);

            var candles = table.ExecuteQuery(new TableQuery<Candle>()).ToList();
                //.Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, "19022014")))
                //.ToList();

            if (!filecota.Exists) return;

            var inbatch = new List<Candle>();
            var batchOperation = new TableBatchOperation();
            var upcandles = new List<Candle>();
            //DeserializeCandle(xmlfile);
            
            foreach (var upcandle in upcandles)
            {
                if (inbatch.Count > 99 || (inbatch.Count != 0 && upcandle.PartitionKey != inbatch.Last().PartitionKey))
                {
                    var resultb = await table.ExecuteBatchAsync(batchOperation);
                    batchOperation.Clear();
                    inbatch.Clear();
                }
                
                inbatch.Add(upcandle);
                batchOperation.InsertOrReplace(upcandle);
            }
            if (batchOperation.Count > 0) await table.ExecuteBatchAsync(batchOperation);

            Console.WriteLine("All read!");
        }
        private static async void GetEvents()
        {
            var timer = DateTime.Now;
            Console.Write("\t" + timer);

            var symbols = new List<string>();
            var data = new List<Candle>();
            Deserialize(filecota.FullName, out data);
            data.Where(q => q.OpenTime.AddMonths(3) >= DateTime.Today).ToList()
                .ForEach(q => { if (!symbols.Contains(q.Symbol.Substring(0, 4))) symbols.Add(q.Symbol.Substring(0, 4)); });
            symbols.OrderBy(s => s);
            
            Console.WriteLine((DateTime.Now - timer).TotalSeconds.ToString("\tData read in #.000 seconds"));

            var CorporateEvents = new List<CorporateEvents>();
            
            for (var id = 1; id <= 23310; id++)
            {
                var thisIdEvents = await GetEventForID(id, symbols, new DateTime(1998, 1, 1));
                if (thisIdEvents == null || thisIdEvents.Count == 0) continue;

                CorporateEvents.AddRange(thisIdEvents);
                Console.WriteLine(Math.Ceiling((DateTime.Now - timer).TotalMilliseconds / id));
            }

            Serialize(filecota.FullName.Replace("COTAHIST", "Proventos"), CorporateEvents.OrderBy(e => e.Date).ThenBy(e => e.Symbols).ToList());
            Console.WriteLine("Done in " + Math.Round((DateTime.Now - timer).TotalMinutes, 3) + " minutes!");
        }

        static private async Task<List<CorporateEvents>> GetEventForID(int id, List<string> symbols, DateTime cutoff)
        {
            var index = 0;
            var symbol = string.Empty;
            var sector = string.Empty;
            var tmpDate = DateTime.Now;
            var mEvents = new List<CorporateEvents>();
            var types = new string[] { "ON", "PN", "PNA", "PNB", "UNT" };

            var thisIdEvents = new List<CorporateEvents>();

            #region ID to symbol
            try
            {
                using (var client = new HttpClient() { MaxResponseContentBufferSize = 100000 })
                {
                    var page0 = await client
                        .GetStringAsync("http://www.bmfbovespa.com.br/pt-br/mercados/acoes/empresas/" +
                        "ExecutaAcaoConsultaInfoEmp.asp?CodCVM=" + id);

                    index = page0.IndexOf("Papel=");
                    if (index < 0) return null;

                    symbol = id != 18112 ? page0.Substring(index + 6, 250) : "ABEV3&";
                    index = symbol.IndexOf("&");
                    if (index < 5) return null;

                    symbol = symbol.Substring(0, index);

                    if (!symbols.Contains(symbol.Substring(0, 4)))
                        return thisIdEvents;

                    #region Classificação Setorial
                    index = page0.IndexOf("Classifica&ccedil;&atilde;o Setorial:") + 100;
                    sector = page0.Substring(index);
                    sector = sector.Substring(sector.IndexOf("Dado") + 8);
                    sector = sector.Substring(0, sector.IndexOf("\r\n")).Trim().Replace("&ccedil;", "ç").Replace("&ecirc;", "ê")
                        .Replace("&aacute;", "á").Replace("&oacute;", "ó").Replace("&eacute;", "é").Replace("&iacute;", "í")
                        .Replace("&atilde;", "ã").Replace("&otilde;", "õ").Replace("&uacute;", "ú");
                    #endregion

                    thisIdEvents.Add(new CorporateEvents(id, symbol, sector, cutoff.ToString(), "", ""));
                }
            }
            catch (Exception e)
            {
                //Console.WriteLine(id.ToString("00000: 1> ") + e.Message);
                return thisIdEvents;
            }


            #endregion

            #region Proventos em Dinheiro
            try
            {
                using (var client = new HttpClient() { MaxResponseContentBufferSize = 1000000 })
                {
                    var page1 = await client
                        .GetStringAsync("http://www.bmfbovespa.com.br/Cias-Listadas/Empresas-Listadas/" +
                        "ResumoProventosDinheiro.aspx?codigoCvm=" + id);

                    foreach (var type in types)
                    {
                        index = 0;
                        while ((index = page1.IndexOf("<td>" + type + "</td>", index)) > 0)
                        {
                            var start = index;
                            index = page1.IndexOf("</tr>", index);
                            var money = page1.Substring(start, index - start);

                            start = money.IndexOf("direita") + 9;
                            var idx = money.IndexOf("<", start) - start;
                            var dMoney = double.Parse(money.Substring(start, idx));

                            start = money.IndexOf("direita", start) + 9;
                            idx = money.IndexOf("<", start) - start;
                            dMoney = dMoney / double.Parse(money.Substring(start, idx));

                            start = money.LastIndexOf("centralizado") + 14;
                            idx = money.IndexOf("<", start) - start;
                            var date = money.Substring(start, idx);

                            if (!DateTime.TryParse(date, out tmpDate))
                            {
                                money = money.Remove(start - 14);
                                start = money.LastIndexOf("centralizado") + 14;
                                idx = money.IndexOf("<", start) - start;
                                date = money.Substring(start, idx);
                            }

                            var corpEvent = new CorporateEvents(id, symbol, sector, date, dMoney.ToString(), type);
                            if (corpEvent.Date.Year < cutoff.Year) continue;

                            mEvents.Add(corpEvent);
                        }
                    }

                    while (mEvents.Count > 0)
                    {
                        var today = mEvents.First().Date;
                        var subEvents = mEvents.FindAll(e => e.Date == today);
                        var corpEvent = mEvents.First();
                        corpEvent.ProvCashON = subEvents.Sum(e => e.ProvCashON);
                        corpEvent.ProvCashPN = subEvents.Sum(e => e.ProvCashPN);
                        corpEvent.ProvCashPNA = subEvents.Sum(e => e.ProvCashPNA);
                        corpEvent.ProvCashPNB = subEvents.Sum(e => e.ProvCashPNB);
                        corpEvent.ProvCashUNT = subEvents.Sum(e => e.ProvCashUNT);
                        thisIdEvents.Add(corpEvent);
                        mEvents.RemoveAll(e => e.Date == today);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Write(id.ToString("00000: 2> ") + e.Message);
                return thisIdEvents;
            }
            #endregion

            #region Proventos em Ações

            #region Special case for "Nova AMBEV"
            if (id == 18112)
            {
                thisIdEvents.Add(new CorporateEvents(id, symbol, sector, "20/10/2000", "50/1", ""));
                thisIdEvents.Add(new CorporateEvents(id, symbol, sector, "01/08/2007", "100/1", ""));
                thisIdEvents.Add(new CorporateEvents(id, symbol, sector, "17/12/2010", "50/1", ""));
                thisIdEvents.Add(new CorporateEvents(id, symbol, sector, "08/11/2013", "50/1", ""));

                Console.Write("18112 (ABEV): "); return thisIdEvents;
            }
            #endregion
            
            try
            {
                using (var client = new HttpClient() { MaxResponseContentBufferSize = 1000000 })
                {
                    var page2 = await client
                        .GetStringAsync("http://www.bmfbovespa.com.br/Cias-Listadas/Empresas-Listadas/" +
                        "ResumoEventosCorporativos.aspx?codigoCvm=" + id);
                    if (page2.Contains("Não há dados")) return thisIdEvents;

                    index = 0;

                    while ((index = page2.IndexOf("lblPercOuFator", index) - 500) > 0)
                    {
                        page2 = page2.Substring(index);
                        var start = page2.IndexOf("center") + 8;

                        start = page2.IndexOf("center", start) + 8;
                        index = page2.IndexOf("<", start);
                        var date = page2.Substring(start, index - start);

                        start = page2.IndexOf("lblPercOuFator", index) + 30;
                        index = page2.IndexOf("<", start);
                        var share = page2.Substring(start, index - start);

                        var corpEvent = new CorporateEvents(id, symbol, sector, date, share, "");
                        if (corpEvent.Date.Year < cutoff.Year) continue;

                        // Special Case: Banco Santander
                        if (id == 20532 && corpEvent.Date.Year == 2014) continue;

                        thisIdEvents.Add(corpEvent);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(id.ToString("00000: 3> ") + e.Message);
                return thisIdEvents;
            }

            #endregion

            Console.Write(id.ToString("00000 (") + symbol.Substring(0, 4) + "): ");
            return thisIdEvents;
        }

        private static async void GetDaily()
        {
            var result = "";
            var timer = DateTime.Now;
            var client = new HttpClient() { MaxResponseContentBufferSize = 1000000 };
            var url = "http://www.bmfbovespa.com.br/Pregao-Online/ExecutaAcaoAjax.asp?CodigoPapel=";
            
            for (var i = 0; i < _iBrA.Length; i++)
            {
                result += await client.GetStringAsync(url + _iBrA[i]);
            }

            File.WriteAllText(@"C:\Users\alex\Documents\IBOV\IBrA.txt", result);
            Console.WriteLine(DateTime.Now - timer);
        }

        private static async void ReadCOTAHIST()
        {
            Console.WriteLine("\t" + DateTime.Now);
            int type;
            var starttime = DateTime.Now;
            var symbols = new List<string>();
            var bvspfiles = new DirectoryInfo(_root + @"\Stock\").GetFiles("COTAHIST_A*zip");
            var imax= bvspfiles.Count() - 1;
            
            for (var i = imax; i >= 0; i--)
            {
                if (int.Parse(bvspfiles[i].Name.Substring(10, 4)) < 1998) continue;
            
                var candles = new List<Candle>();
            
                using (var zip2open = new FileStream(bvspfiles[i].FullName, FileMode.Open, FileAccess.Read))
                {
                    using (var archive = new ZipArchive(zip2open, ZipArchiveMode.Read))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            using (var file = new StreamReader(entry.Open()))
                            {
                                while (!file.EndOfStream)
                                {
                                    var line = await file.ReadLineAsync();
                                    
                                    if (!int.TryParse(line.Substring(16, 3), out type) || type > 11) continue;
                                    
                                    var output = line.Substring(12, 7).Replace("AMBV4", "ABEV3").Trim();
                                    if (i == imax && !symbols.Contains(output)) symbols.Add(output);
                                    if (!symbols.Contains(output)) continue;

                                    candles.Add(new Candle(
                                        output, "D",
                                        line.Substring(8, 2) + "/" + line.Substring(6, 2) + "/" + line.Substring(2, 4),
                                        line.Substring(56, 13),
                                        line.Substring(69, 13),
                                        line.Substring(82, 13),
                                        line.Substring(108, 13),
                                        line.Substring(170, 16),
                                        line.Substring(152, 18)));
                                }
                            }

                            if (i == imax)
                            {
                                symbols.Clear();
                                var lastmonth = candles.Last().OpenTime.Date.AddMonths(-1);
                                candles.FindAll(c => c.OpenTime >= lastmonth)
                                    .ForEach(c => { if (!symbols.Contains(c.Symbol))symbols.Add(c.Symbol); });
                                symbols.OrderBy(s => s);
                                Console.WriteLine("Temos " + symbols.Count + " ações!");
                            }

                            Console.WriteLine("Read " + entry.Name + (DateTime.Now - starttime).TotalSeconds.ToString(" after 00.00 seconds"));
                            
                            Serialize(bvspfiles[i].FullName.ToUpper().Replace(".ZIP", ".xml"),
                                candles.OrderBy(t => t.OpenTime).ThenBy(a => a.Symbol).ToList());
                        }
                    }
                }
            }                  
            Console.WriteLine("Done!");
        }

        private static void COTAHIST2CSV()
        {
            Console.WriteLine("\t" + DateTime.Now);
            var cedir = new DirectoryInfo(@"C:\Users\alex\Documents\IBOV\CorporativeEvents\");
            var candles = new List<Candle>();
            //DeserializeCandle(@"C:\Users\alex\Documents\IBOV\Stock\COTAHIST_1998_A_2014.xml");
            
            while (candles.Count > 0)
            {
                var symbol = candles.First().Symbol;
                var subcandles = candles.FindAll(c => c.Symbol == symbol);
                Console.WriteLine(candles.RemoveAll(c => c.Symbol == symbol) + " candles de " + symbol + " removidos");

                var cedata = cedir.GetFiles(symbol.Substring(0, 4) + "*");

            }        
        }

        private static async void ReadNEG(bool isStock, DateTime cutoff)
        {
            var assetType = isStock ? "Stock" : "Options";

            _iBrA = new string[] { "USIM5" };
            
            var bvspfiles = new DirectoryInfo(@"C:\Users\alex\Documents\IBOV\" + assetType + @"\").GetFiles("NEG_*.zip");
            foreach (var bvspfile in bvspfiles)
            {
                var strStartDate = bvspfile.Name.Substring(4,8);
                var fileStartDate = DateTime.Parse(strStartDate.Substring(0, 4) + "/" + strStartDate.Substring(4, 2) + "/" + strStartDate.Substring(6, 2));
                if (fileStartDate < cutoff) continue;

                using (var zip2open = new FileStream(bvspfile.FullName, FileMode.Open, FileAccess.Read))
                {
                    using (var archive = new ZipArchive(zip2open, ZipArchiveMode.Read))
                    {
                        var ticks = new List<Tick>();
                        var starttime = DateTime.Now;

                        Console.WriteLine("Start reading " + bvspfile.Name + " at " + starttime);

                        foreach (var entry in archive.Entries)
                        {
                            using (var file = new StreamReader(entry.Open()))
                            {
                                while (!file.EndOfStream)
                                {
                                    var line = await file.ReadLineAsync();
                                    var data = line.Split(';');
                                    if (data.Length < 5) continue;

                                    if ((!isStock && data[1].Substring(0, 4) != "PETR" && data[1].Substring(0, 4) != "VALE") ||
                                        (isStock && !_iBrA.Contains(data[1].Trim()))) continue;

                                    var tick = new Tick(data[1], data[0] + " " + data[5], data[3], data[4]);

                                    if (ticks.Count == 0 || (ticks.Last().Value != tick.Value && ticks.Last().Time.Minute != tick.Time.Minute))
                                        ticks.Add(tick);
                                    else
                                        ticks.Last().Qnty = ticks.Last().Qnty + tick.Qnty;
                                }
                            }
                            Console.WriteLine("Read " + entry.Name + " after " + (DateTime.Now - starttime).TotalMinutes.ToString("#00.00") + " minutes.");
                        }
                        Serialize(bvspfile.FullName, ticks.OrderBy(t => t.Time).ToList());
                    }
                }
            }
            Console.WriteLine("Done!");
        }

        private static async void GetLastFromCOTAHIST()
        {
            var starttime = DateTime.Now;
            var dicquotes = new ConcurrentDictionary<string,DateTime>();
            var tmp = "DefaultEndpointsProtocol=https;AccountName=giminidatabase;AccountKey=8Kt6cGFM4lYsSUvm1i8uBbazEVLV+t1F92log2T8AnHA/bdKxPvcFUG/f9PbsqZWaEn+YZmfpV/pV+O/sRrHeA==";
            var tableClient = CloudStorageAccount.Parse(tmp).CreateCloudTableClient();
            var table = tableClient.GetTableReference("Daily");
            var quotes = table.ExecuteQuery(new TableQuery<Candle>()
                .Where(TableQuery.GenerateFilterConditionForDate("Time", QueryComparisons.GreaterThanOrEqual, new DateTime(2014, 2, 1))))
                .ToList().OrderBy(q => q.OpenTime);

            if (quotes.Any()) foreach (var quote in quotes) dicquotes.AddOrUpdate(quote.PartitionKey, quote.OpenTime, (k, v) => v = quote.OpenTime);
            foreach (var q in dicquotes)
                Console.WriteLine(q.Key + " " + q.Value);

            using (var zip2open = new FileStream(@"C:\Users\alex\Documents\IBOV\Stock\COTAHIST_D07022014.zip", FileMode.Open, FileAccess.Read))
            {
                using (var archive = new ZipArchive(zip2open, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        using (var file = new StreamReader(entry.Open()))
                        {
                            while (!file.EndOfStream)
                            {
                                var line = await file.ReadLineAsync();
                                var output = line.Substring(12, 7).Trim();
                                if (!_iBrA.Contains(output)) continue;
                                output += ";" + line.Substring(8, 2) + "/" + line.Substring(6, 2) + "/" + line.Substring(2, 4) + ";";
                                if (dicquotes[output.Split(';')[0]] >= DateTime.Parse(output.Split(';')[1])) continue;
                                
                                output += double.Parse(line.Substring(56, 13)) / 100 + ";";
                                output += double.Parse(line.Substring(69, 13)) / 100 + ";";
                                output += double.Parse(line.Substring(82, 13)) / 100 + ";";
                                output += double.Parse(line.Substring(108, 13)) / 100 + ";";
                                output += double.Parse(line.Substring(170, 16)) + ";";
                                output += Int64.Parse(line.Substring(152, 18)) + ";";

                                var data = output.Split(';');

                                //var resultb = await table.ExecuteAsync(TableOperation
                                //    .InsertOrReplace(new Candle(data[0], data[1], data[2], data[3], data[4], data[5], data[6], data[7])));

                                line = @"C:\Users\alex\Documents\IBOV\RawData\" + output.Split(';')[0] + ".csv";
                                using (var writer = File.AppendText(line)) await writer.WriteLineAsync(output);
                            }
                        }
                        Console.WriteLine("Read " + entry.Name + " after " + (DateTime.Now - starttime).TotalMinutes.ToString("#00.00") + " minutes.");
                    }
                }
            }
            

        
        }

        private static void AdjustedPrice()
        {
            var timer = DateTime.Now;
            Console.Write("\t" + timer);

            var adjp = new List<Candle>();
            var prix = new List<Candle>(); 
            var earn = new List<CorporateEvents>();

            Deserialize(filecota.FullName, out prix);
            Deserialize(fileprov.FullName, out earn);
            
            Console.WriteLine((DateTime.Now - timer).TotalSeconds.ToString("\tData read in #.000 seconds"));

            while (prix.Count > 0)
            {
                var symbol = prix.First().Symbol.Substring(0, 4);
                var subprix = prix.FindAll(c => c.Symbol.Contains(symbol));
                var subearn = earn.FindAll(e => e.Symbols.Contains(symbol));

                Console.WriteLine("Processando " + symbol +
                    prix.RemoveAll(c => c.Symbol.Contains(symbol)).ToString(": 0000 cotações com ") +
                    earn.RemoveAll(e => e.Symbols.Substring(0, 4) == symbol).ToString("000 proventos"));

                foreach (var e in subearn)
                {
                    subprix.ForEach(p => p.Indicators = e.Sector);

                    if (e.ProvShare.HasValue && e.ProvShare > 0)
                    {
                        subprix.Where(p => p.OpenTime <= e.Date).ToList()
                           .ForEach(p => p.AdjClValue = p.AdjClValue / e.ProvShare.Value);
                    }

                    if (e.ProvCashON.HasValue && e.ProvCashON > 0)
                    {
                        var ex = subprix.Find(p => p.OpenTime == e.Date && p.Symbol.Contains("3"));
                        var corr = ex == null ? 1 : 1 - e.ProvCashON.Value / ex.CloseValue;

                        subprix.Where(p => p.OpenTime <= e.Date && p.Symbol.Contains("3")).ToList()
                            .ForEach(p => p.AdjClValue = p.AdjClValue * corr);
                    }

                    if (e.ProvCashPN.HasValue && e.ProvCashPN > 0)
                    {
                        var ex = subprix.Find(p => p.OpenTime == e.Date && p.Symbol.Contains("4"));
                        var corr = ex == null ? 1 : 1 - e.ProvCashPN.Value / ex.CloseValue;

                        subprix.Where(p => p.OpenTime <= e.Date && p.Symbol.Contains("4")).ToList()
                            .ForEach(p => p.AdjClValue = p.AdjClValue * corr);
                    }

                    if (e.ProvCashPNA.HasValue && e.ProvCashPNA > 0)
                    {
                        var ex = subprix.Find(p => p.OpenTime == e.Date && p.Symbol.Contains("5"));
                        var corr = ex == null ? 1 : 1 - e.ProvCashPNA.Value / ex.CloseValue;

                        subprix.Where(p => p.OpenTime <= e.Date && p.Symbol.Contains("5")).ToList()
                            .ForEach(p => p.AdjClValue = p.AdjClValue * corr);
                    }

                    if (e.ProvCashPNB.HasValue && e.ProvCashPNB > 0)
                    {
                        var ex = subprix.Find(p => p.OpenTime == e.Date && p.Symbol.Contains("6"));
                        var corr = ex == null ? 1 : 1 - e.ProvCashPNB.Value / ex.CloseValue;

                        subprix.Where(p => p.OpenTime <= e.Date && p.Symbol.Contains("6")).ToList()
                            .ForEach(p => p.AdjClValue = p.AdjClValue * corr);
                    }
                    
                    if (e.ProvCashUNT.HasValue && e.ProvCashUNT > 0)
                    {
                        var ex = subprix.Find(p => p.OpenTime == e.Date && p.Symbol.Contains("11"));
                        var corr = ex == null ? 1 : 1 - e.ProvCashUNT.Value / ex.CloseValue;

                        subprix.Where(p => p.OpenTime <= e.Date && p.Symbol.Contains("11")).ToList()
                            .ForEach(p => p.AdjClValue = p.AdjClValue * corr);
                    }
                }
                
                adjp.AddRange(subprix);
            }

            Serialize(fileadjc.FullName, adjp.OrderBy(a => a.OpenTime).ThenBy(a => a.Symbol).ToList());
            Console.WriteLine(fileadjc.Name + " is written!");
            return;
        }

        private static void AdjustedPrice(string symbol)
        {
            var bvspfiles = new DirectoryInfo(_root + @"\Stock").GetFiles("NEG_*xml");
            var adjp = new List<Candle>();
            var timer = DateTime.Now;
            Console.Write("\t" + timer);

            Deserialize(fileadjc.FullName, out adjp);
            adjp.RemoveAll(c => c.Symbol != symbol);
            
            Console.WriteLine((DateTime.Now - timer).TotalSeconds.ToString("\tData read in #.000 seconds"));

            var sum = 0;
            var err = 0;

            foreach (var file in bvspfiles)
            {
                var candles = adjp.FindAll(c =>
                    c.OpenTime.Year == int.Parse(file.Name.Substring(4, 4)) &&
                    c.OpenTime.Month == int.Parse(file.Name.Substring(8, 2)));

                var ticks = new List<Tick>();
                Deserialize(file.FullName, out ticks);

                Console.WriteLine(file + "\t" + ticks.RemoveAll(t => t.Symbol != symbol) + "\t");

                var hour = 10;

                foreach (var c in candles)
                {
                    var thisticks = ticks.FindAll(t => t.Time.Date == c.OpenTime.Date);
                    var tmp = thisticks[0].Time.Hour;
                    hour = tmp > 11 ? hour : tmp;

                    var bell = thisticks[0].Time.Date.AddHours(hour + 7).AddMinutes(-5);
                    thisticks.RemoveAll(t => t.Time > bell);

                    sum++;
                    if (c.CloseValue != thisticks.Last().Value) err++;
                     
                    thisticks.ForEach(t => t.Value = t.Value * c.AdjClValue / c.CloseValue);
                }

                Serialize(file.FullName.Replace("NEG", symbol), ticks);
            }
            Console.WriteLine("End. Err=" + 100 * err / sum + "%");
        }

        private static void CreatePriceMatrix()
        {
            var timer = DateTime.Now;
            Console.Write("\t" + timer);

            var output = "Trading Day";
            var symbols = new List<string>();
            var sectors = new List<string>();
            var symbysec = new ConcurrentDictionary<string, List<string>>();
            var cutoff = new DateTime(1998, 1, 1);
            var file = @"C:\Users\alex\Documents\IBOV\Stock\ADJCLOSE_" + cutoff.Year + "_A_2014.xml";
            var quotes = new List<Candle>();
            Deserialize(file, out quotes);
            quotes = quotes.FindAll(q => q.OpenTime.Year >= 2009 && q.Symbol.Contains("BBDC"));
            quotes.Where(q => q.OpenTime.AddMonths(3) >= DateTime.Today).ToList()
                .ForEach(q => 
                {
                    var sector = q.Indicators.Split('/')[0].Trim();
                    if (!symbysec.TryAdd(sector, new List<string>()))
                    {
                        if (!symbysec[sector].Contains(q.Symbol)) symbysec[sector].Add(q.Symbol);
                    }
                    if (!sectors.Contains(sector)) sectors.Add(sector);
                    if (!symbols.Contains(q.Symbol)) symbols.Add(q.Symbol);
                });

            foreach (var kvp in symbysec)
            {
                var tmpsymbols = kvp.Value.OrderBy(s => s).ToList();
                var tmpoutput = "Trading Day";
                foreach (var symbol in tmpsymbols) tmpoutput = tmpoutput + ";" + symbol;

                var newfile = file.Replace(".xml", "_" + kvp.Key.Replace(" ", "_") + ".csv");
                if (File.Exists(newfile)) File.Delete(newfile);
                File.AppendAllText(newfile, tmpoutput);
                Console.WriteLine(newfile + " written!");
            }

            symbols = symbols.OrderBy(s => s).ToList();
            foreach (var symbol in symbols) output = output + ";" + symbol;
            
            Console.WriteLine((DateTime.Now - timer).TotalSeconds.ToString("\tData read in #.000 seconds"));

            while (quotes.Count > 0)
            {
                var today = quotes.First().OpenTime.Date;
                var subq = quotes.FindAll(q => q.OpenTime.Date == today);
                
                Console.WriteLine(quotes.RemoveAll(q => q.OpenTime.Date == today).ToString("000 cotações no dia ") + today.ToShortDateString());

                foreach (var kvp in symbysec)
                {
                    var tmpsymbols = kvp.Value.OrderBy(s => s).ToList();
                    var tmpoutput = "\r\n" + today.ToShortDateString();
                    foreach (var symbol in tmpsymbols)
                    {
                        var quote = subq.Find(q => q.Symbol == symbol);
                        tmpoutput = quote == null ? tmpoutput + ";" : tmpoutput + ";" + quote.AdjClValue +";" + quote.CloseValue;
                    }
                    File.AppendAllText(file.Replace(".xml", "_" + kvp.Key.Replace(" ", "_") + ".csv"), tmpoutput);
                }
            }
            //file = file.Replace(".xml", ".csv");
            //if (File.Exists(file)) File.Delete(file);
            //File.AppendAllText(file, output);
            //Console.WriteLine(file + " written!");
        }
       
        private static  void GetNEGfromFTP()
        {
            // Get the object used to communicate with the server.
            var request = (FtpWebRequest)WebRequest.Create("ftp://ftp.bmf.com.br/marketdata/Bovespa-Vista/NEG_20111001_A_20111031.zip");
            request.Method = WebRequestMethods.Ftp.DownloadFile;

            // This example assumes the FTP site uses anonymous logon.
            request.Credentials = new NetworkCredential("anonymous", "janeDoe@contoso.com");

            var response = (FtpWebResponse)request.GetResponse();

            var responseStream = response.GetResponseStream();
            var reader = new StreamReader(responseStream);
            Console.WriteLine(reader.ReadToEnd());

            Console.WriteLine("Download Complete, status {0}", response.StatusDescription);

            reader.Close();
            response.Close();  
        }

        #region Serialization
        private static void Serialize<T>(string file, List<T> data)
        {
            using (var tw = new StreamWriter(file.ToUpper()))
                (new XmlSerializer(typeof(List<T>))).Serialize(tw, data);
            Console.WriteLine(data.Count + " elements written to\r\n" + file.ToUpper());
        }
        private static void Deserialize<T>(string file, out List<T> data)
        {
            using (var rw = new StreamReader(file))
                data = (List<T>)(new XmlSerializer(typeof(List<T>))).Deserialize(rw);
        }
        #endregion
    }
    #region Tick
    //public class Tick
    //{
    //    public Tick()
    //    {

    //    }
    //    public Tick(string symbol, string time, string value, string qnty)
    //    {
    //        this.Symbol = symbol.Trim();
    //        this.Time = DateTime.Parse(time);
    //        this.Value = double.Parse(value.Replace(".", ","));
    //        this.Qnty = int.Parse(qnty);
    //    }
    //    public string Symbol { get; set; }
    //    public int Qnty { get; set; }
    //    public double Value { get; set; }
    //    public DateTime Time { get; set; }
    //    public override string ToString()
    //    {
    //        return Symbol + ";" + Time + ";" + Value + ";" + Qnty;
    //    }
    //    public string ToShortString()
    //    {
    //        return Symbol + " " + Time.ToString("HH:mm:ss ");
    //    }
    //}
    #endregion
}
