using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace File_test_server_gui.workers
{
    public class Worker_base : IDisposable
    {
        public UdpClient udp_agent;
        public IPEndPoint target;
        public IPEndPoint trash;
        public Server_main owner;
        public static int max_udp_size = 65535;
        public Stopwatch stopwatch;



        public Worker_base(Server_main ow)
        {
            udp_agent = new UdpClient(AddressFamily.InterNetworkV6);
            udp_agent.Client.DualMode = true;
            udp_agent.AllowNatTraversal(true);
            owner = ow;
            stopwatch = new();

            var ips = Dns.GetHostAddresses(Dns.GetHostName())[0];
            target = new IPEndPoint(ips, 1);

        }

        public virtual void begin()
        {

        }

        public virtual void pair_up()
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

                Thread.Sleep(0);
            }

            Debug.WriteLine("Pair-up with client sucessful !");
            begin();



        }

        public void Dispose()
        {
            udp_agent.Dispose();

        }

        public void log_message(string msg)
        {
            Debug.WriteLine(msg);

            if (owner != null)
            {
                owner.add_log_msg(msg);
            }

        }
    }
}
