using File_test_server.workers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;


public enum worker_tasks
{
    none = 0,
    transfer_file = 1,
    query_clients = 2,
}




public class Udp_worker : IDisposable
{

    public IPEndPoint target;

    public Server_main owner;
    public UdpClient udp_agent;
    public List<Dictionary<string, object>> packets_to_send;
    IPEndPoint trash;
    public string complete_file_path;
    public long packet_num;
    public worker_tasks objective;
    public bool task_done;
    public server_file_state sv_file_state;
    public Dictionary<string, object> data;
    public static int max_udp_size = 65535;
    public List<byte[]> packets_to_send2;
    public int max_transfer;
    public long transfer_sum;


    public Udp_worker(Server_main sm)
    {
        var ips = Dns.GetHostAddresses(Dns.GetHostName())[0];
        target = new IPEndPoint(ips, 1);
        udp_agent = new UdpClient();
        udp_agent.Client.SendBufferSize = max_udp_size;
        complete_file_path = "";
        objective = 0;
        task_done = false;
        sv_file_state = server_file_state.idle;
        packets_to_send = new();
        owner = sm;
        Stopwatch stopwatch = new Stopwatch();
        packets_to_send2 = new();
        max_transfer = 1024 * 1024;
        transfer_sum = 0;


    }



    public int get_max_packet()
    {
        int max_size = 1500;
        int val = max_size;

        if (target.AddressFamily == AddressFamily.InterNetwork)
        {
            val -= 28;
        }

        if (target.AddressFamily == AddressFamily.InterNetworkV6)
        {
            val -= 40;
        }


        return val;
    }

    public void pair_up()
    {
        bool paired = false;
        string ack = "ACK";
        var json_dict = JsonConvert.SerializeObject(ack);
        var j_bytes = Encoding.ASCII.GetBytes(json_dict);

        while (!paired)
        {

            try
            {
                udp_agent.Send(j_bytes, target);
                Console.WriteLine("ACK sent to client");

                var resp = udp_agent.Receive(ref trash);


                if (resp != null && resp.Length > 0)
                {
                    paired = true;
                    break;
                }
            }
            catch (Exception e)
            {


            }
        }

        Console.WriteLine("Pair-up with client sucessful !");
        begin();



    }

    public long packets_needed(string filepath)
    {
        Dictionary<string, object> ex_packet = new();
        ex_packet["order"] = 1;
        ex_packet["sum"] = 1;
        ex_packet["bytes"] = new byte[4];

        byte[] file_bytes = System.IO.File.ReadAllBytes(filepath);


        var json_dict = JsonConvert.SerializeObject(ex_packet);

        var j_bytes = Encoding.ASCII.GetBytes(json_dict);

        long header_size = j_bytes.Length;
        long file_size = file_bytes.Length;


        long max_data_size = max_udp_size - header_size;

        long b4_size = file_size;
        b4_size += (b4_size * 34) / 100;
        packet_num = (b4_size / max_data_size) + 1;

        return packet_num;
    }


    public long packets_needed2(string filepath)
    {
        long ex1 = long.MaxValue;
        //int shave = 28;
        byte[] long_size = BitConverter.GetBytes(ex1);
        Console.WriteLine("long size");
        Console.WriteLine(long_size.Length);
        long data_space = get_max_packet() - (long_size.Length * 2);


        byte[] file_bytes = System.IO.File.ReadAllBytes(filepath);

        packet_num = (file_bytes.Length / data_space) + 1;

        return packet_num;
    }


    public void query_clients_for_file()
    {
        Console.WriteLine(this.GetHashCode());
        Console.WriteLine("Begin client query");
        Dictionary<string, object> query = new();
        query["type"] = 1;
        query["hash"] = complete_file_path;


        var json_dict = JsonConvert.SerializeObject(query);

        var j_bytes = Encoding.ASCII.GetBytes(json_dict);


        foreach (IPEndPoint client in owner.client_upd_ports)
        {
            try
            {

                if (owner.receiving_upd_ports.Contains(client))
                {
                    continue;
                }
                udp_agent.Send(j_bytes, client);
                byte[] r_bytes = udp_agent.Receive(ref trash);
                if (r_bytes != null && r_bytes.Length > 0)
                {
                    var reps = Server_main.bytes_to_dict(r_bytes);

                    bool found = (bool)reps["found"];

                    if (found)
                    {
                        IPEndPoint new_iep = new(trash.Address, trash.Port);
                        owner.transfer_aux_ports.Add(new_iep);
                    }
                }
            }

            catch
            {

            }
        }
        Console.WriteLine("Queried clients for file!");
        task_done = true;

    }


    public void answer_file_query(Dictionary<string, object>? dict)
    {
        /*Console.WriteLine("Waiting for broadcast");
        byte[] bytes = listener.Receive(ref groupEP);

        Console.WriteLine($"Received broadcast from {groupEP} :");
        string query_st = Encoding.ASCII.GetString(bytes, 0, bytes.Length);

        Console.WriteLine($" {query_st}");

        Dictionary<string, string>? dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(query_st);*/

        if (dict == null) { return; }


        Console.WriteLine("Answering file query");

        Dictionary<string, object> data = new();
        data["type"] = 2;

        if (File.Exists(complete_file_path))
        {
            data["found"] = true;
            data["filesize"] = System.IO.File.ReadAllBytes(complete_file_path).Length;
            data["packets"] = packets_needed2(complete_file_path);
        }
        else
        {
            data["found"] = false;
            data["filesize"] = 0;
            data["packets"] = 0;
        }


        var json_dict = JsonConvert.SerializeObject(data);

        var j_bytes = Encoding.ASCII.GetBytes(json_dict);

        //IPEndPoint return_ip = new IPEndPoint(Dns.GetHostAddresses(Dns.GetHostName())[0], listenPort - 1);

        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        //Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        s.SendTo(j_bytes, target);
        s.Close();

        if ((bool)data["found"] == true)
        {
            sv_file_state = server_file_state.file_found;
        }


    }

    public long bytesum(byte[] bytes)
    {
        long sum = 0;

        for (int i = 0; i < bytes.Length; i++)
        {
            sum += bytes[i];

        }


        return sum;

    }

    public void split_packets(string filepath)
    {
        Dictionary<string, object> ex_packet = new();
        ex_packet["order"] = 1;
        ex_packet["sum"] = 1;
        ex_packet["bytes"] = new byte[4];

        byte[] file_bytes = System.IO.File.ReadAllBytes(filepath);


        var json_dict = JsonConvert.SerializeObject(ex_packet);

        var j_bytes = Encoding.ASCII.GetBytes(json_dict);
        //long header_size = bytesum(j_bytes);
        //long file_size = bytesum(file_bytes);
        long header_size = j_bytes.Length;
        long file_size = file_bytes.Length;
        Console.WriteLine("Header size:");
        Console.WriteLine(header_size);

        Console.WriteLine("File data length");
        Console.WriteLine(file_bytes.Length);

        long max_data_size = max_udp_size - header_size;

        long b4_size = file_size;
        b4_size += (b4_size * 34) / 100;
        long packet_num = (b4_size / max_data_size) + 1;


        Console.WriteLine("Number of packets:");
        Console.WriteLine(packet_num);

        Console.WriteLine("Max data size");
        Console.WriteLine(max_data_size);

        int i = 0;
        for (int a = 0; a < packet_num; a++)
        {
            Dictionary<string, object> packet = new();
            packet["order"] = a;
            packet["sum"] = 1;
            packet["bytes"] = 0;

            long max_data = max_data_size;
            List<byte> file_data = new();

            //Console.WriteLine("Byte values");
            //while (i < file_bytes.Length && max_data > 0 && max_data >= file_bytes[i])
            while (i < file_bytes.Length && file_data.Count + ((file_data.Count * 34) / 100) < max_data)
            {
                //Console.WriteLine(file_bytes[i]);
                file_data.Add(file_bytes[i]);
                //max_data -= file_bytes[i];
                //max_data -= 1;
                i++;

            }

            byte[] file_array = file_data.ToArray();
            //string sb = JsonConvert.SerializeObject(file_array);
            packet["bytes"] = file_array;
            /*if(a==0)
            {
                Console.WriteLine(sb.Length % 4);
            }*/
            packet["sum"] = file_array.Length;
            //packet["bytes"] = file_data.ToArray();
            packets_to_send.Add(packet);


        }

        var pjson_dict = JsonConvert.SerializeObject(packets_to_send[0]);
        var p_bytes = Encoding.ASCII.GetBytes(pjson_dict);

        long p_size = p_bytes.Length;
        file_bytes = new byte[0];


        //Console.WriteLine(pjson_dict);


        Console.WriteLine("Packet list size:");
        Console.WriteLine(packets_to_send.Count);


        Console.WriteLine("Packet size:");
        Console.WriteLine(p_size);
        //Console.WriteLine(packets_to_send[0]["bytes"]);




    }

    public void split_packets2(string filepath)
    {
        long ex1 = long.MaxValue;
        int shave = 28;
        byte[] long_size = BitConverter.GetBytes(ex1);
        Console.WriteLine("long size");
        Console.WriteLine(long_size.Length);
        int mp = get_max_packet();
        long data_space = mp - (long_size.Length * 2);


        //byte[] file_bytes = System.IO.File.ReadAllBytes(filepath);
        byte[] buffer = new byte[data_space];


        int remaining_bytes = 0;
        using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
        {

            long a = 0;
            int file_pos = 0;

            while ((remaining_bytes = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                int ps = Math.Min(mp, remaining_bytes + 16);
                byte[] packet = new byte[ps];
                byte[] order_bytes = BitConverter.GetBytes((long)file_pos);
                byte[] sum_bytes = BitConverter.GetBytes(a);
                int i = 0;
                for (int b = 0; b < order_bytes.Length; b++)
                {
                    packet[b] = order_bytes[b];
                    i++;

                }

                //Console.WriteLine("i:" + $"{i}");

                for (int b = 0; b < sum_bytes.Length; b++)
                {
                    packet[i] = sum_bytes[b];
                    i++;

                }

                //Console.WriteLine("i:" + $"{i}");

                int bi = 0;
                while (i < packet.Length && bi < buffer.Length)
                {
                    packet[i] = buffer[bi];
                    i++;
                    bi++;
                }

                a++;
                file_pos += remaining_bytes;

                packets_to_send2.Add(packet);
                //remaining_bytes -= (packet.Length - 16);
            }
        }



        //for(int a=0;a<packet_num; a++)

        /*
          packet_num = (file_bytes.Length / data_space) + 1;

            int file_pos = 0;

            int remaining_bytes = file_bytes.Length;
            int offset = 0;
         */

        /*for (int a = 0; a < packet_num; a++)
        {
            //int ps = Math.Min(max_udp_size - shave,remaining_bytes+16);
            int ps = Math.Min(mp, remaining_bytes + 16);
            byte[] packet = new byte[ps];
            byte[] order_bytes = BitConverter.GetBytes((long)file_pos);
            byte[] sum_bytes = BitConverter.GetBytes((long)a);
            int i =0;
            for (int b=0;b<order_bytes.Length;b++)
            {
                packet[b] =order_bytes[b];
                i++;

            }

            for (int b = 0; b < sum_bytes.Length; b++)
            {
                packet[b+i] = sum_bytes[b];
                i++;

            }

            //Console.WriteLine("i:" + $"{i}");
           

            while( i< packet.Length && file_pos < file_bytes.Length)
            {
                packet[i] = file_bytes[file_pos];
                i++;
                file_pos++;
            }

            packets_to_send2.Add(packet);
            remaining_bytes -= (packet.Length-16);
            
        }*/



        Console.WriteLine("Remaining_bytes");
        Console.WriteLine(remaining_bytes);

        Console.WriteLine("Total packets");
        Console.WriteLine(packets_to_send2.Count);

    }


    public void prepare_transfer_data()
    {
        Console.WriteLine("Creating client querier");
        var qr = owner.create_querier_worker(data, "");
        byte[] bytes = udp_agent.Receive(ref trash);



        split_packets2(complete_file_path);

        //Thread.Sleep(200);
        qr.Wait();
        sv_file_state = server_file_state.transfer_file;
    }

    public void file_transfer_state_machine()
    {
        bool finish = false;
        while (!finish)
        {


            switch (sv_file_state)
            {

                case (server_file_state.idle):
                    {
                        answer_file_query(data);
                        break;
                    }

                case (server_file_state.file_found):
                    {
                        prepare_transfer_data();
                        break;
                    }

                case (server_file_state.transfer_file):
                    {
                        bool transf = owner.will_server_do_transfer();
                        if (transf == true)
                        {
                            send_file_data(0, packet_num);
                        }
                        else
                        {
                            Console.WriteLine("Transfer defered to clients");
                            ask_clients_to_transer();
                        }


                        //sv_file_state = server_file_state.done;
                        break;
                    }



                case (server_file_state.wait_confirmation):
                    {
                        wait_confirm();
                        sv_file_state = server_file_state.done;
                        break;
                    }

                case (server_file_state.done):
                    {
                        finish = true;
                        //sv_file_state = server_file_state.idle;
                        break;
                    }
            }

            Thread.Sleep(0);
        }

        task_done = true;
    }

    public void send_file_data(int start, long end)
    {
        //IPEndPoint return_ip = new IPEndPoint(Dns.GetHostAddresses(Dns.GetHostName())[0], listenPort - 1);
        //Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        /*foreach (Dictionary<string, object> packet in packets_to_send)
        {

            var json_dict = JsonConvert.SerializeObject(packet);
            var j_bytes = Encoding.ASCII.GetBytes(json_dict);
            udp_agent.Send(j_bytes, target);
            Thread.Sleep(100);
        }*/

        for (int a = start; a < end; a++)
        {
            //Dictionary<string, object> packet = packets_to_send[a];
            //var json_dict = JsonConvert.SerializeObject(packet);
            //var j_bytes = Encoding.ASCII.GetBytes(json_dict);
            /*if(a % 2 !=0  && end == packet_num)
            {
                continue;
            }*/


            byte[] j_bytes = packets_to_send2[a];



            //Console.WriteLine(j_bytes.Length);
            Console.WriteLine(a + 1);
            udp_agent.Send(j_bytes, target);
            transfer_sum += j_bytes.Length;

            if (transfer_sum >= max_transfer)
            {
                transfer_sum = 0;
                Thread.Sleep(2);
            }


        }


        sv_file_state = server_file_state.wait_confirmation;
        //packets_to_send.Clear();
        //client_upd_ports.Clear();
        //s.Close();
    }

    public void wait_confirm()
    {
        Console.WriteLine("Waiting for client confirmation");
        bool finish = false;
        while (!finish)
        {
            byte[] bytes = udp_agent.Receive(ref trash);
            Dictionary<string, object>? dict = Server_main.bytes_to_dict(bytes);
            if (dict != null)
            {
                bool state = (bool)dict["done"];
                if (state) { finish = true; break; }

                long pos = (long)dict["pos"];

                send_file_data((int)pos, pos + 1);


            }

            Thread.Sleep(0);

        }

        Console.WriteLine("Client has confirmed sucessful file transfer !");

        sv_file_state = server_file_state.idle;
        packets_to_send.Clear();
    }

    public void ask_clients_to_transer()
    {

        int cnt = owner.transfer_aux_ports.Count;
        long packet_mod = packet_num % cnt;
        long packets_per_client = (packet_num + packet_mod) / cnt;
        for (int a = 0; a < cnt; a++)
        {
            long max = packets_per_client * (a + 1);
            max = Math.Min(max, packet_num);
            IPEndPoint aux = owner.transfer_aux_ports[a];

            Dictionary<string, object> resp = new();
            resp["target"] = target.ToString();
            resp["begin"] = a * packets_per_client;
            resp["end"] = max;

            //Console.WriteLine("Begin index" + $"{resp["begin"]}");
            //Console.WriteLine("end index" +  $"{resp["end"]}");

            byte[] conv_byte = Server_main.dict_to_bytes(resp);

            udp_agent.Send(conv_byte, aux);

        }
    }


    public void begin()
    {
        switch (objective)
        {
            case (worker_tasks.none):
                {
                    task_done = true;
                    break;
                }

            case (worker_tasks.transfer_file):
                {
                    file_transfer_state_machine();
                    break;
                }
        }
    }

    public void Dispose()
    {
        udp_agent.Dispose();

    }


}
