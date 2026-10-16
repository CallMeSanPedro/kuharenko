using System.ComponentModel.DataAnnotations;

namespace Store.Device.Models
{
    public class VerifyIdentifierChangeEntity
    {
        public decimal? StoreDocumentId { get; set; }
        public decimal? LineNo { get; set; }
        public decimal? CommId { get; set; }

        [Display(Name = "Серийный номер", ShortName = "Серийный номер", Description = "Серийный номер")]
        public string SerialNumber { get; set; }

        [Display(Name = "MAC адрес", ShortName = "MAC адрес", Description = "MAC адрес")]
        public string MacAddress { get; set; }

        [Display(Name = "PON номер", ShortName = "PON номер", Description = "PON номер")]
        public string PonSerial { get; set; }

        [Display(Name = "Новый серийный номер", ShortName = "Новый серийный номер", Description = "Новый серийный номер")]
        public string NewSerialNumber { get; set; }

        [Display(Name = "Новый MAC", ShortName = "Новый MAC", Description = "Новый MAC")]
        public string NewMacAddress { get; set; }

        [Display(Name = "Новый PON", ShortName = "Новый PON", Description = "Новый PON")]
        public string NewPonSerial { get; set; }

        [Display(Name = "MAC на карточке", ShortName = "MAC на карточке", Description = "MAC на карточке")]
        public string CardMacAddress { get; set; }

        [Display(Name = "PON на карточке", ShortName = "PON на карточке", Description = "PON на карточке")]
        public string CardPonSerial { get; set; }

        public decimal? FilialKod { get; set; }
        public decimal? VerifyStatusNum { get; set; }

        [Display(Name = "Результат проверки", ShortName = "Результат проверки", Description = "Результат проверки")]
        public string VerifyStatusStr { get; set; }

        public bool IsSuccess
        {
            get { return VerifyStatusNum == 1; }
        }
    }
}
