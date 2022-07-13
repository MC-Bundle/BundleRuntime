using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bundle.Runtime
{
    public interface ICompressor
    {
        public byte[] Decompress(byte[] to_decompress, int size_uncompressed);
        public byte[] Compress(byte[] to_compress);
    }
}
