using File_test_server_gui.workers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace File_test_server_gui.workers
{
    public class Client_query_worker : Worker_base
    {

        public long packet_num;
        public long client_code;
        public string complete_file_path;
        public string file_hash;


        public Client_query_worker(Server_main ow) : base(ow)
        {
            file_hash = complete_file_path = string.Empty;
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

            log_message("Possible client udp ports:");
            log_message(owner.client_upd_ports.Count.ToString());

            if(owner.client_upd_ports.Count <= 1)
            {
                log_message("No clients to query!");
                return;
            }

            udp_agent.Client.ReceiveTimeout = 2000;
            queue_timeout = 2000;
            foreach (IPEndPoint client in owner.client_upd_ports)
            {
                try
                {

                    if (owner.receiving_upd_ports.Contains(client))
                    {
                        continue;
                    }
                    Console.WriteLine("Asking for file:");
                    Console.WriteLine(client.ToString());
                    udp_agent.Send(j_bytes, client);
                    //byte[] r_bytes = udp_agent.Receive(ref trash);
                    byte[] r_bytes = queque_receive();
                    if (r_bytes != null && r_bytes.Length > 0)
                    {
                        var reps = Server_main.bytes_to_dict(r_bytes);

                        if (reps == null) { continue; }

                        bool found = (bool)reps["found"];
                        long c_code = (long)reps["client_code"];

                        if (found && c_code != client_code)
                        {
                            IPEndPoint new_iep = new(trash.Address, trash.Port);
                            owner.transfer_aux_ports.Add(new_iep);
                        }
                    }
                }

                catch
                {
                    log_message("Client did not respond to query!");
                }
            }
            log_message("Queried clients for file!");



        }


    }
}
