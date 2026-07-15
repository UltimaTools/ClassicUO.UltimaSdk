using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ultima
{
    public sealed class StringEntry
    {
        [Flags]
        public enum CliLocFlag
        {
            Original = 0x0,
            Custom = 0x1,
            Modified = 0x2
        }

        private string _text;
        private string _fmtTxt;
        private static readonly Regex _regEx = new Regex(
            @"~(\d+)[_\w]+~",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
        private static readonly object[] _args = new object[11];

        public int Number { get; }

        public string Text
        {
            get => _text;
            set => _text = value ?? string.Empty;
        }

        public CliLocFlag Flag { get; set; }

        internal StringEntry(int number, string text)
        {
            Number = number;
            _text = text ?? "";
            Flag = CliLocFlag.Original;
        }

        public StringEntry(int number, string text, byte flag)
        {
            Number = number;
            _text = text ?? "";
            Flag = (CliLocFlag)flag;
        }

        public StringEntry(int number, string text, CliLocFlag flag)
        {
            Number = number;
            _text = text ?? "";
            Flag = flag;
        }

        private string GetFormatString()
        {
            if (_fmtTxt == null)
                _fmtTxt = _regEx.Replace(_text, "{$1}");
            return _fmtTxt;
        }

        public string Format(params object[] args)
        {
            if (args == null || args.Length == 0)
                return _text;
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
                return _text;
            }
        }

        public string SplitFormat(string argString)
        {
            if (string.IsNullOrEmpty(argString))
                return _text;
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
                return _text;
            }
        }
    }

    public class StringList
    {
        private readonly Dictionary<int, StringEntry> _entries = new Dictionary<int, StringEntry>();
        private readonly List<StringEntry> _entryList = new List<StringEntry>();

        public string Language { get; }

        public List<StringEntry> Entries => _entryList;

        public StringList(string language, bool decompress)
        {
            Language = language;
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

        public StringList(string language, string path, bool decompress)
        {
            Language = language;
            LoadFromPath(path);
        }

        private void LoadFromPath(string path)
        {
            var loader = Files.Manager?.Clilocs;
            if (loader == null && string.IsNullOrEmpty(path)) return;

            try
            {
                var all = loader?.AllEntries;
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

        public void SetEntry(int number, string text)
        {
            if (_entries.TryGetValue(number, out var entry))
                entry.Text = text;
        }

        public void SetEntry(int number, string text, StringEntry.CliLocFlag flag)
        {
            if (_entries.TryGetValue(number, out var entry))
            {
                entry.Text = text;
                entry.Flag = flag;
            }
        }

        public void SaveStringList(string path)
        {
            // Not implemented
        }

        public class NumberComparer : IComparer<StringEntry>
        {
            private readonly bool _sortDescending;

            public NumberComparer(bool sortDescending)
            {
                _sortDescending = sortDescending;
            }

            public int Compare(StringEntry x, StringEntry y)
            {
                if (x.Number == y.Number)
                    return 0;
                return _sortDescending
                    ? (x.Number < y.Number ? 1 : -1)
                    : (x.Number < y.Number ? -1 : 1);
            }
        }

        public class TextComparer : IComparer<StringEntry>
        {
            private readonly bool _sortDescending;

            public TextComparer(bool sortDescending)
            {
                _sortDescending = sortDescending;
            }

            public int Compare(StringEntry x, StringEntry y)
            {
                return _sortDescending
                    ? string.CompareOrdinal(y.Text, x.Text)
                    : string.CompareOrdinal(x.Text, y.Text);
            }
        }

        public class FlagComparer : IComparer<StringEntry>
        {
            private readonly bool _sortDescending;

            public FlagComparer(bool sortDescending)
            {
                _sortDescending = sortDescending;
            }

            public int Compare(StringEntry x, StringEntry y)
            {
                if ((byte)x.Flag == (byte)y.Flag)
                {
                    if (x.Number == y.Number)
                        return 0;
                    return _sortDescending
                        ? (x.Number < y.Number ? 1 : -1)
                        : (x.Number < y.Number ? -1 : 1);
                }

                return _sortDescending
                    ? ((byte)x.Flag < (byte)y.Flag ? 1 : -1)
                    : ((byte)x.Flag < (byte)y.Flag ? -1 : 1);
            }
        }
    }
}
