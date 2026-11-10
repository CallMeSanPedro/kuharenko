using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Store.Device.Models;

// Partial к ViewModel формы документа, в которой объявлены AddFromCommand и AddFromFile.
// Имя класса и namespace нужно привести к реальным. DelegateCommand, IRequest, Rep, Filter,
// IsExecuting, ExecuteAsync, Refresh и MainViewModel берутся из основного файла.
namespace Store.ViewModels.Device
{
    public partial class StoreDeviceViewModel
    {
        private ICommand _addIdentifierChangeCommand;

        public ICommand AddIdentifierChangeCommand
        {
            get
            {
                return _addIdentifierChangeCommand ?? (_addIdentifierChangeCommand =
                    new DelegateCommand<IdentifierChangeFromFileViewModel>(AddIdentifierChangeFromFile,
                        x => x != null && CanAddIdentifierChange(x.IdentifierChangeCheckedItems)));
            }
        }

        public bool CanAddIdentifierChange(IEnumerable<VerifyIdentifierChange> rows)
        {
            if (rows == null) return false;
            return !IsExecuting && rows.Any();
        }

        public void AddIdentifierChangeFromFile(IdentifierChangeFromFileViewModel form)
        {
            if (form == null) return;

            var list = form.IdentifierChangeCheckedItems.ToList();
            if (!list.Any()) return;

            var parameters = form.VerificationParameters();
            var xml = IdentifierChangeXml.Build(Filter.StoreDocumentId, list, parameters);
            IRequest cmd = Rep.AddIdentifierChangeToDocXml(Filter.StoreDocumentId, xml,
                parameters == null ? null : parameters.OwnershipId,
                parameters == null ? null : parameters.DevConditionId,
                parameters == null ? null : parameters.SosttechKod);

            MainViewModel.Current.BusyMessage = "Добавление смены идентификаторов в документ...";
            MainViewModel.Current.IsBusy = true;
            ExecuteAsync(cmd, x =>
            {
                MainViewModel.Current.IsBusy = false;
                Refresh();
            }, z =>
            {
                MainViewModel.Current.IsBusy = false;
                Refresh();
            });
        }
    }
}
