using Avalonia.Markup.Xaml.MarkupExtensions;
using File_server_GUI.data;
using File_test_server_gui.workers;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace File_server_GUI.workers
{

    public enum transfer_state
    {
        inform_client = 0,
        send_packets = 1,
        done = 2,
    }

    public struct data_packet
    {
        public byte[] data;
        public int size;

    }


    public class Hash_transfer_worker : Worker_base
    {

        public List<Transfer_registry> Hashes;
        public BlockingCollection<data_packet> packets_to_send;
        public bool sender_started;
        public const int max_packet_size = 1440;
        public transfer_state state;

        public Hash_transfer_worker(Server_main o) : base(o)
        {
            packets_to_send = new BlockingCollection<data_packet>();
            Hashes = new();
            sender_started = false;
            state = transfer_state.inform_client;
        }

        public int calculate_total_packets()
        {
            if (Hashes.Count <= 0)
            {
                return 0;
            }
            byte[] sample_bytes = Convert.FromHexString(Hashes[0].hash);
            int sample_size = sample_bytes.Length;
            double double_total_reps = Math.Ceiling(((double)sample_bytes.Length * Hashes.Count) / max_packet_size);
            int total_reps = (int)double_total_reps;
            return total_reps;
        }


        public void inform_client()
        {
            add_msg("Informing client about incoming hash transfer...");
            Dictionary<string,object> dict = new Dictionary<string, object>();
            dict["packets"] = calculate_total_packets();

            byte[] j_bytes = Server_main.dict_to_bytes(dict);
            udp_agent.Send(j_bytes, target);

            queue_timeout = 5000;

            byte[] resp = queque_receive();

            state = transfer_state.send_packets;




        }

        public void write_packets()
        {

            Debug.WriteLine("Writing pending transfer packets...");
            if (Hashes.Count <= 0) 
            {

                log_message("Warning: No hashes to write");
                return; 
            }

            int i = 0;
            
            int packet_left = max_packet_size;

            byte[] sample_bytes = Convert.FromHexString(Hashes[0].hash);
            int sample_size = sample_bytes.Length;
            //int total_reps = (sample_bytes.Length*hashes.Length)/max_packet_size;
            double double_total_reps = Math.Ceiling(((double)sample_bytes.Length * Hashes.Count) / max_packet_size);
            int total_reps = (int)double_total_reps;

            Debug.WriteLine("Total reps:");
            Debug.WriteLine(total_reps.ToString());

            for (int a = 0; a < total_reps; a++)
            {
                byte[] data = ArrayPool<byte>.Shared.Rent(max_packet_size);
                int inner_reps = Math.Min(max_packet_size / sample_size, Hashes.Count - i);

                for (int b = 0; b < inner_reps; b++)
                {
                    byte[] hex_bytes = Convert.FromHexString(Hashes[b + i].hash);
                    Array.Copy(hex_bytes, 0, data, i * sample_size, sample_size);
                    i++;
                }

                data_packet dp = new data_packet();
                dp.data = sample_bytes;
                dp.size = sample_size * inner_reps;
                packets_to_send.Add(dp);
            }

            log_message("Finished writing packets to send queue");
            packets_to_send.CompleteAdding();
            owner.remove_client(target);

            state = transfer_state.done;
        }

        public void send_packets()
        {
            Debug.WriteLine("Sending pending transfer packets...");
            foreach (var packet in packets_to_send.GetConsumingEnumerable())
            {
                udp_agent.Send(packet.data, packet.size, target);
                ArrayPool<byte>.Shared.Return(packet.data);
            }

            log_message("All packets where sent");
        }

        public void start_send_packets()
        {
            if (!sender_started)
            {
                sender_started = true;
                send_packets();
                Task.Run(() => { send_packets(); });
                int e = 0;
            }

            
        }

        public void add_msg(string msg)
        {
            Debug.WriteLine(msg);
            owner.add_log_msg(msg);
        }


        public void hash_transfer_state_machine()
        {
            while (true)
            {
                switch(state)
                {
                    case transfer_state.inform_client:
                        {
                            inform_client();
                            break;
                        }
                    case transfer_state.send_packets:
                        {
                            add_msg("Starting to send hash packets...");
                            //start_send_packets();
                            write_packets();

                            start_send_packets();
                            
                            //state = transfer_state.done;
                            break;
                        }
                    case transfer_state.done:
                        add_msg("Hash transfer done !");
                        Dispose();
                        return;
                }
                Thread.Sleep(0);
            }
        }

        public override void begin()
        {
            Debug.WriteLine("Beggining transfer hash transmition");
            //var send_task = Task.Factory.StartNew(() => { start_send_packets(); });
            //write_packets();
            hash_transfer_state_machine();
        }

        /*public override void pair_up()
        {
            Debug.WriteLine("Pairing up with client");
            Debug.WriteLine("Listening at:");
            try
            {
                //Debug.WriteLine(udp_agent.Client.LocalEndPoint.ToString());
            }
            catch
            {

            }
            bool paired = false;
            bool fail = false;

            udp_agent.Client.ReceiveTimeout = 10000;
            while (!paired)
            {
                try
                {
                    var resp = queque_receive();
                    Debug.WriteLine("Received client message");
                    if (resp != null && resp.Length > 0)
                    {
                        paired = true;
                        break;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error while pairing up:\n" + e.Message);
                    log_message(e.Message);
                    fail = true;
                    break;

                }

                Thread.Sleep(0);
            }


            if (fail)
            {
                
                return;
            }
            Debug.WriteLine("Out of paired loop");


            string ack = "ACK";
            var json_dict = JsonConvert.SerializeObject(ack);
            var j_bytes = Encoding.ASCII.GetBytes(json_dict);
            udp_agent.Send(j_bytes, target);
            Debug.WriteLine("Pair up done !");
            Debug.WriteLine("new target");
            Debug.WriteLine(target.ToString());
            begin();
        }*/
    }
}
