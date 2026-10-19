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
        private ICommand _identifierChangeChBoxCommand;
        private List<VerifyIdentifierChange> _identifierChangeItems;
        private List<VerifyIdentifierChange> _identifierChangeCheckedItems;
        private bool _identifierChangeMode;
        private int _identifierChangeBadMacCount;

        public bool IdentifierChangeMode
        {
            get { return _identifierChangeMode; }
            set
            {
                SetValue(ref _identifierChangeMode, value, () => IdentifierChangeMode);
                NotifyPropertyChanged(() => ShowIdentifierChange);
                NotifyPropertyChanged(() => ShowFileLoad);
            }
        }

        public bool ShowIdentifierChange
        {
            get { return IdentifierChangeMode; }
        }

        public bool ShowFileLoad
        {
            get { return !IdentifierChangeMode; }
        }

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
                       (_verifyIdentifierChangeCommand = new DelegateCommand(VerifyIdentifierChangeRows, CanVerifyIdentifierChange));
            }
        }

        public ICommand IdentifierChangeChBoxCommand
        {
            get
            {
                return _identifierChangeChBoxCommand ??
                       (_identifierChangeChBoxCommand = new DelegateCommand(UpdateIdentifierChangeChecked, () => true));
            }
        }

        public List<VerifyIdentifierChange> IdentifierChangeItems
        {
            get { return _identifierChangeItems ?? (_identifierChangeItems = new List<VerifyIdentifierChange>()); }
            set { SetValue(ref _identifierChangeItems, value, () => IdentifierChangeItems); }
        }

        public List<VerifyIdentifierChange> IdentifierChangeCheckedItems
        {
            get { return _identifierChangeCheckedItems ?? (_identifierChangeCheckedItems = new List<VerifyIdentifierChange>()); }
            set { SetValue(ref _identifierChangeCheckedItems, value, () => IdentifierChangeCheckedItems); }
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
            IdentifierChangeCheckedItems = new List<VerifyIdentifierChange>();
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
                VerifyIdentifierChangeRows();
            else
                UpdateIdentifierChangeChecked();
        }

        [DependsOn("IdentifierChangeItems")]
        public bool CanVerifyIdentifierChange()
        {
            return !IsExecuting && IdentifierChangeItems != null && IdentifierChangeItems.Any(CanSend);
        }

        public void VerifyIdentifierChangeRows()
        {
            var ready = IdentifierChangeItems.Where(CanSend).ToList();
            if (!ready.Any())
            {
                UpdateIdentifierChangeChecked();
                return;
            }

            var xml = IdentifierChangeXml.Build(Filter.StoreDocumentId, ready);
            if (xml == null)
            {
                UpdateIdentifierChangeChecked();
                return;
            }

            var selectCmd = Rep.SelectVerifyIdentifierChangeXml<List<VerifyIdentifierChange>>(Filter.StoreDocumentId, xml, null);
            BusyMessage = "Проверка смены идентификаторов...";
            IsBusy = true;
            ExecuteAsync(selectCmd, s =>
            {
                IdentifierChangeItems = MergeVerifyResult(IdentifierChangeItems, selectCmd.ResultSet);
                NotifyPropertyChanged(() => IdentifierChangeItems);
                UpdateIdentifierChangeChecked();
                IsBusy = false;
            }, z =>
            {
                UpdateIdentifierChangeChecked();
                IsBusy = false;
            });
        }

        // В документ уходят только строки, которые прошли серверную проверку и отмечены пользователем.
        public void UpdateIdentifierChangeChecked()
        {
            IdentifierChangeCheckedItems = IdentifierChangeItems
                .Where(z => z != null && z.ChBoxS && z.IsSuccess)
                .ToList();
            NotifyPropertyChanged(() => IdentifierChangeCheckedItems);
            RefreshIdentifierChangeResume();
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
            var rows = IdentifierChangeItems ?? new List<VerifyIdentifierChange>();
            FileRowCount = rows.Count;
            BadMacCount = _identifierChangeBadMacCount;
            AddRowCount = IdentifierChangeCheckedItems.Count;
            RowSiftingCount = FileRowCount - AddRowCount;

            FileRowCountText = "";
            BadMacCountText = "";
            AddRowCountText = "";
            RowSiftingCountText = "";
            SetResumeText();
            if (ShowResume || FileRowCount == 0)
                return;

            FileRowCountText = "Считано записей из файла: " + FileRowCount;
            if (BadMacCount > 0)
                BadMacCountText = "\nНе верный mac-адрес: " + BadMacCount;
            AddRowCountText = "\nБудет загружено: " + AddRowCount;
            if (RowSiftingCount > 0)
                RowSiftingCountText = "\nОтсев: " + RowSiftingCount;

            ResumeText = FileRowCountText + BadMacCountText + RowSiftingCountText + AddRowCountText;
            ShowResume = ResumeText.Length > 0;
            NotifyPropertyChanged(() => ShowResume);
            NotifyPropertyChanged(() => ResumeText);
        }

        static int CountBadMac(IEnumerable<VerifyIdentifierChange> rows)
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

        static bool CanSend(VerifyIdentifierChange row)
        {
            return row != null && row.ChBoxS && row.CanAddBool && !string.IsNullOrEmpty(row.SerialNumber);
        }

        static List<VerifyIdentifierChange> MergeVerifyResult(List<VerifyIdentifierChange> current, List<VerifyIdentifierChange> result)
        {
            if (current == null)
                return result ?? new List<VerifyIdentifierChange>();
            if (result == null)
                return current;

            var byLine = new Dictionary<decimal, VerifyIdentifierChange>();
            foreach (var item in result)
            {
                if (item != null && item.LineNo != null)
                    byLine[item.LineNo.Value] = item;
            }

            var merged = new List<VerifyIdentifierChange>();
            foreach (var item in current)
            {
                VerifyIdentifierChange server;
                if (item != null && item.LineNo != null && byLine.TryGetValue(item.LineNo.Value, out server))
                {
                    server.ChBoxS = server.VerifyStatusNum != -1 && item.ChBoxS;
                    merged.Add(server);
                }
                else
                    merged.Add(item);
            }

            return merged;
        }
    }
}
