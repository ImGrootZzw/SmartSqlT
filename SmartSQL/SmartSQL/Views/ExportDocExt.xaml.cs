using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using HandyControl.Controls;
using HandyControl.Data;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using SmartSQL.Annotations;
using SmartSQL.DocUtils;
using SmartSQL.DocUtils.Dtos;
using SmartSQL.Framework;
using SmartSQL.Framework.Const;
using SmartSQL.Framework.Exporter;
using SmartSQL.Framework.PhysicalDataModel;
using SmartSQL.Framework.SqliteModel;
using SmartSQL.Framework.Util;
using SmartSQL.Models;
using SqlSugar;
using DbType = SqlSugar.DbType;

namespace SmartSQL.Views
{
    /// <summary>
    /// ExportDoc.xaml 的交互逻辑
    /// </summary>
    public partial class ExportDocExt : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private static readonly string GROUPICON = "pack://application:,,,/Resources/svg/category.svg";
        private static readonly string TABLEICON = "pack://application:,,,/Resources/svg/table.svg";
        private static readonly string VIEWICON = "pack://application:,,,/Resources/svg/view.svg";
        private static readonly string PROCICON = "pack://application:,,,/Resources/svg/proc.svg";
        #region DependencyProperty
        public static readonly DependencyProperty SelectedConnectionProperty = DependencyProperty.Register(
            "SelectedConnection", typeof(ConnectConfigs), typeof(ExportDoc), new PropertyMetadata(default(ConnectConfigs)));
        /// <summary>
        /// 当前连接
        /// </summary>
        public ConnectConfigs SelectedConnection
        {
            get => (ConnectConfigs)GetValue(SelectedConnectionProperty);
            set => SetValue(SelectedConnectionProperty, value);
        }

        public static readonly DependencyProperty SelectedDataBaseProperty = DependencyProperty.Register(
            "SelectedDataBase", typeof(DataBase), typeof(ExportDoc), new PropertyMetadata(default(DataBase)));
        /// <summary>
        /// 当前数据库
        /// </summary>
        public DataBase SelectedDataBase
        {
            get => (DataBase)GetValue(SelectedDataBaseProperty);
            set => SetValue(SelectedDataBaseProperty, value);
        }

        public static readonly DependencyProperty ExportDataProperty = DependencyProperty.Register(
            "ExportData", typeof(List<PropertyNodeItem>), typeof(ExportDoc), new PropertyMetadata(default(List<PropertyNodeItem>)));
        /// <summary>
        /// 导出目标数据
        /// </summary>
        public List<PropertyNodeItem> ExportData
        {
            get => (List<PropertyNodeItem>)GetValue(ExportDataProperty);
            set
            {
                SetValue(ExportDataProperty, value);
                OnPropertyChanged(nameof(ExportData));

            }
        }

        public static readonly DependencyProperty ExportTypeProperty = DependencyProperty.Register(
            "ExportType", typeof(ExportEnum), typeof(ExportDoc), new PropertyMetadata(default(ExportEnum)));
        /// <summary>
        /// 导出类型
        /// </summary>
        public ExportEnum ExportType
        {
            get => (ExportEnum)GetValue(ExportTypeProperty);
            set => SetValue(ExportTypeProperty, value);
        }
        #endregion

        public ExportDocExt()
        {
            InitializeComponent();
            DataContext = this;
            TxtPath.Text = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        private void ExportDoc_OnLoaded(object sender, RoutedEventArgs e)
        {
            Title = $"{SelectedDataBase.DbName} - {Title}";
            TxtFileName.Text = SelectedDataBase.DbName + "数据库设计文档";
            var dbInstance = ExporterFactory.CreateInstance(SelectedConnection.DbType, SelectedConnection.DbMasterConnectString);
            var list = dbInstance.GetDatabases();
            SelectDatabase.ItemsSource = list;
            HidSelectDatabase.Text = SelectedDataBase.DbName;
            SelectDatabase.SelectedItem = list.FirstOrDefault(x => x.DbName == SelectedDataBase.DbName);
            MenuBind(false, null);
        }

        private void BtnLookPath_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
            {
                TxtPath.Text = dialog.FileName;
            }
        }


        /// <summary>
        /// 选择数据库发生变更
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectDatabase_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }
            var selectDatabase = SelectDatabase.SelectedItem;
            if (selectDatabase != null)
            {
                var selectedDbBase = (DataBase)selectDatabase;
                HidSelectDatabase.Text = ((DataBase)selectDatabase).DbName;
                var sqLiteHelper = new SQLiteHelper();
                sqLiteHelper.SetSysValue(SysConst.Sys_SelectedDataBase, selectedDbBase.DbName);
                MenuBind(false, null);
            }
        }

        private Model dataSource = new Model();
        public void MenuBind(bool isCompare, Model compareData)
        {
            #region MyRegion
            LoadingLine.Visibility = Visibility.Visible;
            NoDataText.Visibility = Visibility.Collapsed;
            /////TreeViewTables.ItemsSource = null;
            var selectDataBase = HidSelectDatabase.Text;
            var selectConnection = SelectedConnection;
            Task.Run(() =>
            {
                var sqLiteHelper = new SQLiteHelper();
                var leftMenuType = sqLiteHelper.GetSysInt(SysConst.Sys_LeftMenuType);
                var curObjects = new List<SObjectDTO>();
                var curGroups = new List<ObjectGroup>();
                var itemParentList = new List<PropertyNodeItem>();
                var itemList = new List<PropertyNodeItem>();
                var nodeTable = new PropertyNodeItem
                {
                    ObejcetId = "0",
                    DisplayName = "数据表",
                    Name = "treeTable",
                    Icon = TABLEICON,
                    Type = ObjType.Type
                };
                itemList.Add(nodeTable);
                var nodeView = new PropertyNodeItem
                {
                    ObejcetId = "0",
                    DisplayName = "视图",
                    Name = "treeView",
                    Icon = VIEWICON,
                    Type = ObjType.Type
                };
                itemList.Add(nodeView);
                var nodeProc = new PropertyNodeItem
                {
                    ObejcetId = "0",
                    DisplayName = "存储过程",
                    Name = "treeProc",
                    Icon = PROCICON,
                    Type = ObjType.Type
                };
                itemList.Add(nodeProc);

                #region 分组业务处理
                //是否业务分组
                if (leftMenuType == LeftMenuType.Group.GetHashCode())
                {
                    curGroups = sqLiteHelper.db.Table<ObjectGroup>().Where(a =>
                        a.ConnectId == selectConnection.ID &&
                        a.DataBaseName == selectDataBase).OrderBy(x => x.OrderFlag).ToList();
                    if (curGroups.Any())
                    {
                        foreach (var group in curGroups)
                        {
                            var itemChildList = new List<PropertyNodeItem>();
                            var nodeGroup = new PropertyNodeItem
                            {
                                ObejcetId = "0",
                                DisplayName = group.GroupName,
                                Name = "treeGroup",
                                Icon = GROUPICON,
                                FontWeight = "Bold",
                                Type = ObjType.Group,
                                IsExpanded = !(!group.OpenLevel.HasValue || group.OpenLevel == 0)
                            };
                            var nodeTable1 = new PropertyNodeItem
                            {
                                ObejcetId = "0",
                                DisplayName = "数据表",
                                Name = "treeTable",
                                Icon = TABLEICON,
                                Parent = nodeGroup,
                                Type = ObjType.Type,
                                IsExpanded = group.OpenLevel == 2
                            };
                            itemChildList.Add(nodeTable1);
                            var nodeView1 = new PropertyNodeItem
                            {
                                ObejcetId = "0",
                                DisplayName = "视图",
                                Name = "treeView",
                                Icon = VIEWICON,
                                Parent = nodeGroup,
                                Type = ObjType.Type,
                                IsExpanded = group.OpenLevel == 2
                            };
                            itemChildList.Add(nodeView1);
                            var nodeProc1 = new PropertyNodeItem
                            {
                                ObejcetId = "0",
                                DisplayName = "存储过程",
                                Name = "treeProc",
                                Icon = PROCICON,
                                Parent = nodeGroup,
                                Type = ObjType.Type,
                                IsExpanded = group.OpenLevel == 2
                            };
                            itemChildList.Add(nodeProc1);
                            nodeGroup.Children = itemChildList;
                            itemParentList.Add(nodeGroup);
                        }
                    }
                    curObjects = (from a in sqLiteHelper.db.Table<ObjectGroup>()
                                  join b in sqLiteHelper.db.Table<SObjects>() on a.Id equals b.GroupId
                                  where a.ConnectId == selectConnection.ID &&
                                        a.DataBaseName == selectDataBase
                                  select new SObjectDTO
                                  {
                                      GroupName = a.GroupName,
                                      ObjectName = b.ObjectName
                                  }).ToList();
                }
                #endregion
                var model = new Model();
                try
                {
                    var dbInstance = ExporterFactory.CreateInstance(selectConnection.DbType, selectConnection.SelectedDbConnectString(selectDataBase));
                    model = dbInstance.Init();
                    dataSource = model;
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Growl.Warning(new GrowlInfo
                        {
                            Message = $"连接失败 {selectConnection.ConnectName}，原因：" + ex.Message,
                            ShowDateTime = false,
                            Type = InfoType.Error
                        });
                    }));
                }
                var textColor = "#333444";
                #region 数据表
                foreach (var table in model.Tables)
                {
                    //是否业务分组
                    if (leftMenuType == LeftMenuType.Group.GetHashCode())
                    {
                        var hasGroup = curObjects.Where(x => x.ObjectName == table.Key).
                            GroupBy(x => x.GroupName).Select(x => x.Key)
                            .ToList();
                        foreach (var group in hasGroup)
                        {
                            var pGroup = itemParentList.FirstOrDefault(x => x.DisplayName == group);
                            if (pGroup != null)
                            {
                                var ppGroup = pGroup.Children.FirstOrDefault(x => x.DisplayName == "数据表");
                                if (ppGroup != null)
                                {
                                    ppGroup.Children.Add(new PropertyNodeItem()
                                    {
                                        ObejcetId = table.Value.Id,
                                        DisplayName = table.Value.DisplayName,
                                        Name = table.Value.Name,
                                        Schema = table.Value.SchemaName,
                                        Comment = table.Value.Comment,
                                        CreateDate = table.Value.CreateDate,
                                        ModifyDate = table.Value.ModifyDate,
                                        TextColor = textColor,
                                        Icon = TABLEICON,
                                        Type = ObjType.Table
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        nodeTable.Children.Add(new PropertyNodeItem()
                        {
                            ObejcetId = table.Value.Id,
                            Parent = nodeTable,
                            DisplayName = table.Value.DisplayName,
                            Name = table.Value.Name,
                            Schema = table.Value.SchemaName,
                            Comment = table.Value.Comment,
                            CreateDate = table.Value.CreateDate,
                            ModifyDate = table.Value.ModifyDate,
                            TextColor = textColor,
                            Icon = TABLEICON,
                            Type = ObjType.Table
                        });
                    }

                }
                #endregion

                #region 视图
                foreach (var view in model.Views)
                {
                    //是否业务分组
                    if (leftMenuType == LeftMenuType.Group.GetHashCode())
                    {
                        var hasGroup = curObjects.Where(x => x.ObjectName == view.Key).
                            GroupBy(x => x.GroupName).Select(x => x.Key)
                            .ToList();
                        foreach (var group in hasGroup)
                        {
                            var pGroup = itemParentList.FirstOrDefault(x => x.DisplayName == group);
                            if (pGroup != null)
                            {
                                var ppGroup = pGroup.Children.FirstOrDefault(x => x.DisplayName == "视图");
                                if (ppGroup != null)
                                {
                                    ppGroup.Children.Add(new PropertyNodeItem()
                                    {
                                        ObejcetId = view.Value.Id,
                                        DisplayName = view.Value.DisplayName,
                                        Name = view.Value.Name,
                                        Schema = view.Value.SchemaName,
                                        Comment = view.Value.Comment,
                                        CreateDate = view.Value.CreateDate,
                                        ModifyDate = view.Value.ModifyDate,
                                        TextColor = textColor,
                                        Icon = VIEWICON,
                                        Type = ObjType.View
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        nodeView.Children.Add(new PropertyNodeItem()
                        {
                            ObejcetId = view.Value.Id,
                            Parent = nodeView,
                            DisplayName = view.Value.DisplayName,
                            Name = view.Value.Name,
                            Schema = view.Value.SchemaName,
                            Comment = view.Value.Comment,
                            CreateDate = view.Value.CreateDate,
                            ModifyDate = view.Value.ModifyDate,
                            TextColor = textColor,
                            Icon = VIEWICON,
                            Type = ObjType.View
                        });
                    }
                }
                #endregion

                #region 存储过程
                foreach (var proc in model.Procedures)
                {
                    //是否业务分组
                    if (leftMenuType == LeftMenuType.Group.GetHashCode())
                    {
                        var hasGroup = curObjects.Where(x => x.ObjectName == proc.Key).GroupBy(x => x.GroupName)
                            .Select(x => x.Key)
                            .ToList();
                        foreach (var group in hasGroup)
                        {
                            var pGroup = itemParentList.FirstOrDefault(x => x.DisplayName == group);
                            if (pGroup != null)
                            {
                                var ppGroup = pGroup.Children.FirstOrDefault(x => x.DisplayName == "存储过程");
                                if (ppGroup != null)
                                {
                                    ppGroup.Children.Add(new PropertyNodeItem()
                                    {
                                        ObejcetId = proc.Value.Id,
                                        DisplayName = proc.Value.DisplayName,
                                        Name = proc.Value.Name,
                                        Schema = proc.Value.SchemaName,
                                        Comment = proc.Value.Comment,
                                        CreateDate = proc.Value.CreateDate,
                                        ModifyDate = proc.Value.ModifyDate,
                                        TextColor = textColor,
                                        Icon = PROCICON,
                                        Type = ObjType.Proc
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        nodeProc.Children.Add(new PropertyNodeItem()
                        {
                            ObejcetId = proc.Value.Id,
                            Parent = nodeProc,
                            DisplayName = proc.Value.DisplayName,
                            Name = proc.Value.Name,
                            Schema = proc.Value.SchemaName,
                            Comment = proc.Value.Comment,
                            CreateDate = proc.Value.CreateDate,
                            ModifyDate = proc.Value.ModifyDate,
                            TextColor = textColor,
                            Icon = PROCICON,
                            Type = ObjType.Proc
                        });
                    }
                }
                #endregion

                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    LoadingLine.Visibility = Visibility.Hidden;
                    //编写获取数据并显示在界面的代码
                    //是否业务分组
                    if (leftMenuType == LeftMenuType.Group.GetHashCode() && !isCompare)
                    {
                        if (!itemParentList.Any())
                        {
                            NoDataAreaText.TipText = "暂无分组，请先建分组";
                            NoDataText.Visibility = Visibility.Visible;
                        }
                        itemParentList.ForEach(group =>
                        {
                            group.Children.ForEach(obj =>
                            {
                                if (!obj.Children.Any())
                                {
                                    obj.Visibility = nameof(Visibility.Collapsed);
                                }
                                obj.DisplayName += $"（{obj.Children.Count}）";
                            });
                        });
                        ExportData = itemParentList;
                        SearchMenu.Text = string.Empty;
                    }
                    else
                    {
                        if (!itemList.Any(x => x.Children.Count > 0))
                        {
                            NoDataAreaText.TipText = "暂无数据";
                            NoDataText.Visibility = Visibility.Visible;
                        }
                        itemList.ForEach(obj =>
                        {
                            if (!obj.Children.Any())
                            {
                                obj.Visibility = nameof(Visibility.Collapsed);
                            }
                            obj.DisplayName += $"（{obj.Children.Count}）";
                        });
                        ExportData = itemList;
                        SearchMenu.Text = string.Empty;
                    }
                }));
            });
            #endregion
        }

        /// <summary>
        /// 导出数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnExport_OnClick(object sender, RoutedEventArgs e)
        {
            var selectedConnection = SelectedConnection;
            var selectedDatabase = SelectedDataBase;
            var exportData = ExportData;
            var floderPath = TxtPath.Text;
            var doctype = DocumentType();
            if (string.IsNullOrEmpty(doctype))
            {
                Growl.WarningGlobal(new GrowlInfo { Message = $"请选择输出文档类型", WaitTime = 1, ShowDateTime = false });
                return;
            }
            if (string.IsNullOrEmpty(TxtFileName.Text))
            {
                TxtFileName.Text = $"{SelectedDataBase.DbName}数据库设计文档";
            }
            //文件扩展名
            var fileNameE = LblFileExtend.Content;
            //文件名
            var fileName = TxtFileName.Text.Trim() + fileNameE;
            LoadingG.Visibility = Visibility.Visible;
            var dbDto = new DBDto(selectedDatabase.DbName);
            Task.Run(() =>
            {
                dbDto.DBType = selectedConnection.DbType.ToString();

                dbDto.Tables = Trans2Table(exportData, selectedConnection, selectedDatabase);
                dbDto.Procs = Trans2Dictionary(exportData, selectedConnection, selectedDatabase, "Proc");
                dbDto.Views = Trans2Dictionary(exportData, selectedConnection, selectedDatabase, "View");

                //判断文档路径是否存在
                if (!Directory.Exists(floderPath))
                {
                    Directory.CreateDirectory(floderPath);
                }
                var filePath = Path.Combine(floderPath, fileName);
                var doc = DocFactory.CreateInstance((DocType)(Enum.Parse(typeof(DocType), doctype)), dbDto);
                var bulResult = doc.Build(filePath);
                Dispatcher.Invoke(() =>
                {
                    LoadingG.Visibility = Visibility.Collapsed;
                    if (bulResult)
                    {
                        Growl.SuccessGlobal("导出成功.");
                    }
                });
            });
        }

        private List<ViewProDto> Trans2Dictionary(List<PropertyNodeItem> treeViewData, ConnectConfigs selectedConnection, DataBase selectedDatabase, string type)
        {
            var selectedConnectionString = selectedConnection.SelectedDbConnectString(selectedDatabase.DbName);
            var exporter = ExporterFactory.CreateInstance(selectedConnection.DbType, selectedConnectionString);
            var objectType = type == "View" ? DbObjectType.View : DbObjectType.Proc;
            var viewPro = new List<ViewProDto>();
            foreach (var group in treeViewData)
            {
                if (group.Name.Equals("treeTable"))
                {
                    continue;
                }
                if (group.Type == "Type")
                {
                    foreach (var item in group.Children)
                    {
                        if (item.Type == type)
                        {
                            var script = exporter.GetScriptInfoById(item.ObejcetId, objectType);
                            viewPro.Add(new ViewProDto
                            {
                                ObjectName = item.DisplayName,
                                Comment = item.Comment,
                                Script = script
                            });
                        }
                    }
                }
                else
                {
                    if (group.Type == type)
                    {
                        var script = exporter.GetScriptInfoById(group.ObejcetId, objectType);
                        viewPro.Add(new ViewProDto
                        {
                            ObjectName = group.DisplayName,
                            Comment = group.Comment,
                            Script = script
                        });
                    }
                }
            }
            return viewPro;
        }

        private List<TableDto> Trans2Table(List<PropertyNodeItem> treeViewData, ConnectConfigs selectedConnection, DataBase selectedDatabase)
        {
            var selectedConnectionString = selectedConnection.SelectedDbConnectString(selectedDatabase.DbName);
            var tables = new List<TableDto>();
            var groupNo = 1;
            foreach (var group in treeViewData)
            {
                if (group.Type == "Type" && group.Name.Equals("treeTable"))
                {
                    int orderNo = 1;
                    foreach (var node in group.Children)
                    {
                        TableDto tbDto = new TableDto();
                        tbDto.TableOrder = orderNo.ToString();
                        tbDto.TableName = node.Name;
                        tbDto.Comment = node.Comment.FilterIllegalDir();
                        tbDto.DBType = nameof(DbType.SqlServer);

                        var lst_col_dto = new List<ColumnDto>();
                        var dbInstance = ExporterFactory.CreateInstance(selectedConnection.DbType,
                            selectedConnectionString);
                        var columns = dbInstance.GetColumnInfoById(node.ObejcetId);
                        var columnIndex = 1;
                        foreach (var col in columns)
                        {
                            var colDto = new ColumnDto();
                            colDto.ColumnOrder = columnIndex.ToString();
                            colDto.ColumnName = col.Value.Name;
                            // 数据类型
                            colDto.ColumnTypeName = col.Value.DataType;
                            // 长度
                            colDto.Length = col.Value.Length;
                            // 小数位
                            //colDto.Scale = "";//(col.Scale.HasValue ? col.Scale.Value.ToString() : "");
                            // 主键
                            colDto.IsPK = (col.Value.IsPrimaryKey ? "√" : "");
                            // 自增
                            colDto.IsIdentity = (col.Value.IsIdentity ? "√" : "");
                            // 允许空
                            colDto.CanNull = (col.Value.IsNullable ? "√" : "");
                            // 默认值
                            colDto.DefaultVal = (!string.IsNullOrWhiteSpace(col.Value.DefaultValue) ? col.Value.DefaultValue : "");
                            // 列注释（说明）
                            colDto.Comment = col.Value.Comment.FilterIllegalDir();

                            lst_col_dto.Add(colDto);
                            columnIndex++;
                        }
                        tbDto.Columns = lst_col_dto;
                        tables.Add(tbDto);
                        orderNo++;
                    }
                }
                if (group.Type == "Table")
                {
                    TableDto tbDto = new TableDto();
                    tbDto.TableOrder = groupNo.ToString();
                    tbDto.TableName = group.Name;
                    tbDto.Comment = group.Comment.FilterIllegalDir();
                    tbDto.DBType = "SqlServer";

                    var lst_col_dto = new List<ColumnDto>();
                    var dbInstance = ExporterFactory.CreateInstance(selectedConnection.DbType,
                        selectedConnectionString);
                    var columns = dbInstance.GetColumnInfoById(group.ObejcetId);
                    var columnIndex = 1;
                    foreach (var col in columns)
                    {
                        ColumnDto colDto = new ColumnDto();
                        colDto.ColumnOrder = columnIndex.ToString();
                        colDto.ColumnName = col.Value.Name;
                        // 数据类型
                        colDto.ColumnTypeName = col.Value.DataType;
                        // 长度
                        colDto.Length = col.Value.Length;
                        // 小数位
                        //colDto.Scale = "";//(col.Scale.HasValue ? col.Scale.Value.ToString() : "");
                        // 主键
                        colDto.IsPK = (col.Value.IsPrimaryKey ? "√" : "");
                        // 自增
                        colDto.IsIdentity = (col.Value.IsIdentity ? "√" : "");
                        // 允许空
                        colDto.CanNull = (col.Value.IsNullable ? "√" : "");
                        // 默认值
                        colDto.DefaultVal = (!string.IsNullOrWhiteSpace(col.Value.DefaultValue) ? col.Value.DefaultValue : "");
                        // 列注释（说明）
                        colDto.Comment = col.Value.Comment.FilterIllegalDir();
                        lst_col_dto.Add(colDto);
                        columnIndex++;
                    }
                    tbDto.Columns = lst_col_dto;
                    tables.Add(tbDto);
                    groupNo++;
                }
            }
            return tables;
        }

        /// <summary>
        /// 取消
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// 导出文档类型单选
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Toggle_OnChecked(object sender, RoutedEventArgs e)
        {
            var button = (ToggleButton)sender;
            foreach (ToggleButton toggle in ToggleWarpPanel.Children)
            {
                if (toggle.Name != button.Name)
                {
                    toggle.IsChecked = false;
                }
            }
            if (IsLoaded)
            {
                var docType = (DocType)(Enum.Parse(typeof(DocType), button.Content.ToString().ToLower()));
                var fileExtend = FileExtend(docType);
                LblFileExtend.Content = "." + fileExtend;
            }
        }

        private string DocumentType()
        {
            var type = string.Empty;
            foreach (ToggleButton button in ToggleWarpPanel.Children)
            {
                if (button.IsChecked == true)
                {
                    type = button.Content.ToString().ToLower();
                }
            }
            return type;
        }

        private string FileExtend(DocType docType)
        {
            switch (docType)
            {
                case DocType.word: return "docx";
                case DocType.chm: return "chm";
                case DocType.excel: return "xlsx";
                case DocType.html: return "html";
                case DocType.markdown: return "md";
                case DocType.pdf: return "pdf";
                case DocType.xml: return "xml";
                default: return "chm";
            }
        }

        private void EventSetter_OnHandler(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }
    }
}
