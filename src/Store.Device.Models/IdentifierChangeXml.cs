using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Store.Device.Models
{
    public class IdentifierChangeParameters
    {
        public decimal? OwnershipId { get; set; }
        public decimal? DevConditionId { get; set; }
        public decimal? SosttechKod { get; set; }
    }

    public static class IdentifierChangeXml
    {
        public static string Build(decimal? storeDocumentId, IList<VerifyIdentifierChange> rows)
        {
            return Build(storeDocumentId, rows, null);
        }

        // parameters == null — смена идентификаторов: элементы параметров в XML не пишутся.
        public static string Build(decimal? storeDocumentId, IList<VerifyIdentifierChange> rows, IdentifierChangeParameters parameters)
        {
            var xml = new StringBuilder();
            xml.Append("<ArrayOfIdentifierChange>");
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    if (row == null || !row.ChBoxS || row.VerifyStatusNum == -1)
                        continue;

                    xml.Append("<IdentifierChange>");
                    Append(xml, "store_document_id", NumberText(storeDocumentId));
                    Append(xml, "line_no", NumberText(row.LineNo));
                    Append(xml, "serial_number", row.SerialNumber);
                    Append(xml, "mac_address", row.MacAddress);
                    Append(xml, "pon_serial", row.PonSerial);
                    Append(xml, "new_serial_number", row.NewSerialNumber);
                    Append(xml, "new_mac_address", row.NewMacAddress);
                    Append(xml, "new_pon_serial", row.NewPonSerial);
                    if (parameters != null)
                    {
                        Append(xml, "parameter_change", "1");
                        Append(xml, "ownership_id", NumberText(parameters.OwnershipId));
                        Append(xml, "dev_condition_id", NumberText(parameters.DevConditionId));
                        Append(xml, "sosttech_kod", NumberText(parameters.SosttechKod));
                    }
                    xml.Append("</IdentifierChange>");
                }
            }

            xml.Append("</ArrayOfIdentifierChange>");
            return xml.ToString();
        }

        private static string NumberText(decimal? value)
        {
            if (!value.HasValue)
                return "";

            return value.Value.ToString("0.################", CultureInfo.InvariantCulture);
        }

        private static void Append(StringBuilder xml, string name, string value)
        {
            xml.Append('<').Append(name).Append('>');
            xml.Append(Escape(value));
            xml.Append("</").Append(name).Append('>');
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value.Replace("&", "&amp;")
                        .Replace("<", "&lt;")
                        .Replace(">", "&gt;")
                        .Replace("\"", "&quot;");
        }
    }
}
