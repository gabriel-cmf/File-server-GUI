using Avalonia.Controls.Shapes;
using File_server_GUI.data;
using File_test_server_gui.data;
using File_test_server_gui.workers;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace File_test_server.workers
{

    public enum server_file_state
    {
        idle = 0,
        file_found = 1,
        transfer_file = 2,
        wait_confirmation = 3,
        done = 4,

    }

    public struct s_packet
    {
        public byte[] data;
        public int size;
    }


    public class File_transfer_worker : Worker_base
    {
        public static int max_udp_size = 65535;
        public long packet_num;
        public const int header_size = 16;
        public server_file_state sv_file_state;
        public int max_transfer;
        public long rcv_client_code;
        public long packet_costs;
        public bool send_started;
        public Task send_task;
        public string complete_file_path;
        public string received_hash;
        public long file_size;
        public List<long> client_sent_packets;
        public Dictionary<string, object> data;
        public BlockingCollection<s_packet> packets_to_send;

        public File_transfer_worker(Server_main ow) : base(ow)
        {
            max_transfer = 1024 * 1024;
            sv_file_state = server_file_state.idle;
            send_started = false;
            packets_to_send = new BlockingCollection<s_packet>(1000);
            received_hash = "";
            rcv_client_code = 0;
            packet_costs = 0;
            file_size = 0;
            client_sent_packets = new();
            udp_agent.AllowNatTraversal(true);
        }

        public int get_max_packet()
        {
            int max_size = 1400;
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


        public int get_packet_filesize()
        {
            int mp = get_max_packet();
            return mp-header_size;
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

        public long packets_needed(string filepath)
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

        public void answer_file_query(Dictionary<string, object>? dict)
        {
            /*Console.WriteLine("Waiting for broadcast");
            byte[] bytes = listener.Receive(ref groupEP);

            Console.WriteLine($"Received broadcast from {groupEP} :");
            string query_st = Encoding.ASCII.GetString(bytes, 0, bytes.Length);

            Console.WriteLine($" {query_st}");

            Dictionary<string, string>? dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(query_st);*/

            if (dict == null) { return; }


            File_registry? fr = owner.try_get_registry(received_hash);


            Debug.WriteLine("Answering file query");

            Dictionary<string, object> data = new();
            data["type"] = 2;


            if (fr != null && fr.is_valid())
            {
                data["found"] = true;
                data["filesize"] = fr.filesize;
                complete_file_path = fr.full_path;
                data["packets"] = packets_needed(complete_file_path);
                data["file_name"] = fr.file_name;
                data["file_ext"] = fr.type;
                /*string hash_st = "";
                using (FileStream fs = new FileStream(complete_file_path, FileMode.Open, FileAccess.Read))

                {
                    byte[] h_bytes = SHA256.HashData(fs);

                    hash_st = Convert.ToHexString(h_bytes);
                }
                data["hash"] = hash_st;*/
            }
            else
            {
                data["found"] = false;
                data["filesize"] = 0;
                data["packets"] = 0;
                Debug.WriteLine("File was not found or is invalid");

            }


            var json_dict = JsonConvert.SerializeObject(data);

            var j_bytes = Encoding.ASCII.GetBytes(json_dict);

            //IPEndPoint return_ip = new IPEndPoint(Dns.GetHostAddresses(Dns.GetHostName())[0], listenPort - 1);

            Socket s = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            //Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Debug.WriteLine(target);
            s.DualMode = true;
            s.SendTo(j_bytes, target);
            s.Close();

            if ((bool)data["found"] == true)
            {
                sv_file_state = server_file_state.file_found;
            }
            else
            {
                sv_file_state = server_file_state.done;
            }


        }

        public void prepare_transfer_data()
        {
            Debug.WriteLine("Creating client querier");
            var qr = owner.create_querier_worker(data, received_hash, rcv_client_code);
            Debug.WriteLine("Awaiting client confirmation");
            byte[] bytes = udp_agent.Receive(ref trash);



            //split_packets2(complete_file_path);

            //Thread.Sleep(200);
            qr.Wait();
            Debug.WriteLine("Beggining file transfer");
            Debug.WriteLine(target);
            sv_file_state = server_file_state.transfer_file;
        }


        public void send_packets()
        {

            Debug.WriteLine("Sending packets !");
            foreach (s_packet packet in packets_to_send.GetConsumingEnumerable())
            {
                //Debug.WriteLine("Sending packet!");
                Debug.WriteLine(target.ToString());
                udp_agent.Send(packet.data, packet.size, target);
                ArrayPool<byte>.Shared.Return(packet.data);
                //Thread.Sleep(1);
            }

            Debug.WriteLine("All packets have been sent !");


        }

        public bool start_send_packets()
        {
            if (send_started) { return false; }
            send_started = true;
            send_task = Task.Factory.StartNew(() => { send_packets(); });
            return true;
        }

        public void make_single_packet(string filepath, long i)
        {
            int mp = get_max_packet();


            long data_space = mp - (8 * 2);
            long offset = data_space * (i);
            int remaining_bytes = 0;
            byte[] buffer = new byte[data_space];
            Console.WriteLine("Single packet offset:");
            Console.WriteLine(offset);

            using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
            {

                fs.Seek(offset, SeekOrigin.Begin);

                if ((remaining_bytes = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    Console.WriteLine(fs.Position);

                    int ps = Math.Min(mp, remaining_bytes + header_size);
                    byte[] packet = ArrayPool<byte>.Shared.Rent(ps);
                    byte[] order_bytes = BitConverter.GetBytes(offset);
                    byte[] sum_bytes = BitConverter.GetBytes(i);

                    Array.Copy(order_bytes, 0, packet, 0, 8);
                    Array.Copy(sum_bytes, 0, packet, 8, 8);
                    Array.Copy(buffer, 0, packet, header_size, buffer.Length);

                    /*s_packet sp = new s_packet();
                    sp.size = ps;
                    sp.data = packet;

                    packets_to_send.Add(sp);*/

                    udp_agent.Send(packet, ps, target);

                    packet_costs += ps;

                    ArrayPool<byte>.Shared.Return(packet);



                }
            }
        }

        public void split_packets(string filepath)
        {
            long ex1 = long.MaxValue;

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
                long file_pos = 0;

                while ((remaining_bytes = fs.Read(buffer, 0, buffer.Length)) > 0)
                {

                    Console.WriteLine(remaining_bytes);
                    int ps = Math.Min(mp, remaining_bytes + header_size);
                    byte[] packet = ArrayPool<byte>.Shared.Rent(ps);
                    byte[] order_bytes = BitConverter.GetBytes(file_pos);
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
                    while (i < ps && bi < buffer.Length)
                    {
                        packet[i] = buffer[bi];
                        i++;
                        bi++;
                    }


                    packet_costs += ps;
                    s_packet sp = new s_packet();
                    sp.size = ps;
                    sp.data = packet;


                    packets_to_send.Add(sp);

                    /*if (a!= 33)
                    {
                        packets_to_send.Add(sp);
                    }
                    else
                    {
                        Console.WriteLine("33 offset:");
                        Console.WriteLine(file_pos);
                        Console.WriteLine(fs.Position);
                    }*/

                    a++;
                    file_pos += remaining_bytes;


                    //packets_to_send2.Add(packet);
                    //remaining_bytes -= (packet.Length - 16);
                }
            }


            Console.WriteLine("All packets have been made !");
            packets_to_send.CompleteAdding();
            send_task.Wait();

            Console.WriteLine("Remaining_bytes");
            Console.WriteLine(remaining_bytes);

            sv_file_state = server_file_state.wait_confirmation;

            //Console.WriteLine(packets_to_send2.Count);

        }

        public void ask_clients_to_transer()
        {

            int cnt = owner.transfer_aux_ports.Count;
            long packet_mod = packet_num % cnt;
            long packets_per_client = (packet_num + packet_mod) / cnt;


            owner.transfer_aux_ports.OrderBy((x) => 
            {
                Client_registry cra = owner.get_c_registry(x);
                //Client_registry crb = owner.get_c_registry(b);
                Client_registry targ = owner.get_c_registry(target);
                
                double dista = Server_main.harvesine(targ.coords.Item1, targ.coords.Item2,cra.coords.Item1, cra.coords.Item2);
                //double distb = owner.harvesine(targ.coords.Item1, targ.coords.Item2, crb.coords.Item1, crb.coords.Item2);
                return  dista;
            }
            
            );
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
                client_sent_packets.Add(max - (a * packets_per_client)); 

            }

            sv_file_state = server_file_state.wait_confirmation;



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


                    if (owner.will_server_do_transfer())
                    {
                        make_single_packet(complete_file_path, pos);
                    }
                    else
                    {

                        IPEndPoint cl = owner.transfer_aux_ports[0];
                        udp_agent.Send(bytes, cl);


                    }

                    //send_packets();


                }

                Thread.Sleep(0);

            }

            Console.WriteLine("Client has confirmed sucessful file transfer !");



            sv_file_state = server_file_state.done;

            //long length = new System.IO.FileInfo(filepath).Length;

            owner.subtract_balance(target.Address,file_size);

            if (!owner.will_server_do_transfer())
            {
                Dictionary<string, object> end_packet = new();
                end_packet["done"] = true;
                end_packet["pos"] = 0;
                long pf = get_packet_filesize();
                byte[] j_bytes = Server_main.dict_to_bytes(end_packet);
                

                for(int i = 0; i < owner.transfer_aux_ports.Count; i++)
                {
                    IPEndPoint cl = owner.transfer_aux_ports[i];
                    udp_agent.Send(j_bytes, cl);
                    owner.add_balance(target.Address, client_sent_packets[i]*pf);
                }
            }

            client_sent_packets.Clear();
            //packets_to_send.Clear();
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
                            if (owner.will_server_do_transfer())
                            {
                                Debug.WriteLine("Server will transfer alone");
                                var st = start_send_packets();
                                if (st)
                                {
                                    split_packets(complete_file_path);
                                }

                                Console.WriteLine(send_task == null);
                            }
                            else
                            {
                                ask_clients_to_transer();
                            }


                            break;
                        }

                    case (server_file_state.wait_confirmation):
                        {


                            wait_confirm();
                            break;
                        }

                    case (server_file_state.done):
                        {
                            finish = true;
                            break;
                        }
                }
                Thread.Sleep(0);
            }
        }


        public override void begin()
        {
            file_transfer_state_machine();
        }
    }


}

