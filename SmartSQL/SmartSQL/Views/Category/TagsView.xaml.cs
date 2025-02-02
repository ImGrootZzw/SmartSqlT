﻿using HandyControl.Controls;
using HandyControl.Data;
using SmartSQL.Annotations;
using SmartSQL.Framework;
using SmartSQL.Framework.PhysicalDataModel;
using SmartSQL.Framework.SqliteModel;
using SmartSQL.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SmartSQL.Views.Category
{
    /// <summary>
    /// UserControl1.xaml 的交互逻辑
    /// </summary>
    public partial class TagsView : INotifyPropertyChanged
    {
        public event ChangeRefreshHandler ChangeRefreshEvent;

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #region DependencyProperty
        public static readonly DependencyProperty ConnectionProperty = DependencyProperty.Register(
            "Connection", typeof(ConnectConfigs), typeof(TagsView), new PropertyMetadata(default(ConnectConfigs)));
        public ConnectConfigs Connection
        {
            get => (ConnectConfigs)GetValue(ConnectionProperty);
            set => SetValue(ConnectionProperty, value);
        }

        public static readonly DependencyProperty SelectedDataBaseProperty = DependencyProperty.Register(
            "SelectedDataBase", typeof(string), typeof(TagsView), new PropertyMetadata(default(string)));
        public string SelectedDataBase
        {
            get => (string)GetValue(SelectedDataBaseProperty);
            set => SetValue(SelectedDataBaseProperty, value);
        }

        public new static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            "Title", typeof(string), typeof(TagsView), new PropertyMetadata(default(string)));
        public new string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty DataListProperty = DependencyProperty.Register(
            "DataList", typeof(List<ObjectGroup>), typeof(TagsView), new PropertyMetadata(default(List<ObjectGroup>)));
        public List<ObjectGroup> DataList
        {
            get => (List<ObjectGroup>)GetValue(DataListProperty);
            set
            {
                SetValue(DataListProperty, value);
                OnPropertyChanged(nameof(DataList));
            }
        }
        #endregion

        public TagsView()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void TagsView_Loaded(object sender, RoutedEventArgs e)
        {
            Title = $"{Connection.ConnectName} - 分组管理";
            var conn = Connection;
            var selectDataBase = SelectedDataBase;
            var dbConnectionString = conn.DbMasterConnectString;
            Task.Run(() =>
            {
                var sqLiteHelper = new SQLiteHelper();
                var datalist = sqLiteHelper.db.Table<ObjectGroup>().
                    Where(x => x.ConnectId == conn.ID && x.DataBaseName == selectDataBase).OrderBy(x => x.OrderFlag).ToList();
                Dispatcher.Invoke(() =>
                {
                    DataList = datalist;
                    if (!datalist.Any())
                    {
                        NoDataText.Visibility = Visibility.Visible;
                    }
                });
                var exporter = ExporterFactory.CreateInstance(conn.DbType, dbConnectionString);
                var list = exporter.GetDatabases();
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var DBase = list;
                    SelectDatabase.ItemsSource = DBase;
                    SelectDatabase.SelectedItem = list.FirstOrDefault(x => x.DbName == SelectedDataBase);

                }));
            });
        }

        private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = (ListBox)sender;
            if (listBox.SelectedItems.Count > 0)
            {
                var group = (ObjectGroup)listBox.SelectedItems[0];

                HidId.Text = group.Id.ToString();
                TextGourpName.Text = group.GroupName;
                CheckCurrent.IsChecked = group.OpenLevel == 1;
                CheckChild.IsChecked = group.OpenLevel == 2;
                CheckNone.IsChecked = group.OpenLevel == null || group.OpenLevel == 0;
            }
        }

        /// <summary>
        /// 保存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnSave_OnClick(object sender, RoutedEventArgs e)
        {
            var groupName = TextGourpName.Text.Trim();
            if (string.IsNullOrEmpty(groupName))
            {
                Oops.Oh("请填写分组名.");
                return;
            }
            var selectedDatabase = (DataBase)SelectDatabase.SelectedItem;
            var sqLiteHelper = new SQLiteHelper();
            var groupId = Convert.ToInt32(HidId.Text);
            if (groupId > 0)
            {
                var groupO = sqLiteHelper.db.Table<ObjectGroup>().FirstOrDefault(x => x.ConnectId == Connection.ID && x.DataBaseName == selectedDatabase.DbName && x.Id != groupId && x.GroupName == groupName);
                if (groupO != null)
                {
                    Oops.Oh("已存在相同名称的分组名.");
                    return;
                }
                //分组菜单左侧默认展开层级
                var openLevel = CheckCurrent.IsChecked == true ? 1 : (CheckChild.IsChecked == true ? 2 : 0);
                var selectedGroup = (ObjectGroup)ListGroup.SelectedItems[0];
                selectedGroup.GroupName = groupName;
                selectedGroup.OpenLevel = openLevel;
                sqLiteHelper.db.Update(selectedGroup);
            }
            else
            {
                var groupO = sqLiteHelper.db.Table<ObjectGroup>().FirstOrDefault(x => x.ConnectId == Connection.ID && x.DataBaseName == selectedDatabase.DbName && x.GroupName == groupName);
                if (groupO != null)
                {
                    Oops.Oh("已存在相同名称的分组名.");
                    return;
                }
                //分组菜单左侧默认展开层级
                var openLevel = CheckCurrent.IsChecked == true ? 1 : (CheckChild.IsChecked == true ? 2 : 0);
                sqLiteHelper.db.Insert(new ObjectGroup()
                {
                    ConnectId = Connection.ID,
                    DataBaseName = selectedDatabase.DbName,
                    GroupName = groupName,
                    OpenLevel = openLevel,
                    OrderFlag = DateTime.Now
                });
            }
            HidId.Text = "0";
            TextGourpName.Text = "";
            CheckNone.IsChecked = true;
            BtnSave.IsEnabled = false;
            var connKey = Connection.ID;
            Task.Run(() =>
            {
                var datalist = sqLiteHelper.db.Table<ObjectGroup>().
                    Where(x => x.ConnectId == connKey && x.DataBaseName == selectedDatabase.DbName).OrderBy(x => x.OrderFlag).ToList();
                Dispatcher.Invoke(() =>
                {
                    DataList = datalist;
                    if (ChangeRefreshEvent != null)
                    {
                        ChangeRefreshEvent();
                    }
                });
            });
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
        /// 删除
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnDelete_OnClick(object sender, RoutedEventArgs e)
        {
            var sqLiteHelper = new SQLiteHelper();
            var groupId = Convert.ToInt32(HidId.Text);
            if (groupId < 1)
            {
                Oops.Oh("请选择需要删除的分组.");
                return;
            }
            var selectedDatabase = (DataBase)SelectDatabase.SelectedItem;
            var connKey = Connection.ID;
            Task.Run(() =>
            {
                sqLiteHelper.db.Delete<ObjectGroup>(groupId);

                var list = sqLiteHelper.db.Table<SObjects>().Where(x =>
                    x.ConnectId == connKey &&
                    x.DatabaseName == selectedDatabase.DbName &&
                    x.GroupId == groupId).ToList();
                if (list.Any())
                {
                    foreach (var sobj in list)
                    {
                        sqLiteHelper.db.Delete<SObjects>(sobj.Id);
                    }
                }
                var datalist = sqLiteHelper.db.Table<ObjectGroup>().
                    Where(x => x.ConnectId == connKey && x.DataBaseName == selectedDatabase.DbName).ToList();
                Dispatcher.Invoke(() =>
                {
                    HidId.Text = "0";
                    TextGourpName.Text = "";
                    CheckNone.IsChecked = true;
                    BtnSave.IsEnabled = false;
                    DataList = datalist;
                    if (ChangeRefreshEvent != null)
                    {
                        ChangeRefreshEvent();
                    }
                });
            });
        }

        private void BtnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            HidId.Text = "0";
            TextGourpName.Text = "";
            CheckNone.IsChecked = true;
            BtnSave.IsEnabled = false;
        }

        private void SelectDatabase_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedDatabase = (DataBase)SelectDatabase.SelectedItem;
            var conn = Connection;
            Task.Run(() =>
            {
                var sqLiteHelper = new SQLiteHelper();
                var datalist = sqLiteHelper.db.Table<ObjectGroup>().
                    Where(x => x.ConnectId == conn.ID && x.DataBaseName == selectedDatabase.DbName).OrderBy(x => x.OrderFlag).ToList();
                Dispatcher.Invoke(() =>
                {
                    DataList = datalist;
                    NoDataText.Visibility = datalist.Any() ? Visibility.Collapsed : Visibility.Visible;
                });
            });
        }

        private void TextGourpName_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TextGourpName.Text))
            {
                BtnSave.IsEnabled = true;
            }
        }

        private void TextGourpName_OnPreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnSave_OnClick(sender, e);
            }
        }
    }
}
