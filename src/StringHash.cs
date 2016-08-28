

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordSearcher
{
    /// <summary>
    /// The main reason I made this as a separate class instead of using HashSet<T> is that I wanted to 
    /// be able to check if a string was included without having to create a new string first.
    /// </summary>
    public class StringHash
    {        
        // Constants used for the fnv-1a hash
        private const uint fnv32Offset = 2166136261u;
        private const uint fnv32Prime = 16777619u;

        // Hash a string using fnv-1a. This function gives the same results 
        // as if the other hashing functions were called with identical contents.
        protected unsafe static uint Hash(string value)
        {
            fixed(char *ptr = value)
            {
                return Hash(ptr, value.Length);
            }
        }

        // Hash an array of chars using fnv-1a. This function gives the same results 
        // as if the other hashing functions were called with identical contents.
        protected static unsafe uint Hash(char[] buffer, int len)
        {
            fixed (char *ptr = buffer)
            {
                return Hash(ptr, len);
            }
        }

        // Hash an array of chars using fnv-1a. This function gives the same results 
        // as if the other hashing functions were called with identical contents.
        // This is separate from Hash(char[], int) so it can be called in an unsafe context.
        protected static unsafe uint Hash(char *buffer, int len)
        {
            uint hash = fnv32Offset;

            for (var i = 0; i < len; i++)
            {
                hash = hash ^ buffer[i];
                hash *= fnv32Prime;
            }

            return hash;
        }
        
        // This equality method is implemented because the default string comparison does a bunch of extra work
        // like culture based comparisons, that is not necessary and a perf hit.
        protected unsafe static bool Equal(string value, string item)
        {
            fixed(char *ptr = item)
            {
                return Equal(value, ptr, item.Length);
            }
        }

        // Same as Equal(string, string), but allows checking with a char[] so it doesn't have to be
        // converted in to a string first.
        protected static unsafe bool Equal(string value, char[] buffer, int count)
        {
            fixed (char *ptr = buffer)
            {
                return Equal(value, ptr, count);
            }
        }

        // Same as Equal(string, string), but allows checking with a char * so it doesn't have to be
        // converted in to a string first, and can be called from an unsafe context.
        protected static unsafe bool Equal(string value, char *buffer, int count)
        {
            if (value.Length != count)
            {
                return false;
            }

            for (int i = 0; i < count; ++i)
            {
                if (value[i] != buffer[i])
                {
                    return false;
                }
            }

            return true;
        }

        internal class Node
        {
            public Node(uint hash, string value)
            {
                Hash = hash;
                Value = value;
            }
            
            public uint Hash { get; private set; }
            public string Value { get; private set; }
        }

        // The number of buckets created on startup
        private const int InitialSize = 100;
        // How many collisions can occur before we grow
        private const double MaxLoadFactor = 0.33;
        
        private const double GrowFactor = 1.5;

        private int _count;
        
        private Node[] _data;

        private void Resize()
        {
            Node[] newData = new Node[(int)(_data.Length * GrowFactor)];

            int newCount = 0;
            foreach (Node n in _data)
            {
                if(n != null)
                {
                    AddInternal(newData, n.Value, n.Hash, ref newCount);
                }
            }

            _data = newData;
            _count = newCount;
        }

        private bool AddInternal(Node[] data, string item, uint hash, ref int count)
        {
            int pos = (int)(hash % data.Length);
            while (true)
            {
                if(pos >= data.Length)
                {
                    pos = 0;
                }

                if(data[pos] == null)
                {
                    data[pos] = new Node(hash, item);
                    count++;
                    return true;
                }
                
                if(data[pos].Hash == hash && Equal(data[pos].Value, item))
                {
                    return false;
                }

                ++pos;
            }
        }

        public StringHash(int size = InitialSize)
        {
            _data = new Node[size];
        }

        public bool Add(string item)
        {
            if(((double)_count / (double)_data.Length) >= MaxLoadFactor)
            {
                Resize();
            }

            return AddInternal(_data, item, Hash(item), ref _count);
        }

        public void AddRange(StringHash hash)
        {
            for(int i = 0; i < hash._data.Length; ++i)
            {
                if(hash._data[i] != null)
                {
                    Add(hash._data[i].Value);
                }
            }
        }

        public bool Contains(string item)
        {
            uint hash = Hash(item);
            int pos = (int)(hash % _data.Length);

            int i = pos;
            while(true)
            {
                if(i >= _data.Length)
                {
                    i = 0;
                }

                if(_data[i] == null)
                {
                    return false;
                }

                if(_data[i].Hash == hash && Equal(_data[i].Value, item))
                {
                    return true;
                }

                ++i;
            }
        }

        public unsafe bool Contains(char[] arr, int count)
        {
            fixed(char *ptr = arr)
            {
                return Contains(ptr, count);
            }
        }

        public unsafe bool Contains(char *buffer, int count)
        {
            uint hash = Hash(buffer, count);
            int pos = (int)(hash % _data.Length);

            int i = pos;
            while(true)
            {
                if(i >= _data.Length)
                {
                    i = 0;
                }

                if(_data[i] == null)
                {
                    return false;
                }

                if(_data[i].Hash == hash && Equal(_data[i].Value, buffer, count))
                {
                    return true;
                }

                ++i;
            }
        }

        public int Count()
        {
            return _count;
        }

        public IEnumerable<string> EnumerateItems()
        {
            foreach(Node n in _data)
            {
                if(n != null)
                {
                    yield return n.Value;
                }
            }
        }
    }
}
