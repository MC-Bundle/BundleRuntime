using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Bundle.Runtime
{
    public interface ITcpClientFactory
    {
        public TcpClient Create();
        public TcpClient Create(string host, int port);
    }
}
