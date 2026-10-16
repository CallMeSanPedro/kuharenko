using System.IO;
using System.Windows.Input;

// DelegateCommand и Encoding1251 — те же типы, что уже использует AddDeviceFromFileViewModel.
// Объявление основного класса нужно дополнить ключевым словом partial.
namespace Store.ViewModels.Device
{
    public partial class AddDeviceFromFileViewModel
    {
        private ICommand _downloadIdentifierTemplateCommand;

        public ICommand DownloadIdentifierTemplateCommand
        {
            get
            {
                return _downloadIdentifierTemplateCommand ??
                       (_downloadIdentifierTemplateCommand = new DelegateCommand(DownloadIdentifierTemplate, () => true));
            }
        }

        public void DownloadIdentifierTemplate()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV (*.csv)|*.csv",
                FileName = "Шаблон смены идентификаторов.csv",
                DefaultExt = ".csv",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog() != true)
                return;

            using (var writer = new StreamWriter(dialog.FileName, false, new Encoding1251()))
            {
                writer.WriteLine("SN_текущий;MAC_текущий;PON_текущий;SN_новый;MAC_новый;PON_новый");
            }
        }
    }
}
