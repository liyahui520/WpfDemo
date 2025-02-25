using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Tools.Extend
{
    public class BindingHelper
    {
        public static readonly DependencyProperty DynamicParamProperty =
            DependencyProperty.RegisterAttached(
                "DynamicParam",
                typeof(object),
                typeof(BindingHelper),
                new FrameworkPropertyMetadata(null)
            );

        public static object GetDynamicParam(DependencyObject obj) =>
            obj.GetValue(DynamicParamProperty);

        public static void SetDynamicParam(DependencyObject obj, object value) =>
            obj.SetValue(DynamicParamProperty, value);
    }
}
