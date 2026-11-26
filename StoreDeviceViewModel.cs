using DatumNode.Controls;
using DatumNode.Models;
using Store.Localization;
//using DatumNode.Report;
using DatumNode.ViewModels;
using Infrastructure.Common.Helpers;
using Infrastructure.Common.ViewModels;
using Microsoft.Office.Interop.Excel;
using Store.Common;
using Store.Common.Utils;
using Store.DatumNodeRepository.Device;
using Store.Device.Models;
using Store.Device.Repository;
using Store.Models.Device;
using Store.Models.Reference;
using Store.Models.Reference.Marka;
using Store.Models.Reference.Repair;
using Store.Models.Tree;
using Store.Repository.Device;
using Store.ViewModels;
using Store.ViewModels.Documents;
using Store.ViewModels.Extensions.Report;
using Store.ViewModels.Filtering;
using Store.ViewModels.Main;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
//using System.Runtime.InteropServices.Automation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Store.ViewModels.Localization;


namespace Store.ViewModels.Device
{
    public partial class
        StoreDeviceViewModel : ItemsViewModel<StoreDevice, StoreDeviceFilter>
    {
        public DialogDirector Dlg;

        private bool _myIsBusy;

        public bool MyIsBusy
        {
            get { return _myIsBusy; }
            set
            {
                if (value != _myIsBusy)
                    SetValue(ref _myIsBusy, value, () => MyIsBusy);
            }
        }

        private ICommand _reportExportCommand;

        public Visibility TransferCpeVisibility { get; set; }
        public Visibility TransferBackCpeVisibility { get; set; }
        public Visibility ReplaceCpeVisibility { get; set; }
        public Visibility TransferFakeBackCpeVisibility { get; set; }
        public Visibility UnReserveCpeVisibility { get; set; }
        public Visibility ChangeStatusCpeVisibility { get; set; }
        public Visibility RepaymentCpeVisibility { get; set; }

        private bool _canTransferCpe = false;
        public StoreDeviceTechReservWorkspaceViewModel ListMode { get; set; }

        public ColumnHeaderFilterSet HeaderFilters { get; } = new ColumnHeaderFilterSet();
        public decimal? TreeUzel { get; private set; }



        public bool SegmentAccountingVisibility
        {
            get
            {
                return MainViewModel/*Base*/.Current.SegmentAccountingEnabled;
            }
        }

        public bool CanTransferCpe
        {
            get { return _canTransferCpe; }
            set
            {
                SetValue(ref _canTransferCpe, value, () => CanTransferCpe);
                NotifyPropertyChanged("CanTransferCpe");
            }
        }

        private bool _canTransferBackCpe = false;

        public bool CanTransferBackCpe
        {
            get { return _canTransferBackCpe; }
            set
            {
                SetValue(ref _canTransferBackCpe, value, () => CanTransferBackCpe);
                NotifyPropertyChanged("CanTransferBackCpe");
            }
        }

        private bool _canReplaceCpe = false;

        public bool CanReplaceCpe
        {
            get { return _canReplaceCpe; }
            set { SetValue(ref _canReplaceCpe, value, () => CanReplaceCpe); }
        }

        private bool _canUnReserveCpe = false;

        public bool CanUnReserveCpe
        {
            get { return _canUnReserveCpe; }
            set { SetValue(ref _canUnReserveCpe, value, () => CanUnReserveCpe); }
        }

        private bool _canChangeStatusCpe = false;

        public bool CanChangeStatusCpe
        {
            get { return _canChangeStatusCpe; }
            set { SetValue(ref _canChangeStatusCpe, value, () => CanChangeStatusCpe); }
        }

        private bool _canTransferFakeBackCpe = false;

        public bool CanTransferFakeBackCpe
        {
            get { return _canTransferFakeBackCpe; }
            set { SetValue(ref _canTransferFakeBackCpe, value, () => CanTransferFakeBackCpe); }
        }

        private bool _canRepaymentCpe = false;

        public bool CanRepaymentCpe
        {
            get { return _canRepaymentCpe; }
            set
            {
                SetValue(ref _canRepaymentCpe, value, () => CanRepaymentCpe);
                NotifyPropertyChanged("CanRepaymentCpe");
            }
        }


        public bool EnableContextMenu { get; set; }
        public bool EnableFakeReturnMenu { get; set; }

        public string StoreName { get; set; }
        public decimal? ClientSegment { get; set; }

        public new IDeviceRepository Rep
        {
            get { return (IDeviceRepository) base.Repository; }
        }

        public bool CanDeleteDev;

        private EquipMarkaCom _localEquipMarkaCom = new EquipMarkaCom();
        private DeviceCondition _localDeviceCondition = new DeviceCondition();
        private decimal? _localDocOper = 0;

        private ObservableCollection<string> _ignoreParamList = new ObservableCollection<string>();
        public ObservableCollection<string> IgoreParamList
        { 
            get { return _ignoreParamList; }
            set { SetValue(ref _ignoreParamList, value, () => IgoreParamList); }
        }


        public bool CanOnlyView { get; set; }
       

        //public string MyTotalItemCount = "sss";

        public StoreDeviceViewModel()
            : base(MainViewModel /*Base*/.Resolve<IRepository<StoreDevice, StoreDeviceFilter>>())
        {
            Owner = MainViewModel /*Base*/.Current;
            StoreDevice cur = null;
            decimal? curRowNum = 0;
            LocalizedTitle.Set(this, "Title_Detalizatsiya", "Детализация");
            CPEMode = true;

            Deleting += (s, e) => MainViewModel /*Base*/.OnDeleting("Удалить оборудование?", e, this);
            Inserted += (s, e) =>
            {
                cur = CurrentItem;
                Refresh();
            };

            CurrentChanged += (s, e) =>
            {
               

                    if (CurrentItem != null)
                    {
                        curRowNum = CurrentItem.Rown;
                        if (_localDocOper == (int)EnumStoreOperations.AcceptingByNomenclature)
                        {
                            if (_localEquipMarkaCom != null)
                                CurrentItem.EquipMarkaCom = _localEquipMarkaCom;
                            if (_localDeviceCondition != null)
                                CurrentItem.DeviceCondition = _localDeviceCondition;
                            CurrentItem.CanSelectMarkaComm = false;
                        }

                        //StoreMessages.StoreDeviceToStoreDocument.Send(Filter.Uzel);
                        Dictionary<string, object> dict = new Dictionary<string, object>();
                        dict.Add("Uzel", Filter.Uzel);
                        dict.Add("CommId", CurrentItem.CommId);
                        dict.Add("InfouzelKod", CurrentItem.InfouzelKod);

                        StoreMessages.StoreDeviceToStoreDocument.Send(dict);

                        var curExpStat = CurrentItem.ExploitStatusId;
                        var owner = CurrentItem.OwnershipId;
                        CanTransferCpe = (curExpStat == (int)EnumExploitStatus.OnStore);
                        CanTransferFakeBackCpe = CanTransferCpe;
                        CanTransferBackCpe = (curExpStat == (int)EnumExploitStatus.Abon ||
                                              curExpStat == (int)EnumExploitStatus.WaitReturn ||
                                              curExpStat == (int)EnumExploitStatus.WaitResell ||
                                              curExpStat == (int)EnumExploitStatus.AbonNoService);
                        CanReplaceCpe = CanTransferBackCpe;
                        CanUnReserveCpe = curExpStat == (int)EnumExploitStatus.Reserve;
                        CanChangeStatusCpe = (curExpStat != (int)EnumExploitStatus.Abon &&
                                              curExpStat != (int)EnumExploitStatus.WaitReturn &&
                                              curExpStat != (int)EnumExploitStatus.WaitResell &&
                                              curExpStat != (int)EnumExploitStatus.AbonNoService &&
                                              curExpStat != (int)EnumExploitStatus.Reserve);
                        CanRepaymentCpe =
                            (curExpStat == (int)EnumExploitStatus.Abon && owner == 1); //собственность общества.
                        if (Filter.StoreDocumentId == null)
                            Filter.CanChange = curExpStat != (int)EnumExploitStatus.Reserve;// && curExpStat!= (int)EnumExploitStatus.Established;
                    }
                

            };

            Refreshed += (s, e) =>
            {
                if (cur != null)
                {
                    cur.Rown = curRowNum;
                    CurrentItem = cur;
                }


            };

            ItemRefreshed += (s, e) =>
            {
                if (CurrentItem != null)
                    CurrentItem.Rown = curRowNum;
            };

            Rejected += (s, e) =>
            {
                if (CurrentItem != null)
                    CurrentItem.Rown = curRowNum;
            };

            PageSize = 100;
            Filter.DevicePageSize = 100;
            RegisterMaster<DocumentAdditionalMasterDetailViewModel>((container, master) =>
            {
                var newMaster = master.MasterViewModel;
                LocalizedTitle.Set(this, "Title_Oborudovanie", "Оборудование");
                CanOnlyView = master.MasterViewModel.CanOnlyView;
                Filter.Uzel = newMaster.Filter.Uzel;
                TreeUzel = newMaster.Filter.Uzel;
                StoreName = newMaster.CurrentItem.StoreName;
                Filter.StoreDocumentId = newMaster.CurrentItem.StoreDocumentId ?? -1;
                Filter.TreeIsParent = false;
                Filter.ToolbarIsVisible = true;
                Filter.Mode = null;
                Filter.CanEditSingleDevice = true;
                Filter.CanShowDetail = false;
                Filter.CanEditAllDevices =
                    newMaster.CurrentItem.StoreOperationId == (int) EnumStoreOperations.Accepting;
                Filter.CanDeleteDevice = true;
                Filter.MasterStoreDocumentId = newMaster.CurrentItem.StoreDocumentId;
                ClientSegment = newMaster.CurrentItem.StoreSegmentId;
                if (newMaster.CurrentItem != null)
                {
                    if (newMaster.CurrentItem.DocStatus != null)
                    {
                        Filter.CanChange = newMaster.CurrentItem.DocStatus.StoreDocStatusId ==
                                           (int) EnumStoreDocStatus.New;
                        if (Filter.CanChange)
                        {
                            //Если операция приёмка, возврат или приёмка на основание, тогда блокируем кнопку "Добавление оборудования со склада"
                            if (newMaster.CurrentItem.StoreOperationId ==
                                (int) EnumStoreOperations.Accepting
                                ||
                                newMaster.CurrentItem.StoreOperationId ==
                                (int) EnumStoreOperations.Return
                                ||
                                newMaster.CurrentItem.StoreOperationId ==
                                (int) EnumStoreOperations.AcceptingByNomenclature
                                 ||
                                newMaster.CurrentItem.StoreOperationId ==
                                (int)EnumStoreOperations.GroupRelocation
                            )
                            {
                                Filter.CanAddFromStore = false;
                            }
                            else
                            {
                                Filter.CanAddFromStore = true;
                            }
                          

                            Filter.CanAddFromFile = true;
                            Filter.CanFastMove = false;
                            Filter.CanMassChangeFromFile = false;

                            if (newMaster.CurrentItem.StoreOperationId ==
                              (int)EnumStoreOperations.ChangeDeviceParam)
                            {
                                Filter.CanMassChangeFromFile = true;
                                Filter.CanAddFromFile = false;
                                Filter.CanAddFromStore = false;
                            }


                            //Filter.CanAddSingleDevice = MainViewModel.Current.CheckPriveleges("StoreDeviceView.AddSingleDevice"); 
                            Filter.CanAddSingleDevice = true;
                        }
                    }

                    if (newMaster.CurrentItem.StoreOperation != null && newMaster.CurrentItem.EquipMarkaCom != null)
                    {
                        if (newMaster.CurrentItem.StoreOperation.StoreOperationId ==
                            (int) EnumStoreOperations.AcceptingByNomenclature)
                        {
                            _localEquipMarkaCom = newMaster.CurrentItem.EquipMarkaCom;
                            _localDeviceCondition = newMaster.CurrentItem.DeviceCondition;
                            _localDocOper = newMaster.CurrentItem.StoreOperation.StoreOperationId;
                        }
                    }

                }

                EnableContextMenu = false;
                EnableFakeReturnMenu = true;
            });
            RegisterMaster<IViewModel<StoreTree, StoreTreeFilter>>((container, master) =>
            {
                LocalizedTitle.Set(this, "Title_PoSeriynymNomeram", "По серийным номерам");
                TreeUzel = master.CurrentItem.Unid;
                Filter.Uzel = TreeUzel; //.ObjId;
                StoreName = master.CurrentItem.InfouzelSname; //.ObjName;
                Filter.TreeIsParent = true;
                Filter.ToolbarIsVisible = true;
                Filter.Mode = null;
                Filter.CanAddFromFile = false;
                Filter.CanEditAllDevices = false;
                Filter.CanFastMove = true;
                Filter.CanAddFromStore = false;
                Filter.CanAddSingleDevice = false;
                Filter.CanDeleteDevice = false;
                Filter.CanChange = true;
                Filter.MasterStoreDocumentId = null;
                Filter.StoreType = master.CurrentItem.Dopinfo2; //Надо в нормальное место тип склада засунуть!!!
                EnableContextMenu = true;
                Filter.CanShowDetail = true;
            });

            RegisterMaster<ReplaceCpeViewModel>((container, master) =>
            {
                TreeUzel = master.Filter.Uzel;
                Filter.Uzel = TreeUzel;
                Filter.ExploitStatus.ExploitStatusId = (int) EnumExploitStatus.OnStore;
                Filter.TreeIsParent = true;
                Filter.ToolbarIsVisible = false;
                Filter.Mode = null;
                Filter.CanAddFromFile = false;
                Filter.CanEditAllDevices = false;
                Filter.CanFastMove = false;
                Filter.CanAddFromStore = false;
                Filter.CanAddSingleDevice = false;
                Filter.CanDeleteDevice = false;
                Filter.CanChange = true;
                Filter.MasterStoreDocumentId = null;
                Filter.EnableExploitFilter = false;
                Filter.AcceptChanges();
                EnableContextMenu = false;
                EnableFakeReturnMenu = false;
                Filter.CanShowDetail = false;
            });

            if (MainViewModel.Current != null && !MainViewModel.Current.ReadinessAccountingEnabled)
            {
                IgoreParamList.Add("ReadinessTypes");
            }
            if (MainViewModel.Current != null && !MainViewModel.Current.SegmentAccountingEnabled)
            {
                IgoreParamList.Add("StoreSegmentName");
            }

            HeaderFilters.AddServerText("MarkacommName", "Модель", _ => string.Empty, value => Filter.EquipMarkaName = value);
            HeaderFilters.AddServerText("ResponsibleName", "МОЛ", _ => string.Empty, value => Filter.ResponsibleName = value);
            HeaderFilters.AddServerText("Uzel", "Узел", _ => string.Empty, value =>
            {
                if (value == null)
                    Filter.Uzel = TreeUzel;
            });
            HeaderFilters.AddServerText("ExploitStatus", "Статус эксплуатации", _ => string.Empty, value =>
            {
                if (value == null)
                    Filter.ExploitStatus = new Store.Models.Reference.ExploitStatus();
            });
            HeaderFilters.AddServerText("DeviceCondition", "Состояние оборудования", _ => string.Empty, value =>
            {
                if (value == null)
                    Filter.DeviceCondition = new DeviceCondition();
            });
            HeaderFilters.AddServerText("SosttechName", "Тех. состояние", _ => string.Empty, value =>
            {
                if (value == null)
                    Filter.EthTechStat = new EthTechStatRef();
            });

            HeaderFilters.AddServerText("SerialNumber", "Серийный №", _ => string.Empty, value => ApplyNumber(value, 1), "Number");
            HeaderFilters.AddServerText("MacAddress", "MAC-Адрес", _ => string.Empty, value => ApplyNumber(value, 2), "Number");
            HeaderFilters.AddServerText("InventoryNumber", "№ Инвентарный", _ => string.Empty, value => ApplyNumber(value, 3), "Number");
            HeaderFilters.AddServerText("PonSerial", "PON Номер", _ => string.Empty, value => ApplyNumber(value, 8), "Number");

            // HeaderFilters.AddServerText("Number", "Номер", _ => string.Empty, value => Filter.Number = value);

            HeaderFilters.Changed += (s, e) =>
            {
                Filter.AcceptChanges();
                Refresh();
            };
        }

        //private void ApplyNumber(string value, int type)
        //{
        //    if (string.IsNullOrEmpty(value))
        //    {
        //        if (Filter.DeviceTypeNum != null && Filter.DeviceTypeNum.TypnumKod == type)
        //        {
        //            Filter.Number = null;
        //            Filter.DeviceTypeNum = null;
        //        }
        //        return;
        //    }

        //    Filter.Number = value;
        //    Filter.DeviceTypeNum.TypnumKod = type;
        //}

        private int _numberKind;
        private void ApplyNumber(string value, int kind)
        {
            if (value == null && _numberKind != kind)
                return;

            _numberKind = value == null ? 0 : kind;
            Filter.DeviceTypeNum.TypnumKod = _numberKind;
            Filter.Number = value;
        }
        //Привилегии пользователя
        public override bool CanShowDialog(IDialogDirector viewDirector)
        {
            return base.CanShowDialog(viewDirector) &&
                   StoreExtensions.CanOpenDialog(viewDirector, this.ToString(), CurrentItem != null);
        }

        public override bool CanDelete(DeleteOperation operation)
        {
            var transferPrivilegeCount = MainViewModel/*Base*/.Current.UserPrivList.Where(x => x.FormName.Equals("Other") && x.ApplicationPrivilegesAlias.Contains("TransferInsert")).Count();
            var transferBackPrivilegeCount = MainViewModel/*Base*/.Current.UserPrivList.Where(x => x.FormName.Equals("Other") && x.ApplicationPrivilegesAlias.Contains("ReturnInsert")).Count();
            var unreservPrivilegeCount = MainViewModel/*Base*/.Current.UserPrivList.Where(x => x.FormName.Equals("Other") && x.ApplicationPrivilegesAlias.Contains("UnReservInsert")).Count();
            TransferCpeVisibility =
                EnableContextMenu &&
                (MainViewModel /*Base*/.Current.CheckPriveleges(
                    StoreExtensions.GetFormName(this.ToString()) + ".TransferCpe") || transferPrivilegeCount > 0)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            TransferBackCpeVisibility =
                EnableContextMenu &&
                (MainViewModel /*Base*/.Current.CheckPriveleges(
                    StoreExtensions.GetFormName(this.ToString()) + ".BackTransferCpe") || transferBackPrivilegeCount > 0)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            ReplaceCpeVisibility =
                EnableContextMenu &&
                MainViewModel /*Base*/.Current.CheckPriveleges(
                    StoreExtensions.GetFormName(this.ToString()) + ".ReplaceCpe")
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            UnReserveCpeVisibility =
                EnableContextMenu &&
                (MainViewModel /*Base*/.Current.CheckPriveleges(
                    StoreExtensions.GetFormName(this.ToString()) + ".UnReserveCpe") || unreservPrivilegeCount > 0)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            ChangeStatusCpeVisibility =
                EnableContextMenu &&
                MainViewModel /*Base*/.Current.CheckPriveleges(
                    StoreExtensions.GetFormName(this.ToString()) + ".ChangeStatusCpe")
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            TransferFakeBackCpeVisibility =
                EnableFakeReturnMenu &&
                (MainViewModel /*Base*/.Current.CheckPriveleges(
                    StoreExtensions.GetFormName(this.ToString()) + ".FakeBackTransferCpe") || transferBackPrivilegeCount > 0)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            RepaymentCpeVisibility =
                EnableContextMenu &&
                MainViewModel /*Base*/.Current.CheckPriveleges(
                    StoreExtensions.GetFormName(this.ToString()) + ".RepaymentCpe")
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            return base.CanDelete(operation) &&
                   MainViewModel /*Base*/.Current.CheckPriveleges(
                       StoreExtensions.GetFormName(this.ToString()) + ".Delete");
        }


        public override System.Type GetBaseModelType()
        {
            return typeof(IEquipment);
        }

        //Обновление заголовка при изменение количества записей.
        [DependsOn("ItemCount")]
        public string MyTitle
        {
            get
            {
                if (ItemCount > 0)
                    return base.Title + " (" + ItemCount + ")";
                return base.Title;
            }
        }

        [DependsOn("CurrentItem")]
        [DependsOnModel("ExploitStatusId")]
        public bool CanEdit
        {
            get
            {
              //  if (Filter.CanChange) return true;
                if (CurrentItem != null)
                {
                    if (CurrentItem.ExploitStatusId == (int) EnumExploitStatus.OnStore ||
                        CurrentItem.ExploitStatusId == (int) EnumExploitStatus.OnRepair ||
                        CurrentItem.ExploitStatusId == (int) EnumExploitStatus.WaitReturn)
                    {
                        return true;
                    }
                }

                return false;
            }
        }


        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _clearFilterCommand;

        //Команда очистки фильтра
        public ICommand ClearFilterCommand
        {
            get { return _clearFilterCommand ?? (_clearFilterCommand = new DelegateCommand(ClearFilter)); }
        }

        public void ClearFilter()
        {
            //Messenger.Default.Register<string>(this, MessageTypes.Information,
            //x => MessageBox.Show(x, "Данный серийный номер уже есть в списке.", MessageBoxButton.OK));
            Filter.Clear();
            Filter.Mode = null;
            Filter.SetExpanderText();
            Filter.AcceptChanges();
            Refresh();
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _changePageSizeCommand;

        //Команда изменения кол-ва строк в выборке
        public ICommand ChangePageSizeCommand
        {
            get { return _changePageSizeCommand ?? (_changePageSizeCommand = new DelegateCommand(ChangePageSize)); }
        }

        public void ChangePageSize()
        {
            if (Filter.DevicePageSize > 0)
            {
                PageSize = Filter.DevicePageSize;
                Refresh();
            }
            else
            {
                Filter.DevicePageSize = PageSize;
            }
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _myRefreshCommand;

        //Команда обновления содержимого
        public ICommand MyRefreshCommand
        {
            get { return _myRefreshCommand ?? (_myRefreshCommand = new DelegateCommand(MyRefresh, CanMyRefresh)); }
        }

        public void MyRefresh()
        {
            Filter.EquipMarkaCom = null;
            Filter.InvProjList = null;
            Filter.Ownership = null;
            Filter.Locations = null;
            Filter.Price = null;
            Filter.Mode = null;
            Refresh();
        }

        public bool CanMyRefresh()
        {
            return true;
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        public IEnumerable NewDeviceItems
        {
            get { return Items; }
            set
            {
                if (value != null)
                {
                    Items.Add(new StoreDevice());
                }
            }
        }



        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        public IEnumerable<StoreDeviceStruct> FileItems { get; set; }

        public IList NewFileDeviceItems
        {
            get { return Items.GetNewItems(); }
            set
            {
                //RejectAllChanges();
                if (value != null)
                {
                    var itemsf = value as IEnumerable<StoreDeviceStruct>;
                    if (itemsf == null) return;
                    FileItems = itemsf;
                    //Добавляем фейковую запись
                    if (FileItems.Any())
                    {
                        if (FileItems.Any(x => x.MarkaComNameS != null))
                            Items.Add(new StoreDevice());
                    }

                }
            }
        }


        public IList NewFastDeviceItems
        {
            get { return Items.GetNewItems(); }
            set
            {
                //RejectAllChanges();
                if (value != null)
                {
                    var itemsf = value as IEnumerable<StoreDeviceStruct>;
                    if (itemsf == null) return;
                    FileItems = itemsf;
                    //Добавляем фейковую запись
                    if (FileItems.Any())
                    {
                        //if (FileItems.Any(x => x.MarkaComNameS != null))
                        Items.Add(new StoreDevice());
                    }

                }
            }
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _addFromCommand;

        public ICommand AddFromCommand
        {
            get
            {
                return _addFromCommand ?? (_addFromCommand =
                    new DelegateCommand<AddDeviceFromFileViewModel>(x => AddFromFile(x.CheckedItems),
                        x => CanAddFromFile(x.CheckedItems)));
            }           
        }

        public bool CanAddFromFile(IEnumerable<StoreDeviceStruct> devices)
        {         
            if (devices == null) return false;
            return !IsExecuting && devices.Any();
        }

        public void AddFromFile(IEnumerable<StoreDeviceStruct> devices)
        {

            if (devices != null)
            {
                var storeDeviceStructs = devices.ToList();
                if (storeDeviceStructs.Any())
                {
                    FileItems = storeDeviceStructs;
                }
            }

            IRequest cmd;
            if (FileItems.Any())
            {
                var xml = ConvertList(FileItems);
                if (xml == null) return;
                cmd = Rep.AddDeviceToDocXml(xml);
            }
            else
            {
                cmd = Rep.AddDeviceFromFile(FileItems);
            }

            MainViewModel /*Base*/.Current.BusyMessage = Translation.Instance.Get("Status_AddDeviceToDoc", "Добавление оборудования в документ...");
            MainViewModel /*Base*/.Current.IsBusy = true;
            ExecuteAsync(cmd, x =>
            {
                MainViewModel /*Base*/.Current.IsBusy = false;
                Refresh();
            }, z =>
            {
                MainViewModel /*Base*/.Current.IsBusy = false;
                Refresh();
            });
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _editAllDevicesCommand;

        public ICommand EditAllDevicesCommand
        {
            //get { return _editAllDevicesCommand ?? (_editAllDevicesCommand = new DelegateCommand(EditAllDevicesCmd, EditAllDevices)); }
            get
            {
                return _editAllDevicesCommand ?? (_editAllDevicesCommand =
                    new DelegateCommand<AddDeviceFromFileViewModel>(x => EditAllDevicesCmd(x.CheckedItems),
                        x => EditAllDevices()));
            }
        }

        public bool EditAllDevices()
        {
            return true;
            //return NewFileDeviceItems != null && FileItems != null;
        }

        public void EditAllDevicesCmd(IEnumerable<StoreDeviceStruct> devices)
        {
            RejectAllChanges();
            var cmd = Rep.EditAllDevices(Filter, devices.ToList()[0]);
            ExecuteAsync(cmd, x =>
            {
                MainViewModel /*Base*/.Current.IsBusy = false;
                Refresh();
            }, z =>
            {
                MainViewModel /*Base*/.Current.IsBusy = false;
                Refresh();
            });
            AcceptAllChanges();

            //if (FileItems != null && FileItems.Count() > 0)
            //{
            //    var cmd = Rep.EditAllDevices(Filter, FileItems.ToList()[0]);
            //    MainViewModel/*Base*/.Current.BusyMessage = "Редактирование оборудования...";
            //    MainViewModel/*Base*/.Current.IsBusy = true;
            //ExecuteAsync(cmd, x =>
            //{
            //    MainViewModel/*Base*/.Current.IsBusy = false;
            //    Refresh();
            //}, z =>
            //{
            //    MainViewModel/*Base*/.Current.IsBusy = false;
            //    Refresh();
            //});
            //}
        }


        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private readonly ExportToExcel _exp = new ExportToExcel();

        private ICommand _exportCommand;

        //Команда экспорта в эксель
        public ICommand ExportCommand
        {
            get { return _exportCommand ?? (_exportCommand = new DelegateCommand(Export, CanExport)); }
        }

        public bool CanExport()
        {
            return Items.Count > 0;
            //return AutomationFactory.IsAvailable && Items.Count > 0 && _exp.CanExport();
        }

        //Экспорт в эксель
        public void Export()
        {
            _exp.HeaderColor = Colors.LightGray;
            _exp.Export(Items.Cast<Object>().ToList());
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _keyDownCommand;

        public ICommand KeyDownCommand
        {
            get { return _keyDownCommand ?? (_keyDownCommand = new DelegateCommand<object>(KeyDownCmd)); }
        }


        public void KeyDownCmd(object sender)
        {
            var e = (KeyEventArgs) sender;
            if (e.Key == Key.Enter)
            {
                ChangePageSize();
               // var tBox = (TextBox) e.OriginalSource;
            }
        }

        //ВЕРНУТЬ
        private ICommand _AttachCommand;

        public ICommand AttachCommand
        {
            get { return _AttachCommand ?? (_AttachCommand = new DelegateCommand(AttachCommandE, CanAttachCommand)); }
        }

        public void AttachCommandE()
        {
            var mainRep = new EquipMainDatumNodeRepository();
            var command = mainRep.GetAttachInfo((IEquipment) CurrentItem);
            ExecuteAsync(command,
                s => AppMessages.HttpResChange.Send(command.Result));

        }

        public bool CanAttachCommand()
        {
            return CurrentItem != null && !IsExecuting;

        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _mySearchCommand;

        public ICommand MySearchCommand
        {
            get { return _mySearchCommand ?? (_mySearchCommand = new DelegateCommand(MySearch, CanMySearch)); }
        }

        public void MySearch()
        {
            SearchCommand.Execute(null);
            Filter.SetExpanderText();
        }

        public bool CanMySearch()
        {
            return (Filter.StoreDocumentId != null || Filter.EquipMarkaName != null || Filter.DeviceTypeNum != null ||
                    Filter.Number != null
                    || Filter.ResponsibleName != null || Filter.Ownership != null || Filter.ExploitStatus != null ||
                    Filter.DeviceCondition != null || Filter.EthTechStat != null
                );
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        public ICommand ReportExportCommand
        {
            get
            {
                return _reportExportCommand ??
                       (_reportExportCommand = new DelegateCommand(ReportExport, CanReportExport));
            }
        }

        protected void ReportExport()
        {
            //Filter.StoreDocumentId = 1;
            //Filter.EquipMarkaName = "1";
            //Filter.NumberType = 0;
            //Filter.Number = "0";
            //Filter.ResponsibleName = "11";
            //Filter.OwnershipId = 1;
            //Filter.ExpStatusId = 1;
            //Filter.DevConditionId = 1;
            //Filter.Mode = 10;

            var reportParams = ReportClient.FillParameters(Filter);
            var typ = typeof(StoreDevice);

            //MainViewModel /*Base*/.Current.IsBusy = true;
            var reportScript =
                ReportClient.FillReportScript("/Store.Views;component/Extensions/StoreReports/StoreDevice.txt",
                    reportParams, typ);
            reportScript = reportScript.Replace("%Row%", PageIndex.ToString());
            reportScript = reportScript.Replace("%RowCount%", PageSize.ToString());
            reportScript = reportScript.Replace("%UzelName%", StoreName);
            reportScript = reportScript.Replace("%StoreName%", StoreName);
            //reportScript = reportScript.Replace("%SosttechKod%", Filter.EthTechStat.SosttechKod.ToString());


            var reportClient = new ReportClient();
            //reportClient.ReportOpened += (s, e) => MainViewModel /*Base*/.Current.IsBusy = false;
            reportClient.OpenHtmlReport(reportScript,
                new Uri(MainViewModel /*Base*/.Current.AppConfig.ReportUrlGetFull, UriKind.Absolute),
                new Uri(MainViewModel /*Base*/.Current.AppConfig.ReportUrlPostFull, UriKind.Absolute));
        }

        protected bool CanReportExport()
        {
            return Items.Count > 0;
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _transferCpeCommand;

        //Команда передать оборудование абоненту
        public ICommand TransferCpeCommand
        {
            get
            {
                return _transferCpeCommand ??
                       (_transferCpeCommand = new DelegateCommand(TransferCpeCmd, CanTransferCpeCmd));
            }
        }

        public bool CanTransferCpeCmd()
        {
            if (CurrentItem == null) return false;
            var curExpStat = CurrentItem.ExploitStatusId;
            var oldPrivilegeForTransfer = MainViewModel/*Base*/.Current.UserPrivList.Where(x => x.FormName.Equals("StoreDeviceView") && x.ApplicationPrivilegesAlias.Contains("TransferCpe")).Count();
            var privilegesCount = MainViewModel/*Base*/.Current.UserPrivList.Where(x => x.FormName.Equals("Other") && x.ApplicationPrivilegesAlias.Contains("TransferInsert")).Count();
            return (curExpStat == (int) EnumExploitStatus.OnStore) && (privilegesCount > 0 || oldPrivilegeForTransfer > 0);
            //return CurrentItem != null; //&&!IsExecuting;
        }

        //передать обрудование
        public void TransferCpeCmd()
        {
            if (CurrentItem.CommId == null) return;
            var vm = new TransferCpeViewModel();
            vm.Filter.SerialNumber = CurrentItem.SerialNumber;
            vm.Filter.CommId = CurrentItem.CommId;
            vm.Filter.UserCreate = MainViewModel /*Base*/.Current.Login;
            vm.Filter.AcceptChanges();

            Dlg = new EditDialogDirector
            {
                Operation = Operation.Insert,
                Title = "Передать оборудование абоненту",
                OKButtonContent = "Выполнить",
                CancelButtonContent = "Отмена",
                //UpdateTargetProperties = true,
                ViewModel = vm,
                ApplyCommand = MyRefreshCurrentItemCommand,
                ApplyCommandParameter = "Оборудование передано абоненту."

            };

            var z = Dlg.ShowDialog();
            //Dlg.ViewModel.Accepted += (s, e) =>
            //{
            //    MessageBox.Show("Оборудование передано абоненту.");
            //    RefreshCurrentItem();
            //};
        }


        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _transferBackCpeCommand;

        //Команда передать оборудование абоненту
        public ICommand TransferBackCpeCommand
        {
            get
            {
                return _transferBackCpeCommand ?? (_transferBackCpeCommand =
                    new DelegateCommand(TransferBackCpeCmd, CanTransferBackCpeCmd));
            }
        }

        public bool CanTransferBackCpeCmd()
        {
            if (CurrentItem == null) return false;
            var curExpStat = CurrentItem.ExploitStatusId;
            return (curExpStat == (int) EnumExploitStatus.Abon || curExpStat == (int) EnumExploitStatus.WaitReturn ||
                    curExpStat == (int) EnumExploitStatus.WaitResell ||
                    curExpStat == (int) EnumExploitStatus.AbonNoService);
            //return CurrentItem != null;//&& !IsExecuting;
        }

        //передать обрудование
        public void TransferBackCpeCmd()
        {
            if (CurrentItem == null) return;
            if (CurrentItem.CommId == null) return;
            var vm = new BackTransferCpeViewModel();
            vm.Filter.SerialNumber = CurrentItem.SerialNumber;
            vm.Filter.CommId = CurrentItem.CommId;
            vm.Filter.TypeDeviceKod = CurrentItem.TypdeviceKod;
            vm.Filter.AcceptChanges();

            Dlg = new EditDialogDirector
            {
                Operation = Operation.Insert,
                Title = "Вернуть оборудование на склад",
                OKButtonContent = "Выполнить",
                CancelButtonContent = "Отмена",
                //UpdateTargetProperties = true,
                ViewModel = vm,
                ApplyCommand = MyRefreshCurrentItemCommand,
                ApplyCommandParameter = "Оборудование возвращено."

            };

            var z = Dlg.ShowDialog();
            //Dlg.ViewModel.Accepted += (s, e) =>
            //{
            //    MessageBox.Show("Оборудование возвращено.");
            //    RefreshCurrentItem();
            //    //Refresh();
            //};
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _transferFakeBackCpeCommand;

        //Команда передать оборудование абоненту
        public ICommand TransferFakeBackCpeCommand
        {
            get
            {
                return _transferFakeBackCpeCommand ?? (_transferFakeBackCpeCommand =
                    new DelegateCommand(TransferFakeBackCpeCmd, CanTransferFakeBackCpeCmd));
            }
        }

        public bool CanTransferFakeBackCpeCmd()
        {
            if (CurrentItem == null) return false;
            var curExpStat = CurrentItem.ExploitStatusId;
            return (curExpStat == (int) EnumExploitStatus.OnStore);
            //return CurrentItem != null; //&&!IsExecuting;
        }

        //передать обрудование
        public void TransferFakeBackCpeCmd()
        {
            if (CurrentItem.CommId == null) return;
            var vm = new TransferCpeViewModel();
            vm.Filter.SerialNumber = CurrentItem.SerialNumber;
            vm.Filter.CommId = CurrentItem.CommId;
            vm.Filter.UserCreate = MainViewModel /*Base*/.Current.Login;
            vm.Filter.FakeTransfer = 1;
            vm.Filter.AcceptChanges();

            Dlg = new EditDialogDirector
            {
                Operation = Operation.Insert,
                Title = "Возврат оборудования до 01.04.2012",
                OKButtonContent = "Выполнить",
                CancelButtonContent = "Отмена",
                //UpdateTargetProperties = true,
                ViewModel = vm,
                ApplyCommand = MyRefreshCurrentItemCommand,
                ApplyCommandParameter = "Оборудование возвращено."

            };

            var z = Dlg.ShowDialog();
            //Dlg.ViewModel.Accepted += (s, e) =>
            //{
            //    MessageBox.Show("Оборудование возвращено.");
            //    RefreshCurrentItem();
            //};
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _replaceCpeCommand;

        //Команда передать оборудование абоненту
        public ICommand ReplaceCpeCommand
        {
            get
            {
                return _replaceCpeCommand ??
                       (_replaceCpeCommand = new DelegateCommand(ReplaceCpeCmd, CanReplaceCpeCmd));
            }
        }

        public bool CanReplaceCpeCmd()
        {
            if (CurrentItem == null) return false;
            var curExpStat = CurrentItem.ExploitStatusId;
            return (curExpStat == (int) EnumExploitStatus.Abon || curExpStat == (int) EnumExploitStatus.WaitReturn ||
                    curExpStat == (int) EnumExploitStatus.WaitResell ||
                    curExpStat == (int) EnumExploitStatus.AbonNoService);
            //return CurrentItem != null; //&&!IsExecuting;
        }

        //передать обрудование
        public void ReplaceCpeCmd()
        {
            if (CurrentItem.CommId == null) return;
            var vm = new ReplaceCpeViewModel();
            vm.Filter.SerialNumber = CurrentItem.SerialNumber;
            vm.Filter.CommId = CurrentItem.CommId;
            vm.Filter.UserCreate = MainViewModel /*Base*/.Current.Login;
            vm.Filter.Uzel = Filter.Uzel;
            vm.Filter.TypeDeviceKod = CurrentItem.TypdeviceKod;
            vm.Filter.AcceptChanges();

            Dlg = new EditDialogDirector
            {
                Operation = Operation.Insert,
                Title = "Замена оборудования",
                OKButtonContent = "Выполнить",
                CancelButtonContent = "Отмена",
                // UpdateTargetProperties = true,
                ViewModel = vm,
                ApplyCommand = MyRefreshCurrentItemCommand,
                ApplyCommandParameter = "Оборудование заменено."

            };

            var z = Dlg.ShowDialog();
            //Dlg.ViewModel.Accepted += (s, e) =>
            //{
            //    MessageBox.Show("Оборудование заменено.");
            //    RefreshCurrentItem();
            //};
        }

        //--------------------------------------------------------------------------------------------------------
        private ICommand _storeDeviceActivCmd;

        public ICommand StoreDeviceActivCmd
        {
            get
            {
                return _storeDeviceActivCmd ?? (_storeDeviceActivCmd =
                    new DelegateCommand<DialogDirector>(StoreDeviceActive, CanStoreDeviceActive));
            }
        }

        public bool CanStoreDeviceActive(DialogDirector director)
        {
            return CurrentItem != null;

        }

        //public IDeviceRepository Rep
        //{
        //    get { return (IDeviceRepository)Repository; }
        //}
        public void StoreDeviceActive(DialogDirector director)
        {
            //var urlButton2 = new HyperlinkButton { FontSize = 14, Content = "", TargetName = "_blank", HorizontalAlignment = HorizontalAlignment.Stretch };
            //var url2 = new Uri("http://10.36.138.147/WebReports/reports/view/3/1/3");
            //urlButton2.NavigateUri = url2;
            //WebBrowserOpener.OpenUrl(urlButton2.NavigateUri);

            var cur = CurrentItem;
            var cmd = Rep.ActivateDevice(cur);
            ExecuteAsync(cmd, s =>
            {
                //var urlButton = new HyperlinkButton { FontSize = 14, Content = "", TargetName = "_blank", HorizontalAlignment = HorizontalAlignment.Stretch };
                //var url = new Uri("http://ya.ru");
                //urlButton.NavigateUri = url;
                //WebBrowserOpener.OpenUrl(urlButton.NavigateUri);
                ////WebBrowserOpener.OpenUrl("http://ya.ru");
                var z =
                    (((
                        DatumNode.Commands.StoreCpe.Store.Api.MeasureDslamStore
                        <object, List<object>>) (cmd.Command)).Params).PUrl;

                var h = (((
                    DatumNode.Commands.StoreCpe.Store.Api.MeasureDslamStore
                    <object, List<object>>) (cmd.Command)).Params).PResult;
                //WebBrowserOpener.OpenUrl(h.ToString());
                //MessageBox.Show(h.ToString());
                //if (h == 0)
                //{
                //    WebBrowserOpener.OpenUrl(z.ToString());
                //}
                //var a = cmd;
            });
        }

        private ICommand _moveBackPackCommand;

        //Команда передать оборудование абоненту
        public ICommand MoveBackPackCommand
        {
            get
            {
                return _moveBackPackCommand ??
                       (_moveBackPackCommand = new DelegateCommand(MoveBackPackCmd, CanMoveBackPackCmd));
            }
        }

        public bool CanMoveBackPackCmd()
        {
            //Склад абонентского оборудования
            return Filter.StoreType == "4" &&
                   MainViewModel /*Base*/.Current.CheckPriveleges(
                       StoreExtensions.GetFormName(this.ToString()) + ".MoveBackPack");
            //return CurrentItem != null; //&&!IsExecuting;
        }

        //Быстрое перемещение в рюкзак
        public void MoveBackPackCmd()
        {
            var vm = new DeviceFastMoveViewModel();
            vm.Filter.Uzel = Filter.Uzel;
            vm.Filter.AcceptChanges();

            Dlg = new EditDialogDirector
            {
                Operation = Operation.Insert,
                Title = "Быстрое перемещение в рюкзак",
                OKButtonContent = "Переместить",
                CancelButtonContent = "Отмена",
                //UpdateTargetProperties = true,
                ViewModel = vm,
                ApplyCommand = MyRefreshCurrentItemCommand,
                ApplyCommandParameter = "Оборудование передано."
                //,
                //Width = 800,
                //Height = 490                
            };

            var z = Dlg.ShowDialog();
            //Dlg.ViewModel.Accepted += (s, e) =>
            //{
            //    MessageBox.Show("Оборудование передано.");
            //    RefreshCurrentItem();
            //};
        }

        [DependsOn("IsLoaded")]
        public Visibility EditAllDevicesVisibility
        {
            get
            {
                return Filter.CanEditAllDevices && Items != null && Items.Count != 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _unReserveCpeCommand;

        //Команда для исключения/включения документа в отчет.
        public ICommand UnReserveCpeCommand
        {
            get
            {
                return _unReserveCpeCommand ??
                       (_unReserveCpeCommand = new DelegateCommand(UnReserveCpeCmd, CanUnReserveCpeCommand));
            }
        }

        public bool CanUnReserveCpeCommand()
        {
            if (CurrentItem == null) return false;
            var curExpStat = CurrentItem.ExploitStatusId;
            var oldPrivilegeForUnreserv = MainViewModel/*Base*/.Current.UserPrivList.Where(x => x.FormName.Equals("StoreDeviceView") && x.ApplicationPrivilegesAlias.Contains("UnReserveCpe")).Count();
            var privilegesCount = MainViewModel/*Base*/.Current.UserPrivList.Where(x => x.FormName.Equals("Other") && x.ApplicationPrivilegesAlias.Contains("UnReservInsert")).Count();
            return (curExpStat == (int) EnumExploitStatus.Reserve && !IsExecuting) && (oldPrivilegeForUnreserv > 0 || privilegesCount > 0);
            //return CurrentItem != null &&!IsExecuting;
        }

        //Передать обрудование
        public void UnReserveCpeCmd()
        {

            if (CurrentItem == null) return;
            var filter = new UnReserveCpeFilter();
            var entity = new UnReserveCpe();
            var rep = new UnReserveCpeRepository();
            filter.CommId = CurrentItem.CommId;

            var mes = string.Format(Translation.Instance.Get("Confirm_RemoveReserve", "Снять бронь для: {0} ?"), CurrentItem.SerialNumber);
            Messenger.Default.Send(new NotificationMessageAction(mes, () =>
                {
                    MainViewModel /*Base*/.Current.BusyMessage = Translation.Instance.Get("Status_RemoveReserve", "Сятие брони...");
                    MainViewModel /*Base*/.Current.IsBusy = true;
                    ExecuteAsync(rep.UnReserveCpe(filter, entity), s =>
                    {
                        MainViewModel /*Base*/.Current.IsBusy = false;
                        RefreshCurrentItem();
                        Messenger.Default.Send(new NotificationMessage(Translation.Instance.Get("Message_ReserveRemoved", "Бронь снята.")));
                        //MessageBox.Show(Translation.Instance.Get("Message_ReserveRemoved", "Бронь снята."));
                    }, r =>
                    {
                        MainViewModel /*Base*/.Current.IsBusy = false;
                        RefreshCurrentItem();
                    });

                }),
                MainViewModel.MessageTypes.Confirm);
        }

        //--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==--==
        private ICommand _changeStatusCpeCommand;

        //Команда передать оборудование абоненту
        public ICommand ChangeStatusCpeCommand
        {
            get
            {
                return _changeStatusCpeCommand ?? (_changeStatusCpeCommand =
                    new DelegateCommand(ChangeStatusCpeCmd, CanChangeStatusCpeCmd));
            }
        }

        public bool CanChangeStatusCpeCmd()
        {
            if (CurrentItem == null) return false;
            var curExpStat = CurrentItem.ExploitStatusId;
            return (curExpStat != (int) EnumExploitStatus.Abon || curExpStat != (int) EnumExploitStatus.WaitReturn ||
                    curExpStat != (int) EnumExploitStatus.WaitResell ||
                    curExpStat != (int) EnumExploitStatus.AbonNoService && !IsExecuting);
            //return CurrentItem != null; //&&!IsExecuting;
        }

        //передать обрудование
        public void ChangeStatusCpeCmd()
        {
            if (CurrentItem.CommId == null) return;
            var vm = new ChangeStatusCpeViewModel();
            vm.Filter.SerialNumber = CurrentItem.SerialNumber;
            vm.Filter.CommId = CurrentItem.CommId;
            //vm.Filter.UserCreate = MainViewModel/*Base*/.Current.Login;
            //vm.Filter.Uzel = Filter.Uzel;
            vm.Filter.AcceptChanges();

            Dlg = new EditDialogDirector
            {
                Operation = Operation.Insert,
                Title = "Изменение статуса эксплуатации",
                OKButtonContent = "Выполнить",
                CancelButtonContent = "Отмена",
                //UpdateTargetProperties = true,
                ViewModel = vm,
                ApplyCommand = MyRefreshCurrentItemCommand,
                ApplyCommandParameter = "Статус оборудования изменён."

            };

            var z = Dlg.ShowDialog();
            //Dlg.ViewModel.Accepted += (s, e) =>
            //{
            //    MessageBox.Show("Статус оборудования изменён.");
            //    RefreshCurrentItem();
            //};

        }
        //<toolkit:MenuItem Header="Выкуп из аренды"    Command="{Binding Path=RepaymentCpeCommand}" Visibility="{Binding Path=RepaymentCpeVisibility}" IsEnabled="{Binding Path=CanRepaymentCpe, Mode=TwoWay}" />

        private ICommand _repaymentCpeCommand;

        //Команда передать оборудование абоненту
        public ICommand RepaymentCpeCommand
        {
            get
            {
                return _repaymentCpeCommand ??
                       (_repaymentCpeCommand = new DelegateCommand(RepaymenCpeCmd, CanRepaymenCpeCmd));
            }
        }

        public bool CanRepaymenCpeCmd()
        {
            if (CurrentItem == null) return false;
            var curExpStat = CurrentItem.ExploitStatusId;
            var own = CurrentItem.OwnershipId;
            return (curExpStat == (int) EnumExploitStatus.Abon && own == 1); //1=собственность общества
        }

        //передать обрудование
        public void RepaymenCpeCmd()
        {
            if (CurrentItem.CommId == null) return;
            var vm = new RepaymentCpeViewModel();
            vm.Filter.SerialNumber = CurrentItem.SerialNumber;
            vm.Filter.CommId = CurrentItem.CommId;
            vm.Filter.UserCreate = MainViewModel /*Base*/.Current.Login;
            vm.Filter.AcceptChanges();

            Dlg = new EditDialogDirector
            {
                Operation = Operation.Insert,
                Title = "Выкуп оборудования из аренды",
                OKButtonContent = "Выполнить",
                CancelButtonContent = "Отмена",
                //UpdateTargetProperties = true,
                ViewModel = vm,
                ApplyCommand = MyRefreshCurrentItemCommand,
                ApplyCommandParameter = "Операция выполнена успешно."

            };

            var z = Dlg.ShowDialog();
            //Dlg.ViewModel.Accepted += (s, e) =>
            //{
            //    MessageBox.Show("Операция выполнена успешно.");
            //    RefreshCurrentItem();
            //};
        }

        public void CntCmdExec()
        {
            if (Filter != null)
            {
                var cmd = Repository.CreateCountCommand(Filter);
                ExecuteAsync(cmd, x =>
                {
                    TotalItemCount = cmd.Result;
                    NotifyPropertyChanged("TotalItemCount");
                });
            }
        }

        //[DependsOn("CurrentItem")]
        //[DependsOnModel("ExploitStatusId")]
        //public override bool CanDeleteSelectedItems()
        //{
        //    if (CurrentItem != null && CurrentItem.ExploitStatusId == (int) EnumExploitStatus.Reserve)
        //        return false;
        //    return base.CanDeleteSelectedItems();
        //}

        public string ConvertList(IEnumerable<StoreDeviceStruct> list)
        {
            if (list == null) return null;
            var first = list.FirstOrDefault();
            var l = new List<CpeInfo>();
            var cnt = list.Count();

            foreach (var item in list)
            {
                var it = new CpeInfo()
                {
                    SerialNumber = item.SerialNumberS,
                    InventoryNumber = item.InventoryNumberS,
                    MacAddress = item.MacAddressS,
                    PonSerial = item.PonSerialS,
                    PalletNumber = item.PalletNumberS,
                    CellPhoneNumber = item.CellPhoneNumberS,
                    CellPhoneTariff = item.CellPhoneTariffS
                };
                if (item.EquipMarkaComS != null) it.ModelId = item.EquipMarkaComS.MarkacommKod;
                l.Add(it);
            }

            if (first != null && first.DeviceConditionS.DevConditionId != null &&
                first.EquipMarkaComS.MarkacommKod != null && first.EthTechStatRefS.SosttechKod != null &&
                first.OwnershipS.OwnershipId != null &&
                first.StoreDocumentIdS != null && first.UzelKodS != null// &&
               // first.StoreReadinessTypeS?.StoreReadinessTypeId != null
            )
            {
                return StoreExtensions.GetXml(new StoreDeviceStructShort
                {
                    CpeInfoList = l,
                    DeviceConditionId = (decimal) first.DeviceConditionS.DevConditionId,
                    EquipMarkaComId = (decimal) first.EquipMarkaComS.MarkacommKod,
                    EthTechStatId = (decimal) first.EthTechStatRefS.SosttechKod,
                    InvProjId = first.InvProjListS.InvprojId,
                    Locations = first.LocationsS,
                    OwnershipId = (decimal) first.OwnershipS.OwnershipId,
                    Price = first.PriceCoinS,
                    StoreDocumentId = (decimal) first.StoreDocumentIdS,
                    UzelKod = (decimal) first.UzelKodS,
                    DeviceCount = cnt,
                    PalletId = first.PalletId,
                    PalletNumber = first.PalletNumberS,
                    ClientSegment = ClientSegment,
                    StoreReadinessType = (decimal) first.StoreReadinessTypeS?.StoreReadinessTypeId

                });
            }

            return null;
        }

        //
        public override bool CanAcceptChanges(bool refresh)
        {
            if (CurrentItem == null) return false;
            var currentItem = CurrentItem;
            if (currentItem != null)
            {
                if (!currentItem.IsDeleted && !IsExecuting)
                    return true;
            }

            return false;
        }

        //Команда для удаления всего оборудования из документа
        private ICommand _delAllDeviceCommand;

        public ICommand DelAllDeviceCommand
        {
            get
            {
                return _delAllDeviceCommand ??
                       (_delAllDeviceCommand = new DelegateCommand(DelAllDevice, CanDelAllDevice));
            }
        }

        public bool CanDelAllDevice()
        {
            return MainViewModel /*Base*/.Current.CheckPriveleges(
                StoreExtensions.GetFormName(this.ToString()) + ".Delete") && Items.Count() > 0;
        }

        public void DelAllDevice()
        {

        }

        private ICommand _applyCommand;

        public ICommand ApplyCommand
        {
            get
            {
                return _applyCommand ?? (_applyCommand =
                    new DelegateCommand<AddDeviceFromStoreViewModel>(x => ApplyCommandE(x.CheckedItems),
                        x => CanApplyCommand()));
            }
        }

        public void ApplyCommandE(IEnumerable<StoreDevice> devices)
        {
            RejectAllChanges();


            //ResultCheckedItems = new List<StoreDevice>(Items.Where(x => x.ChBox));
            //if (ResultCheckedItems.Any())
            //{
            //    var cmd = Repository.CreateInsertBatchCommand(Filter, ResultCheckedItems);
            //}
            //ResultCheckedItems = new List<StoreDevice>(Items.Where(x => x.ChBox));
            if (devices == null || !devices.Any()) return;

            MainViewModel.Current.BusyMessage = Translation.Instance.Get("Status_AddDeviceToDoc", "Добавление оборудования в документ...");
            MainViewModel.Current.IsBusy = true;

            Items.AddRange(devices.Select(dev => new StoreDevice()
            {
                CommId = dev.CommId,
                SerialNumber = dev.SerialNumber,
                Ownership = dev.Ownership,
                EquipMarkaCom = dev.EquipMarkaCom,
                InventoryNumber = dev.InventoryNumber,
                InvProjList = dev.InvProjList,
                Locations = dev.Locations,
                Price = dev.Price,
                DeviceCondition = dev.DeviceCondition
            }));

            //foreach (var dev in devices)
            //{
            //    Items.Add(new StoreDevice()
            //    {
            //        CommId = dev.CommId,
            //        SerialNumber = dev.SerialNumber,
            //        Ownership = dev.Ownership,
            //        EquipMarkaCom = dev.EquipMarkaCom,
            //        InventoryNumber = dev.InventoryNumber,
            //        InvProjList = dev.InvProjList,
            //        Locations = dev.Locations,
            //        Price = dev.Price,
            //        DeviceCondition = dev.DeviceCondition

            //    });
            //}
            //MainViewModel.Current.IsBusy = false;
            AcceptAllChanges();
            Refresh();
        }

        protected override void OnAcceptAllError(EventArgs e)
        {
            base.OnAcceptAllError(e);
            MainViewModel.Current.IsBusy = false;
        }

        protected override void OnAcceptedAll(EventArgs e)
        {
            base.OnAcceptedAll(e);
            MainViewModel.Current.IsBusy = false;
        }


        public bool CanApplyCommand()
        {
            return !IsExecuting;
        }

        //Команда для для обновления текущей записи
        private ICommand _myRefreshCurrentItemCommand;

        public ICommand MyRefreshCurrentItemCommand
        {
            get
            {
                return _myRefreshCurrentItemCommand ?? (_myRefreshCurrentItemCommand =
                    new DelegateCommand<string>(RefrCurrItem, CanRefrCurrItem));
            }
        }

        public bool CanRefrCurrItem(string message)
        {
            return !IsExecuting && CurrentItem != null;
        }

        public void RefrCurrItem(string message)
        {
            if (message != null) MessageBox.Show(message);
            RefreshCurrentItem();
        }


        public void EthTechStatRefOnSelectionChanged()
        {
            if (CurrentItem is not null)
            if (CurrentItem.EthTechStatRef is not null)
            switch (CurrentItem.EthTechStatRef.SosttechKod)
            {
                case 1:
                    CurrentItem.StateConditions = null;
                    CurrentItem.RepairReason = null;
                    break;

                case 2:
                    CurrentItem.ReadinessTypes = new StoreReadinessTypes() {StoreReadinessTypeId = 2};
                    break;

                default: break;
            }
        }

        public void StateConditionsOnSelectionChanged()
        {
            if (CurrentItem.StateConditions == null)
            {
                return;
            }

            switch (CurrentItem.StateConditions.StoreStateConditionId)
            {
                case 1:
                    CurrentItem.RepairReason = null;
                    break;

                default: break;
            }
        }

        private bool _sparesMode = false;

        public bool SparesMode
        {
            get { return _sparesMode; }
            set
            {
                SetValue(ref _sparesMode, value, () => SparesMode);
                if (value)
                {
                    Filter.ShowSpares = 1;
                    Filter.AcceptChanges();
                    Refresh();
                }
            }
        }

        private bool _CPEMode = true;

        public bool CPEMode
        {
            get { return _CPEMode; }
            set
            {
                SetValue(ref _CPEMode, value, () => CPEMode);
                if (value)
                {
                    Filter.ShowSpares = null;
                    Filter.AcceptChanges();
                    Refresh();
                }
            }
        }

        public bool ReadinessAccountingEnabled
        {
            get
            {
                return MainViewModel /*Base*/.Current.ReadinessAccountingEnabled;
            }
        }


        private ICommand _showBalanceCommand;
        public ICommand ShowBalanceCommand
        {
            get { return _showBalanceCommand ?? (_showBalanceCommand = new DelegateCommand(ShowBalancecmd)); }
        }
        protected void ShowBalancecmd()
        {
            HeaderFilters.Clear();
            ListMode.ShowBalance = true;
        }

        private void RegisterHeaderFilters()
        {
            HeaderFilters.AddServerText(
                "MarkacommName",
                "Модель",
                _ => string.Empty,
                value => Filter.EquipMarkaName = value);

            HeaderFilters.AddServerText(
                "ResponsibleName",
                "МОЛ",
                _ => string.Empty,
                value => Filter.ResponsibleName = value);

            //// CreateSelectCommand читает DeviceTypeNum.TypnumKod, когда Number не пустой.
            //// Если тип номера не задан, перед записью Number его нужно выставить, иначе селектор упадёт.
            //HeaderFilters.AddServerText(
            //    "Number",
            //    "Номер",
            //    _ => string.Empty,
            //    value => Filter.Number = value);

            HeaderFilters.Changed += (s, e) =>
            {
                Filter.AcceptChanges();
                Refresh();
            };
        }


        //public bool RepairReasonEnabled
        //{
        //    get
        //    {
        //        int sosttechKod = 0;
        //        if(MainViewModel /*Base*/.Current.ReadinessAccountingEnabled)
        //        {
        //            if (CurrentItem.StateConditions != null && CurrentItem.StateConditions.IsNeedRepairReason > 0) sosttechKod = (int)CurrentItem.StateConditions.IsNeedRepairReason;
        //            switch (sosttechKod)
        //            {
        //                case 1:
        //                    return true; break;
        //                default:
        //                    return false; break;

        //            }
        //        }
        //        if (CurrentItem.EthTechStatRef != null && CurrentItem.EthTechStatRef.SosttechKod > 0) sosttechKod = (int)CurrentItem.EthTechStatRef.SosttechKod;
        //        switch (sosttechKod)
        //        {
        //            case 2:
        //                return true; break;
        //            default: return false; break;

        //        }



        //    }
        //}

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
            IRequest cmd = Rep.VerifyWarServRepToDoc(xml, Filter?.StoreDocumentId);

            MainViewModel.Current.BusyMessage = Translation.Instance.Get("Status_AddIdentifierChange", "Добавление смены идентификаторов в документ...");
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

    //   
}