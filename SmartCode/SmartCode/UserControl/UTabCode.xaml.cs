﻿using System;
using System.Collections.Generic;
using System.Linq;
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
using HandyControl.Data;
using SmartCode.Framework;
using SmartCode.Framework.Exporter;
using SmartCode.Framework.PhysicalDataModel;
using SmartCode.Framework.Util;
using SmartCode.Helper;
using SmartCode.Models;
using SqlSugar;
using UserControlE = System.Windows.Controls.UserControl;

namespace SmartCode.UserControl
{
    /// <summary>
    /// UTabCode.xaml 的交互逻辑
    /// </summary>
    public partial class UTabCode : BaseUserControl
    {
        public static readonly DependencyProperty SelectedObjectProperty = DependencyProperty.Register(
            "SelectedObject", typeof(PropertyNodeItem), typeof(UTabCode), new PropertyMetadata(default(PropertyNodeItem)));

        public static readonly DependencyProperty ConnStringProperty = DependencyProperty.Register(
            "ConnString", typeof(string), typeof(UTabCode), new PropertyMetadata(default(string)));

        public static readonly DependencyProperty SelectedDataBaseProperty = DependencyProperty.Register(
            "SelectedDataBase", typeof(string), typeof(UTabCode), new PropertyMetadata(default(string)));

        public static readonly DependencyProperty SelectedTableColunmsProperty = DependencyProperty.Register(
            "SelectedTableColunms", typeof(List<Column>), typeof(UTabCode), new PropertyMetadata(default(List<Column>)));

        public PropertyNodeItem SelectedObject
        {
            get => (PropertyNodeItem)GetValue(SelectedObjectProperty);
            set => SetValue(SelectedObjectProperty, value);
        }
        public string ConnString
        {
            get => (string)GetValue(ConnStringProperty);
            set => SetValue(ConnStringProperty, value);
        }
        public string SelectedDataBase
        {
            get => (string)GetValue(SelectedDataBaseProperty);
            set => SetValue(SelectedDataBaseProperty, value);
        }
        /// <summary>
        /// 当前选中表列数据
        /// </summary>
        public List<Column> SelectedTableColunms
        {
            get => (List<Column>)GetValue(SelectedTableColunmsProperty);
            set
            {
                SetValue(SelectedTableColunmsProperty, value);
            }
        }
        public UTabCode()
        {
            InitializeComponent();
            DataContext = this;
            HighlightingProvider.Register(SkinType.Dark, new HighlightingProviderDark());
            TextTableEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "SQL");
            TextColumnsEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "SQL");
            TextCsharpEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "C#");
            TextSelectEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "SQL");
            TextInsertEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "SQL");
            TextUpdateEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "SQL");
            TextDeleteEditor.SyntaxHighlighting = HighlightingProvider.GetDefinition(SkinType.Dark, "SQL");
        }

        private void TabCode_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (SelectedObject == null)
            {
                return;
            }
            TabParentSql.IsSelected = true;
            TabTableSql.IsSelected = true;
            var objE = SelectedObject;

            var list = SelectedTableColunms;
            var sb = new StringBuilder();

            #region 1、生成新建表脚本

            var instance = ExporterFactory.CreateInstance(DbType.SqlServer);
            var createTableSql = instance.CreateTableSql(objE.DisplayName, list);
            TextTableEditor.Text = createTableSql;
            #endregion

            sb.Clear();
            #region 2、生成新建字段脚本
            foreach (var column in list)
            {
                sb.Append($"ALTER TABLE dbo.{column.ObjectName} ADD {column.DisplayName} {column.DataType.ToLower()} ");
                if (SqlServerDbTypeMapHelper.IsMulObj(column.DataType))
                {
                    sb.Append($"{column.Length} ");
                }
                var isNull = column.IsNullable ? "NULL " : "NOT NULL ";
                sb.Append(isNull);
                sb.Append(Environment.NewLine);
                sb.Append("GO");
                sb.Append(Environment.NewLine);
                #region 字段注释
                if (!string.IsNullOrEmpty(column.Comment))
                {
                    sb.Append($@"EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'{column.Comment}',");
                    sb.Append("@level0type=N'SCHEMA',@level0name=N'dbo',");
                    sb.Append("@level1type=N'TABLE',@level1name=N'{column.ObjectName}',");
                    sb.Append("@level2type=N'COLUMN',@level2name=N'{column.DisplayName}'");
                    sb.Append(Environment.NewLine);
                    sb.Append("GO");
                    sb.Append(Environment.NewLine);
                }
                #endregion
            }
            TextColumnsEditor.Text = sb.ToString();
            #endregion

            sb.Clear();
            //查询sql
            var selSql = instance.SelectSql(objE.Name, list);
            TextSelectEditor.Text = selSql.SqlFormat();
            //插入sql
            var insSql = instance.InsertSql(objE.Name, list);
            TextInsertEditor.Text = insSql.SqlFormat();
            //更新sql
            var updSql = instance.UpdateSql(objE.Name, list);
            TextUpdateEditor.Text = updSql.SqlFormat();
            //删除sql
            var delSql = instance.DeleteSql(objE.Name, list);
            TextDeleteEditor.Text = delSql.SqlFormat();
            var langInstance = LangFactory.CreateInstance(LangType.Csharp);
            var csharpEntityCode = langInstance.BuildEntity(objE.Name, list);
            TextCsharpEditor.Text = csharpEntityCode;
        }
    }
}
