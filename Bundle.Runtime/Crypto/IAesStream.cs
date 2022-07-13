using Bundle.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bundle.Runtime.Crypto
{
    /// <summary>
    /// Interface for AES stream
    /// Allows to use a different implementation depending on the framework being used.
    /// </summary>

    public interface IAesStream : IScrambler
    {

    }
}
