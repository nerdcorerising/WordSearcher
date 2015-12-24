

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
        // The hash set is an array of linked lists, these are the linked list nodes
        internal class Node
        {
            internal uint hash;
            internal string value;
            internal Node next;
        }

        // The number of buckets created on startup
        private const int InitialBucketsSize = 100;
        // How many collisions can occur before we grow
        private const int MaxCollisions = 10;
        // The factor to grow by
        private const double GrowFactor = 1.5;

        // Constants used for the fnv-1a hash
        private const uint fnv32Offset = 2166136261u;
        private const uint fnv32Prime = 16777619u;

        // The array used to store the values
        private Node[] _buckets;

        // Hash a string using fnv-1a. This function gives the same results 
        // as if the other hashing functions were called with identical contents.
        private static uint StringToHash(string value)
        {
            uint hash = fnv32Offset;

            for (var i = 0; i < value.Length; i++)
            {
                hash = hash ^ value[i];
                hash *= fnv32Prime;
            }

            return hash;
        }

        // Hash an array of chars using fnv-1a. This function gives the same results 
        // as if the other hashing functions were called with identical contents.
        private static unsafe uint ArrayToHash(char[] buffer, int len)
        {
            fixed (char* buf = buffer)
            {
                return PtrToHash(buf, len);
            }
        }

        // Hash an array of chars using fnv-1a. This function gives the same results 
        // as if the other hashing functions were called with identical contents.
        // This is separate from ArrayToHash so it can be called in an unsafe context.
        private static unsafe uint PtrToHash(char* buffer, int len)
        {
            uint hash = fnv32Offset;

            for (var i = 0; i < len; i++)
            {
                hash = hash ^ buffer[i];
                hash *= fnv32Prime;
            }

            return hash;
        }

        // Grow the internal array used to store the values and rehash all the values. 
        // This function makes no attempt to be thread safe.
        private void Resize()
        {
            Node[] currArray = _buckets;
            int newLength = (int)(currArray.Length * GrowFactor);
            Node[] newArray = new Node[newLength];

            for (int i = 0; i < currArray.Length; ++i)
            {
                Node n = currArray[i];
                while (n != null)
                {
                    Debug.Assert(n.hash == StringToHash(n.value));

                    int pos = (int)(n.hash % newArray.Length);
                    Node curr = newArray[pos];
                    newArray[pos] = new Node()
                    {
                        hash = n.hash,
                        value = n.value,
                        next = curr
                    };

                    n = n.next;
                }
            }

            _buckets = newArray;
        }

        // This equality method is implemented because the default string comparison does a bunch of extra work
        // like culture based comparisons, that is not necessary and a perf hit.
        private bool Equal(string value, string item)
        {
            if (value.Length != item.Length)
            {
                return false;
            }

            for (int i = 0; i < item.Length; ++i)
            {
                if (value[i] != item[i])
                {
                    return false;
                }
            }

            return true;
        }

        // Same as Equal(string, string), but allows checking with a char[] so it doesn't have to be
        // converted in to a string first.
        private unsafe bool Equal(string value, char[] buffer, int count)
        {
            fixed (char* buf = buffer)
            {
                return Equal(value, buf, count);
            }
        }

        // Same as Equal(string, string), but allows checking with a char* so it doesn't have to be
        // converted in to a string first, and can be called from an unsafe context.
        private unsafe bool Equal(string value, char* buffer, int count)
        {
            if (count == 0)
            {
                return true;
            }

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

        public StringHash()
        {
            _buckets = new Node[InitialBucketsSize];
        }

        public bool Add(string item)
        {
            uint hash = StringToHash(item);
            int pos = (int)(hash % _buckets.Length);
            Node curr = _buckets[pos];
            if (curr == null)
            {
                // No existing nodes in the bucket, create one
                curr = new Node()
                {
                    hash = hash,
                    value = item,
                    next = null
                };

                _buckets[pos] = curr;
                return true;
            }
            else
            {
                // There already is a node in this bucket, see if any contain our value
                int collisions = 0;
                Node prev = curr;
                while (curr != null)
                {
                    ++collisions;

                    // Check the hash first as a perf optimization, 
                    // only do the expensive equals check if the hash doesn't match.
                    if (curr.hash == hash && Equal(item, curr.value))
                    {
                        // Found our string, quit searching and indicate it wasn't inserted.
                        return false;
                    }

                    prev = curr;
                    curr = curr.next;
                }

                prev.next = new Node()
                {
                    hash = hash,
                    value = item,
                    next = null
                };

                // We want to keep the cost of lookup as low as possible so grow the array if
                // the number of collisions is too large.
                if (collisions >= MaxCollisions)
                {
                    Resize();
                }

                return true;
            }
        }

        public bool Contains(string item)
        {
            uint hash = StringToHash(item);
            int pos = (int)(hash % _buckets.Length);
            Node curr = _buckets[pos];
            while (curr != null)
            {
                if (curr.hash == hash && Equal(curr.value, item))
                {
                    return true;
                }

                curr = curr.next;
            }

            return false;
        }

        public bool Contains(char[] buffer, int count)
        {
            uint hash = ArrayToHash(buffer, count);
            int pos = (int)(hash % _buckets.Length);
            Node curr = _buckets[pos];
            while (curr != null)
            {
                if (curr.hash == hash && Equal(curr.value, buffer, count))
                {
                    return true;
                }

                curr = curr.next;
            }

            return false;
        }

        public unsafe bool Contains(char* buffer, int count)
        {
            uint hash = PtrToHash(buffer, count);
            int pos = (int)(hash % _buckets.Length);
            Node curr = _buckets[pos];
            while (curr != null)
            {
                if (curr.hash == hash && Equal(curr.value, buffer, count))
                {
                    return true;
                }

                curr = curr.next;
            }

            return false;
        }

        public int Count()
        {
            int count = 0;
            for (int i = 0; i < _buckets.Length; ++i)
            {
                Node n = _buckets[i];
                while (n != null)
                {
                    ++count;
                    n = n.next;
                }
            }

            return count;
        }

        public IEnumerable<string> EnumerateItems()
        {
            for (int i = 0; i < _buckets.Length; ++i)
            {
                Node n = _buckets[i];
                while (n != null)
                {
                    yield return n.value;
                    n = n.next;
                }
            }
        }
    }
}
