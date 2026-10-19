using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Store.Device.Models;

// DelegateCommand, Encoding1251, Messenger, MainViewModel, Rep, Filter, _exp, SkipFirstRow, MacPattern, SetValue,
// VerifyData, ShowResume, ResumeText, FileRowCount, BadMacCount, AddRowCount, RowSiftingCount и тексты резюме
// берутся из основного файла AddDeviceFromFileViewModel. Класс нужно объявить как partial.
namespace Store.ViewModels.Device
{
    public partial class AddDeviceFromFileViewModel
    {
        private ICommand _downloadIdentifierTemplateCommand;
        private ICommand _loadIdentifierChangeCommand;
        private ICommand _verifyIdentifierChangeCommand;
        private List<VerifyIdentifierChangeEntity> _identifierChangeItems;
        private bool _showIdentifierChange;
        private int _identifierChangeBadMacCount;

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

        public ICommand VerifyIdentifierChangeCommand
        {
            get
            {
                return _verifyIdentifierChangeCommand ??
                       (_verifyIdentifierChangeCommand = new DelegateCommand(VerifyIdentifierChange, CanVerifyIdentifierChange));
            }
        }

        public List<VerifyIdentifierChangeEntity> IdentifierChangeItems
        {
            get { return _identifierChangeItems ?? (_identifierChangeItems = new List<VerifyIdentifierChangeEntity>()); }
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
            if (file == null)
                return;

            ClearIdentifierChangeResume();
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
                    return;

                StreamReader sr = sourceExt == ".csv"
                    ? new StreamReader(file.OpenRead(), new Encoding1251())
                    : new StreamReader(file.OpenRead());

                using (sr)
                {
                    IdentifierChangeItems = IdentifierChangeFileParser.Parse(sr, SkipFirstRow, MacPattern);
                    _identifierChangeBadMacCount = CountBadMac(IdentifierChangeItems);
                    ShowIdentifierChange = true;
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

            if (CanVerifyIdentifierChange())
                VerifyIdentifierChange();
            else
                RefreshIdentifierChangeResume();
        }

        [DependsOn("IdentifierChangeItems")]
        public bool CanVerifyIdentifierChange()
        {
            return !IsExecuting && IdentifierChangeItems != null && IdentifierChangeItems.Any(CanSend);
        }

        public void VerifyIdentifierChange()
        {
            var ready = IdentifierChangeItems.Where(CanSend).ToList();
            if (!ready.Any())
            {
                RefreshIdentifierChangeResume();
                return;
            }

            var xml = IdentifierChangeXml.Build(Filter.StoreDocumentId, ready);
            if (xml == null)
            {
                RefreshIdentifierChangeResume();
                return;
            }

            var selectCmd = Rep.SelectVerifyIdentifierChangeXml<List<VerifyIdentifierChangeEntity>>(Filter.StoreDocumentId, xml, null);
            BusyMessage = "Проверка смены идентификаторов...";
            IsBusy = true;
            ExecuteAsync(selectCmd, s =>
            {
                IdentifierChangeItems = MergeVerifyResult(IdentifierChangeItems, selectCmd.ResultSet);
                NotifyPropertyChanged(() => IdentifierChangeItems);
                RefreshIdentifierChangeResume();
                IsBusy = false;
            }, z =>
            {
                RefreshIdentifierChangeResume();
                IsBusy = false;
            });
        }

        void ClearIdentifierChangeResume()
        {
            _identifierChangeBadMacCount = 0;
            FileRowCount = 0;
            BadMacCount = 0;
            AddRowCount = -1;
            RowSiftingCount = 0;
            FileRowCountText = "";
            BadMacCountText = "";
            AddRowCountText = "";
            RowSiftingCountText = "";
            ResumeText = "";
            ShowResume = false;
            NotifyPropertyChanged(() => ShowResume);
            NotifyPropertyChanged(() => ResumeText);
        }

        void RefreshIdentifierChangeResume()
        {
            var rows = IdentifierChangeItems ?? new List<VerifyIdentifierChangeEntity>();
            FileRowCount = rows.Count;
            BadMacCount = _identifierChangeBadMacCount;
            AddRowCount = 0;
            RowSiftingCount = 0;
            foreach (var row in rows)
            {
                if (row != null && row.ChBoxS)
                    AddRowCount++;
                else
                    RowSiftingCount++;
            }

            FileRowCountText = "";
            BadMacCountText = "";
            AddRowCountText = "";
            RowSiftingCountText = "";
            SetResumeText();
            if (ShowResume || FileRowCount == 0)
                return;

            if (FileRowCount > 0)
                FileRowCountText = "Считано записей из файла: " + FileRowCount;
            if (BadMacCount > 0)
                BadMacCountText = "\nНе верный mac-адрес: " + BadMacCount;
            if (AddRowCount > -1)
                AddRowCountText = "\nБудет загружено: " + AddRowCount;
            if (RowSiftingCount > 0)
                RowSiftingCountText = "\nОтсев: " + RowSiftingCount;

            ResumeText = FileRowCountText + BadMacCountText + RowSiftingCountText + AddRowCountText;
            ShowResume = ResumeText.Length > 0;
            NotifyPropertyChanged(() => ShowResume);
            NotifyPropertyChanged(() => ResumeText);
        }

        static int CountBadMac(IEnumerable<VerifyIdentifierChangeEntity> rows)
        {
            int count = 0;
            if (rows == null)
                return count;

            foreach (var row in rows)
            {
                var status = row == null ? "" : (row.VerifyStatusStr ?? "");
                if (status.IndexOf("Некорректный MAC_текущий", StringComparison.Ordinal) >= 0)
                    count++;
                if (status.IndexOf("Некорректный MAC_новый", StringComparison.Ordinal) >= 0)
                    count++;
            }

            return count;
        }

        static bool CanSend(VerifyIdentifierChangeEntity row)
        {
            return row != null && row.ChBoxS && row.CanPressCheckBoxS && !string.IsNullOrEmpty(row.SerialNumber);
        }

        static List<VerifyIdentifierChangeEntity> MergeVerifyResult(List<VerifyIdentifierChangeEntity> current, List<VerifyIdentifierChangeEntity> result)
        {
            if (current == null)
                return result ?? new List<VerifyIdentifierChangeEntity>();
            if (result == null)
                return current;

            var byLine = new Dictionary<decimal, VerifyIdentifierChangeEntity>();
            foreach (var item in result)
            {
                if (item != null && item.LineNo != null)
                    byLine[item.LineNo.Value] = item;
            }

            var merged = new List<VerifyIdentifierChangeEntity>();
            foreach (var item in current)
            {
                VerifyIdentifierChangeEntity server;
                if (item != null && item.LineNo != null && byLine.TryGetValue(item.LineNo.Value, out server))
                {
                    server.ChBoxS = server.VerifyStatusNum == -1 ? false : item.ChBoxS;
                    merged.Add(server);
                }
                else
                    merged.Add(item);
            }

            return merged;
        }
    }
}
