using File_test_server.workers;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
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
        public BlockingCollection<byte[]> server_packets;
        public int queue_timeout = 5000;


        public Worker_base(Server_main ow)
        {
            udp_agent = new UdpClient(AddressFamily.InterNetworkV6);
            udp_agent.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            udp_agent.Client.DualMode = true;
            udp_agent.ExclusiveAddressUse = false;
            udp_agent.Client.Bind(new IPEndPoint(IPAddress.IPv6Any, Server_main.listenPort));
            udp_agent.Client.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, true);
            //udp_agent.JoinMulticastGroup(IPAddress.Parse("FF02::1234"));


            if (OperatingSystem.IsWindows())
            {
                udp_agent.AllowNatTraversal(true);
            }

            owner = ow;
            stopwatch = new();

            var ips = Dns.GetHostAddresses(Dns.GetHostName())[0];
            target = new IPEndPoint(ips, 1);
            server_packets = new();

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
            //udp_agent.Connect(target);
            
            log_message("Attempting to pair-up with client...");
            while (!paired)
            {

                try
                {
                    udp_agent.Send(j_bytes, target);
                    log_message("ACK sent to client");

                    //var resp = udp_agent.Receive(ref trash);
                    var resp = queque_receive();
                    log_message("Response received from client");

                    if (resp != null && resp.Length > 0)
                    {
                        paired = true;
                        break;
                    }
                }
                catch (Exception e)
                {
                    log_message("Error ocurred in pair-up:");
                    log_message(e.Message);

                }

                Thread.Sleep(0);
            }

            log_message("Pair-up with client sucessful !");
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

        public void send_keep_alive(IPEndPoint iep, byte[] data)
        {
            udp_agent.SendAsync(data, iep);

        }

        public void add_packet(byte[] data)
        {
            server_packets.Add(data);
        }

        public byte[] queque_receive()
        {
            while (server_packets.Count <= 0)
            {
                Thread.Sleep(0);
            }

            return server_packets.Take();
        }

        public async Task<byte[]> queque_receive_async()
        {
            stopwatch.Restart();
            while (server_packets.Count <= 0 && stopwatch.ElapsedMilliseconds < queue_timeout)
            {
                await Task.Delay(0);
            }
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds  >= queue_timeout)
            {
                return Array.Empty<byte>();
            }

            return server_packets.Take();
        }

    }
}
