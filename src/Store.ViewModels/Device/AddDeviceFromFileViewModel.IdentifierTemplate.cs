using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Store.Device.Models;

// DelegateCommand, Encoding1251, Messenger, MainViewModel, _exp, SkipFirstRow, MacPattern и SetValue
// берутся из основного файла AddDeviceFromFileViewModel. Класс нужно объявить как partial.
namespace Store.ViewModels.Device
{
    public partial class AddDeviceFromFileViewModel
    {
        private ICommand _downloadIdentifierTemplateCommand;
        private ICommand _loadIdentifierChangeCommand;
        private List<IdentifierChangeRow> _identifierChangeItems;
        private bool _showIdentifierChange;

        public ICommand DownloadIdentifierTemplateCommand
        {
            get
            {
                return _downloadIdentifierTemplateCommand ??
                       (_downloadIdentifierTemplateCommand = new DelegateCommand(DownloadIdentifierTemplate, () => true));
            }
        }

        public ICommand LoadIdentifierChangeCommand
        {
            get
            {
                return _loadIdentifierChangeCommand ??
                       (_loadIdentifierChangeCommand = new DelegateCommand(LoadIdentifierChange, () => true));
            }
        }

        public List<IdentifierChangeRow> IdentifierChangeItems
        {
            get { return _identifierChangeItems ?? (_identifierChangeItems = new List<IdentifierChangeRow>()); }
            set { SetValue(ref _identifierChangeItems, value, () => IdentifierChangeItems); }
        }

        public bool ShowIdentifierChange
        {
            get { return _showIdentifierChange; }
            set { SetValue(ref _showIdentifierChange, value, () => ShowIdentifierChange); }
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
                writer.WriteLine(IdentifierChangeFileParser.ColSn + ";" +
                                 IdentifierChangeFileParser.ColMac + ";" +
                                 IdentifierChangeFileParser.ColPon + ";" +
                                 IdentifierChangeFileParser.ColSnNew + ";" +
                                 IdentifierChangeFileParser.ColMacNew + ";" +
                                 IdentifierChangeFileParser.ColPonNew);
            }
        }

        public void LoadIdentifierChange()
        {
            Messenger.Default.Send(new NotificationMessageAction<FileInfo>(file =>
            {
                try
                {
                    LoadIdentifierChangeFromFile(file);
                }
                catch (Exception error)
                {
                    Messenger.Default.Send(error, MainViewModel.MessageTypes.RaiseError);
                }
            }), MainViewModel.MessageTypes.OpenFile);
        }

        public void LoadIdentifierChangeFromFile(FileInfo file)
        {
            var rows = ReadIdentifierChangeFile(file);
            if (rows == null)
                return;

            IdentifierChangeItems = rows;
            ShowIdentifierChange = true;
            VerifyIdentifierChange(rows);
        }

        public void VerifyIdentifierChange(List<IdentifierChangeRow> rows)
        {
            var ready = rows == null
                ? new List<IdentifierChangeRow>()
                : rows.Where(row => row != null && row.CanApply).ToList();

            if (ready.Count == 0)
                return;

            var xml = IdentifierChangeXml.Build(Filter.StoreDocumentId, ready);
            var selectCmd = Rep.SelectVerifyIdentifierChangeXml<List<VerifyIdentifierChangeEntity>>(Filter.StoreDocumentId, xml, Filter);
            BusyMessage = "Проверка идентификаторов...";
            IsBusy = true;
            ExecuteAsync(selectCmd, s =>
            {
                IdentifierChangeVerify.Apply(rows, selectCmd.ResultSet);
                IdentifierChangeItems = new List<IdentifierChangeRow>(rows);
                IsBusy = false;
            }, z => { IsBusy = false; });
        }

        List<IdentifierChangeRow> ReadIdentifierChangeFile(FileInfo file)
        {
            if (file == null)
                return null;

            var sourceExt = file.Extension.ToLower();
            if (sourceExt == ".xml")
                throw new InvalidDataException("Файл смены идентификаторов должен быть csv или xlsx.");

            bool needDelete = false;
            try
            {
                if (sourceExt == ".xls" || sourceExt == ".xlsx")
                {
                    file = _exp.ConvertXlsxToCsv(file);
                    needDelete = true;
                }

                if (file == null)
                    return null;

                StreamReader sr = sourceExt == ".csv"
                    ? new StreamReader(file.OpenRead(), new Encoding1251())
                    : new StreamReader(file.OpenRead());

                using (sr)
                {
                    return IdentifierChangeFileParser.Parse(sr, SkipFirstRow, MacPattern);
                }
            }
            finally
            {
                if (needDelete && file != null)
                {
                    try
                    {
                        File.Delete(file.FullName);
                    }
                    catch (Exception e)
                    {
                        throw new Exception(e.Message);
                    }
                }
            }
        }
    }
}
