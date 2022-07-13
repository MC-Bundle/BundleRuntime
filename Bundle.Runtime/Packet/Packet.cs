using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Bundle.Runtime
{
    [DebuggerDisplay("Count = {Count}")]
    [Serializable]
    public struct Packet : IEnumerable<byte>, IEnumerable, IReadOnlyCollection<byte>, ICollection, IDisposable
    {
        public int? Id;
        private byte[] _array;
        private int _head = 0;       // The index from which to dequeue if the queue isn't empty.
        private int _tail = 0;       // The index at which to enqueue if the queue isn't full.
        private int _size = 0;       // Number of elements.
        private int _version = 0;

        public Packet()
        {
            Id = null;
            _array = new byte[0];
        }

        public Packet(int id, byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            _array = EnumerableHelpers.ToArray(bytes, out _size);
            if (_size != _array.Length) _tail = _size;

            Id = id;
        }

        public override string ToString()
        {
            return $"{Id} [{string.Join(", ", _array)}]";
        }

        public static Packet FromBytes(int id, byte[] bytes)
        {
            return new Packet(id, bytes);
        }

        public byte[] ReadData(int offset)
        {
            byte[] result = new byte[offset];
            for (int i = 0; i < offset; i++)
                result[i] = Dequeue();
            return result;
        }

        /// <summary>
        /// Read a string from a cache of bytes and remove it from the cache
        /// </summary>
        /// <param name="cache">Cache of bytes to read from</param>
        /// <returns>The string</returns>
        public string ReadNextString()
        {
            int length = ReadNextVarInt();
            if (length > 0)
            {
                return Encoding.UTF8.GetString(ReadData(length));
            }
            else return "";
        }

        /// <summary>
        /// Read a boolean from a cache of bytes and remove it from the cache
        /// </summary>
        /// <returns>The boolean value</returns>
        public bool ReadNextBool()
        {
            return ReadNextByte() != 0x00;
        }

        /// <summary>
        /// Read a short integer from a cache of bytes and remove it from the cache
        /// </summary>
        /// <returns>The short integer value</returns>
        public short ReadNextShort()
        {
            byte[] rawValue = ReadData(2);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToInt16(rawValue, 0);
        }

        /// <summary>
        /// Read an integer from a cache of bytes and remove it from the cache
        /// </summary>
        /// <returns>The integer value</returns>
        public int ReadNextInt()
        {
            byte[] rawValue = ReadData(4);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToInt32(rawValue, 0);
        }

        /// <summary>
        /// Read a long integer from a cache of bytes and remove it from the cache
        /// </summary>
        /// <returns>The unsigned long integer value</returns>
        public long ReadNextLong()
        {
            byte[] rawValue = ReadData(8);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToInt64(rawValue, 0);
        }

        /// <summary>
        /// Read an unsigned short integer from a cache of bytes and remove it from the cache
        /// </summary>
        /// <returns>The unsigned short integer value</returns>
        public ushort ReadNextUShort()
        {
            byte[] rawValue = ReadData(2);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToUInt16(rawValue, 0);
        }

        /// <summary>
        /// Read an unsigned long integer from a cache of bytes and remove it from the cache
        /// </summary>
        /// <returns>The unsigned long integer value</returns>
        public ulong ReadNextULong()
        {
            byte[] rawValue = ReadData(8);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToUInt64(rawValue, 0);
        }

        /// <summary>
        /// Read a Location encoded as an ulong field and remove it from the cache
        /// </summary>
        /// <returns>The Location value</returns>


        /// <summary>
        /// Read several little endian unsigned short integers at once from a cache of bytes and remove them from the cache
        /// </summary>
        /// <returns>The unsigned short integer value</returns>
        public ushort[] ReadNextUShortsLittleEndian(int amount)
        {
            byte[] rawValues = ReadData(2 * amount);
            ushort[] result = new ushort[amount];
            for (int i = 0; i < amount; i++)
                result[i] = BitConverter.ToUInt16(rawValues, i * 2);
            return result;
        }

        /// <summary>
        /// Read a uuid from a cache of bytes and remove it from the cache
        /// </summary>
        /// <param name="cache">Cache of bytes to read from</param>
        /// <returns>The uuid</returns>
        public Guid ReadNextUUID()
        {
            byte[] javaUUID = ReadData(16);
            Guid guid = new Guid(javaUUID);
            if (BitConverter.IsLittleEndian)
                guid = LittleEndian(guid);
            return guid;
        }

        private Guid LittleEndian(Guid javaGuid)
        {
            byte[] net = new byte[16];
            byte[] java = javaGuid.ToByteArray();
            for (int i = 8; i < 16; i++)
            {
                net[i] = java[i];
            }
            net[3] = java[0];
            net[2] = java[1];
            net[1] = java[2];
            net[0] = java[3];
            net[5] = java[4];
            net[4] = java[5];
            net[6] = java[7];
            net[7] = java[6];
            return new Guid(net);
        }

        /// <summary>
        /// Read a byte array from a cache of bytes and remove it from the cache
        /// </summary>
        /// <param name="cache">Cache of bytes to read from</param>
        /// <returns>The byte array</returns>
        public byte[] ReadNextByteArray()
        {
            int len = GlobalProtocolVersion.Value >= 47//MC18Version
                ? ReadNextVarInt()
                : ReadNextShort();
            return ReadData(len);
        }

        /// <summary>
        /// Reads a length-prefixed array of unsigned long integers and removes it from the cache
        /// </summary>
        /// <returns>The unsigned long integer values</returns>
        public ulong[] ReadNextULongArray()
        {
            int len = ReadNextVarInt();
            ulong[] result = new ulong[len];
            for (int i = 0; i < len; i++)
                result[i] = ReadNextULong();
            return result;
        }

        /// <summary>
        /// Read a double from a cache of bytes and remove it from the cache
        /// </summary>
        /// <returns>The double value</returns>
        public double ReadNextDouble()
        {
            byte[] rawValue = ReadData(8);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToDouble(rawValue, 0);
        }

        /// <summary>
        /// Read a float from a cache of bytes and remove it from the cache
        /// </summary>
        /// <returns>The float value</returns>
        public float ReadNextFloat()
        {
            byte[] rawValue = ReadData(4);
            Array.Reverse(rawValue); //Endianness
            return BitConverter.ToSingle(rawValue, 0);
        }

        /// <summary>
        /// Read an integer from a cache of bytes and remove it from the cache
        /// </summary>
        /// <param name="cache">Cache of bytes to read from</param>
        /// <returns>The integer</returns>
        public int ReadNextVarInt()
        {
            string rawData = BitConverter.ToString(ToArray());
            int i = 0;
            int j = 0;
            int k = 0;
            while (true)
            {
                k = ReadNextByte();
                i |= (k & 0x7F) << j++ * 7;
                if (j > 5) throw new OverflowException("VarInt too big " + rawData);
                if ((k & 0x80) != 128) break;
            }
            return i;
        }

        /// <summary>
        /// Skip a VarInt from a cache of bytes with better performance
        /// </summary>
        /// <param name="cache">Cache of bytes to read from</param>
        public void SkipNextVarInt()
        {
            while (true)
            {
                if ((ReadNextByte() & 0x80) != 128)
                    break;
            }
        }

        /// <summary>
        /// Read an "extended short", which is actually an int of some kind, from the cache of bytes.
        /// This is only done with forge.  It looks like it's a normal short, except that if the high
        /// bit is set, it has an extra byte.
        /// </summary>
        /// <param name="cache">Cache of bytes to read from</param>
        /// <returns>The int</returns>
        public int ReadNextVarShort()
        {
            ushort low = ReadNextUShort();
            byte high = 0;
            if ((low & 0x8000) != 0)
            {
                low &= 0x7FFF;
                high = ReadNextByte();
            }
            return ((high & 0xFF) << 15) | low;
        }

        /// <summary>
        /// Read a long from a cache of bytes and remove it from the cache
        /// </summary>
        /// <param name="cache">Cache of bytes to read from</param>
        /// <returns>The long value</returns>
        public long ReadNextVarLong()
        {
            int numRead = 0;
            long result = 0;
            byte read;
            do
            {
                read = ReadNextByte();
                long value = (read & 0x7F);
                result |= (value << (7 * numRead));

                numRead++;
                if (numRead > 10)
                {
                    throw new OverflowException("VarLong is too big");
                }
            } while ((read & 0x80) != 0);
            return result;
        }

        /// <summary>
        /// Read a single byte from a cache of bytes and remove it from the cache
        /// </summary>
        /// <returns>The byte that was read</returns>
        public byte ReadNextByte()
        {
            return Dequeue();
        }

        /// <summary>
        /// Read an uncompressed Named Binary Tag blob and remove it from the cache
        /// </summary>
        public Dictionary<string, object> ReadNextNbt()
        {
            return ReadNextNbt(true);
        }


        /// <summary>
        /// Read an uncompressed Named Binary Tag blob and remove it from the cache (internal)
        /// </summary>
        private Dictionary<string, object> ReadNextNbt(bool root)
        {
            Dictionary<string, object> nbtData = new Dictionary<string, object>();

            if (root)
            {
                if (Peek() == 0) // TAG_End
                {
                    Dequeue();
                    return nbtData;
                }
                if (Peek() != 10) // TAG_Compound
                    throw new System.IO.InvalidDataException("Failed to decode NBT: Does not start with TAG_Compound");
                ReadNextByte(); // Tag type (TAG_Compound)

                // NBT root name
                string rootName = Encoding.ASCII.GetString(ReadData(ReadNextUShort()));
                if (!String.IsNullOrEmpty(rootName))
                    nbtData[""] = rootName;
            }

            while (true)
            {
                int fieldType = ReadNextByte();

                if (fieldType == 0) // TAG_End
                    return nbtData;

                int fieldNameLength = ReadNextUShort();
                string fieldName = Encoding.ASCII.GetString(ReadData(fieldNameLength));
                object fieldValue = ReadNbtField(fieldType);

                // This will override previous tags with the same name
                nbtData[fieldName] = fieldValue;
            }
        }

        /// <summary>
        /// Read a single Named Binary Tag field of the specified type and remove it from the cache
        /// </summary>
        private object ReadNbtField(int fieldType)
        {
            switch (fieldType)
            {
                case 1: // TAG_Byte
                    return ReadNextByte();
                case 2: // TAG_Short
                    return ReadNextShort();
                case 3: // TAG_Int
                    return ReadNextInt();
                case 4: // TAG_Long
                    return ReadNextLong();
                case 5: // TAG_Float
                    return ReadNextFloat();
                case 6: // TAG_Double
                    return ReadNextDouble();
                case 7: // TAG_Byte_Array
                    return ReadData(ReadNextInt());
                case 8: // TAG_String
                    return Encoding.UTF8.GetString(ReadData(ReadNextUShort()));
                case 9: // TAG_List
                    int listType = ReadNextByte();
                    int listLength = ReadNextInt();
                    object[] listItems = new object[listLength];
                    for (int i = 0; i < listLength; i++)
                        listItems[i] = ReadNbtField(listType);
                    return listItems;
                case 10: // TAG_Compound
                    return ReadNextNbt(false);
                case 11: // TAG_Int_Array
                    listType = 3;
                    listLength = ReadNextInt();
                    listItems = new object[listLength];
                    for (int i = 0; i < listLength; i++)
                        listItems[i] = ReadNbtField(listType);
                    return listItems;
                case 12: // TAG_Long_Array
                    listType = 4;
                    listLength = ReadNextInt();
                    listItems = new object[listLength];
                    for (int i = 0; i < listLength; i++)
                        listItems[i] = ReadNbtField(listType);
                    return listItems;
                default:
                    throw new System.IO.InvalidDataException("Failed to decode NBT: Unknown field type " + fieldType);
            }
        }


        
        //=========================

        public int Count => _size;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public void Clear()
        {
            if (_size != 0)
            {
                _size = 0;
            }

            _head = 0;
            _tail = 0;
            _version++;
        }

        public void CopyTo(Array array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (array.Rank != 1)
            {
                throw new ArgumentException("Arg_RankMultiDimNotSupported", nameof(array));
            }

            if (array.GetLowerBound(0) != 0)
            {
                throw new ArgumentException("Arg_NonZeroLowerBound", nameof(array));
            }

            int arrayLen = array.Length;
            if (index < 0 || index > arrayLen)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "ArgumentOutOfRange_Index");
            }

            if (arrayLen - index < _size)
            {
                throw new ArgumentException("Argument_InvalidOffLen");
            }

            int numToCopy = _size;
            if (numToCopy == 0) return;

            try
            {
                int firstPart = (_array.Length - _head < numToCopy) ? _array.Length - _head : numToCopy;
                Array.Copy(_array, _head, array, index, firstPart);
                numToCopy -= firstPart;

                if (numToCopy > 0)
                {
                    Array.Copy(_array, 0, array, index + _array.Length - _head, numToCopy);
                }
            }
            catch (ArrayTypeMismatchException)
            {
                throw new ArgumentException("Argument_InvalidArrayType", nameof(array));
            }
        }
        public void Enqueue(byte item)
        {
            if (_size == _array.Length)
            {
                Grow(_size + 1);
            }

            _array[_tail] = item;
            MoveNext(ref _tail);
            _size++;
            _version++;
        }

        private void Grow(int capacity)
        {
            Debug.Assert(_array.Length < capacity);

            const int GrowFactor = 2;
            const int MinimumGrow = 4;

            int newcapacity = GrowFactor * _array.Length;

            // Allow the list to grow to maximum possible capacity (~2G elements) before encountering overflow.
            // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
            if ((uint)newcapacity > 2147483591) newcapacity = 2147483591;

            // Ensure minimum growth is respected.
            newcapacity = Math.Max(newcapacity, _array.Length + MinimumGrow);

            // If the computed capacity is still less than specified, set to the original argument.
            // Capacities exceeding Array.MaxLength will be surfaced as OutOfMemoryException by Array.Resize.
            if (newcapacity < capacity) newcapacity = capacity;

            SetCapacity(newcapacity);
        }

        public int EnsureCapacity(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "ArgumentOutOfRange_NeedNonNegNum");
            }

            if (_array.Length < capacity)
            {
                Grow(capacity);
            }

            return _array.Length;
        }

        private void ThrowForEmptyQueue()
        {
            Debug.Assert(_size == 0);
            throw new InvalidOperationException("InvalidOperation_EmptyQueue");
        }

        public void TrimExcess()
        {
            int threshold = (int)(_array.Length * 0.9);
            if (_size < threshold)
            {
                SetCapacity(_size);
            }
        }

        public bool Contains(byte item)
        {
            if (_size == 0)
            {
                return false;
            }

            if (_head < _tail)
            {
                return Array.IndexOf(_array, item, _head, _size) >= 0;
            }

            // We've wrapped around. Check both partitions, the least recently enqueued first.
            return
                Array.IndexOf(_array, item, _head, _array.Length - _head) >= 0 ||
                Array.IndexOf(_array, item, 0, _tail) >= 0;
        }

        public byte Peek()
        {
            if (_size == 0)
            {
                ThrowForEmptyQueue();
            }

            return _array[_head];
        }

        public bool TryPeek(out byte result)
        {
            if (_size == 0)
            {
                result = default!;
                return false;
            }

            result = _array[_head];
            return true;
        }

        public byte Dequeue()
        {
            int head = _head;
            byte[] array = _array;

            if (_size == 0)
            {
                ThrowForEmptyQueue();
            }

            byte removed = array[head];
            MoveNext(ref _head);
            _size--;
            _version++;
            return removed;
        }

        public bool TryDequeue(out byte result)
        {
            int head = _head;
            byte[] array = _array;

            if (_size == 0)
            {
                result = default!;
                return false;
            }

            result = array[head];
            MoveNext(ref _head);
            _size--;
            _version++;
            return true;
        }

        public byte[] ToArray()
        {
            if (_size == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] arr = new byte[_size];

            if (_head < _tail)
            {
                Array.Copy(_array, _head, arr, 0, _size);
            }
            else
            {
                Array.Copy(_array, _head, arr, 0, _array.Length - _head);
                Array.Copy(_array, 0, arr, _array.Length - _head, _tail);
            }

            return arr;
        }

        private void MoveNext(ref int index)
        {
            // It is tempting to use the remainder operator here but it is actually much slower
            // than a simple comparison and a rarely taken branch.
            // JIT produces better code than with ternary operator ?:
            int tmp = index + 1;
            if (tmp == _array.Length)
            {
                tmp = 0;
            }
            index = tmp;
        }

        private void SetCapacity(int capacity)
        {
            byte[] newarray = new byte[capacity];
            if (_size > 0)
            {
                if (_head < _tail)
                {
                    Array.Copy(_array, _head, newarray, 0, _size);
                }
                else
                {
                    Array.Copy(_array, _head, newarray, 0, _array.Length - _head);
                    Array.Copy(_array, 0, newarray, _array.Length - _head, _tail);
                }
            }

            _array = newarray;
            _head = 0;
            _tail = (_size == capacity) ? 0 : _size;
            _version++;
        }

        public void Dispose()
        {
            GC.Collect();
        }


        public IEnumerator<byte> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        public struct Enumerator : IEnumerator<byte>,
            System.Collections.IEnumerator
        {
            private readonly Packet _q;
            private readonly int _version;
            private int _index;   // -1 = not started, -2 = ended/disposed
            private byte? _currentElement;

            internal Enumerator(Packet q)
            {
                _q = q;
                _version = q._version;
                _index = -1;
                _currentElement = default;
            }

            public void Dispose()
            {
                _index = -2;
                _currentElement = default;
            }

            public bool MoveNext()
            {
                if (_version != _q._version) throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");

                if (_index == -2)
                    return false;

                _index++;

                if (_index == _q._size)
                {
                    // We've run past the last element
                    _index = -2;
                    _currentElement = default;
                    return false;
                }

                // Cache some fields in locals to decrease code size
                byte[] array = _q._array;
                int capacity = array.Length;

                // _index represents the 0-based index into the queue, however the queue
                // doesn't have to start from 0 and it may not even be stored contiguously in memory.

                int arrayIndex = _q._head + _index; // this is the actual index into the queue's backing array
                if (arrayIndex >= capacity)
                {
                    // NOTE: Originally we were using the modulo operator here, however
                    // on Intel processors it has a very high instruction latency which
                    // was slowing down the loop quite a bit.
                    // Replacing it with simple comparison/subtraction operations sped up
                    // the average foreach loop by 2x.

                    arrayIndex -= capacity; // wrap around if needed
                }

                _currentElement = array[arrayIndex];
                return true;
            }

            public byte Current
            {
                get
                {
                    if (_index < 0)
                        ThrowEnumerationNotStartedOrEnded();
                    return _currentElement!.Value;
                }
            }

            private void ThrowEnumerationNotStartedOrEnded()
            {
                Debug.Assert(_index == -1 || _index == -2);
                throw new InvalidOperationException(_index == -1 ? "InvalidOperation_EnumNotStarted" : "InvalidOperation_EnumEnded");
            }

            object? IEnumerator.Current
            {
                get { return Current; }
            }

            void IEnumerator.Reset()
            {
                if (_version != _q._version) throw new InvalidOperationException("InvalidOperation_EnumFailedVersion");
                _index = -1;
                _currentElement = default;
            }
        }
    }
}
