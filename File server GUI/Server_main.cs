using Avalonia.Threading;
using File_server_GUI.data;
using File_server_GUI.Views;
using File_test_server.workers;
using File_test_server_gui.data;
using File_test_server_gui.workers;
using LiteDB;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Avalonia.Collections;
using File_server_GUI.workers;

public enum client_message_type
{
    None = 0,
    wakeup = 1,
    file_request = 2,
    shutdown = 3,
    file_storage_response =4,

}


public class Server_main
{
    public const int listenPort = 3336;
    UdpClient udp_agent;
    public List<Udp_worker> workerList;
    public List<IPEndPoint> client_upd_ports;
    public List<Task> tasks;
    public List<IPEndPoint> receiving_upd_ports;
    public List<IPEndPoint> transfer_aux_ports;
    public Dictionary<string, File_registry> available_files;
    public Dictionary<IPAddress, Client_registry> active_clients;
    public Stopwatch keep_alive_timer;
    public HashSet<long> server_filter;
    public byte[] keep_alive_packet;
    public HashSet<int> rcv_client_codes;
    public HashSet<IPAddress> ip_filter;
    public MainWindow main_window;
    public static string geodb_path = "GeoLite2-Country.mmdb";
    public static readonly string[] size_suff = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
    public Logger_worker log_work;
    public Queue<IPEndPoint> file_listing_queue;


    public Server_main()
    {
        udp_agent = new UdpClient(AddressFamily.InterNetworkV6);
        udp_agent.Client.DualMode = true;
        udp_agent.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, listenPort));
        workerList = new();
        client_upd_ports = new();
        tasks = new();
        receiving_upd_ports = new();
        transfer_aux_ports = new();

        available_files = new();
        server_filter = new();
        rcv_client_codes = new();
        main_window = null;
        active_clients = new();
        ip_filter = new();
        string day = DateTime.Now.ToString("yyyy-MM-dd");
        log_work = new(this, "logs\\" + day + ".txt");
        file_listing_queue = new();

        BsonMapper.Global.RegisterType<IPAddress>(

             serialize: (endpoint) => endpoint.ToString(),


           deserialize: (bson) => IPAddress.Parse(bson.AsString)
    );

        udp_agent.Client.ReceiveTimeout = 5000;

        using (var db = new LiteDatabase("mydb.db"))
        {
            var files = db.GetCollection<File_registry>("files");

            if (files.Count() <= 0)
            {
                File_registry f1 = new();
                var b1 = f1.try_register_file("C:\\Users\\Campo\\source\\repos\\File_test_server_gui\\File_test_server_gui\\bin\\Debug\\net8.0\\macuco.jpg");
                if (b1)
                {
                    available_files[f1.file_hash] = f1;
                    files.Insert(f1);
                }


                File_registry f2 = new();
                var b2 = f2.try_register_file("C:\\Users\\Campo\\source\\repos\\File_test_server_gui\\File_test_server_gui\\bin\\Debug\\net8.0\\RelatorioFinal-INF491.pdf");
                if (b2)
                {
                    available_files[f2.file_hash] = f2;
                    files.Insert(f2);
                }



            }
            else
            {
                Debug.WriteLine("Reading from db");
                add_log_msg("Reading from db");
                foreach (File_registry file in files.FindAll())
                {
                    available_files[file.file_hash] = file;
                }
            }

            var clients = db.GetCollection<Client_registry>("clients");
            clients.DeleteAll();
            if (clients.Count() > 0)
            {
                Debug.WriteLine("Clients found in db");
                add_log_msg("Clients found in db");
                foreach (Client_registry cr in clients.FindAll())
                {
                    Debug.WriteLine(cr.balance);
                   
                    cr.online = false;
                    ip_filter.Add(cr.ip_adress);
                    active_clients[cr.ip_adress] = cr;
                    active_clients[cr.ip_adress].update_balance();

                }
            }

        }



        Dictionary<string, object> kpal_dict = new();
        kpal_dict["type"] = 0;

        keep_alive_packet = dict_to_bytes(kpal_dict);

        keep_alive_timer = new();
        keep_alive_timer.Start();

        Debug.WriteLine("Server open at:");
        
        Debug.WriteLine(get_main_ip());
        add_log_msg("Server open at:");
        add_log_msg(get_main_ip().ToString());

    }

    public Task create_querier_worker(Dictionary<string, object> dict, string full_path, long client_code = 0)
    {

        Client_query_worker uw = new(this);

        uw.target = client_upd_ports[0];
        /*string filename = (string)dict["filename"];
        string ext = (string)dict["file_ext"];
        
        string comp = filename + ext;*/
        uw.file_hash = full_path;
        uw.client_code = client_code;
        //uw.objective = worker_tasks.query_clients;
        //uw.data = dict;

        Task worker_task = Task.Factory.StartNew(() => { uw.query_clients_for_file(); });
        tasks.Add(worker_task);





        return worker_task;
    }



    public void init_client_observables()
    {
        foreach (Client_registry cr in active_clients.Values)
        {
            cr.update_observable();
            Dispatcher.UIThread.Post(() => main_window.add_client_observable(cr.client_ob));
        }
    }
    public bool will_server_do_transfer()
    {
        return transfer_aux_ports.Count <= 0;
    }

    public static Dictionary<string, object>? bytes_to_dict(byte[] bytes)
    {
        string b_st = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
        Dictionary<string, object>? dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(b_st);
        return dict;
    }

    public static string bytesize_to_str(long sz)
    {
        if (sz == 0)
        {
            return "0 B";
        }


        if (sz < 0)
        {
            return "-" + bytesize_to_str(-sz);
        }

        int mag = (int)Math.Log(sz, 1024);
        mag = Math.Min(mag, size_suff.Length - 1);

        double dec_size = sz / Math.Pow(1024, mag);

        Span<char> buffer = stackalloc char[64];

        int chars_written = 0;


        bool success = dec_size.TryFormat(buffer, out int number_length, "0.##");

        if (success)
        {
            chars_written += number_length;
            buffer[chars_written++] = ' ';
            ReadOnlySpan<char> suff_span = size_suff[mag].AsSpan();
            suff_span.CopyTo(buffer.Slice(chars_written));
            chars_written += suff_span.Length;
        }

        return new string(buffer.Slice(0, chars_written));
    }

    public static byte[] dict_to_bytes(Dictionary<string, object> dict)
    {
        var json_dict = JsonConvert.SerializeObject(dict);
        var j_bytes = Encoding.ASCII.GetBytes(json_dict);

        return j_bytes;
    }

    public static string generate_incremental_hash(string full_path)
    {

        string file_hash = string.Empty;


        using (FileStream fs = new FileStream(full_path, FileMode.Open, FileAccess.Read))
        {
            byte[] h_bytes = SHA256.HashData(fs);

            file_hash = Convert.ToHexString(h_bytes);
        }

        return file_hash;
    }

    public static double harvesine(double lat1,double lon1,double lat2,double lon2)
    {
        double earth_r = 6371;
        double d_lat = (lat2 - lat1) * (Math.PI/180.0d);
        double d_lon = (lon2 - lon1) * (Math.PI / 180.0d);

        double rad_lat1 = lat1 * (Math.PI / 180.0d);
        double rad_lat2 = lat2 * (Math.PI / 180.0d);

        double a = Math.Sin(d_lat / 2) * Math.Sin(d_lat / 2) +
               Math.Cos(rad_lat1) * Math.Cos(rad_lat2) *
               Math.Sin(d_lon / 2) * Math.Sin(d_lon / 2);


        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earth_r * c;


    }

    public void save_clients()
    {
        using (var db = new LiteDatabase("mydb.db"))
        {
            var clients = db.GetCollection<Client_registry>("clients");
            var atv_clients = active_clients.Values.ToList();
            Debug.WriteLine(atv_clients.Count);
            foreach (var cr in atv_clients)
            {
                Debug.WriteLine(cr.balance);
                clients.Upsert(cr);
            }
        }

        Debug.WriteLine("Clients saved to db");
        add_log_msg("Clients saved to db");
    }

    public void save_files()
    {
        using (var db = new LiteDatabase("mydb.db"))
        {
            var files = db.GetCollection<File_registry>("files");
            var files_list = available_files.Values.ToList();
            foreach (File_registry fr in files_list)
            {
                files.Upsert(fr);
            }

        }

        Debug.WriteLine("Clients saved to db");
        add_log_msg("Clients saved to db");
    }

    public void update_client_registry(IPAddress ipa)
    {
        active_clients[ipa].set_online();
    }

    public void listen_for_clients()
    {
        
        bool client_busy=false;
        while (true)
        {

            var ips = Dns.GetHostAddresses(Dns.GetHostName())[0];
            IPEndPoint groupEP = new(ips, 1);
            Console.WriteLine("Waiting for broadcast");
            byte[] bytes = Array.Empty<byte>();

            try
            {
                bytes = udp_agent.Receive(ref groupEP);
            }
            catch
            {
                Debug.WriteLine("Listener timeout");
                add_log_msg("Listener timeout");
                Thread.Sleep(0);

            }




            string b_st = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
            Console.WriteLine($"{b_st}");
            Dictionary<string, object>? dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(b_st);

            if (dict != null && bytes.Length > 0)
            {

                bool exists = false;
                long client_code = (long)dict["client_code"];
                if (bytes.Length > 0 && server_filter.Add(client_code))
                {
                    client_upd_ports.Add(groupEP);
                }
                else
                {
                    exists = true;
                }

                if (bytes.Length > 0 && ip_filter.Add(groupEP.Address))
                {
                    Client_registry cr = new(groupEP.Address);
                    cr.set_online();
                    active_clients[cr.ip_adress] = cr;

                    Dispatcher.UIThread.Post(() => main_window.add_client_observable(cr.client_ob));
                    Debug.WriteLine("Requesting file listing");
                    //send_file_listing_request(groupEP);
                    file_listing_queue.Enqueue(groupEP);

                }
                else
                {
                    update_client_registry(groupEP.Address);
                }


                long ti = (long)dict["type"];
                client_message_type type = (client_message_type)ti;

                switch (type)
                {
                    case (client_message_type.wakeup):
                        {
                            Console.WriteLine("Received a wakeup message from client");
                            Console.WriteLine(client_upd_ports.Last());





                            break;
                        }

                    case (client_message_type.file_request):
                        {
                            Console.WriteLine("Beggining file negotiations with");
                            Console.WriteLine(client_upd_ports.Last());

                            receiving_upd_ports.Add(groupEP);

                            create_fileresp_worker(dict, groupEP, client_code);
                            dict["type"] = 0;
                            client_busy=true;

                            break;
                        }

                    case (client_message_type.shutdown):
                        {
                            bool found = false;
                            Client_registry cr;
                            found = active_clients.TryGetValue(groupEP.Address, out cr);

                            if (found)
                            {
                                active_clients[groupEP.Address].set_offline();
                            }

                            break;
                        }

                    case (client_message_type.file_storage_response):
                        {
                            long count = (long)dict["count"];

                            if(count  > 0)
                            {
                                Client_registry cr = active_clients[groupEP.Address];
                                File_storage_worker fsw= new(this,cr);
                                fsw.target = groupEP;
                                fsw.expected_packets = count;

                                Task worker_task = Task.Factory.StartNew(() => { fsw.pair_up(); });
                                tasks.Add(worker_task);

                            }

                            break;
                        }
                }




            }

            if(file_listing_queue.Count > 0 && !client_busy)
            {

                while(file_listing_queue.Count > 0)
                {
                    IPEndPoint qiep = file_listing_queue.Dequeue();
                    send_file_listing_request(qiep);
                }
                

                
            }

            client_busy = false;

            foreach (Task t in tasks)
            {
                if (t.IsCompleted)
                {
                    t.Dispose();
                    //tasks.Remove(t);
                }
            }


            if (keep_alive_timer.ElapsedMilliseconds >= 30000)
            {
                Console.WriteLine("Sending keep-alives");
                foreach (IPEndPoint client in client_upd_ports)
                {
                    udp_agent.Send(keep_alive_packet, client);
                }
                keep_alive_timer.Restart();
            }



            Thread.Sleep(0);



        }




    }

    public void init_file_registry_info()
    {
        foreach (File_registry fr in available_files.Values)
        {
            fr.update_fr_info();
            main_window.add_fr_info(fr.fr_info);
        }
    }

    public File_registry? try_get_registry(string hash)
    {
        try
        {
            return available_files[hash];
        }
        catch
        {
            return null;
        }
    }


    public void create_fileresp_worker(Dictionary<string, object>? dict, IPEndPoint tg, long client_code = 0)
    {
        if (dict == null) { return; }
        File_transfer_worker uw = new File_transfer_worker(this);
        string rcv_hash = (string)dict["hash"];
        uw.received_hash = rcv_hash;
        uw.rcv_client_code = client_code;
        uw.file_size = available_files[rcv_hash].filesize;



        uw.target = tg;
        /*string filename = (string)dict["filename"];
        string ext = (string)dict["file_ext"];

        string comp = filename + ext;
        uw.complete_file_path = comp;*/
        //uw.objective = worker_tasks.transfer_file;
        uw.data = dict;

        Task worker_task = Task.Factory.StartNew(() => { uw.pair_up(); });
        tasks.Add(worker_task);


    }

    public void subtract_balance(IPAddress ipa, long size)
    {
        //active_clients[ipa].balance -= size;
        //active_clients[ipa].update_balance();
        active_clients[ipa].decrease_balance(size);
        
        // active_clients[ipa].client_ob.Balance -= active_clients[ipa].balance;
    }

    public void add_balance(IPAddress ipa, long size)
    {
        //active_clients[ipa].balance -= size;
        //active_clients[ipa].update_balance();
        active_clients[ipa].increase_balance(size);

        // active_clients[ipa].client_ob.Balance -= active_clients[ipa].balance;
    }




    public void add_file_registry(File_registry fr)
    {
        available_files[fr.file_hash] = fr;

    }

    public string get_main_ip()
    {
        EndPoint? local_endpoint = udp_agent.Client.LocalEndPoint;
        if (local_endpoint == null) { return "Error"; }
        IPEndPoint iep = (IPEndPoint)local_endpoint;
        return iep.Address.ToString();
    }

    public string get_main_port()
    {

        return listenPort.ToString();
    }

    public static long gdc(long a,long b)
    {
        while (b != 0)
        {
            long temp = b;
            b = a%b;
            a = temp;
            
        }
        return a;
    }

    public Client_registry get_c_registry(IPEndPoint iep)
    {
         
        Client_registry cr = active_clients[iep.Address];

        return cr;
    }

    public void send_file_listing_request(IPEndPoint target)
    {
        Dictionary<string,object> data = new Dictionary<string,object>();
        data["type"] = 3;
        byte[] j_bytes = dict_to_bytes(data);

        udp_agent.Send(j_bytes,target);

    }

    public void add_log_msg(string msg)
    {
        log_work.add_msg(msg);
    }




}
