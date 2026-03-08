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
using System.Threading;
using System.Threading.Tasks;

namespace File_server_GUI.workers
{

    public struct raw_data_packet
    {
        public byte[] data;
        public int size;
    }


    public enum storage_state
    {
        receive = 0,
        done = 1,
    }
    public class File_storage_worker : Worker_base
    {
        public Client_registry sender;
        public BlockingCollection<raw_data_packet> raw_packets;
        public byte[] file_buffer;
        public EndPoint trash;
        public long received_packets;
        public long expected_packets;
        public storage_state state;
        public bool write_started;

        public File_storage_worker(Server_main sm, Client_registry cr) : base(sm)
        {
            sender = cr;
            file_buffer = new byte[65536];
            trash = new IPEndPoint(IPAddress.Any, 1);
            raw_packets = new();
            received_packets = 0;
            expected_packets = 0;
            state=0;
            write_started= false;
            state = storage_state.receive;

            udp_agent.AllowNatTraversal(true);
            udp_agent.Client.ReceiveBufferSize = 1024 * 1024 * 2;
            udp_agent.Client.ReceiveTimeout = 3000;
        }

        public void read_raw_data()
        {
            const int sha_256_hash_size = 32;
            foreach (var item in raw_packets.GetConsumingEnumerable())
            {
                int reps = item.size/sha_256_hash_size;

                for (int i = 0; i < reps; i++) 
                {
                    string st = Convert.ToHexString(item.data,i*sha_256_hash_size,sha_256_hash_size);

                    if(owner.available_files.TryGetValue(st,out _))
                    {
                        sender.know_files.Add(st);
                    }
                   
                    
                }

                ArrayPool<byte>.Shared.Return(item.data);
            }
        }

        public void receive_raw_data()
        {
            int ps = 0;
            bool hit = false;

           

            try
            {
                ps = udp_agent.Client.ReceiveFrom(file_buffer, SocketFlags.None, ref trash);
                hit= true;
            }
            catch (Exception ex) 
            {
                Debug.WriteLine("Error in receive_raw_data");
                Debug.WriteLine(ex.Message);
            }

            if (hit==true&&ps > 0) 
            { 
                  byte[] raw_data = ArrayPool<byte>.Shared.Rent(ps);

                  Array.Copy(file_buffer,0,raw_data,0,ps);

                  raw_data_packet rdp = new();
                  rdp.data = raw_data;
                  rdp.size = ps;

                  raw_packets.Add(rdp);
                  received_packets++;

                  

                  
                 
            }


            if(received_packets==expected_packets)
            {
                Debug.WriteLine("File listing has concluded");
                raw_packets.CompleteAdding();
                state = storage_state.done;
            }


        }


        public bool start_read_raw_data()
        {
            if (write_started) { return false; }
            write_started = true;
            Task.Factory.StartNew(() => {read_raw_data(); });
            return true;

        }


        public void File_storage_worker_state_machine()
        {
            bool done = false;
            while (!done)
            {
                  
                  switch(state)
                  {

                    case (storage_state.receive):
                        {
                            //Debug.WriteLine("Receive loop");
                            start_read_raw_data();
                            receive_raw_data();
                            break;
                        }

                    case (storage_state.done):
                        {
                            done = true;
                            break;
                        }
                  }
                  Thread.Sleep(0);
            } 
        }

        public override void begin()
        {
            Debug.WriteLine("Starting file listing receive");
            File_storage_worker_state_machine();
        }
    }
}
