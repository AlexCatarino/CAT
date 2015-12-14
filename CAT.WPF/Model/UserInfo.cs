namespace CAT.WPF.Model
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Management;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;

    public class UserInfo
    {
        private static string _id = string.Empty;

        public string Id { get; set; }
        public DateTime Expiration { get; set; }
        public string Setups { get; set; }
        public UserInfo()
        {
            this.Id = GetId();
        }
        public static string GetId()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                _id = GetHash("CPU >> " + cpuId() + "\nBIOS >> " + biosId() + "\nBASE >> " + baseId()
                    //+"\nDISK >> "+ diskId() + "\nVIDEO >> " + videoId() +"\nMAC >> "+ macId()
                                     );
            }
            return _id;
        }
        public int IsMember()
        {
            GetId();
            var ismember = -1;
                
            try
            {
                var file = "Users.txt";                
                var data = File.Exists(file) ? File.ReadAllText(file) : string.Empty;

                if (string.IsNullOrEmpty(data))
                {
                    var url = new Uri("https://catarino.blob.core.windows.net/catinstall/" + file);
                    using (var stream = new StreamReader(WebRequest.Create(url).GetResponse().GetResponseStream()))
                        data = stream.ReadToEnd();
                }

                foreach (var line in data.Split('\n'))
                {
                    var user = line.Split(';');
                    if (user[0].Trim() != _id) continue;

                    this.Setups = user[2].Trim();
                    this.Expiration = DateTime.Parse(user[1].Trim(), CultureInfo.CurrentCulture);
                    ismember = this.Expiration < DateTime.Today ? 0 : 1;
                }               
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                ismember = -2;
            }
            return IsDeveloper() ? 2 : ismember;
        }
        public bool IsDeveloper()
        {
            GetId();

            var isDev = //
                _id == "7811-0A7B-930C-459E-79B3-3AE9-4B71-AE11" ||
                _id == "F655-A52B-EB33-76CF-CE0C-5921-08DE-95B7" ||
                _id == "4D5D-04B6-0A5B-AA4A-A95E-1811-F907-74FD";

            if (!isDev) return false;

            this.Expiration = DateTime.Today.AddYears(1);
            this.Setups = "1 2 3 4 5 6 7 8 9 10 12 13 14 15 16 17 99";
            return true;
        }
        public static void Serialize()
        {
            var user = new UserInfo();
            user.Expiration = new DateTime(2015, 1, 1);
            user.Setups = " ";

            var userinfoes = new List<UserInfo>();
            userinfoes.Add(user);

            //using (var tw = new StreamWriter("UserInfoes.xml"))
            //    (new System.Xml.Serialization.XmlSerializer(typeof(List<UserInfo>))).Serialize(tw, userinfoes);
        }
        public override string ToString()
        {
            return this.Id + ";" + this.Expiration.ToShortDateString() + ";" + this.Setups;
        }
        //string GetPublicIP()
        //{
        //    try
        //    {
        //        using (var stream = new StreamReader(WebRequest.Create("http://checkip.dyndns.org/").GetResponse().GetResponseStream()))
        //        {
        //            var ip = stream.ReadToEnd();
        //            var i = new int[] { ip.IndexOf("Address: ") + 9, ip.LastIndexOf("</body>") };
        //            return ip.Substring(i[0], i[1] - i[0]);
        //        }
        //    }
        //    catch (Exception e) { return e.Message; }
        //}
        private static string GetHash(string s)
        {
            using (var sec = new MD5CryptoServiceProvider())
            {
                var bt = (new ASCIIEncoding()).GetBytes(s);
                return GetHexString(sec.ComputeHash(bt));
            }
        }
        private static string GetHexString(byte[] bt)
        {
            var s = string.Empty;
            for (var i = 0; i < bt.Length; i++)
            {
                byte b = bt[i];
                int n, n1, n2;
                n = (int)b;
                n1 = n & 15;
                n2 = (n >> 4) & 15;
                if (n2 > 9)
                    s += ((char)(n2 - 10 + (int)'A')).ToString();
                else
                    s += n2.ToString(CultureInfo.CurrentCulture);
                if (n1 > 9)
                    s += ((char)(n1 - 10 + (int)'A')).ToString();
                else
                    s += n1.ToString(CultureInfo.CurrentCulture);
                if ((i + 1) != bt.Length && (i + 1) % 2 == 0) s += "-";
            }
            return s;
        }
        #region Original Device ID Getting Code
        //Return a hardware identifier
        private static string identifier(string wmiClass, string wmiProperty, string wmiMustBeTrue)
        {
            using (var mc = new ManagementClass(wmiClass))
            {
                foreach (var mo in mc.GetInstances())
                {
                    //Only get the first one        
                    try
                    {
                        return (bool)mo[wmiMustBeTrue] ? mo[wmiProperty].ToString() : string.Empty;
                    }
                    catch { }
                }
                return string.Empty;
            }
        }
        //Return a hardware identifier
        private static string identifier(string wmiClass, string wmiProperty)
        {
            using (var mc = new ManagementClass(wmiClass))
            {
                foreach (var mo in mc.GetInstances())
                {
                    //Only get the first one
                    try { return mo[wmiProperty].ToString(); }
                    catch { }
                }
                return string.Empty;
            }
        }
        private static string cpuId()
        {
            //Uses first CPU identifier available in order of preference
            //Don't get all identifiers, as very time consuming
            var retVal = identifier("Win32_Processor", "UniqueId");
            if (string.IsNullOrWhiteSpace(retVal)) //If no UniqueID, use ProcessorID
            {
                retVal = identifier("Win32_Processor", "ProcessorId");
                if (string.IsNullOrWhiteSpace(retVal)) //If no ProcessorId, use Name
                {
                    retVal = identifier("Win32_Processor", "Name");
                    if (string.IsNullOrWhiteSpace(retVal)) //If no Name, use Manufacturer
                    {
                        retVal = identifier("Win32_Processor", "Manufacturer");
                    }
                    //Add clock speed for extra security
                    retVal += identifier("Win32_Processor", "MaxClockSpeed");
                }
            }
            return retVal;
        }
        //BIOS Identifier
        private static string biosId()
        {
            return identifier("Win32_BIOS", "Manufacturer")
                + identifier("Win32_BIOS", "SMBIOSBIOSVersion")
                + identifier("Win32_BIOS", "IdentificationCode")
                + identifier("Win32_BIOS", "SerialNumber")
                + identifier("Win32_BIOS", "ReleaseDate")
                + identifier("Win32_BIOS", "Version");
        }
        //Main physical hard drive ID
        private static string diskId()
        {
            return identifier("Win32_DiskDrive", "Model")
                + identifier("Win32_DiskDrive", "Manufacturer")
                + identifier("Win32_DiskDrive", "Signature")
                + identifier("Win32_DiskDrive", "TotalHeads");
        }
        //Motherboard ID
        private static string baseId()
        {
            return identifier("Win32_BaseBoard", "Model")
                + identifier("Win32_BaseBoard", "Manufacturer")
                + identifier("Win32_BaseBoard", "Name")
                + identifier("Win32_BaseBoard", "SerialNumber");
        }
        //Primary video controller ID
        private static string videoId()
        {
            return identifier("Win32_VideoController", "DriverVersion")
                + identifier("Win32_VideoController", "Name");
        }
        //First enabled network card ID
        private static string macId()
        {
            return identifier("Win32_NetworkAdapterConfiguration", "MACAddress", "IPEnabled");
        }
        #endregion
    }
}
