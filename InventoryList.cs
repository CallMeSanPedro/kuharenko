using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using DatumNode.Models;
using DatumNode.Models.StoreCpe.Store.Reports;
using Store.Models.Reference;

namespace Store.Models.Device
{
    public class InventoryList : GetInventoryListRepEntity, IExploitStatus
    {
     
        public override string ToString()
        {
            return base.MarkacommName;
        }

        [Display(Name = nameof(Store.Localization.Strings.InventoryList_Uzel), ShortName = nameof(Store.Localization.Strings.InventoryList_Uzel), Description = nameof(Store.Localization.Strings.InventoryList_Uzel), Order = 1, ResourceType = typeof(Store.Localization.Strings))]
        [DisplayFormat(ConvertEmptyStringToNull = true)]
        public override string Uzel
        {
            get => base.Uzel;
            set => base.Uzel = value;
        }



        [Display(Name = nameof(Store.Localization.Strings.InventoryList_ResponsibleName), ShortName = nameof(Store.Localization.Strings.InventoryList_ResponsibleName), Description = nameof(Store.Localization.Strings.InventoryList_ResponsibleName), Order = 2, ResourceType = typeof(Store.Localization.Strings))]
        [DisplayFormat(ConvertEmptyStringToNull = true)]
        public override string ResponsibleName
        {
            get { return base.ResponsibleName; }
            set { base.ResponsibleName = value; }
        }


        [Display(Name = nameof(Store.Localization.Strings.InventoryList_VendorName), ShortName = nameof(Store.Localization.Strings.InventoryList_VendorName), Description = nameof(Store.Localization.Strings.InventoryList_VendorName), Order = 3, ResourceType = typeof(Store.Localization.Strings))]
        [DisplayFormat(ConvertEmptyStringToNull = true)]
        public override string VendorName
        {
            get { return base.VendorName; }
            set { base.VendorName = value; }
        }


        [Display(Name = nameof(Store.Localization.Strings.InventoryList_MarkacommName), ShortName = nameof(Store.Localization.Strings.InventoryList_MarkacommName), Description = nameof(Store.Localization.Strings.InventoryList_MarkacommName), Order = 4, ResourceType = typeof(Store.Localization.Strings))]
        [DisplayFormat(ConvertEmptyStringToNull = true)]

        public override string MarkacommName
        {
            get { return base.MarkacommName; }
            set { base.MarkacommName = value; }
        }

        [Display(Name = nameof(Store.Localization.Strings.InventoryList_TypdeviceName), ShortName = nameof(Store.Localization.Strings.InventoryList_TypdeviceName), Description = nameof(Store.Localization.Strings.InventoryList_TypdeviceName), Order = 5, ResourceType = typeof(Store.Localization.Strings))]
        [DisplayFormat(ConvertEmptyStringToNull = true)]
        public override string TypdeviceName { get => base.TypdeviceName; set => base.TypdeviceName = value; }

        [Display(Name = nameof(Store.Localization.Strings.InventoryList_OebsNomenclatureCode), ShortName = nameof(Store.Localization.Strings.InventoryList_OebsNomenclatureCode), Description = nameof(Store.Localization.Strings.InventoryList_OebsNomenclatureCode), Order = 6, ResourceType = typeof(Store.Localization.Strings))]
        [DisplayFormat(ConvertEmptyStringToNull = true)]

        public override string OebsNomenclatureCode
        {
            get { return base.OebsNomenclatureCode; }
            set { base.OebsNomenclatureCode = value; }
        }


       // [Display(Name = "Сегмент", ShortName = "Сегмент", Description = "Сегмент", Order = 10)]
        [DisplayFormat(ConvertEmptyStringToNull = true)]
        public override string StoreSegmentName
        {
            get { return base.StoreSegmentName; }
            set { base.StoreSegmentName = value; }
        }

      
        [Display(Name = nameof(Store.Localization.Strings.InventoryList_KodTou), Description = nameof(Store.Localization.Strings.InventoryList_KodTou), Order = 3, ResourceType = typeof(Store.Localization.Strings))]
        [DisplayFormat(ConvertEmptyStringToNull = true)]
        //[Key]
        public override string KodTou
        {
            get { return base.KodTou; }
            set { base.KodTou = value; }
        }

        [Display(Name = nameof(Store.Localization.Strings.StoreDeviceHistory_ExploitStatusName), ShortName = nameof(Store.Localization.Strings.InventoryList_Status), Description = nameof(Store.Localization.Strings.StoreDeviceHistory_ExploitStatusName), Order = 7, ResourceType = typeof(Store.Localization.Strings))]
        [DisplayFormat(ConvertEmptyStringToNull = true)]
        public override string Status
        {
            get { return base.Status; }
            set { base.Status = value; }
        }

        [Display(Name = nameof(Store.Localization.Strings.Column_Condition), ShortName = nameof(Store.Localization.Strings.InventoryList_Condition), Description = nameof(Store.Localization.Strings.Column_Condition), Order = 8, ResourceType = typeof(Store.Localization.Strings))]
        [DisplayFormat(ConvertEmptyStringToNull = true)]
        public override string Condition
        {
            get { return base.Condition; }
            set { base.Condition = value; }
        }


        [Display(Name = nameof(Store.Localization.Strings.InventoryList_SosttechName), ShortName = nameof(Store.Localization.Strings.InventoryList_SosttechName), Description = nameof(Store.Localization.Strings.InventoryList_SosttechName), Order = 9, ResourceType = typeof(Store.Localization.Strings))]
        [DisplayFormat(ConvertEmptyStringToNull = true)]
        public override string SosttechName { get => base.SosttechName; set => base.SosttechName = value; }



        //[Display(Name = "Количество", ShortName = "Количество", Description = "Количество", Order = 10)]
        [DisplayFormat(ConvertEmptyStringToNull = true)]
        public override decimal? Count
        {
            get { return base.Count; }
            set { base.Count = value; }
        }


        //[Display(Name = "На складе", ShortName = "На складе", Description = "На складе")]
        [Display(Name = nameof(Store.Localization.Strings.InventoryList_CurrentCount), ShortName = nameof(Store.Localization.Strings.InventoryList_CurrentCount), Description = nameof(Store.Localization.Strings.InventoryList_CurrentCount), Order = 10, ResourceType = typeof(Store.Localization.Strings))]
        public string CurrentCount
        {
            // get { return DevOnStoreCount+""; }//(DevOnStoreCount + DevOnStoreBalance) + " (" + DevOnStoreCount + "/" + DevOnStoreBalance + ")"; }
            get { return DevOnStoreBalance == 0 ? Count.ToString(): (Count + DevOnStoreBalance) + " (" + Count + "/" + DevOnStoreBalance + ")"; }
        }

        public string ToolTipCurrentCount
        {
            get
            {
                string str = "По серийным номерам: " + Count + "\n";
                str = str + "По номенклатуре: " + DevOnStoreBalance + "\n";
                str = str + "Всего: " + (Count + DevOnStoreBalance);
                return str;
            }
        }

        public string ExploitStatusName
        {
            get { return base.Status; }
            set { base.Status = value; }
        }

        [Ignore]
        [Display(AutoGenerateField = false)]
        public ObservableCollection<StoreDevice> SerialDevices { get; } = new ObservableCollection<StoreDevice>();

        private bool _isLoadingSerials;
        [Ignore]
        [Display(AutoGenerateField = false)]
        public bool IsLoadingSerials
        {
            get { return _isLoadingSerials; }
            set { SetValue(ref _isLoadingSerials, value, () => IsLoadingSerials); }
        }

        private int _loadedSerialCount;
        [Ignore]
        [Display(AutoGenerateField = false)]
        public int LoadedSerialCount
        {
            get { return _loadedSerialCount; }
            set
            {
                SetValue(ref _loadedSerialCount, value, () => LoadedSerialCount);
                NotifyPropertyChanged(nameof(HasPreviewTruncated));
            }
        }

        private bool _serialsLoaded;
        [Ignore]
        [Display(AutoGenerateField = false)]
        public bool SerialsLoaded
        {
            get { return _serialsLoaded; }
            set
            {
                SetValue(ref _serialsLoaded, value, () => SerialsLoaded);
                NotifyPropertyChanged(nameof(HasPreviewTruncated));
            }
        }

        private string _serialsError;
        [Ignore]
        [Display(AutoGenerateField = false)]
        public string SerialsError
        {
            get { return _serialsError; }
            set
            {
                SetValue(ref _serialsError, value, () => SerialsError);
                NotifyPropertyChanged(nameof(HasSerialsError));
            }
        }

        [Ignore]
        [Display(AutoGenerateField = false)]
        public bool HasSerials => (Count ?? 0) > 0;

        [Ignore]
        [Display(AutoGenerateField = false)]
        public bool HasSerialsError => !string.IsNullOrEmpty(SerialsError);

        [Ignore]
        [Display(AutoGenerateField = false)]
        public bool HasPreviewTruncated => SerialsLoaded && (Count ?? 0) > LoadedSerialCount && LoadedSerialCount > 0;
    }
}
