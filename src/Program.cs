

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WordSearcher
{
    internal class Program
    {
        private class Result
        {
            public Thread t;
            public StringHash words;
        }

        private static readonly string s_fileName = "english.txt";

        private static void Main(string[] args)
        {
            DateTime startTime = DateTime.Now;
            if (args.Length < 1)
            {
                Console.WriteLine("Provide input file.");
                return;
            }

            if (args[0].Equals("generate", StringComparison.OrdinalIgnoreCase))
            {
                int r;
                int c;
                if (args.Length < 3 || !Int32.TryParse(args[1], out r) || !Int32.TryParse(args[2], out c))
                {
                    GenerateRandomChars(10000, 10000);
                }
                else
                {
                    GenerateRandomChars(r, c);
                }

                return;
            }

            if (args.Length != 1)
            {
                Console.WriteLine("Assuming first argument is an input file, ignoring all other arguments.");
            }

            CharTrie englishWords;
            if (!LoadWordList(out englishWords))
            {
                return;
            }

            int rowCount;
            int colCount;
            char[] wordSearch;
            if (!ReadFile(args[0], out rowCount, out colCount, out wordSearch))
            {
                return;
            }

            StringHash words = new StringHash();
            Stopwatch sw = new Stopwatch();
            sw.Start();

            List<Result> results = new List<Result>();

            // Originally I had it so that it divided by half of the number of processers.
            // Whatever we end up dividing by effectively creates a square though, which works for 4 procs
            // since 2+2==2^2. However, for larger numbers of processors it would create way too many threads.
            // I.e. for 16 processors, it would create 64 threads.
            //
            // It does have the downside of it only spawns threads in squares, i.e. 4, 9, 16, etc.
            int divisor = (int)Math.Ceiling(Math.Sqrt(Environment.ProcessorCount));
            int rowSection = rowCount / divisor;
            int startRow = 0;
            int endRow = rowSection;

            int colSection = colCount / divisor;

            while (true)
            {
                int startCol = 0;
                int endCol = colSection;

                while (true)
                {
                    StringHash threadWords = new StringHash();
                    Thread t = new Thread(
                                    new ThreadStart(
                                        () =>
                                            FindWords(englishWords, rowCount, colCount, wordSearch, threadWords, startRow, endRow, startCol, endCol)));
                    t.Start();
                    Console.WriteLine("Started thread startRow={0} endRow={1} startCol={2} endCol={3}", startRow, endRow, startCol, endCol);
                    results.Add(new Result()
                    {
                        t = t,
                        words = threadWords
                    });

                    if (endCol == colCount)
                    {
                        break;
                    }

                    startCol = endCol;
                    endCol += rowSection;
                    if (endCol > colCount)
                    {
                        endCol = colCount;
                    }
                }

                if (endRow == rowCount)
                {
                    break;
                }

                startRow = endRow;
                endRow += rowSection;
                if (endRow > rowCount)
                {
                    endRow = rowCount;
                }
            }

            for (int i = 0; i < results.Count; ++i)
            {
                Result result = results[i];
                result.t.Join();
                //Console.WriteLine("Thread {0} done, produced {1} results.", i, result.words.Count);
                words.AddRange(result.words);
                
                Console.WriteLine("After Union words.Count() = {0}", words.Count());
            }

            sw.Stop();

            Console.WriteLine("{0} words", words.Count());
            Console.WriteLine("Core search done in {0} seconds", TimeSpan.FromMilliseconds(sw.ElapsedMilliseconds).TotalSeconds);
            Console.WriteLine("Whole process took {0} seconds", DateTime.Now.Subtract(startTime).TotalSeconds);
            // Output before changing anything, still single threaded
            // 11392 words
            // Done in 171.026 seconds

            // After going multi threaded (4 threads)
            // Core search done in 37.122 seconds
            // Whole process took 40.0338292 seconds

            // After implementing custom hashset
            // 11393 words
            // Core search done in 22.135 seconds
            // Whole process took 24.9273619 seconds
        }

        private static bool LoadWordList(out CharTrie englishWords)
        {
            englishWords = new CharTrie();
            try
            {
                using (StreamReader wordList = new StreamReader(new FileStream(s_fileName, FileMode.Open, FileAccess.Read)))
                {
                    string line;
                    while ((line = wordList.ReadLine()) != null)
                    {
                        string trimmed = line.Trim().ToUpper();
                        englishWords.Add(trimmed);
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool ReadFile(string fileName, out int rowCount, out int colCount, out char[] wordSearch)
        {
            wordSearch = null;
            rowCount = 0;
            colCount = 0;
            try
            {
                using (StreamReader input = new StreamReader(new FileStream(fileName, FileMode.Open)))
                {
                    string line = input.ReadLine();
                    if (line == null)
                    {
                        return false;
                    }

                    string[] nums = line.Split(',');
                    if (nums.Length != 2)
                    {
                        return false;
                    }

                    rowCount = Int32.Parse(nums[0]);
                    colCount = Int32.Parse(nums[1]);

                    int numChars = rowCount * colCount;
                    wordSearch = new char[numChars];

                    const int bufferSize = 4096;
                    char[] buffer = new char[bufferSize];
                    int nonWSChars = 0;
                    while (nonWSChars < numChars)
                    {
                        int read = input.Read(buffer, 0, bufferSize);
                        for (int i = 0; i < read; ++i)
                        {
                            char ch = buffer[i];
                            if (Char.IsWhiteSpace(ch))
                            {
                                continue;
                            }

                            wordSearch[nonWSChars] = buffer[i];
                            ++nonWSChars;
                        }

                        if (read == 0 && nonWSChars < numChars)
                        {
                            Console.WriteLine("Invalid word search provided.");
                            wordSearch = null;
                            rowCount = 0;
                            colCount = 0;
                            return false;
                        }
                    }

                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static unsafe void FindWords(CharTrie englishWords, int rowCount, int colCount, char[] wordSearch, StringHash words, int startRow, int stopRow, int startCol, int stopCol)
        {
            char[] buffer = new char[englishWords.MaxLength];

            int bufLen = buffer.Length;
            fixed (char* buf = buffer)
            {
                fixed (char* search = wordSearch)
                {
                    for (int currentRow = startRow; currentRow < stopRow; ++currentRow)
                    {
                        for (int currentCol = startCol; currentCol < stopCol; ++currentCol)
                        {
                            FindUpperLeftWords(englishWords, rowCount, colCount, search, words, buf, currentRow, currentCol);

                            FindUpperWords(englishWords, rowCount, colCount, search, words, buf, currentRow, currentCol);

                            FindUpperRightWords(englishWords, rowCount, colCount, search, words, buf, currentRow, currentCol);

                            FindLeftWords(englishWords, rowCount, colCount, search, words, buf, currentRow, currentCol);

                            FindRightWords(englishWords, rowCount, colCount, search, words, buf, currentRow, currentCol);

                            FindLowerLeftWords(englishWords, rowCount, colCount, search, words, buf, currentRow, currentCol);

                            FindLowerWords(englishWords, rowCount, colCount, search, words, buf, currentRow, currentCol);

                            FindLowerRightWords(englishWords, rowCount, colCount, search, words, buf, currentRow, currentCol);
                        }
                    }
                }
            }
        }

        private static unsafe void FindLowerRightWords(CharTrie englishWords, int rowCount, int colCount, char* wordSearch, StringHash words, char* buffer, int currentRow, int currentCol)
        {
            int curPos = 0;
            int row = currentRow;
            int col = currentCol;
            CharTrie ct = englishWords;
            while (row < rowCount && col < colCount)
            {
                buffer[curPos] = wordSearch[(row * colCount) + col];

                ct = ct.GetChildForChar(buffer[curPos]);
                if (ct == null)
                {
                    break;
                }

                ++curPos;
                if (ct.Terminator)
                {
                    if (!words.Contains(buffer, curPos))
                    {
                        words.Add(buffer, curPos);
                    }
                }

                ++row;
                ++col;
            }
        }

        private static unsafe void FindLowerWords(CharTrie englishWords, int rowCount, int colCount, char* wordSearch, StringHash words, char* buffer, int currentRow, int currentCol)
        {
            int curPos = 0;
            int row = currentRow;
            int col = currentCol;
            CharTrie ct = englishWords;
            while (row < rowCount)
            {
                buffer[curPos] = wordSearch[(row * colCount) + col];

                ct = ct.GetChildForChar(buffer[curPos]);
                if (ct == null)
                {
                    break;
                }

                ++curPos;
                if (ct.Terminator)
                {
                    if (!words.Contains(buffer, curPos))
                    {
                        words.Add(buffer, curPos);
                    }
                }

                ++row;
            }
        }

        private static unsafe void FindLowerLeftWords(CharTrie englishWords, int rowCount, int colCount, char* wordSearch, StringHash words, char* buffer, int currentRow, int currentCol)
        {
            int curPos = 0;
            int row = currentRow;
            int col = currentCol;
            CharTrie ct = englishWords;
            while (row < rowCount && col >= 0)
            {
                buffer[curPos] = wordSearch[(row * colCount) + col];

                ct = ct.GetChildForChar(buffer[curPos]);
                if (ct == null)
                {
                    break;
                }

                ++curPos;
                if (ct.Terminator)
                {
                    if (!words.Contains(buffer, curPos))
                    {
                        words.Add(buffer, curPos);
                    }
                }

                ++row;
                --col;
            }
        }

        private static unsafe void FindRightWords(CharTrie englishWords, int rowCount, int colCount, char* wordSearch, StringHash words, char* buffer, int currentRow, int currentCol)
        {
            int curPos = 0;
            int row = currentRow;
            int col = currentCol;
            CharTrie ct = englishWords;
            while (col < colCount)
            {
                buffer[curPos] = wordSearch[(row * colCount) + col];

                ct = ct.GetChildForChar(buffer[curPos]);
                if (ct == null)
                {
                    break;
                }

                ++curPos;
                if (ct.Terminator)
                {
                    if (!words.Contains(buffer, curPos))
                    {
                        words.Add(buffer, curPos);
                    }
                }

                ++col;
            }
        }

        private static unsafe void FindLeftWords(CharTrie englishWords, int rowCount, int colCount, char* wordSearch, StringHash words, char* buffer, int currentRow, int currentCol)
        {
            int curPos = 0;
            int row = currentRow;
            int col = currentCol;
            CharTrie ct = englishWords;
            while (col >= 0)
            {
                buffer[curPos] = wordSearch[(row * colCount) + col];

                ct = ct.GetChildForChar(buffer[curPos]);
                if (ct == null)
                {
                    break;
                }

                ++curPos;
                if (ct.Terminator)
                {
                    if (!words.Contains(buffer, curPos))
                    {
                        words.Add(buffer, curPos);
                    }
                }

                --col;
            }
        }

        private static unsafe void FindUpperRightWords(CharTrie englishWords, int rowCount, int colCount, char* wordSearch, StringHash words, char* buffer, int currentRow, int currentCol)
        {
            int curPos = 0;
            int row = currentRow;
            int col = currentCol;
            CharTrie ct = englishWords;
            while (row >= 0 && col < colCount)
            {
                buffer[curPos] = wordSearch[(row * colCount) + col];

                ct = ct.GetChildForChar(buffer[curPos]);
                if (ct == null)
                {
                    break;
                }

                ++curPos;
                if (ct.Terminator)
                {
                    if (!words.Contains(buffer, curPos))
                    {
                        words.Add(buffer, curPos);
                    }
                }

                --row;
                ++col;
            }
        }

        private static unsafe void FindUpperWords(CharTrie englishWords, int rowCount, int colCount, char* wordSearch, StringHash words, char* buffer, int currentRow, int currentCol)
        {
            int curPos = 0;
            int row = currentRow;
            int col = currentCol;
            CharTrie ct = englishWords;
            while (row >= 0)
            {
                buffer[curPos] = wordSearch[(row * colCount) + col];

                ct = ct.GetChildForChar(buffer[curPos]);
                if (ct == null)
                {
                    break;
                }

                ++curPos;
                if (ct.Terminator)
                {
                    if (!words.Contains(buffer, curPos))
                    {
                        words.Add(buffer, curPos);
                    }
                }

                --row;
            }
        }

        private static unsafe void FindUpperLeftWords(CharTrie englishWords, int rowCount, int colCount, char* wordSearch, StringHash words, char* buffer, int currentRow, int currentCol)
        {
            int curPos = 0;
            int row = currentRow;
            int col = currentCol;
            CharTrie ct = englishWords;
            while (row >= 0 && col >= 0)
            {
                buffer[curPos] = wordSearch[(row * colCount) + col];

                ct = ct.GetChildForChar(buffer[curPos]);
                if (ct == null)
                {
                    break;
                }

                ++curPos;
                if (ct.Terminator)
                {
                    if (!words.Contains(buffer, curPos))
                    {
                        words.Add(buffer, curPos);
                    }
                }

                --row;
                --col;
            }
        }

        private static void GenerateRandomChars(int width, int height)
        {
            using (var file = new StreamWriter(new FileStream("input.txt", FileMode.Create, FileAccess.ReadWrite)))
            {
                char[] buffer = new char[4096];
                int pos = 0;

                file.WriteLine(String.Format("{0},{1}", width, height));
                Random random = new Random();

                for (int i = 0; i < height; ++i)
                {
                    for (int j = 0; j < width; ++j)
                    {
                        char ch = Convert.ToChar(Convert.ToInt32(Math.Floor(26 * random.NextDouble() + 65)));
                        buffer[pos] = ch;
                        ++pos;
                        if (pos >= buffer.Length)
                        {
                            file.Write(buffer);
                            pos = 0;
                        }
                    }

                    // Corner case, there isn't enough room in the buffer to write a newline.
                    // the + 1 is so we don't end in the scenario where the buffer is completely full
                    // after adding the newline since the loop above expects there to be a space at the top
                    // of the loop.
                    if (pos > (buffer.Length - (Environment.NewLine.Length + 1)))
                    {
                        file.Write(buffer, 0, pos);
                        pos = 0;
                    }

                    // Write the newline to the buffer
                    for (int index = 0; index < Environment.NewLine.Length; ++index)
                    {
                        buffer[pos] = Environment.NewLine[index];
                        ++pos;
                    }
                }

                if (pos != 0)
                {
                    // There is still data left to write because we didn't happen to write a multiple 
                    // of the buffer length
                    file.Write(buffer, 0, pos);
                    pos = 0;
                }

                file.Flush();
            }
        }
    }
}
