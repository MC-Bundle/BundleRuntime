using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bundle.Runtime
{
    public interface IPacketReader
    {
        public Packet Next();
        public void SwitchToEncrypted(Stream stream, byte[] secretKey);
        public void SetCompressionTreshold(int compression_treshold);
    }
}
