using System;
using System.Net.Sockets;

namespace Bundle.Runtime
{
    public class MinecraftConnect
    {
        public string Host { get; set; }
        public int Port { get; set; }
    }
    public sealed class MinecraftClient : IMinecraftClient
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient _tcpClient;
        private ICompressor _compressor;

        public IScrambler Scrambler { get; set; }

        public event IMinecraftClient.NextPacketDelegate OnNextPacket;
        private readonly ITcpClientFactory _tcpClientFactory;
        public MinecraftClient(MinecraftConnect minecraftConnect, ITcpClientFactory tcpClientFactory, ICompressor compressor)
        {
            _tcpClientFactory = tcpClientFactory;
            _host = minecraftConnect.Host;
            _port = minecraftConnect.Port;
            OnNextPacket += p => { };
            _compressor = compressor;
        }

        public MinecraftClient(MinecraftConnect minecraftConnect, ICompressor compressor)
        {
            _host = minecraftConnect.Host;
            _port = minecraftConnect.Port;
            OnNextPacket += p => { };
            _compressor = compressor;
        }

        public void Connect()
        {
            if(_tcpClientFactory != null)
            {
                _tcpClient = _tcpClientFactory.Create();
            }
            else
            {
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveBufferSize = 1024 * 1024;
                _tcpClient.ReceiveTimeout = 300000;
            }
            _tcpClient.Connect(_host, _port);
        }

        public bool IsConnected()
        {
            return _tcpClient != null && _tcpClient.Connected;
        }

        public bool HasDataAvailable()
        {
            return _tcpClient.Available > 0;
        }


        public Packet ReadNextPacket(int compressionTreshold, bool encrypted)
        {
            var packet = new Packet();
            int size = ReadNextVarIntRAW(encrypted); //Packet size

            byte[] rawpacket = ReadDataRAW(size, encrypted); //Packet contents
            for (int i = 0; i < rawpacket.Length; i++)
                packet.Enqueue(rawpacket[i]);

            //Handle packet decompression
            if (GlobalProtocolVersion.Value >= MinecraftVersion.MC18Version
                && compressionTreshold > 0)
            {
                int sizeUncompressed = packet.ReadNextVarInt();
                if (sizeUncompressed != 0) // != 0 means compressed, let's decompress
                {
                    byte[] toDecompress = packet.ToArray();
                    byte[] uncompressed = _compressor.Decompress(toDecompress, sizeUncompressed);
                    packet.Clear();
                    for (int i = 0; i < uncompressed.Length; i++)
                        packet.Enqueue(uncompressed[i]);
                }
            }

            packet.Id = packet.ReadNextVarInt();
            OnNextPacket(packet);
            return packet;
        }

        public void SendPacket(Packet packet, int compressionTreshold, bool encrypted)
        {
            //The inner packet
            byte[] the_packet = ByteHelper.ConcatBytes(ByteHelper.GetVarInt(packet.Id!.Value), packet.ToArray());

            if (compressionTreshold > 0)
            {
                if (the_packet.Length >= compressionTreshold)
                {
                    byte[] compressed_packet = _compressor.Compress(the_packet);
                    the_packet = ByteHelper.ConcatBytes(ByteHelper.GetVarInt(the_packet.Length), compressed_packet);
                }
                else
                {
                    byte[] uncompressed_length = ByteHelper.GetVarInt(0);
                    the_packet = ByteHelper.ConcatBytes(uncompressed_length, the_packet);
                }
            }

            SendDataRAW(ByteHelper.ConcatBytes(ByteHelper.GetVarInt(the_packet.Length), the_packet), encrypted);
        }

        public void SendDataRAW(byte[] buffer, bool encrypted)
        {
            if (encrypted)
            {
                Scrambler.Write(buffer, 0, buffer.Length);
            }
            else _tcpClient.Client.Send(buffer);
        }

        public byte[] ReadDataRAW(int length, bool encrypted)
        {
            if (length > 0)
            {
                byte[] cache = new byte[length];
                Receive(cache, 0, length, SocketFlags.None, encrypted);
                return cache;
            }
            return new byte[] { };
        }

        private void Receive(byte[] buffer, int start, int offset, SocketFlags f, bool encrypted)
        {
            int read = 0;
            while (read < offset)
            {
                if (encrypted)
                {
                    read += Scrambler.Read(buffer, start + read, offset - read);
                }
                else read += _tcpClient.Client.Receive(buffer, start + read, offset - read, f);
            }
        }

        public int ReadNextVarIntRAW(bool encrypted)
        {
            int i = 0;
            int j = 0;
            int k = 0;
            while (true)
            {
                k = ReadDataRAW(1, encrypted)[0];
                i |= (k & 0x7F) << j++ * 7;
                if (j > 5) throw new OverflowException("VarInt too big");
                if ((k & 0x80) != 128) break;
            }
            return i;
        }


        public void Disconnect()
        {
            if (_tcpClient != null)
                _tcpClient.Close();
        }

        public void Dispose()
        {
            if (_tcpClient != null && IsConnected())
            {
                _tcpClient.Close();
            }

            _tcpClient = null;
        }

        public NetworkStream GetStream()
        {
            return _tcpClient.GetStream();
        }
    }
}
