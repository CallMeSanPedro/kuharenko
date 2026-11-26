using Store.ViewModels.Localization;
//using System.Runtime.InteropServices.Automation;

//using DatumNode.Report;
using DatumNode.Models;
using DatumNode.ViewModels;
using Store.Models.Device;
using Store.Models.Device.TechnoReserv;
using Store.Models.Tree;
using Store.ViewModels.Extensions.Report;
using Store.ViewModels.Filtering;
using Store.ViewModels.Main;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Store.ViewModels.Device
{
	public class InventoryListViewModel : ItemsViewModel<InventoryList, InventoryListFilter> 
	{

        private ICommand _reportExportCommand;
        private string Uzelname { get; set; }
        private string _storeType;
        private decimal? HasChild { get; set; }
        private IRepository<StoreDevice, StoreDeviceFilter> _deviceRepository;
        private const int SerialPreviewLimit = 100;

        private ObservableCollection<string> _ignoreParamList = new ObservableCollection<string>();
        public StoreDeviceTechReservWorkspaceViewModel ListMode { get; set; }
        public ObservableCollection<string> IgoreParamList
        {
            get { return _ignoreParamList; }
            set { SetValue(ref _ignoreParamList, value, () => IgoreParamList); }
        }

        public ColumnHeaderFilterSet HeaderFilters { get; } = new ColumnHeaderFilterSet();
        public ColumnHeaderFilter Find(string propertyName) => HeaderFilters.Find(propertyName);

        public InventoryListViewModel()
			: base(MainViewModel/*Base*/.Resolve<IRepository<InventoryList, InventoryListFilter>>())
		{
            //Переименовали для ДНР
            //LocalizedTitle.Set(this, "Title_PoSeriynymNomeram", "По серийным номерам");
            LocalizedTitle.Set(this, "Title_Svodno", "Сводно");
            Deleting += (s, e) => MainViewModel/*Base*/.OnDeleting("Удалить запись", e, this);

			RegisterMaster<IViewModel<StoreTree, StoreTreeFilter>>((container, master) =>
			{				
				Filter.Uzel = master.CurrentItem.Unid;//.ObjId;
			    Uzelname = master.CurrentItem.InfouzelSname;//.ObjName            
                _storeType = master.CurrentItem.Dopinfo2;
                if (master.CurrentItem.HasChild == 0) { IgoreParamList.Add("Uzel"); }

            });

            if (MainViewModel.Current != null && !MainViewModel.Current.SegmentAccountingEnabled)
            {
                IgoreParamList.Add("StoreSegmentName");
            }


            ViewFilter += x => (
               (
                   string.IsNullOrEmpty(LoweredFilterText) ||
                   (
                       (x.VendorName ?? string.Empty).ToLower().Contains(LoweredFilterText) ||
                       (x.TypdeviceName ?? string.Empty).ToLower().Contains(LoweredFilterText) ||
                       (x.OebsNomenclatureCode ?? string.Empty).ToLower().Contains(LoweredFilterText) ||
                       (x.Uzel ?? string.Empty).ToLower().Contains(LoweredFilterText)
                   )
               )
           );


            HeaderFilters.Add(nameof(InventoryList.MarkacommName), "Модель", item => ((InventoryList)item).MarkacommName);
            HeaderFilters.Add(nameof(InventoryList.TypdeviceName), "Вид оборудования", item => ((InventoryList)item).TypdeviceName);
            HeaderFilters.AddText(nameof(InventoryList.OebsNomenclatureCode), "Номенклатурный код", item => ((InventoryList)item).OebsNomenclatureCode);
            HeaderFilters.Add(nameof(InventoryList.VendorName), "Производитель", item => ((InventoryList)item).VendorName);
            HeaderFilters.Add(nameof(InventoryList.ResponsibleName), "МОЛ", item => ((InventoryList)item).ResponsibleName);
            HeaderFilters.Add(nameof(InventoryList.Status), "Статус эксплуатации", item => ((InventoryList)item).Status);
            HeaderFilters.Add(nameof(InventoryList.Condition), "Состояние оборудования", item => ((InventoryList)item).Condition);
            HeaderFilters.Add(nameof(InventoryList.SosttechName), "Техническое состояние", item => ((InventoryList)item).SosttechName);
            HeaderFilters.AddText(nameof(InventoryList.Uzel), "Узел", item => ((InventoryList)item).Uzel);
            HeaderFilters.Changed += (s, e) => ItemsView?.Refresh();
            ViewFilter += item => HeaderFilters.Passes(item);

            CurrentChanged += (s, e) =>
            {
                if (CurrentItem != null)
                    LoadSerialsForRow(CurrentItem);
            };
        }



        private ICommand _clearFilterCommand;

		public ICommand ClearFilterCommand
		{
			get { return _clearFilterCommand ?? (_clearFilterCommand = new DelegateCommand(ClearFilter)); }
		}

		public void ClearFilter()
		{
			Filter.Clear();
			Filter.SetExpanderText();
			Filter.AcceptChanges();			
			Refresh();
		}

        protected override void OnRefreshed(EventArgs e)
        {
            base.OnRefreshed(e);
            HeaderFilters.Reload(Items);

            if (Items != null)
            {
                foreach (var item in Items)
                {
                    item.SerialsLoaded = false;
                    item.SerialsError = null;
                }
            }

            if (CurrentItem != null)
                LoadSerialsForRow(CurrentItem);
            //  LoadModelDiagram();
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        //private readonly ExportToExcel _exp = new ExportToExcel();
        //private ICommand _exportCommand;
        //public ICommand ExportCommand
        //{
        //	get { return _exportCommand ?? (_exportCommand = new DelegateCommand(Export, CanExport)); }
        //}

        //public bool CanExport()
        //{
        //	return AutomationFactory.IsAvailable && Items.Count > 0 && _exp.CanExport(); 
        //	//return true;
        //}

        //public void Export()
        //{						
        //	_exp.HeaderColor = Colors.LightGray;
        //	_exp.Export(Items.Cast<Object>().ToList());
        //}
        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _mySearchCommand;

		public ICommand MySearchCommand
		{
			get { return _mySearchCommand ?? (_mySearchCommand = new DelegateCommand(MySearch, CanMySearch)); }
		}

		public void MySearch()
        {
            Filter.CanLoad = true;
            SearchCommand.Execute(null);
			Filter.SetExpanderText();
		}

		public bool CanMySearch()
		{
			return (Filter.ResponsibleName != null || Filter.EquipMarkaName != null || Filter.KodTou != null || Filter.ExploitStatus != null || Filter.DeviceCondition != null||Filter.StoreCustomerSegments!=null);
		}
        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==

        public ICommand ReportExportCommand
        {
            get { return _reportExportCommand ?? (_reportExportCommand = new DelegateCommand(ReportExport, CanReportExport)); }
        }
        protected void ReportExport()
        {
           // MainViewModel/*Base*/.Current.IsBusy = true;
            var reportScript = ReportClient.FillReportScript("/Store.Views;component/Extensions/StoreReports/InventoryList.txt", ReportClient.FillParameters(Filter),
                                GetBaseModelType());

            reportScript = reportScript.Replace("%UzelName%", Uzelname);
            reportScript = reportScript.Replace("%StoreName%", Uzelname);
            var reportClient = new ReportClient();
           // reportClient.ReportOpened += (s, e) => MainViewModel/*Base*/.Current.IsBusy = false;
            reportClient.OpenHtmlReport(reportScript, new Uri(MainViewModel/*Base*/.Current.AppConfig.ReportUrlGetFull, UriKind.Absolute), new Uri(MainViewModel/*Base*/.Current.AppConfig.ReportUrlPostFull, UriKind.Absolute));

        }
        protected bool CanReportExport()
        {
            return Items.Count > 0;
        }


        public override bool CanRefresh()
        {
            if (Items.Count == 0) return false;
            return base.CanRefresh();
        }

        public bool SegmentAccountingVisibility
        {
            get
            {
                return MainViewModel/*Base*/.Current.SegmentAccountingEnabled;
            }
        }

        private ICommand _showSerialCommand;
        public ICommand ShowSerialCommand
        {
            get { return _showSerialCommand ?? (_showSerialCommand = new DelegateCommand(ShowSerials, CanShowSerials)); }
        }
        protected void ShowSerials()
        {
            if (CurrentItem == null || ListMode == null)
                return;

            ListMode.ShowSerialsFor(CurrentItem);
        }
        protected bool CanShowSerials()
        {
            return (CurrentItem != null && CurrentItem?.Count > 0);
        }

        private ICommand _openAllSerialsCommand;
        public ICommand OpenAllSerialsCommand
        {
            get
            {
                return _openAllSerialsCommand ?? (_openAllSerialsCommand =
                    new DelegateCommand<InventoryList>(OpenAllSerials, CanOpenAllSerials));
            }
        }

        protected void OpenAllSerials(InventoryList row)
        {
            if (row == null || ListMode == null)
                return;

            ListMode.ShowSerialsFor(row);
        }

        protected bool CanOpenAllSerials(InventoryList row)
        {
            return row != null && (row.Count ?? 0) > 0;
        }

        private void LoadSerialsForRow(InventoryList row)
        {
            if (row == null || row.SerialsLoaded || row.IsLoadingSerials)
                return;

            if ((row.Count ?? 0) <= 0)
            {
                row.SerialDevices.Clear();
                row.LoadedSerialCount = 0;
                row.SerialsError = null;
                row.SerialsLoaded = true;
                return;
            }

            var repository = _deviceRepository ?? (_deviceRepository = MainViewModel.Resolve<IRepository<StoreDevice, StoreDeviceFilter>>());
            if (repository == null)
                return;

            row.SerialsError = null;
            row.IsLoadingSerials = true;

            var filter = new StoreDeviceFilter
            {
                Uzel = row.InfouzelKod ?? Filter.Uzel,
                TreeIsParent = true,
                Mode = null,
                MasterStoreDocumentId = null,
                StoreType = _storeType,
                DevicePageSize = SerialPreviewLimit,
                ResponsibleName = row.ResponsibleName,
                EquipMarkaName = row.MarkacommName,
                ExploitStatus = new Store.Models.Reference.ExploitStatus
                {
                    ExploitStatusId = row.ExploitStatusId,
                    ExploitStatusName = row.ExploitStatusName
                },
                DeviceCondition = new Store.Models.Reference.DeviceCondition
                {
                    DevConditionId = row.DeviceConditionId,
                    DevConditionName = row.Condition
                },
                EthTechStat = new Store.Models.Reference.EthTechStatRef
                {
                    SosttechKod = row.EthTechStat?.SosttechKod,
                    SosttechName = row.SosttechName
                }
            };
            filter.AcceptChanges();

            var request = repository.CreateSelectCommand<ObservableCollection<StoreDevice>>(filter, 0, SerialPreviewLimit);

            ExecuteAsync(
                request,
                s =>
                {
                    row.SerialDevices.Clear();
                    if (request.ResultSet != null)
                    {
                        foreach (var dev in request.ResultSet)
                        {
                            if (dev != null)
                                row.SerialDevices.Add(dev);
                        }
                    }

                    row.LoadedSerialCount = row.SerialDevices.Count;
                    row.SerialsLoaded = true;
                    row.IsLoadingSerials = false;
                },
                err =>
                {
                    row.SerialsError = "Не удалось загрузить серийные номера";
                    row.IsLoadingSerials = false;
                });
        }

        //private ICommand _autoGenerateColumnCommand;

        //public ICommand AutoGenerateColumnCommand
        //{
        //    get { return _autoGenerateColumnCommand ?? (_autoGenerateColumnCommand = new DelegateCommand<DataGridAutoGeneratingColumnEventArgs>(AutoGenerateColumnCommande)); }
        //}

        //public void AutoGenerateColumnCommande(DataGridAutoGeneratingColumnEventArgs e)
        //{
        //    if (e.PropertyName != "CurrentCount")
        //        return;

        //    var cellStyle = new Style(typeof(DataGridCell), (Style)Application.Current.TryFindResource(typeof(DataGridCell)));
        //    cellStyle.Setters.Add(new Setter(
        //        ToolTipService.ToolTipProperty,
        //        new Binding("ToolTipCurrentCount")));

        //    e.Column = new DataGridTemplateColumn
        //    {
        //        Header = "Количество",
        //        CellTemplate = (DataTemplate)Application.Current.Resources["CurrentCountTemplate"],
        //        SortMemberPath = "CurrentCount",
        //        CanUserSort = true,
        //        CellStyle = cellStyle
        //    };
        //}



    }


}


