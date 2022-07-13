using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bundle.Runtime
{
    public interface IScrambler
    {
        public int Read(byte[] buffer, int offset, int count);
        public void Write(byte[] buffer, int offset, int count);
    }
}
