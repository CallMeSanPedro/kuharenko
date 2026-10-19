using DatumNode.Models.StoreCpe.Store.Device;
using System.ComponentModel.DataAnnotations;

namespace Store.Device.Models
{
    public class VerifyIdentifierChange : VerifyIdentifierChangeEntity
    {
        private bool _chBoxS;

        public bool ChBoxS
        {
            get { return _chBoxS; }
            set { SetValue(ref _chBoxS, value, () => ChBoxS); }
        }

        [Display(Name = "", Description = "Выбор строки для загрузки")]
        public bool CanAddBool
        {
            get { return VerifyStatusNum != -1; }
        }

        public bool IsSuccess
        {
            get { return VerifyStatusNum == 1; }
        }

        public override decimal? StoreDocumentId { get; set; }

        [Display(Name = "Строка", Description = "Строка")]
        public override decimal? LineNo { get; set; }

        [Display(Name = "Серийный номер", ShortName = "Серийный номер", Description = "Серийный номер")]
        public override string SerialNumber { get; set; }

        [Display(Name = "MAC адрес", ShortName = "MAC адрес", Description = "MAC адрес")]
        public override string MacAddress { get; set; }

        [Display(Name = "PON номер", ShortName = "PON номер", Description = "PON номер")]
        public override string PonSerial { get; set; }

        [Display(Name = "Новый серийный номер", ShortName = "Новый серийный номер", Description = "Новый серийный номер")]
        public override string NewSerialNumber { get; set; }

        [Display(Name = "Новый MAC", ShortName = "Новый MAC", Description = "Новый MAC")]
        public override string NewMacAddress { get; set; }

        [Display(Name = "Новый PON", ShortName = "Новый PON", Description = "Новый PON")]
        public override string NewPonSerial { get; set; }

        [Display(Name = "Карточка", ShortName = "Карточка", Description = "Карточка")]
        public override decimal? CommId { get; set; }

        [Display(Name = "MAC на карточке", ShortName = "MAC на карточке", Description = "MAC на карточке")]
        public override string CardMacAddress { get; set; }

        [Display(Name = "PON на карточке", ShortName = "PON на карточке", Description = "PON на карточке")]
        public override string CardPonSerial { get; set; }

        public override decimal? FilialKod { get; set; }

        public override decimal? VerifyStatusNum { get; set; }

        [Display(Name = "Результат проверки", ShortName = "Результат проверки", Description = "Результат проверки")]
        public override string VerifyStatusStr { get; set; }
    }
}
