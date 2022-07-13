using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Bundle.Runtime
{
    public interface IMinecraftClient : IDisposable
    {
        public IScrambler Scrambler { get; set; }
        public void Connect();
        public void Disconnect();
        public bool IsConnected();
        public bool HasDataAvailable();
        public Packet ReadNextPacket(int compressionTreshold, bool encrypted);
        public void SendPacket(Packet packet, int compressionTreshold, bool encrypted);

        public void SendDataRAW(byte[] buffer, bool encrypted);
        public int ReadNextVarIntRAW(bool encrypted);
        public byte[] ReadDataRAW(int length, bool encrypted);
        public NetworkStream GetStream();

        public delegate void NextPacketDelegate(Packet packet);
        public event NextPacketDelegate OnNextPacket;
    }
}
