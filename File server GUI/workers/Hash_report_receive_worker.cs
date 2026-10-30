using File_server_GUI.data;
using File_test_server_gui.workers;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace File_server_GUI.workers
{


    public enum hash_report_state
    {
        waiting_for_info,
        waiting_for_report,
        done
    }

    public struct hash_report
    {
        public byte[] data;
        public long size;
    }


    public class Hash_report_receive_worker : Worker_base
    {
        public Client_registry sender;
        public BlockingCollection<hash_report> report_packets;
        public List<Tuple<string, int>> hash_pairs;
        public byte[] file_buffer;
        public EndPoint trash;
        public hash_report_state state;
        public int expected_packets;
        public int received_packets;
        public bool write_started;

        public Hash_report_receive_worker(Server_main sm, Client_registry cr) : base(sm)
        {
            sender = cr;
            report_packets = new();
            trash = new IPEndPoint(IPAddress.Any, 1);
            file_buffer = new byte[65536];
            state = hash_report_state.waiting_for_info;
            expected_packets = 0;
            received_packets = 0;
            hash_pairs = new();
            write_started = false;

        }

        public void receive_info()
        {
            queue_timeout = 5000;
            log_message("Awaiting report details...");
            byte[] info_bytes = Array.Empty<byte>();

            try
            {
                info_bytes = queque_receive();
            }
            catch (Exception e) 
            {
                log_message("Error: Excepetion caught at hash_report receive receive_info");
                log_message(e.HResult.ToString());
                log_message(e.Message);
            }
            if (info_bytes == null)
            {
                log_message("Failed to receive hash report info.");
                state = hash_report_state.done;
                return;
            }


            Dictionary<string, object>? dict = Server_main.bytes_to_dict(info_bytes);
            int packets = 0;

            if (dict != null)
            {
                byte[] ack = Encoding.ASCII.GetBytes("HRC");
                udp_agent.Send(ack, target);
                packets = Convert.ToInt32(dict["packets"]);
                expected_packets = packets;
                state = hash_report_state.waiting_for_report;
                log_message($"Expecting {expected_packets} packets in the hash report...");
            }
            else
            {
                log_message("Failed to receive hash report info.");
                state = hash_report_state.done;
                return;
            }
        }

        public void read_raw_data()
        {
            log_message("Reading hash report packets...");
            const int sha_256_hash_size = 32;
            foreach (var item in report_packets.GetConsumingEnumerable())
            {
                long reps = item.size / (sha_256_hash_size + 1);

                for (int i = 0; i < reps; i++)
                {
                    string st = Convert.ToHexString(item.data, i * (sha_256_hash_size + 1), sha_256_hash_size);

                    if (owner.available_files.TryGetValue(st, out _))
                    {
                        sender.know_files.Add(st);
                    }
                    else
                    {
                        log_message($"Warning: Client reported hash {st} which is not in the server's registry.");
                    }

                    int per_cent = (int)(item.data[(i * (sha_256_hash_size + 1))]);

                    Tuple<string, int> pair = new(st, per_cent);
                    hash_pairs.Add(pair);


                }

                ArrayPool<byte>.Shared.Return(item.data);
            }

            log_message("Finished reading hash report packets.");
        }

        public void receive_raw_data()
        {
            int ps = 0;
            bool hit = false;


            log_message("Receiving hash report packets...");
            try
            {
                //ps = udp_agent.Client.ReceiveFrom(file_buffer, SocketFlags.None, ref trash);
                ps = queque_receive_from(ref file_buffer);
                hit = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error in receive_raw_data");
                Debug.WriteLine(ex.Message);
            }

            if (hit == true && ps > 0)
            {
                byte[] raw_data = ArrayPool<byte>.Shared.Rent(ps);

                Array.Copy(file_buffer, 0, raw_data, 0, ps);

                hash_report rdp = new();
                rdp.data = raw_data;
                rdp.size = ps;

                report_packets.Add(rdp);
                received_packets++;





            }


            if (received_packets == expected_packets)
            {
                Debug.WriteLine("Hash report transfer has concluded");
                report_packets.CompleteAdding();
                state = hash_report_state.done;
            }




        }

        public bool start_read_raw_data()
        {
            if (write_started) { return false; }
            write_started = true;
            Task.Run(() => { read_raw_data(); });
            return true;

        }


        public void charge_client()
        {
            
            log_message("Charging client...");

            if(hash_pairs.Count == 0)
            {
                log_message("Warning: no hashes found the report");
            }

            foreach (var item in hash_pairs)
            {
                log_message(item.Item1 + ":" + item.Item2 + "%");

                //Transfer has concluded under special circutanses
                if(item.Item2== 255)
                {
                    continue;
                }

                float mod = item.Item2 / 100f;
                long val = (long)(owner.available_files[item.Item1].filesize * mod);
                sender.decrease_balance(val);
            }
            
        }

        public void state_machine()
        {
            bool done = false;
            while (!done)
            {
                switch (state)
                {
                    case hash_report_state.waiting_for_info:
                        {
                            receive_info();
                            break;
                        }

                    case hash_report_state.waiting_for_report:
                        {
                            start_read_raw_data();
                            receive_raw_data();
                            break;
                        }

                    case hash_report_state.done:
                        log_message("Hash report receive worker has completed its task.");
                        done = true;
                        charge_client();
                        break;
                }
            }
        }


        public override void begin()
        {
            log_message("Receiving hash report from client");
            state_machine();
        
        }
        
    }
}
