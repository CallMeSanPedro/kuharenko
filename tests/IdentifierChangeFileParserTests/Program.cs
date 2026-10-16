using System;
using System.Collections.Generic;
using System.IO;
using Store.Device.Models;

class Program
{
    const string MacPattern = "^[0-9A-F]{12}$";

    static int Main()
    {
        HeaderAndOneChange();
        ReorderedHeader();
        EmptyNewMeansNoChange();
        RejectsEmptyCurrentAndEmptyNew();
        RejectsBadMacAndKeepsRow();
        MarksDuplicateCurrentSerialAndNewMac();
        SkipsBlankLinesAndTitleWhenRequested();
        DetectsHeaderWithoutSkipFlag();
        MissingSerialColumnThrows();
        PositionalRowWithoutHeader();
        QuotedCellsAndBom();
        Console.WriteLine("ok");
        return 0;
    }

    static void HeaderAndOneChange()
    {
        var rows = Parse(
            "SN_текущий;MAC_текущий;PON_текущий;SN_новый;MAC_новый;PON_новый\r\n" +
            "OLD1;AABBCCDDEEFF;PON1;NEW1;;\r\n",
            false);

        Expect(rows.Count == 1, "one data row");
        Expect(rows[0].SerialNumberCurrent == "OLD1", "current sn");
        Expect(rows[0].MacCurrent == "AABBCCDDEEFF", "current mac");
        Expect(rows[0].PonCurrent == "PON1", "current pon");
        Expect(rows[0].SerialNumberNew == "NEW1", "new sn");
        Expect(rows[0].MacNew == "", "empty new mac");
        Expect(rows[0].PonNew == "", "empty new pon");
        Expect(rows[0].CanApply, "row applies");
        Expect(rows[0].ChBoxS, "row checked");
        Expect(rows[0].LineNumber == 2, "line number");
    }

    static void ReorderedHeader()
    {
        var rows = Parse("PON_новый;SN_текущий;MAC_новый\nPON9;OLD9;AABBCCDDEEFF\n", false);
        Expect(rows.Count == 1, "reordered row");
        Expect(rows[0].SerialNumberCurrent == "OLD9", "reordered sn");
        Expect(rows[0].MacNew == "AABBCCDDEEFF", "reordered mac");
        Expect(rows[0].PonNew == "PON9", "reordered pon");
        Expect(rows[0].MacCurrent == "" && rows[0].PonCurrent == "" && rows[0].SerialNumberNew == "", "missing columns empty");
        Expect(rows[0].CanApply, "reordered applies");
    }

    static void EmptyNewMeansNoChange()
    {
        var rows = Parse("SN_текущий;MAC_текущий;PON_текущий;SN_новый;MAC_новый;PON_новый\nA;AABBCCDDEEFF;P1;;;P2\n", false);
        Expect(rows[0].SerialNumberNew == "" && rows[0].MacNew == "" && rows[0].PonNew == "P2", "only pon changes");
        Expect(rows[0].CanApply, "partial change applies");
    }

    static void RejectsEmptyCurrentAndEmptyNew()
    {
        var rows = Parse("SN_текущий;MAC_текущий;PON_текущий;SN_новый;MAC_новый;PON_новый\n;;; ; ;\n", false);
        Expect(rows.Count == 1, "invalid row kept");
        Expect(!rows[0].CanApply && !rows[0].ChBoxS, "invalid unchecked");
        Expect(rows[0].Error.Contains("Не заполнен SN_текущий"), "missing sn");
        Expect(rows[0].Error.Contains("Не задано ни одного нового значения"), "missing new");
    }

    static void RejectsBadMacAndKeepsRow()
    {
        var rows = Parse("SN_текущий;MAC_текущий;PON_текущий;SN_новый;MAC_новый;PON_новый\nA;ZZ;;NEW;;\n", false);
        Expect(!rows[0].CanApply, "bad mac blocked");
        Expect(rows[0].Error.Contains("Некорректный MAC_текущий"), "bad current mac");
        Expect(rows[0].SerialNumberNew == "NEW", "value preserved");
    }

    static void MarksDuplicateCurrentSerialAndNewMac()
    {
        var text =
            "SN_текущий;SN_новый;MAC_новый\n" +
            "A;N1;AABBCCDDEEFF\n" +
            "A;N2;AABBCCDDEEFF\n" +
            "B;N1;112233445566\n";
        var rows = Parse(text, false);
        Expect(rows.Count == 3, "three rows");
        Expect(rows[0].Error.Contains("Повторяющийся SN_текущий"), "dup current on first");
        Expect(rows[1].Error.Contains("Повторяющийся SN_текущий"), "dup current on second");
        Expect(rows[0].Error.Contains("Повторяющийся MAC_новый"), "dup mac on first");
        Expect(rows[1].Error.Contains("Повторяющийся MAC_новый"), "dup mac on second");
        Expect(rows[0].Error.Contains("Повторяющийся SN_новый"), "dup new sn on first");
        Expect(rows[1].Error == null || rows[1].Error.Contains("Повторяющийся SN_новый") == false, "second new sn is unique");
        Expect(rows[2].Error.Contains("Повторяющийся SN_новый"), "dup new sn on third");
        Expect(!rows[0].CanApply && !rows[1].CanApply && !rows[2].CanApply, "duplicates blocked");
    }

    static void SkipsBlankLinesAndTitleWhenRequested()
    {
        var rows = Parse("список\n\nOLD;AABBCCDDEEFF;P;; ;NEWP\n", true);
        Expect(rows.Count == 1, "title skipped");
        Expect(rows[0].SerialNumberCurrent == "OLD", "positional sn");
        Expect(rows[0].PonNew == "NEWP", "positional new pon");
        Expect(rows[0].CanApply, "positional applies");
    }

    static void DetectsHeaderWithoutSkipFlag()
    {
        var rows = Parse("SN_текущий;SN_новый\nOLD;NEW\n", false);
        Expect(rows.Count == 1 && rows[0].SerialNumberCurrent == "OLD" && rows[0].SerialNumberNew == "NEW", "header autodetect");
    }

    static void MissingSerialColumnThrows()
    {
        try
        {
            Parse("MAC_новый;PON_новый\nAABBCCDDEEFF;P\n", false);
            throw new Exception("expected missing column");
        }
        catch (InvalidDataException)
        {
        }
    }

    static void PositionalRowWithoutHeader()
    {
        var rows = Parse("OLD;AABBCCDDEEFF;P1;NEW;112233445566;P2\n", false);
        Expect(rows[0].SerialNumberCurrent == "OLD" && rows[0].MacNew == "112233445566" && rows[0].PonNew == "P2", "positional");
        Expect(rows[0].CanApply, "positional valid");
    }

    static void QuotedCellsAndBom()
    {
        var rows = Parse("\uFEFF\"SN_текущий\";\"MAC_текущий\";\"PON_текущий\";\"SN_новый\";\"MAC_новый\";\"PON_новый\"\n\"OLD 1\";\"AABBCCDDEEFF\";;\"NEW 1\";;\n", false);
        Expect(rows.Count == 1, "quoted row");
        Expect(rows[0].SerialNumberCurrent == "OLD 1" && rows[0].SerialNumberNew == "NEW 1", "quotes trimmed");
        Expect(rows[0].CanApply, "quoted applies");
    }

    static List<IdentifierChangeRow> Parse(string text, bool skipFirstRow)
    {
        using (var reader = new StringReader(text))
            return IdentifierChangeFileParser.Parse(reader, skipFirstRow, MacPattern);
    }

    static void Expect(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
