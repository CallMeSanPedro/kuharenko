using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Store.Device.Models
{
    public static class IdentifierChangeFileParser
    {
        public const string ColSn = "SN_текущий";
        public const string ColMac = "MAC_текущий";
        public const string ColPon = "PON_текущий";
        public const string ColSnNew = "SN_новый";
        public const string ColMacNew = "MAC_новый";
        public const string ColPonNew = "PON_новый";

        public static List<IdentifierChangeRow> Parse(TextReader reader, bool skipFirstRow, string macPattern)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            var rows = new List<IdentifierChangeRow>();
            int lineNumber = 0;
            bool columnsResolved = false;
            int iSn = 0;
            int iMac = 1;
            int iPon = 2;
            int iSnNew = 3;
            int iMacNew = 4;
            int iPonNew = 5;
            string line;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (line.Trim().Length == 0)
                    continue;

                string[] raw = line.Split(new char[] { ';', ',', '\t', '#' });
                if (!columnsResolved)
                {
                    columnsResolved = true;
                    if (IsHeader(raw))
                    {
                        MapHeader(raw, ref iSn, ref iMac, ref iPon, ref iSnNew, ref iMacNew, ref iPonNew);
                        continue;
                    }

                    if (skipFirstRow)
                        continue;
                }

                var row = new IdentifierChangeRow
                {
                    LineNumber = lineNumber,
                    SerialNumberCurrent = Cell(raw, iSn),
                    MacCurrent = Cell(raw, iMac),
                    PonCurrent = Cell(raw, iPon),
                    SerialNumberNew = Cell(raw, iSnNew),
                    MacNew = Cell(raw, iMacNew),
                    PonNew = Cell(raw, iPonNew)
                };
                ValidateRow(row, macPattern);
                rows.Add(row);
            }

            MarkDuplicates(rows, r => r.SerialNumberCurrent, "Повторяющийся SN_текущий");
            MarkDuplicates(rows, r => r.SerialNumberNew, "Повторяющийся SN_новый");
            MarkDuplicates(rows, r => r.MacNew, "Повторяющийся MAC_новый");

            foreach (var row in rows)
            {
                row.CanApply = string.IsNullOrEmpty(row.Error);
                row.ChBoxS = row.CanApply;
            }

            return rows;
        }

        private static void ValidateRow(IdentifierChangeRow row, string macPattern)
        {
            if (row.SerialNumberCurrent.Length == 0)
                AddError(row, "Не заполнен SN_текущий");

            if (row.SerialNumberNew.Length == 0 && row.MacNew.Length == 0 && row.PonNew.Length == 0)
                AddError(row, "Не задано ни одного нового значения");

            if (row.MacCurrent.Length > 0 && !IsValidMac(row.MacCurrent, macPattern))
                AddError(row, "Некорректный MAC_текущий");

            if (row.MacNew.Length > 0 && !IsValidMac(row.MacNew, macPattern))
                AddError(row, "Некорректный MAC_новый");
        }

        private static bool IsValidMac(string mac, string macPattern)
        {
            if (string.IsNullOrEmpty(macPattern))
                return true;

            return Regex.IsMatch(mac, macPattern) &&
                   Regex.Match(mac, macPattern).ToString().Length == mac.Length;
        }

        private static void MarkDuplicates(List<IdentifierChangeRow> rows, Func<IdentifierChangeRow, string> value, string message)
        {
            var groups = new Dictionary<string, List<IdentifierChangeRow>>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var key = value(row);
                if (string.IsNullOrEmpty(key))
                    continue;

                List<IdentifierChangeRow> bucket;
                if (!groups.TryGetValue(key, out bucket))
                {
                    bucket = new List<IdentifierChangeRow>();
                    groups.Add(key, bucket);
                }

                bucket.Add(row);
            }

            foreach (var bucket in groups.Values)
            {
                if (bucket.Count < 2)
                    continue;

                foreach (var row in bucket)
                    AddError(row, message);
            }
        }

        private static void AddError(IdentifierChangeRow row, string message)
        {
            row.Error = string.IsNullOrEmpty(row.Error) ? message : row.Error + "; " + message;
        }

        private static bool IsHeader(string[] raw)
        {
            foreach (var cell in raw)
            {
                var name = Clean(cell);
                if (string.Equals(name, ColSn, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, ColMac, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, ColPon, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, ColSnNew, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, ColMacNew, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, ColPonNew, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void MapHeader(string[] raw, ref int iSn, ref int iMac, ref int iPon, ref int iSnNew, ref int iMacNew, ref int iPonNew)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < raw.Length; i++)
            {
                var name = Clean(raw[i]);
                if (name.Length > 0 && !map.ContainsKey(name))
                    map.Add(name, i);
            }

            int sn;
            if (!map.TryGetValue(ColSn, out sn))
                throw new InvalidDataException("В файле нет колонки SN_текущий");

            iSn = sn;
            iMac = IndexOf(map, ColMac);
            iPon = IndexOf(map, ColPon);
            iSnNew = IndexOf(map, ColSnNew);
            iMacNew = IndexOf(map, ColMacNew);
            iPonNew = IndexOf(map, ColPonNew);
        }

        private static int IndexOf(Dictionary<string, int> map, string name)
        {
            int index;
            return map.TryGetValue(name, out index) ? index : -1;
        }

        private static string Cell(string[] raw, int index)
        {
            if (index < 0 || index >= raw.Length)
                return "";

            return Clean(raw[index]);
        }

        private static string Clean(string value)
        {
            if (value == null)
                return "";

            return value.Trim(' ', '\t', '"', '\uFEFF');
        }
    }
}
