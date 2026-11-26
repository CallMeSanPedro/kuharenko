using DatumNode.ViewModels;
using Store.Models.Device;
using Store.ViewModels.Device;
using Store.ViewModels.Device.TechnoReserv;
using Store.ViewModels.Main;
using System.Windows.Controls.Primitives;
using Store.ViewModels.Localization;

namespace Store.ViewModels.Main
{
    public class StoreDeviceTechReservWorkspaceViewModel : WorkspaceViewModel
    {
        private bool _showSerials;

        public StoreDeviceDocViewModel StoreDeviceDocViewModel { get; set; }
        public TechReservViewModel TechReservViewModel { get; set; }
        public InventoryListViewModel InventoryListViewModel { get; set; }

        private object _serialSourceRow;

        public bool HasSerialFilter
        {
            get { return _serialSourceRow != null; }
        }

        public void ShowSerialsFor(object balanceRow)
        {
            var row = balanceRow as InventoryList;
            if (row == null || StoreDeviceDocViewModel == null || StoreDeviceDocViewModel.MasterViewModel == null)
                return;

            _serialSourceRow = row;
            FillFromBalanceRow(row);
            ReloadSerials();
            NotifyPropertyChanged("HasSerialFilter");
            SetSerialMode(true);
        }

        public void ShowAllSerials()
        {
            if (StoreDeviceDocViewModel == null || StoreDeviceDocViewModel.MasterViewModel == null)
                return;

            _serialSourceRow = null;
            ClearBalanceRow();
            ReloadSerials();
            NotifyPropertyChanged("HasSerialFilter");
            SetSerialMode(true);
        }

        private void ReloadSerials()
        {
            StoreDeviceDocViewModel.MasterViewModel.Filter.AcceptChanges();
            StoreDeviceDocViewModel.MasterViewModel.Refresh();
        }

        private void SetSerialMode(bool showSerials)
        {
            if (_showSerials == showSerials)
                return;

            _showSerials = showSerials;
            NotifyPropertyChanged("ShowBalance");
            NotifyPropertyChanged("ShowSerials");
        }

        private void SetShowSerials(bool showSerials, bool selected)
        {
            if (!selected)
                return;

            if (showSerials)
            {
                if (_serialSourceRow == null)
                    ShowAllSerials();
                return;
            }

            _serialSourceRow = null;
            if (StoreDeviceDocViewModel != null && StoreDeviceDocViewModel.MasterViewModel != null)
                ClearBalanceRow();
            NotifyPropertyChanged("HasSerialFilter");
            SetSerialMode(false);
        }

        private void FillFromBalanceRow(InventoryList row)
        {
            var master = StoreDeviceDocViewModel.MasterViewModel;
            var filter = master.Filter;
            filter.ResponsibleName = row.ResponsibleName;
            filter.EquipMarkaName = row.MarkacommName;
            filter.Uzel = row.InfouzelKod ?? master.TreeUzel;
            filter.ExploitStatus = new Models.Reference.ExploitStatus
            {
                ExploitStatusId = row.ExploitStatusId,
                ExploitStatusName = row.ExploitStatusName
            };
            filter.DeviceCondition = new Models.Reference.DeviceCondition
            {
                DevConditionId = row.DeviceConditionId,
                DevConditionName = row.Condition
            };
            filter.EthTechStat = new Models.Reference.EthTechStatRef
            {
                SosttechKod = row.EthTechStat?.SosttechKod,
                SosttechName = row.SosttechName
            };
            master.HeaderFilters.ShowApplied("MarkacommName", filter.EquipMarkaName);
            master.HeaderFilters.ShowApplied("ResponsibleName", filter.ResponsibleName);
            master.HeaderFilters.ShowApplied("Uzel", row.Uzel);
            master.HeaderFilters.ShowApplied("ExploitStatus", filter.ExploitStatus?.ExploitStatusName);
            master.HeaderFilters.ShowApplied("DeviceCondition", row.Condition);
            master.HeaderFilters.ShowApplied("SosttechName", row.SosttechName);
        }

        private void ClearBalanceRow()
        {
            var master = StoreDeviceDocViewModel.MasterViewModel;
            var filter = master.Filter;
            filter.ResponsibleName = null;
            filter.EquipMarkaName = null;
            filter.Uzel = master.TreeUzel;
            filter.ExploitStatus = new Models.Reference.ExploitStatus();
            filter.DeviceCondition = new Models.Reference.DeviceCondition();
            filter.EthTechStat = new Models.Reference.EthTechStatRef();
            master.HeaderFilters.ShowApplied("MarkacommName", null);
            master.HeaderFilters.ShowApplied("ResponsibleName", null);
            master.HeaderFilters.ShowApplied("Uzel", null);
            master.HeaderFilters.ShowApplied("ExploitStatus", null);
            master.HeaderFilters.ShowApplied("DeviceCondition", null);
            master.HeaderFilters.ShowApplied("SosttechName", null);
        }

        public bool ShowBalance
        {
            get => !_showSerials;
            set => SetShowSerials(false, value);
        }

        public bool ShowSerials
        {
            get => _showSerials;
            set => SetShowSerials(true, value);
        }

        public StoreDeviceTechReservWorkspaceViewModel()
        {
            LocalizedTitle.Set(this, "Title_Oborudovanie", "Оборудование");

            StoreDeviceDocViewModel = new StoreDeviceDocViewModel();
            InventoryListViewModel = new InventoryListViewModel();
            StoreDeviceDocViewModel.MasterViewModel.ListMode = this;
            InventoryListViewModel.ListMode = this;

            AddWorkspace(InventoryListViewModel, () => InventoryListViewModel);
            AddWorkspace(StoreDeviceDocViewModel, () => StoreDeviceDocViewModel);
        }

        //private void SetShowSerials(bool showSerials, bool selected)
        //{
        //    if (!selected || _showSerials == showSerials)
        //        return;

        //    _showSerials = showSerials;
        //    NotifyPropertyChanged(nameof(ShowBalance));
        //    NotifyPropertyChanged(nameof(ShowSerials));
        //}
    }
}
