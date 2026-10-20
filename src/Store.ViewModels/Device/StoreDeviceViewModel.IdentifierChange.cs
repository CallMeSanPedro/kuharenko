using System;
using System.Linq;
using System.Windows.Input;
using Store.Device.Models;

// Partial к ViewModel формы документа, в которой объявлены AddFromCommand и AddFromFile.
// Имя класса и namespace нужно привести к реальным. DelegateCommand, IRequest, Rep, Filter,
// IsExecuting, ExecuteAsync, Refresh, Messenger и MainViewModel берутся из основного файла.
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
                    new DelegateCommand<IdentifierChangeFromFileViewModel>(AddIdentifierChangeFromFile, CanAddIdentifierChange));
            }
        }

        // Доступность не зависит от отмеченных строк: DialogDirector может не пересчитать CanExecute
        // после проверки в форме. Строки собираются в момент нажатия.
        public bool CanAddIdentifierChange(IdentifierChangeFromFileViewModel form)
        {
            return form != null && !IsExecuting;
        }

        public void AddIdentifierChangeFromFile(IdentifierChangeFromFileViewModel form)
        {
            if (form == null) return;

            var list = form.IdentifierChangeItems
                .Where(z => z != null && z.ChBoxS && z.IsSuccess)
                .ToList();
            if (!list.Any())
            {
                Messenger.Default.Send(new Exception("Нет отмеченных строк, прошедших проверку."), MainViewModel.MessageTypes.RaiseError);
                return;
            }

            var xml = IdentifierChangeXml.Build(Filter.StoreDocumentId, list);
            IRequest cmd = Rep.AddIdentifierChangeToDocXml(Filter.StoreDocumentId, xml);

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
