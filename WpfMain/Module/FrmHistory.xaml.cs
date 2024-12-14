using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Policy;
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
using WpfMain.Module.PetModule;

namespace WpfMain.Module
{
    /// <summary>
    /// FrmHistory.xaml 的交互逻辑
    /// </summary>
    public partial class FrmHistory : UserControl
    { 

        public static readonly DependencyProperty DataListProperty = DependencyProperty.Register(
            nameof(DataList), typeof(List<PropertyGridDataList>), typeof(FrmHistory), new PropertyMetadata(default(List<PropertyGridDataList>)));

        public List<PropertyGridDataList> DataList
        {
            get => (List<PropertyGridDataList>)GetValue(DataListProperty);
            set => SetValue(DataListProperty, value);
        }

        public FrmHistory()
        {
            InitializeComponent();
            DataList = new List<PropertyGridDataList>();
            DataList.Add(new PropertyGridDataList { Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark = "检查结果可以帮助医生对有症状患者明确诊断（诊断试验），我的检查组或发现无生对有症状患者症状患者的隐匿检查组或发现无生对有症状患者症状患者的性疾病（筛查）。 如果根据...." });
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark ="备注:无"});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark ="细胞大小：100PM，单面积内无发现"});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark = "细胞大小：100PM，单面积内无发现" });
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark = "国家卫生健康委、国家发展改革委、财政部等7部门11月27日公布《关于进一步推进医疗机构检查检验结果互认的指导意见》： 到2025年底，各紧密型医联体（含城市医疗集团和县域医共 …" });
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList{ Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
            DataList.Add(new PropertyGridDataList { Index=1, Name ="张三", Phone="13100000001", Sex="男", DeviceName ="设备1", Remark =""});
        }
    }

    public class PropertyGridDataList
    {
        public int Index { get; set; }
        public string  Name { get; set; }
        public string Phone { get; set; }
        public string Sex { get; set; }
        public string DeviceName { get; set; }
        public string Remark { get; set; }
    }
}
