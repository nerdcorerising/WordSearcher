

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WordSearcher
{
    public enum TrieContains
    {
        Contains,
        ValidPrefix,
        InvalidPrefix
    }

    /// <summary>
    /// This class stores a whole bunch of words in a very space efficient manner.
    /// 
    /// This is architected for a very specialized purpose. I want to be able to know when we hit a dead end when
    /// processing a word in the word search. To do this, you can request a child for a specific letter and walk
    /// it recursively until there are no more children left. This means that instead of searching through the 
    /// whole set looking for prefixes like we would have to in a set or list, we can just do one operation per
    /// letter as we search.
    /// </summary>
    public class CharTrie
    {
        // 26 characters
        private static readonly uint s_childSize = 26;

        private char _ch;
        private bool _terminator;
        private readonly CharTrie[] _children = new CharTrie[s_childSize];

        public CharTrie()
            : this((char)0, false)
        {
        }

        private CharTrie(char ch, bool terminator)
        {
            _ch = ch;
            _terminator = terminator;
        }
        
        private static int CharToIndex(char ch)
        {
            return (int)((int)ch - (int)'A');
        }

        private void SetTerminator(bool terminator)
        {
            _terminator = terminator;
        }

        public int MaxLength
        {
            get
            {
                int maxLength = 0;
                foreach (CharTrie ct in _children)
                {
                    if (ct != null)
                    {
                        int candidateLength = ct.MaxLength;
                        if (candidateLength > maxLength)
                        {
                            maxLength = candidateLength;
                        }
                    }
                }

                // If all children are null, maxLength is 0, so 0 + 1 = 1 is correct.
                // Otherwise, the max length is 1 (the current position) + the max child length.
                return maxLength + 1;
            }
        }

        public bool Terminator
        {
            get
            {
                bool term = _terminator;

                return term;
            }
        }

        public CharTrie GetChildForChar(char ch)
        {
            int index = CharToIndex(ch);
            CharTrie ct = _children[index];

            return ct;
        }

        public void Add(string str)
        {
            CharTrie current = this;
            for (int curPos = 0; curPos < str.Length; ++curPos)
            {
                char ch = str[curPos];
                int index = CharToIndex(ch);
                CharTrie child = current._children[index];

                if (child == null)
                {
                    child = new CharTrie(ch, false);
                    current._children[index] = child;
                }

                if (str.Length - 1 == curPos)
                {
                    child.SetTerminator(true);
                    return;
                }

                current = child;
            }
        }

        public TrieContains Contains(char[] buffer, int length)
        {
            CharTrie ct = this;
            int curPos = 0;
            
            while (true)
            {
                if (curPos == length)
                {
                    if (ct._terminator)
                    {
                        return TrieContains.Contains;
                    }
                    return TrieContains.ValidPrefix;
                }

                char ch = buffer[curPos];
                ct = ct.GetChildForChar(ch);
                ++curPos;
                if (ct == null)
                {
                    return TrieContains.InvalidPrefix;
                }
            }
        }
    }
}
