using System;
using System.Diagnostics;

namespace X3Util
{
    /// <summary>
    /// RC4 cipher.
    /// </summary>
    public class RC4
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private byte[] _key;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _x;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _y;

        /// <summary>
        /// Create an RC4 cipher of specified key length.
        /// </summary>
        /// <param name="keyLength"></param>
        public RC4(int keyLength = 256)
        {
            _key = new byte[keyLength];
        }

        /// <summary>
        /// Initialize the RC4 key with the specified seed.
        /// </summary>
        /// <param name="seed"></param>
        public void Init(byte[] seed)
        {
            _x = 0; _y = 0;
            int i1 = 0, i2 = 0;

            for (int i = 0; i < _key.Length; i++)
            {
                _key[i] = (byte)(i % _key.Length);
            }

            for (int i = 0; i < _key.Length; i++)
            {
                i2 = (seed[i1] + _key[i] + i2) % _key.Length;
                i1 = (i1 + 1) % seed.Length;
                SwapByte(_key, i, i2);
            }
        }

        /// <summary>
        /// In-place crypt of data at the specified offset and of specified length.
        /// </summary>
        /// <param name="data">The data to crypt.</param>
        /// <param name="offset">The offset within the data to start crypting.</param>
        /// <param name="length">The length of data to crypt.</param>
        public void Crypt(byte[] data, int offset, int length)
        {
            if (offset + length > data.Length)
                throw new IndexOutOfRangeException();

            for (int i = 0; i < length; i++)
            {
                _x = (_x + 1) % _key.Length;
                _y = (_key[_x] + _y) % _key.Length;
                SwapByte(_key, _x, _y);
                data[offset + i] ^= _key[(_key[_x] + _key[_y]) % _key.Length];
            }
        }

        /// <summary>
        /// Swaps bytes of specified data indices.
        /// </summary>
        /// <param name="data">The data array.</param>
        /// <param name="firstIndex">The index of the first byte to swap.</param>
        /// <param name="secondIndex">The index of the second byte to swap.</param>
        private void SwapByte(byte[] data, int firstIndex, int secondIndex)
        {
            byte temp = data[firstIndex];
            data[firstIndex] = data[secondIndex];
            data[secondIndex] = temp;
        }
    }
}
