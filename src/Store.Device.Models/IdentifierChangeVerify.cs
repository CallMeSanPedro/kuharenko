using System.Collections.Generic;

namespace Store.Device.Models
{
    public static class IdentifierChangeVerify
    {
        public static void Apply(IList<IdentifierChangeRow> rows, IList<VerifyIdentifierChangeEntity> results)
        {
            if (rows == null)
                return;

            var byLine = new Dictionary<int, VerifyIdentifierChangeEntity>();
            if (results != null)
            {
                foreach (var result in results)
                {
                    if (result == null || result.LineNo == null)
                        continue;

                    byLine[(int)result.LineNo.Value] = result;
                }
            }

            foreach (var row in rows)
            {
                if (row == null || !row.CanApply)
                    continue;

                VerifyIdentifierChangeEntity result;
                if (!byLine.TryGetValue(row.LineNumber, out result))
                {
                    Reject(row, "Нет ответа проверки");
                    continue;
                }

                row.CommId = result.CommId;
                row.CardMacAddress = result.CardMacAddress;
                row.CardPonSerial = result.CardPonSerial;
                row.VerifyStatusNum = result.VerifyStatusNum;
                row.VerifyStatusStr = result.VerifyStatusStr;

                if (!result.IsSuccess)
                    Reject(row, string.IsNullOrEmpty(result.VerifyStatusStr) ? "Проверка не пройдена" : result.VerifyStatusStr);
            }
        }

        private static void Reject(IdentifierChangeRow row, string message)
        {
            row.Error = message;
            row.VerifyStatusStr = message;
            row.VerifyStatusNum = -1;
            row.CanApply = false;
            row.ChBoxS = false;
        }
    }
}
