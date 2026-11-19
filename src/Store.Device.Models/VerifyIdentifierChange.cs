using DatumNode.Models.StoreCpe.Store.Device;
using Store.Device.Models.Properties;
using System.ComponentModel.DataAnnotations;

namespace Store.Device.Models
{
    public class VerifyIdentifierChange : VerifyIdentifierChangeEntity
    {
        private bool _chBoxS;

        [Display(AutoGenerateField = false)]
        public bool ChBoxS
        {
            get { return _chBoxS; }
            set { SetValue(ref _chBoxS, value, () => ChBoxS); }
        }

        [Display(ResourceType = typeof(Resources), Name = "", Description = "IdentifierChange_SelectRow")]
        public bool CanAddBool
        {
            get { return VerifyStatusNum != -1; }
        }

        [Display(AutoGenerateField = false)]
        public bool IsSuccess
        {
            get { return VerifyStatusNum == 1; }
        }

        public override decimal? StoreDocumentId { get; set; }

        [Display(ResourceType = typeof(Resources), Name = "IdentifierChange_Line", Description = "IdentifierChange_Line")]
        public override decimal? LineNo { get; set; }

        [Display(ResourceType = typeof(Resources), Name = "IdentifierChange_SerialNumber", ShortName = "IdentifierChange_SerialNumber", Description = "IdentifierChange_SerialNumber")]
        public override string SerialNumber { get; set; }

        [Display(ResourceType = typeof(Resources), Name = "IdentifierChange_MacAddress", ShortName = "IdentifierChange_MacAddress", Description = "IdentifierChange_MacAddress")]
        public override string MacAddress { get; set; }

        [Display(ResourceType = typeof(Resources), Name = "IdentifierChange_PonSerial", ShortName = "IdentifierChange_PonSerial", Description = "IdentifierChange_PonSerial")]
        public override string PonSerial { get; set; }

        [Display(ResourceType = typeof(Resources), Name = "IdentifierChange_NewSerialNumber", ShortName = "IdentifierChange_NewSerialNumber", Description = "IdentifierChange_NewSerialNumber")]
        public override string NewSerialNumber { get; set; }

        [Display(ResourceType = typeof(Resources), Name = "IdentifierChange_NewMac", ShortName = "IdentifierChange_NewMac", Description = "IdentifierChange_NewMac")]
        public override string NewMacAddress { get; set; }

        [Display(ResourceType = typeof(Resources), Name = "IdentifierChange_NewPon", ShortName = "IdentifierChange_NewPon", Description = "IdentifierChange_NewPon")]
        public override string NewPonSerial { get; set; }

        [Display(ResourceType = typeof(Resources), Name = "IdentifierChange_Card", ShortName = "IdentifierChange_Card", Description = "IdentifierChange_Card")]
        public override decimal? CommId { get; set; }

        [Display(ResourceType = typeof(Resources), Name = "IdentifierChange_CardMac", ShortName = "IdentifierChange_CardMac", Description = "IdentifierChange_CardMac")]
        public override string CardMacAddress { get; set; }

        [Display(ResourceType = typeof(Resources), Name = "IdentifierChange_CardPon", ShortName = "IdentifierChange_CardPon", Description = "IdentifierChange_CardPon")]
        public override string CardPonSerial { get; set; }

        public override decimal? FilialKod { get; set; }

        public override decimal? VerifyStatusNum { get; set; }

        [Display(ResourceType = typeof(Resources), Name = "IdentifierChange_VerifyResult", ShortName = "IdentifierChange_VerifyResult", Description = "IdentifierChange_VerifyResult")]
        public override string VerifyStatusStr { get; set; }
    }
}
