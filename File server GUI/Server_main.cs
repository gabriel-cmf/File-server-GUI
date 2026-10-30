using Avalonia.Collections;
using Avalonia.Threading;
using File_server_GUI.data;
using File_server_GUI.Views;
using File_server_GUI.workers;
using File_test_server.workers;
using File_test_server_gui.data;
using File_test_server_gui.workers;
using LiteDB;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

public enum client_message_type
{
    None = 0,
    wakeup = 1,
    file_request = 2,
    shutdown = 3,
    file_storage_response =4,
    hash_transfer = 5,
    hash_report = 6,
    file_repair=7

}


public class Server_main
{
    public const int listenPort = 3336;
    public UdpClient udp_agent;
    public List<Worker_base> workerList;
    public List<IPEndPoint> client_upd_ports;
    public List<Task> tasks;
    public List<IPEndPoint> receiving_upd_ports;
    public List<IPEndPoint> transfer_aux_ports;
    public List<Transfer_registry> transfer_registries;
    public Dictionary<string, File_registry> available_files;
    public Dictionary<IPAddress, Client_registry> active_clients;
    public Dictionary<IPEndPoint, Worker_base> worker_acess_dict;
    public Stopwatch keep_alive_timer;
    public HashSet<long> server_filter;
    public byte[] keep_alive_packet;
    public HashSet<int> rcv_client_codes;
    public HashSet<IPAddress> ip_filter;
    public HashSet<IPEndPoint> worker_filter;
    public HashSet<IPAddress> pending_filter;
    public HashSet<IPAddress> pending_filter2;
    public ConcurrentQueue<Worker_base> urgent_worker_queue;

    public MainWindow main_window;
    public static string geodb_path = "GeoLite2-Country.mmdb";
    public static string geodb_path2 = "GeoLite2-City.mmdb";
    public static readonly string[] size_suff = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
    public Logger_worker log_work;
    public Queue<IPEndPoint> file_listing_queue;
    public IPEndPoint dummy_iep;
    public bool do_listen_loop;

    public Dictionary<int, Client_query_worker> query_filter;




    public void load_db()
    {
        udp_agent.Client.ReceiveTimeout = 5000;

        using (var db = new LiteDatabase("mydb.db"))
        {
            var files = db.GetCollection<File_registry>("files");

            Debug.WriteLine("Reading from db");
            add_log_msg("Reading from db");
            foreach (File_registry file in files.FindAll())
            {
                available_files[file.file_hash] = file;
            }

            var clients = db.GetCollection<Client_registry>("clients");

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


            var regs = db.GetCollection<Transfer_registry>("transfers");

            if (regs.Count() > 0)
            {
                Debug.WriteLine("Transfer registries found in db");
                add_log_msg("Transfer registries found in db");
                foreach (Transfer_registry tr in regs.FindAll())
                {
                    if (!tr.finished)
                    {
                        //tr.finished = true;
                        transfer_registries.Add(tr);
                        //pending_filter.Add(tr.target.Address);
                        Debug.WriteLine(tr.target.Address);
                    }
                }

            }

            regs.DeleteAll();
        } 
    }


    public Server_main()
    {
        udp_agent = new UdpClient(AddressFamily.InterNetworkV6);
        
        udp_agent.Client.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true);

        udp_agent.Client.DualMode = true;
        udp_agent.ExclusiveAddressUse = false;
        

        //udp_agent.Client.Bind(new IPEndPoint(get_ipv6_adress(), listenPort));
        udp_agent.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, listenPort));
        udp_agent.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, true);
        //udp_agent.JoinMulticastGroup(IPAddress.Parse("FF02::1234"));


        if (OperatingSystem.IsWindows())
        {
            udp_agent.AllowNatTraversal(true);
        }

        dummy_iep = new IPEndPoint(IPAddress.IPv6Any, listenPort);
        workerList = new();
        client_upd_ports = new();
        tasks = new();
        receiving_upd_ports = new();
        transfer_aux_ports = new();

        available_files = new();
        server_filter = new();
        worker_filter = new();
        pending_filter = new();
        pending_filter2 = new();
        worker_acess_dict = new();
        rcv_client_codes = new();
        main_window = null;
        active_clients = new();
        ip_filter = new();
        string day = DateTime.Now.ToString("yyyy-MM-dd");
        log_work = new(this, "logs/" + day + ".txt");
        file_listing_queue = new();

        do_listen_loop=true;

        query_filter = new();


        transfer_registries = new();

        BsonMapper.Global.RegisterType<IPAddress>(

             serialize: (endpoint) => endpoint.ToString(),


           deserialize: (bson) => IPAddress.Parse(bson.AsString) );


        load_db();


        Dictionary<string, object> kpal_dict = new();
        kpal_dict["type"] = 0;

        keep_alive_packet = dict_to_bytes(kpal_dict);

        keep_alive_timer = new();
        keep_alive_timer.Start();

        Debug.WriteLine("Server open at:");
        
        Debug.WriteLine(get_main_ip());
        add_log_msg("Server open at:");
        add_log_msg(get_main_ip().ToString());

        urgent_worker_queue = new();

    }

    public IPAddress get_ipv6_adress()
    {
        var nics = NetworkInterface.GetAllNetworkInterfaces();
        foreach (var adap in nics)
        {
            if (!adap.Supports(NetworkInterfaceComponent.IPv6)) { continue; }

            var props = adap.GetIPProperties();
            var ipv6_props = props.GetIPv6Properties();

            if(ipv6_props == null) { continue; }

            foreach (var uap in props.UnicastAddresses)
            {
                if(uap.Address.AddressFamily == AddressFamily.InterNetworkV6 && !uap.Address.IsIPv6LinkLocal)
                {
                    return uap.Address;
                }
            }


        }

        return IPAddress.IPv6Any;
    }

    public Task create_querier_worker(Dictionary<string, object> dict, string hash,IPEndPoint target, long client_code = 0)
    {

        Client_query_worker uw = new(this);

        
        /*string filename = (string)dict["filename"];
        string ext = (string)dict["file_ext"];
        
        string comp = filename + ext;*/
        uw.file_hash = hash;
        uw.client_code = client_code;
        //uw.objective = worker_tasks.query_clients;
        //uw.data = dict;

        //worker_acess_dict[uw.target] = uw;
        uw.target = target;

        query_filter[uw.GetHashCode()]= uw;

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


    public void add_transfer_registry(Transfer_registry tr)
    {
        transfer_registries.Add(tr);
    }

    public void remove_transfer_registry(Transfer_registry tr)
    {
        transfer_registries.Remove(tr);
    }


    public void add_transfer_registry(string h,string p,IPEndPoint t)
    {
        Transfer_registry tr = new Transfer_registry(h, p, t);
        transfer_registries.Add(tr);
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


    public void save_transfers()
    {
        using (var db = new LiteDatabase("mydb.db"))
        {
            var transfers = db.GetCollection<Transfer_registry>("transfers");
            var transfers_list = transfer_registries.ToList();
            foreach (Transfer_registry tr in transfers_list)
            {
                if(!tr.finished)
                {
                    transfers.Upsert(tr);
                }
                
            }
        }
        Debug.WriteLine("Transfers saved to db");
        add_log_msg("Transfers saved to db");
    }

    public void update_client_registry(IPAddress ipa)
    {
        active_clients[ipa].set_online();
    }

    public void listen_for_clients()
    {
        
        bool client_busy=false;
        bool linux = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
        while (do_listen_loop)
        {


            udp_agent.Client.ReceiveTimeout = 5000;

            if(linux)
            {
                udp_agent.Client.ReceiveTimeout = 20000;
            }

            if (urgent_worker_queue.Count > 0 )
            {
                Worker_base? uw = null;
                urgent_worker_queue.TryDequeue(out uw);

                if(uw != null)
                {
                    switch (uw)
                    {
                        case File_repair_worker frw:
                            {
                                Debug.WriteLine("Urgent worker: File repair worker");
                                create_file_resp_worker(frw);
                                break;
                            }
                    }

                }
            }
            var ips = Dns.GetHostAddresses(Dns.GetHostName())[0];
            IPEndPoint groupEP = new(ips, 1);
            Console.WriteLine("Waiting for broadcast");
            byte[] bytes = Array.Empty<byte>();

            try
            {
                bytes = udp_agent.Receive(ref groupEP);
                //Debug.WriteLine($"Received broadcast from {groupEP}");
            }
            catch(Exception e)
            {
                //Debug.WriteLine("Listener timeout");
                add_log_msg("Listener timeout");
                add_log_msg(e.Message);
                add_log_msg(e.GetType().ToString());
                Thread.Sleep(0);

            }

            if(worker_filter.Contains(groupEP))
            {
                //Debug.WriteLine("Message ment for worker caught");
                log_work.add_msg("Message ment for worker caught");

                bool new_add = worker_acess_dict.TryAdd(groupEP, null);


                if(!new_add && bytes.Length > 0)
                {
                    var wb = worker_acess_dict[groupEP];
                    if (wb.GetType() == typeof(Client_query_worker))
                    {
                        //Debug.WriteLine("Received packet for query worker");
                        var cqw = wb as Client_query_worker;
                        if (cqw != null) 
                        {
                            cqw.remote_addr = groupEP;
                        }
                        else
                        {
                            throw new NullReferenceException("Client query worker was null");
                        }
                        
                    }
                    wb.add_packet(bytes);
                }
                else
                {
                    //Debug.WriteLine("Worker not found for endpoint");
                    log_work.add_msg("Worker not found for endpoint");
                }
                
                bytes = Array.Empty<byte>();
            }


           

            string b_st = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
            Console.WriteLine($"{b_st}");
            Dictionary<string, object>? dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(b_st);
            object? temp=null;
            if (dict != null && bytes.Length > 0)
            {


               

                if (dict.TryGetValue("code",out temp))
                {
                    //Debug.WriteLine("Client query packet received");
                    int? qcode = temp as int?;
                    if (qcode != null)
                    {
                        query_filter[(int)qcode].add_packet(bytes);
                        query_filter[(int)qcode].remote_addr = groupEP;
                        continue;
                    }
                    
                }

                bool exists = false;
                long client_code = (long)dict["client_code"];
                if (bytes.Length > 0 && server_filter.Add(client_code))
                {
                    Debug.WriteLine("New client code added to server filter:");
                    Debug.WriteLine(client_code);
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
                            Debug.WriteLine("Received a wakeup message from client");
                            Debug.WriteLine(client_upd_ports.Last());


                            Dictionary<string, object> wra = new();
                            wra["type"] = 0;


                            //Client has pending transfer, ask if it wants to resume
                            if (pending_filter.Contains(groupEP.Address))
                            {
                                Debug.WriteLine("Client has pending transfer, asking if it wants to resume");
                                wra["type"] = 5;
                                pending_filter.Remove(groupEP.Address);
                                pending_filter2.Add(groupEP.Address);
                                //create_hash_transfer_worker(groupEP);

                            }



                            var j_bytes = dict_to_bytes(wra);

                            udp_agent.Send(j_bytes, groupEP);

                            client_busy = true;




                            break;
                        }

                    case (client_message_type.file_request):
                        {

                            add_log_msg("Received endpoint:");
                            add_log_msg(groupEP.ToString());


                            Debug.WriteLine("Beggining file negotiations with");
                            Debug.WriteLine(client_upd_ports.Last());

                            add_log_msg("Beggining file negotiations with:");
                            add_log_msg(client_upd_ports.Last().ToString());

                            receiving_upd_ports.Add(groupEP);
                            worker_filter.Add(groupEP);

                            create_fileresp_worker(dict, groupEP, client_code);
                            dict["type"] = 0;
                            client_busy = true;

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

                            client_busy = true;

                            break;
                        }

                    case (client_message_type.file_storage_response):
                        {
                            long count = (long)dict["count"];

                            if (count > 0)
                            {
                                Client_registry cr = active_clients[groupEP.Address];
                                File_storage_worker fsw = new(this, cr);
                                fsw.target = groupEP;
                                fsw.expected_packets = count;

                                Task worker_task = Task.Factory.StartNew(() => { fsw.pair_up(); });
                                tasks.Add(worker_task);

                            }

                            break;
                        }

                    case (client_message_type.hash_transfer):
                        {

                            Debug.WriteLine("Sending hashes to client");

                            if (pending_filter2.Contains(groupEP.Address))
                            {
                                pending_filter2.Remove(groupEP.Address);
                                create_hash_transfer_worker(groupEP);

                            }
                            break;
                        }

                    case (client_message_type.hash_report):
                        {


                            Debug.WriteLine("Received hash report from client");
                            create_hash_report_worker(groupEP);
                            break;
                        }

                    case (client_message_type.file_repair):
                        {
                            Debug.WriteLine("Received file repair request from client");
                            string hash = (string)dict["hash"];
                            File_registry fr = available_files[hash];
                            create_file_repair_worker(groupEP, fr, dict);
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
            int it = 0;
            
            while(it < tasks.Count && tasks.Count >0)
            {
                if (tasks[it].IsCompleted)
                {
                    Debug.WriteLine(tasks[it].Status.ToString());
                    //tasks[it].Dispose();
                    tasks.RemoveAt(it);
                }
                else
                {
                    it++;
                }
            }

            Debug.WriteLine($"Active workers: {workerList.Count}");






            if (keep_alive_timer.ElapsedMilliseconds >= 5000)
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

        if(do_listen_loop)
        {
            throw new Exception("Listen loop somehow was exited");
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

        add_log_msg("Creating file response worker");
        if (dict == null) { return; }
        File_transfer_worker uw = new File_transfer_worker(this);
        string rcv_hash = (string)dict["hash"];
        uw.received_hash = rcv_hash;
        uw.rcv_client_code = client_code;
        //uw.file_size = available_files[rcv_hash].filesize;
        worker_filter.Add(tg);


        bool partial = dict.TryGetValue("partial",out _);


        uw.target = tg;
        /*string filename = (string)dict["filename"];
        string ext = (string)dict["file_ext"];

        string comp = filename + ext;
        uw.complete_file_path = comp;*/
        //uw.objective = worker_tasks.transfer_file;
        uw.data = dict;
        workerList.Add(uw);
        worker_acess_dict[tg] = uw;
        Task worker_task = Task.Run(() => { uw.pair_up(); });
        tasks.Add(worker_task);
        add_log_msg("File response worker created");


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

    public string get_endpoint2()
    {
        EndPoint? local_endpoint = udp_agent.Client.LocalEndPoint;
        if (local_endpoint == null) 
        {
            add_log_msg("Get server endpoint failed");
            return "Error"; 
        }
        
        IPEndPoint iep = (IPEndPoint)local_endpoint;
        return iep.ToString();
    }

    public string get_endpoint()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return new IPEndPoint(ip, listenPort).ToString();
            }
        }

        return "";
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

    public long get_client_balance(IPAddress ipa)
    {
        return active_clients[ipa].balance;
    }

    public double get_client_ratio(IPAddress ipa)
    {
        return active_clients[ipa].client_ob.Ratio;
    }

    public bool is_client_banned(IPAddress ipa)
    {
        return active_clients[ipa].banned;
    }

    public void stop_server()
    {
      
        udp_agent.Close();
    }

    public void add_worker_filter_client(IPEndPoint iep,Worker_base wb)
    {
        worker_filter.Add(iep);
        worker_acess_dict[iep] = wb;
        Debug.WriteLine("Added worker filter for client");
        Debug.WriteLine(iep.ToString());
    }

    public void remove_worker_filter_client(IPEndPoint iep)
    {
        if(worker_acess_dict.ContainsKey(iep))
        {
            worker_acess_dict.Remove(iep);
        }

        if(worker_filter.Contains(iep))
        {
            worker_filter.Remove(iep);
        }
    }

    public void remove_client(IPEndPoint iep)
    {
        worker_filter.Remove(iep);
    }

    public void remove_worker(IPEndPoint iep)
    {
        //worker_filter.Remove(iep);
        worker_acess_dict.Remove(iep);
    }

    public void create_hash_transfer_worker(IPEndPoint iep)
    {
        //.MapToIPv4()
        IPAddress iepv4 = iep.Address;
        Debug.WriteLine(iep.ToString());
        //List<Transfer_registry> hashes = transfer_registries.Where(x=> x.target.Address.Equals(iepv4)).ToList();
        List<Transfer_registry> hashes = new();

        for(int i = 0;i<transfer_registries.Count;i++)
        {
            if(transfer_registries[i].target.Address.Equals(iepv4))
            {
                transfer_registries[i].finished = true;
                hashes.Add(transfer_registries[i]);
            }
        }
        Debug.WriteLine("Creating hash transfer worker for client with pending transfers");
        
        Debug.WriteLine(hashes.Count);
        Hash_transfer_worker htw = new(this);
        htw.Hashes = hashes;
        htw.target = iep;
        workerList.Add(htw);

        worker_acess_dict[iep] = htw;

        worker_filter.Add(iep);

        //Dictionary<string,object> packet = new Dictionary<string,object>();
        //packet["type"] = 5;

        //byte[] j_bytes = dict_to_bytes(packet);
        //htw.send_data(j_bytes, iep);

        Task worker_task = Task.Factory.StartNew(() => { htw.pair_up(); });
        tasks.Add(worker_task);
    }

    public void create_hash_report_worker(IPEndPoint iep)
    {
        Hash_report_receive_worker hrrw = new(this, active_clients[iep.Address]);
        hrrw.target = iep;
        workerList.Add(hrrw);
        worker_acess_dict[iep] = hrrw;
        worker_filter.Add(iep);
        Task worker_task = Task.Factory.StartNew(() => { hrrw.pair_up(); });
        tasks.Add(worker_task);
    }

    public void create_file_repair_worker(IPEndPoint iep, File_registry fr, Dictionary<string, object> dict)
    {
        File_repair_worker frw = new(this);
        frw.target = iep;
        //frw.file_hash = hash;

        frw.registry = fr;
        frw.file_path = fr.full_path;
        frw.file_size = fr.filesize;
        frw.packets_per_chunk = (long)dict["packets"];
        workerList.Add(frw);
        worker_acess_dict[iep] = frw;
        worker_filter.Add(iep);
        Task worker_task = Task.Factory.StartNew(() => { frw.pair_up(); });
        tasks.Add(worker_task);
    }

    public void create_file_resp_worker(File_repair_worker frw)
    {
        File_transfer_worker ftw = new(this);
        ftw.target = frw.target;
        ftw.received_hash = frw.registry.file_hash;
        ftw.file_size = frw.file_size;
        ftw.complete_file_path = frw.file_path;


        workerList.Add(ftw);
        worker_acess_dict[frw.target] = ftw;
        worker_filter.Add(frw.target);

        ftw.sv_file_state = server_file_state.wait_confirmation;


        Debug.WriteLine("Creating file response worker for file repair");
        Task worker_task = Task.Run(() => { ftw.begin(); });
        tasks.Add(worker_task);
    }








}

