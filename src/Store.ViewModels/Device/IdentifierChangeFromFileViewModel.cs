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
        private VerifyIdentifierChange _identifierChangeCurrent;
        private bool? _identifierChangeAllChecked = true;
        private bool _parameterChangeMode;
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

        public VerifyIdentifierChange IdentifierChangeCurrent
        {
            get { return _identifierChangeCurrent; }
            set { SetValue(ref _identifierChangeCurrent, value, () => IdentifierChangeCurrent); }
        }

        public bool IdentifierChangeMode
        {
            get { return !_parameterChangeMode; }
            set
            {
                if (value)
                    SetParameterMode(false);
            }
        }

        public bool ParameterChangeMode
        {
            get { return _parameterChangeMode; }
            set
            {
                if (value)
                    SetParameterMode(true);
                else if (_parameterChangeMode)
                    SetParameterMode(false);
            }
        }

        public bool ShowParameterFields
        {
            get { return _parameterChangeMode; }
        }

        public decimal? IdentifierOwnershipId
        {
            get { return _parameterChangeMode && Ownership != null ? Ownership.OwnershipId : (decimal?)null; }
        }

        public decimal? IdentifierConditionId
        {
            get { return _parameterChangeMode && DeviceCondition != null ? DeviceCondition.DevConditionId : (decimal?)null; }
        }

        public decimal? IdentifierTechStatusId
        {
            get { return _parameterChangeMode && EthTechStatRef != null ? EthTechStatRef.SosttechKod : (decimal?)null; }
        }

        public IdentifierChangeParameters VerificationParameters()
        {
            if (!_parameterChangeMode)
                return null;

            return new IdentifierChangeParameters
            {
                OwnershipId = IdentifierOwnershipId,
                DevConditionId = IdentifierConditionId,
                SosttechKod = IdentifierTechStatusId
            };
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

            RefreshVerifyCommand();
        }

        [DependsOn("IdentifierChangeItems")]
        public bool CanVerifyIdentifierChange()
        {
            return !IsExecuting && IdentifierChangeItems != null && IdentifierChangeItems.Any(row => row != null);
        }

        public void VerifyIdentifierChangeRows()
        {
            AdjustRowsForMode();
            if (!CanVerifyIdentifierChange())
            {
                UpdateIdentifierChangeChecked();
                return;
            }

            var parameters = VerificationParameters();
            var xml = IdentifierChangeXml.Build(Filter.StoreDocumentId, IdentifierChangeItems, parameters, false);
            var selectCmd = Rep.SelectVerifyIdentifierChangeXml<List<VerifyIdentifierChange>>(Filter.StoreDocumentId, xml, null);
            BusyMessage = "Проверка смены идентификаторов...";
            IsBusy = true;
            ExecuteAsync(selectCmd, s =>
            {
                IdentifierChangeItems = MergeVerifyResult(IdentifierChangeItems, selectCmd.ResultSet);
                NotifyPropertyChanged(() => IdentifierChangeItems);
                UpdateIdentifierChangeChecked();
                IsBusy = false;
                RefreshVerifyCommand();
            }, z =>
            {
                UpdateIdentifierChangeChecked();
                IsBusy = false;
                RefreshVerifyCommand();
            });
        }

        public void CheckAllIdentifierChange()
        {
            bool value = IdentifierChangeAllChecked == true;
            foreach (var row in IdentifierChangeItems.Where(z => z != null && z.CanAddBool))
                row.ChBoxS = value;

            UpdateIdentifierChangeChecked();
        }

        // В документ уходят только строки, которые прошли серверную проверку и отмечены пользователем.
        public void UpdateIdentifierChangeChecked()
        {
            IdentifierChangeCheckedItems = IdentifierChangeItems
                .Where(z => z != null && z.ChBoxS && z.IsSuccess)
                .ToList();
            NotifyPropertyChanged(() => IdentifierChangeCheckedItems);
            if (IdentifierChangeCurrent == null || !IdentifierChangeItems.Contains(IdentifierChangeCurrent))
                IdentifierChangeCurrent = IdentifierChangeCheckedItems.FirstOrDefault()
                                          ?? IdentifierChangeItems.FirstOrDefault(z => z != null);
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

        // Список для грида остаётся VerifyIdentifierChange. Копия в StoreDeviceStruct нужна диалогу:
        // «Добавить» включается, когда заполнены FileItems, CheckedItems и LocalCurrentItem.
        void PublishDevices()
        {
            var devices = IdentifierChangeItems.Select(ToDevice).ToList();
            FileItems = devices;
            CheckedItems = new List<StoreDeviceStruct>(devices.Where(z => z != null && z.ChBoxS));
            NotifyPropertyChanged(() => FileItems);
            NotifyPropertyChanged(() => CheckedItems);
            LocalCurrentItem = CheckedItems.FirstOrDefault() ?? devices.FirstOrDefault();
        }

        StoreDeviceStruct ToDevice(VerifyIdentifierChange row)
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
                NoteS = NewValuesText(row),
                OwnershipS = _parameterChangeMode ? Ownership : null,
                DeviceConditionS = _parameterChangeMode ? DeviceCondition : null,
                EthTechStatRefS = _parameterChangeMode ? EthTechStatRef : null
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

        static readonly string[] ReplacementMessages = new[]
        {
            "Не задано ни одного нового значения",
            "Повторяющийся SN_новый",
            "Повторяющийся MAC_новый",
            "Некорректный MAC_новый"
        };

        void SetParameterMode(bool parameterMode)
        {
            if (_parameterChangeMode == parameterMode)
                return;

            _parameterChangeMode = parameterMode;
            if (!parameterMode)
            {
                Ownership = null;
                DeviceCondition = null;
                EthTechStatRef = null;
            }

            NotifyPropertyChanged(() => ParameterChangeMode);
            NotifyPropertyChanged(() => IdentifierChangeMode);
            NotifyPropertyChanged(() => ShowParameterFields);

            if (IdentifierChangeItems.Count == 0)
            {
                IdentifierChangeCheckedItems = new List<VerifyIdentifierChange>();
                CheckedItems = new List<StoreDeviceStruct>();
                LocalCurrentItem = null;
                IdentifierChangeAllChecked = false;
                RefreshVerifyCommand();
                return;
            }

            KeepLocalMessages();
            AdjustRowsForMode();
            foreach (var row in IdentifierChangeItems)
            {
                if (row != null)
                    row.ChBoxS = false;
            }

            IdentifierChangeAllChecked = false;
            UpdateIdentifierChangeChecked();
            LocalCurrentItem = null;
            RefreshVerifyCommand();
        }

        void RefreshVerifyCommand()
        {
            _verifyIdentifierChangeCommand = new DelegateCommand(VerifyIdentifierChangeRows, CanVerifyIdentifierChange);
            NotifyPropertyChanged(() => VerifyIdentifierChangeCommand);
            CommandManager.InvalidateRequerySuggested();
        }

        static readonly string[] LocalMessages = new[]
        {
            "Не заполнен SN_текущий",
            "Не задано ни одного нового значения",
            "Некорректный MAC_текущий",
            "Некорректный MAC_новый",
            "Повторяющийся SN_текущий",
            "Повторяющийся SN_новый",
            "Повторяющийся MAC_новый"
        };

        void KeepLocalMessages()
        {
            var known = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var message in LocalMessages)
                known[message] = true;

            foreach (var row in IdentifierChangeItems)
            {
                if (row == null)
                    continue;

                var left = new List<string>();
                foreach (var part in (row.VerifyStatusStr ?? "").Split(new[] { "; " }, StringSplitOptions.None))
                {
                    if (part.Length > 0 && known.ContainsKey(part))
                        left.Add(part);
                }

                row.VerifyStatusStr = left.Count == 0 ? null : string.Join("; ", left.ToArray());
                if (left.Count == 0)
                {
                    row.VerifyStatusNum = null;
                    row.ChBoxS = true;
                }
                else
                {
                    row.VerifyStatusNum = -1;
                    row.ChBoxS = false;
                }
            }
        }

        void AdjustRowsForMode()
        {
            bool parameterMode = _parameterChangeMode;
            foreach (var row in IdentifierChangeItems)
            {
                if (row == null)
                    continue;

                var hadError = row.VerifyStatusNum == -1;
                var text = row.VerifyStatusStr ?? "";
                if (parameterMode)
                {
                    foreach (var message in ReplacementMessages)
                        text = RemoveMessage(text, message);
                }
                else if (row.VerifyStatusNum != 1 && NoNewValues(row) && text.IndexOf(ReplacementMessages[0], StringComparison.Ordinal) < 0)
                {
                    text = string.IsNullOrEmpty(text) ? ReplacementMessages[0] : text + "; " + ReplacementMessages[0];
                }

                row.VerifyStatusStr = string.IsNullOrEmpty(text) ? null : text;
                if (string.IsNullOrEmpty(text))
                {
                    if (hadError)
                        row.VerifyStatusNum = null;
                }
                else if (hadError)
                {
                    row.VerifyStatusNum = -1;
                    row.ChBoxS = false;
                }
                else if (!parameterMode && NoNewValues(row))
                {
                    row.VerifyStatusNum = -1;
                    row.ChBoxS = false;
                }
            }
        }

        static bool NoNewValues(VerifyIdentifierChange row)
        {
            return string.IsNullOrEmpty(row.NewSerialNumber) &&
                   string.IsNullOrEmpty(row.NewMacAddress) &&
                   string.IsNullOrEmpty(row.NewPonSerial);
        }

        static string RemoveMessage(string text, string message)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            var parts = text.Split(new[] { "; " }, StringSplitOptions.None);
            var left = new List<string>();
            foreach (var part in parts)
            {
                if (part != message && part.Length > 0)
                    left.Add(part);
            }

            return string.Join("; ", left.ToArray());
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
