using File_server_GUI.workers;
using File_test_server_gui.workers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public IPEndPoint remote_addr;


        public Client_query_worker(Server_main ow) : base(ow)
        {
            file_hash = complete_file_path = string.Empty;
            remote_addr=new(0,0);
        }

        public void query_clients_for_file()
        {
            
            log_message("Begin client query");
            log_message(this.GetHashCode().ToString());
            Dictionary<string, object> query = new();
            Dictionary<string, object> resp_check = null;
            query["type"] = 1;
            query["hash"] = file_hash;
            query["code"] = this.GetHashCode();


            IPEndPoint last_client  = new IPEndPoint(IPAddress.Any, 1);


            var json_dict = JsonConvert.SerializeObject(query);

            var j_bytes = Encoding.ASCII.GetBytes(json_dict);

            log_message("Possible client udp ports for query:");
            log_message(owner.client_upd_ports.Count.ToString());

            log_message("Qcode sanity check:");
            log_message(query["code"].ToString());

            if(owner.client_upd_ports.Count <= 1)
            {
                log_message("No clients to query!");
                //owner.query_filter.Remove(this.GetHashCode());
                Dispose();
                return;
            }

            udp_agent.Client.ReceiveTimeout = 2000;
            queue_timeout = 3000;
            foreach (IPEndPoint client in owner.client_upd_ports)
            {
                try
                {

                    if (owner.receiving_upd_ports.Contains(client))
                    {
                        log_message("Client " + client + " Is currently receiving");
                        continue;
                    }

                    if (client.Address.Equals(target.Address) || target.Equals(client))
                    {
                        log_message("Client " + client + " Is the current receiver");
                        continue;
                    }
                    Debug.WriteLine("Asking for file");
                   
                    owner.add_worker_filter_client(client,this);
                    udp_agent.Send(j_bytes, client);
                   
                    last_client = client;
                    //byte[] r_bytes = udp_agent.Receive(ref trash);
                    byte[] r_bytes = queque_receive();
                    if (r_bytes != null && r_bytes.Length > 0)
                    {
                        var reps = Server_main.bytes_to_dict(r_bytes);

                        if (reps == null) { continue; }

                        resp_check = reps;
                        bool found = (bool)reps["found"];
                        long c_code = (long)reps["client_code"];
                        string? worker_ip = reps["worker_ip"] as string;



                        if (found && c_code != client_code && worker_ip != null)
                        {
                            log_message("Client has file, Adding to list of clients with file");
                            //IPEndPoint new_iep = new(trash.Address, trash.Port);
                            IPEndPoint port_iep = IPEndPoint.Parse(worker_ip);
                            IPEndPoint new_iep = new(remote_addr.Address, port_iep.Port);

                            owner.transfer_aux_ports.Add(new_iep);
                        }
                    }
                    owner.remove_worker_filter_client(client);
                }

                catch
                {
                    log_message("Client did not respond to query!");
                    owner.remove_worker_filter_client(last_client);

                    Debug.WriteLine("Dict sanity check");
                    Debug.WriteLine(resp_check.TryGetValue("found", out _));
                    Debug.WriteLine(resp_check.TryGetValue("client_code", out _));
                    Debug.WriteLine(resp_check.TryGetValue("worker_ip", out _));
                }
            }
            log_message("Queried clients for file!");
            owner.query_filter.Remove(this.GetHashCode());
            Dispose();



        }


    }
}
