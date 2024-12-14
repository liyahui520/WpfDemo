using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace WpfMain.Module.PetModule
{
    /// <summary>
    /// FrmPet.xaml 的交互逻辑
    /// </summary>
    public partial class FrmPetImage : UserControl
    {
        public List<object> ResolutionDataList = new List<object>();

        public static readonly DependencyProperty DemoModel1Property = DependencyProperty.Register(
            nameof(DemoModel1), typeof(PropertyGridDemoModel), typeof(FrmPet), new PropertyMetadata(default(PropertyGridDemoModel)));

        public PropertyGridDemoModel DemoModel1
        {
            get => (PropertyGridDemoModel)GetValue(DemoModel1Property);
            set => SetValue(DemoModel1Property, value);
        }
        public FrmPetImage()
        {
            InitializeComponent();
            DemoModel1 = new PropertyGridDemoModel
            {
                String = "TestString",
                Enum = Gender.Female,
                Boolean = true,
                Integer = 98,
                VerticalAlignment = VerticalAlignment.Stretch
            };
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            
        }
    }
}

