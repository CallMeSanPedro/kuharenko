namespace Store.Device.Models
{
    public class IdentifierChangeRow
    {
        public int LineNumber { get; set; }
        public string SerialNumberCurrent { get; set; }
        public string MacCurrent { get; set; }
        public string PonCurrent { get; set; }
        public string SerialNumberNew { get; set; }
        public string MacNew { get; set; }
        public string PonNew { get; set; }
        public string Error { get; set; }
        public bool ChBoxS { get; set; }
        public bool CanApply { get; set; }
        public decimal? CommId { get; set; }
        public string CardMacAddress { get; set; }
        public string CardPonSerial { get; set; }
        public decimal? VerifyStatusNum { get; set; }
        public string VerifyStatusStr { get; set; }
    }
}
