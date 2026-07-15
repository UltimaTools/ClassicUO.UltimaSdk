using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ultima
{
    public sealed class StringEntry
    {
        private static readonly Regex _regEx = new Regex(
            @"~(\d+)[_\w]+~",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private string _fmtTxt;
        private static readonly object[] _args = new object[11];

        public int Number { get; }
        public string Text { get; }

        internal StringEntry(int number, string text)
        {
            Number = number;
            Text = text ?? "";
        }

        private string GetFormatString()
        {
            if (_fmtTxt == null)
                _fmtTxt = _regEx.Replace(Text, "{$1}");
            return _fmtTxt;
        }

        public string Format(params object[] args)
        {
            if (args == null || args.Length == 0)
                return Text;
            try
            {
                lock (_args)
                {
                    for (int i = 0; i < _args.Length; i++)
                        _args[i] = "";
                    for (int i = 0; i < args.Length && i < 10; i++)
                        _args[i + 1] = args[i];
                    return string.Format(GetFormatString(), _args);
                }
            }
            catch
            {
                return Text;
            }
        }

        public string SplitFormat(string argString)
        {
            if (string.IsNullOrEmpty(argString))
                return Text;
            try
            {
                lock (_args)
                {
                    for (int i = 0; i < _args.Length; i++)
                        _args[i] = "";
                    var split = argString.Split('\t');
                    for (int i = 0; i < split.Length && i < 10; i++)
                        _args[i + 1] = split[i];
                    return string.Format(GetFormatString(), _args);
                }
            }
            catch
            {
                return Text;
            }
        }
    }

    public class StringList
    {
        private readonly Dictionary<int, StringEntry> _entries = new Dictionary<int, StringEntry>();
        private readonly List<StringEntry> _entryList = new List<StringEntry>();

        public List<StringEntry> Entries => _entryList;

        public StringList(string language, bool decompress)
        {
            var loader = Files.Manager?.Clilocs;
            if (loader == null) return;

            try
            {
                var all = loader.AllEntries;
                if (all != null && all.Count > 0)
                {
                    foreach (var kvp in all)
                    {
                        var entry = new StringEntry(kvp.Key, kvp.Value);
                        _entries[kvp.Key] = entry;
                        _entryList.Add(entry);
                    }
                }
            }
            catch
            {
            }
        }

        public string GetString(int number)
        {
            if (_entries.TryGetValue(number, out var entry))
                return entry.Text;
            return null;
        }

        public StringEntry GetEntry(int number)
        {
            _entries.TryGetValue(number, out var entry);
            return entry;
        }
    }
}
