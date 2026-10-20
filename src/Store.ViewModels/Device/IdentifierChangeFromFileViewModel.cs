using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Store.Device.Models;

// Usings для DelegateCommand, DependsOn, Encoding1251, ExportToExcel, Messenger, NotificationMessageAction
// и MainViewModel скопировать из AddDeviceFromFileViewModel.
// Из AddDeviceFromFileViewModel наследуются Rep, Filter, ExecuteAsync, IsExecuting, IsBusy, BusyMessage,
// SetValue, NotifyPropertyChanged, SkipFirstRow, MacPattern, ResumeText и ShowResume.
namespace Store.ViewModels.Device
{
    public class IdentifierChangeFromFileViewModel : AddDeviceFromFileViewModel
    {
        private ICommand _downloadIdentifierTemplateCommand;
        private ICommand _loadIdentifierChangeCommand;
        private ICommand _verifyIdentifierChangeCommand;
        private ICommand _identifierChangeChBoxCommand;
        private ICommand _identifierChangeChBoxAllCommand;
        private List<VerifyIdentifierChange> _identifierChangeItems;
        private List<VerifyIdentifierChange> _identifierChangeCheckedItems;
        private bool? _identifierChangeAllChecked = true;
        private bool _devicesProjected;
        private int _badMacCount;

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
                       (_loadIdentifierChangeCommand = new DelegateCommand(LoadIdentifierChange, () => !IsExecuting));
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

        public ICommand IdentifierChangeChBoxAllCommand
        {
            get
            {
                return _identifierChangeChBoxAllCommand ??
                       (_identifierChangeChBoxAllCommand = new DelegateCommand(CheckAllIdentifierChange, () => true));
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

        public bool? IdentifierChangeAllChecked
        {
            get { return _identifierChangeAllChecked; }
            set { SetValue(ref _identifierChangeAllChecked, value, () => IdentifierChangeAllChecked); }
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

            IdentifierChangeItems = new List<VerifyIdentifierChange>();
            IdentifierChangeCheckedItems = new List<VerifyIdentifierChange>();
            _devicesProjected = false;
            FileItems = new List<StoreDeviceStruct>();
            CheckedItems = new List<StoreDeviceStruct>();
            LocalCurrentItem = null;
            _badMacCount = 0;
            SetIdentifierChangeResume("");

            var sourceExt = file.Extension.ToLower();
            if (sourceExt != ".csv" && sourceExt != ".xls" && sourceExt != ".xlsx")
                throw new InvalidDataException("Файл смены идентификаторов должен быть csv, xls или xlsx.");

            bool needDelete = false;
            try
            {
                if (sourceExt == ".xls" || sourceExt == ".xlsx")
                {
                    file = new ExportToExcel().ConvertXlsxToCsv(file);
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
                    _badMacCount = CountBadMac(IdentifierChangeItems);
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

        public void CheckAllIdentifierChange()
        {
            bool value = IdentifierChangeAllChecked == true;
            foreach (var row in IdentifierChangeItems.Where(z => z != null && z.CanAddBool))
                row.ChBoxS = value;

            _devicesProjected = false;
            UpdateIdentifierChangeChecked();
        }

        // В документ уходят только строки, которые прошли серверную проверку и отмечены пользователем.
        public void UpdateIdentifierChangeChecked()
        {
            PullSelectionFromDevices();
            IdentifierChangeCheckedItems = IdentifierChangeItems
                .Where(z => z != null && z.ChBoxS && z.IsSuccess)
                .ToList();
            NotifyPropertyChanged(() => IdentifierChangeCheckedItems);
            PublishDevices();

            var selectable = IdentifierChangeItems.Where(z => z != null && z.CanAddBool).ToList();
            if (!selectable.Any() || selectable.All(z => !z.ChBoxS))
                IdentifierChangeAllChecked = false;
            else if (selectable.All(z => z.ChBoxS))
                IdentifierChangeAllChecked = true;
            else
                IdentifierChangeAllChecked = null;

            RefreshIdentifierChangeResume();
        }

        void PullSelectionFromDevices()
        {
            if (!_devicesProjected || FileItems == null)
                return;

            int index = 0;
            foreach (var device in FileItems)
            {
                if (index >= IdentifierChangeItems.Count)
                    break;
                if (device != null && IdentifierChangeItems[index] != null)
                    IdentifierChangeItems[index].ChBoxS = device.ChBoxS;
                index++;
            }
        }

        // VerifyIdentifierChange не приводится к StoreDeviceStruct. Копируем поля и кладём строки
        // туда же, куда их кладёт проверка приёмки: FileItems, CheckedItems, LocalCurrentItem.
        void PublishDevices()
        {
            var devices = IdentifierChangeItems.Select(ToDevice).ToList();
            FileItems = devices;
            CheckedItems = new List<StoreDeviceStruct>(devices.Where(z => z != null && z.ChBoxS));
            NotifyPropertyChanged(() => FileItems);
            NotifyPropertyChanged(() => CheckedItems);
            LocalCurrentItem = CheckedItems.FirstOrDefault() ?? devices.FirstOrDefault();
            _devicesProjected = true;
        }

        static StoreDeviceStruct ToDevice(VerifyIdentifierChange row)
        {
            if (row == null)
                return null;

            return new StoreDeviceStruct
            {
                SerialNumberS = row.SerialNumber,
                SerialNumber = FirstFilled(row.NewSerialNumber, row.SerialNumber),
                MacAddressS = FirstFilled(row.NewMacAddress, row.MacAddress),
                PonSerialS = FirstFilled(row.NewPonSerial, row.PonSerial),
                StoreDocumentIdS = row.StoreDocumentId,
                CanAdd = row.IsSuccess ? 1 : 0,
                CanPressCheckBoxS = row.CanAddBool,
                ChBoxS = row.ChBoxS && row.IsSuccess,
                ExploitStatusName = row.VerifyStatusStr,
                NoteS = NewValuesText(row)
            };
        }

        static string NewValuesText(VerifyIdentifierChange row)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(row.NewSerialNumber))
                parts.Add("SN " + row.NewSerialNumber);
            if (!string.IsNullOrEmpty(row.NewMacAddress))
                parts.Add("MAC " + row.NewMacAddress);
            if (!string.IsNullOrEmpty(row.NewPonSerial))
                parts.Add("PON " + row.NewPonSerial);
            return string.Join("; ", parts.ToArray());
        }

        static string FirstFilled(string primary, string fallback)
        {
            return string.IsNullOrEmpty(primary) ? fallback : primary;
        }

        void RefreshIdentifierChangeResume()
        {
            int fileRowCount = IdentifierChangeItems.Count;
            if (fileRowCount == 0)
            {
                SetIdentifierChangeResume("");
                return;
            }

            int addRowCount = IdentifierChangeCheckedItems.Count;
            int siftingCount = fileRowCount - addRowCount;

            var text = "Считано записей из файла: " + fileRowCount;
            if (_badMacCount > 0)
                text += "\nНе верный mac-адрес: " + _badMacCount;
            if (siftingCount > 0)
                text += "\nОтсев: " + siftingCount;
            text += "\nБудет загружено: " + addRowCount;

            SetIdentifierChangeResume(text);
        }

        void SetIdentifierChangeResume(string text)
        {
            ResumeText = text;
            ShowResume = text.Length > 0;
            NotifyPropertyChanged(() => ShowResume);
            NotifyPropertyChanged(() => ResumeText);
        }

        static int CountBadMac(IEnumerable<VerifyIdentifierChange> rows)
        {
            int count = 0;
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
