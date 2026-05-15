using LiteDB;
using MaxMind.GeoIP2;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace File_server_GUI.data
{
    public  class Client_registry
    {
        [BsonId]
        public IPAddress ip_adress { get; set; }

        public long balance { get; set; }

        public long lifetime_upload { get; set; }

        public long lifetime_download { get; set; }

        public DateTime first_contact_date;

        public Tuple<double,double> coords;

        public bool online { get; set; }


        public bool banned { get; set; }

        [BsonIgnore]
        public Client_observable client_ob;

        [BsonIgnore]
        public HashSet<string> know_files;


        public Client_registry() 
        { 
               ip_adress = IPAddress.Any;
               balance = 0;
               client_ob = new(this);
               lifetime_upload = lifetime_download = 0;
               first_contact_date = DateTime.Now;
               coords=new(0,0);
               know_files = new();
            banned = false;

        }

        public Client_registry(IPAddress ipa)
        {
            ip_adress = ipa;
            balance = 0;
            client_ob = new(this);
            client_ob.Ip_adress = ipa.ToString();
            find_country();
            coords = new(0, 0);
            know_files = new();
        }

        public void set_online()
        {
            online = true;
            client_ob.Online = online;
        }

        public void set_offline()
        {
            online = false;
            client_ob.Online = online;
        }

        public IPAddress GetIPAddress()
        {
            return ip_adress;
        }

        public void Set_ip_adress(IPAddress ipa)
        {
            ip_adress = ipa;
            find_country();
        }

        public void Set_banned(bool val)
        {
            banned = val;
            client_ob.Banned = banned;
        }

        public void find_country()
        {
            using (var reader = new DatabaseReader(Server_main.geodb_path))
            {
                try
                {
                    
                    var country = reader.Country(ip_adress);
                   
                    var cf = country.RepresentedCountry.IsoCode;
                    client_ob.Flag = cf ?? string.Empty;
                    
                }
                catch (MaxMind.GeoIP2.Exceptions.AddressNotFoundException)
                {
                    Debug.WriteLine("Could not find country address");
                }
            }

            using (var reader = new DatabaseReader(Server_main.geodb_path2))
            {
                try
                {

                   
                    var city = reader.City(ip_adress);
                    
                    
                    double lat = city.Location.Latitude ?? 0;
                    double lon = city.Location.Longitude ?? 0;
                    coords = new(lat, lon);
                }
                catch (MaxMind.GeoIP2.Exceptions.AddressNotFoundException)
                {
                    Debug.WriteLine("Could not find city address");
                }
            }
        }

        public void update_balance()
        {
            client_ob.Balance = balance;
        }

        public void update_ratio()
        {
            if (lifetime_upload == 0 && lifetime_download == 0)
            {
                client_ob.Ratio = 1;
                return;
            }

            long gdc = Server_main.gdc(Math.Abs(lifetime_upload), Math.Abs(lifetime_download));
            Debug.WriteLine(gdc);
            long a = Math.Abs(lifetime_upload) / gdc;
            long b = Math.Abs(lifetime_download) / gdc;
            long min = Math.Min(a, b);
            long max = Math.Max(a, b);

            if (min == 0) { min = 1; }


            client_ob.Ratio = (double)max / min;

            

            if (lifetime_download > lifetime_upload)
            {
                client_ob.Ratio *= -1;
            }
        }

        public void update_observable()
        {

            
            client_ob.Balance = balance;
            client_ob.Formt_balance = Server_main.bytesize_to_str(balance);
            client_ob.Ip_adress = ip_adress.ToString();
            update_ratio();



        }

        public void decrease_balance(long val)
        {
            balance -= val;
            lifetime_download += val;
            update_observable();
        }

        public void increase_balance(long val)
        {
            balance += val;
            lifetime_upload += val;
            update_observable();
        }
    }
}
