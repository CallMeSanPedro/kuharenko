// Переход из строки остатка в серийные.
// Селектор: StoreDeviceViewModel.Refresh() отправляет StoreDeviceFilter
// в CreateSelectCommand (StoreDeviceGet). С базы приходит одна страница.
//
// В фильтр селектора из InventoryList попадают только строковые параметры:
//   ResponsibleName -> PResponsibleName
//   MarkacommName   -> EquipMarkaName -> PEthMarkaCommName
//
// Не записывать:
//   InventoryList.Uzel — это подпись места хранения, а Filter.Uzel уже равен Unid склада из RegisterMaster.
//   Status, Condition, TypdeviceName, SosttechName, StoreSegmentName — на строке остатка это имена,
//   а селектор ждёт ExploitStatusId, DevConditionId, TypdeviceKod, SosttechKod, StoreSegmentId.

using Store.Models.Device;

namespace Store.ViewModels.Main
{
    public partial class StoreDeviceTechReservWorkspaceViewModel
    {
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
            var filter = StoreDeviceDocViewModel.MasterViewModel.Filter;
            filter.ResponsibleName = row.ResponsibleName;
            filter.EquipMarkaName = row.MarkacommName;
        }

        private void ClearBalanceRow()
        {
            var filter = StoreDeviceDocViewModel.MasterViewModel.Filter;
            filter.ResponsibleName = null;
            filter.EquipMarkaName = null;
        }
    }
}
