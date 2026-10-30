using File_test_server_gui.data;
using File_test_server_gui.workers;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace File_server_GUI.workers
{


    public enum File_repair_worker_state
    {
        idle,
        repairing,
        done,
        error
    }
    public class File_repair_worker : Worker_base
    {
        public File_registry registry;
        public IncrementalHash gen_hash;
        public File_repair_worker_state state;
        public string file_path;
        public long packets_per_chunk;
        public long file_size;

        public File_repair_worker(Server_main sm) : base(sm)
        {
            gen_hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            state = File_repair_worker_state.idle;
            packets_per_chunk = 0;
            file_path = String.Empty;
        }

        public string generate_chunk_hash(long start, long end)
        {
            gen_hash.GetHashAndReset();
            long remaining = end - start + 1;
            using (FileStream fs = new FileStream(file_path, FileMode.Open, FileAccess.Read))
            {
                fs.Position = start;


                while (remaining > 0)
                {
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(1440);
                    int read_size = (int)Math.Min(1440, remaining);
                    int read = fs.Read(buffer, 0, read_size);
                    if (read <= 0)
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                        break;
                    }
                    gen_hash.AppendData(buffer, 0, read);
                    remaining -= read;
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            byte[] raw_hash = gen_hash.GetHashAndReset();
            string st = Convert.ToHexString(raw_hash, 0, raw_hash.Length);
            return st;
        }


        public void repair_loop()
        {
            bool done =false;
            int i = 0;
            long remain = file_size;
            log_message("File repair loop started");
            while (!done)
            {
                byte[] recv = queque_receive();
                Dictionary<string, object>? data = Server_main.bytes_to_dict(recv);
                if (data == null) { continue; }

                bool stop = data["done" ] as bool? ?? false;
                if (stop)
                {
                    done = true;
                    break;
                }

                string hash = data["hash"] as string ?? String.Empty;
                if(hash == String.Empty)
                {
                    continue;
                }

                long start = data["start"] as long? ?? 0;
                long end = data["end"] as long? ?? 0;

                string gen_hash  = generate_chunk_hash(start, end);


                if (gen_hash == hash)
                {
                    Dictionary<string, object> send_data = new Dictionary<string, object>();
                    send_data["match"] = true;
                    byte[] send_bytes = Server_main.dict_to_bytes(send_data);
                    udp_agent.Send(send_bytes,target);
                }
                else
                {
                    Dictionary<string, object> send_data = new Dictionary<string, object>();
                    send_data["match"] = false;
                    byte[] send_bytes = Server_main.dict_to_bytes(send_data);
                    udp_agent.Send(send_bytes, target);


                }
                Thread.Sleep(0);
                i++;
            }

            log_message("File repair loop done");
        }

        public void state_machine()
        {
            bool done = false;
            while(!done)
            {
                switch (state)
                {
                    case File_repair_worker_state.idle:
                        break;
                    case File_repair_worker_state.repairing:
                        {
                            repair_loop();
                            state = File_repair_worker_state.done;
                            break;
                        }
                        
                    case File_repair_worker_state.done:
                        {

                            done =true;
                            owner.urgent_worker_queue.Enqueue(this);
                            break;
                        }
                        
                    case File_repair_worker_state.error:
                        break;
                }

                Thread.Sleep(0);
            }
        }

        public override void begin()
        {
            log_message("File repair started");
            state = File_repair_worker_state.repairing;
            state_machine();
        }


        
    }
}
