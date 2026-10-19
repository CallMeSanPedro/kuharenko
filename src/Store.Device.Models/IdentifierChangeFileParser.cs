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

        public static List<VerifyIdentifierChangeEntity> Parse(TextReader reader, bool skipFirstRow, string macPattern)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            var rows = new List<VerifyIdentifierChangeEntity>();
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

                var row = new VerifyIdentifierChangeEntity
                {
                    LineNo = lineNumber,
                    SerialNumber = Cell(raw, iSn),
                    MacAddress = Cell(raw, iMac),
                    PonSerial = Cell(raw, iPon),
                    NewSerialNumber = Cell(raw, iSnNew),
                    NewMacAddress = Cell(raw, iMacNew),
                    NewPonSerial = Cell(raw, iPonNew)
                };
                ValidateRow(row, macPattern);
                rows.Add(row);
            }

            MarkDuplicates(rows, row => row.SerialNumber, "Повторяющийся SN_текущий");
            MarkDuplicates(rows, row => row.NewSerialNumber, "Повторяющийся SN_новый");
            MarkDuplicates(rows, row => row.NewMacAddress, "Повторяющийся MAC_новый");
            return rows;
        }

        private static void ValidateRow(VerifyIdentifierChangeEntity row, string macPattern)
        {
            if ((row.SerialNumber ?? "").Length == 0)
                AddError(row, "Не заполнен SN_текущий");

            if ((row.NewSerialNumber ?? "").Length == 0 &&
                (row.NewMacAddress ?? "").Length == 0 &&
                (row.NewPonSerial ?? "").Length == 0)
                AddError(row, "Не задано ни одного нового значения");

            if ((row.MacAddress ?? "").Length > 0 && !IsValidMac(row.MacAddress, macPattern))
                AddError(row, "Некорректный MAC_текущий");

            if ((row.NewMacAddress ?? "").Length > 0 && !IsValidMac(row.NewMacAddress, macPattern))
                AddError(row, "Некорректный MAC_новый");
        }

        private static bool IsValidMac(string mac, string macPattern)
        {
            if (string.IsNullOrEmpty(macPattern))
                return true;

            return Regex.IsMatch(mac, macPattern) &&
                   Regex.Match(mac, macPattern).ToString().Length == mac.Length;
        }

        private static void MarkDuplicates(List<VerifyIdentifierChangeEntity> rows, Func<VerifyIdentifierChangeEntity, string> value, string message)
        {
            var groups = new Dictionary<string, List<VerifyIdentifierChangeEntity>>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var key = value(row);
                if (string.IsNullOrEmpty(key))
                    continue;

                List<VerifyIdentifierChangeEntity> bucket;
                if (!groups.TryGetValue(key, out bucket))
                {
                    bucket = new List<VerifyIdentifierChangeEntity>();
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

        private static void AddError(VerifyIdentifierChangeEntity row, string message)
        {
            row.VerifyStatusNum = -1;
            row.VerifyStatusStr = string.IsNullOrEmpty(row.VerifyStatusStr) ? message : row.VerifyStatusStr + "; " + message;
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
