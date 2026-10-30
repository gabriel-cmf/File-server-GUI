using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace File_server_GUI.data
{
    public class Transfer_registry
    {

        [BsonId]
        public string hash { get; set; }

        public bool finished { get; set; }
        public string path { get; set; }
        public IPEndPoint target { get; set; }

        public Transfer_registry() 
        {
            hash = path = string.Empty;
            target = new IPEndPoint(IPAddress.None, 0);
            finished = false;
        }


        public Transfer_registry(string h,  string p, IPEndPoint t)
        {
            hash = h;
            path = p;
            target = t;
            finished = false;
        }
    }
}
