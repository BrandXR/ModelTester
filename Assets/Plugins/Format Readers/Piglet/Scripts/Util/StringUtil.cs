using System;
using System.Collections.Generic;
using System.Linq;

namespace Piglet
{
    public class StringUtil
    {
        /// <summary>
        /// Generate a random alpha-numeric string with the requested
        /// length.
        ///
        /// Copied from: https://stackoverflow.com/a/1344258/12989671
        /// </summary>
        public static string GetRandomString(int length)
        {
            var validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var charArray = new char[length];

            // Note: We must supply a random seed argument here to prevent
            // duplicate results from being generated when this
            // method is called multiple times in rapid succession.
            // For further details, see:
            // https://stackoverflow.com/questions/1785744/how-do-i-seed-a-random-class-to-avoid-getting-duplicate-random-values
            var random = new Random(Guid.NewGuid().GetHashCode());

            for (int i = 0; i < charArray.Length; i++)
                charArray[i] = validChars[random.Next(validChars.Length)];

            return new string(charArray);
        }

        /// <summary>
        /// Given a candidate name, return a modified name that is unique with
        /// respect to the set of already-used names, by appending
        /// an underscore and numeric suffix. If the candidate name
        /// is already unique/unused, return it unmodified.
        /// </summary>
        public static string GetUniqueName(string candidate, HashSet<string> usedNames)
        {
            var result = candidate;
            for (var i = 2; usedNames.Contains(result); ++i)
                result = string.Format("{0}_{1}", candidate, i);
            return result;
        }

        /// <summary>
        /// Wrap text across multiple lines (i.e. "word wrap"), so
        /// that no line exceeds the given maximum line length.
        /// Whenever possible, break lines at spaces between words,
        /// so that individual words are not broken. In the case where
        /// a word is longer than the maximum line length,
        /// split the word at the end of the line and add a backslash
        /// character ('\') to indicate the position of the
        /// word break.
        /// </summary>
        public static string WrapText(string text, int maxLineLength)
        {
            if (text == null)
                return null;

            var inputLines = text.Split('\n');
            var outputLines = new List<string>();

            foreach (var inputLine in inputLines)
            {
                if (inputLine.Length == 0)
                {
                    outputLines.Add("");
                    continue;
                }

                var words = new Stack<string>(inputLine.Split(' ').Reverse());

                string outputLine = "";
                while (words.Count > 0)
                {
                    var word = words.Pop();

                    // collapse consecutive spaces
                    if (word.Length == 0)
                        continue;

                    // the number of characters that would be added
                    // to the current line, if we appended the current word
                    var appendLength = word.Length;

                    // if this is not the first word on the current line,
                    // add one character for the space character (' ')
                    if (outputLine.Length > 0)
                        appendLength++;

                    // if next word will not fit on current line
                    if (outputLine.Length + appendLength > maxLineLength)
                    {
                        // if current word is longer than maxLineLength,
                        // we need to split the word in the middle and
                        // use a backslash ('\') to indicate line continuation
                        if (word.Length > maxLineLength)
                        {
                            // we need at least 3 characters to append
                            // the first word fragment to the current line:
                            // (1) space (' ')
                            // (2) first character of current word
                            // (3) backslash ('\')
                            if (maxLineLength - outputLine.Length < 3)
                            {
                                outputLines.Add(outputLine);
                                outputLine = "";
                                words.Push(word);
                                continue;
                            }

                            // subtract 2 for space (' ') and backslash ('\') chars
                            var wordSplitPos = maxLineLength - outputLine.Length - 2;
                            words.Push(word.Substring(wordSplitPos));
                            words.Push(word.Substring(0, wordSplitPos) + '\\');
                            continue;
                        }

                        outputLines.Add(outputLine);
                        outputLine = "";
                        words.Push(word);
                        continue;
                    }

                    // the happy case: the word can be appended to the current line
                    // without exceeding maxLineLength

                    if (outputLine.Length > 0)
                        outputLine += ' ';
                    outputLine += word;
                }

                // output final line, if non-empty
                if (outputLine.Length > 0)
                    outputLines.Add(outputLine);
            }

            return string.Join("\n", outputLines);
        }
    }
}