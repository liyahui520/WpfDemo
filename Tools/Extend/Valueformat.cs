using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tools.Extend
{
    public class Valueformat
    {
        private System.Reflection.PropertyInfo _property;
        private StringAction _action;
        public Valueformat(System.Reflection.PropertyInfo property, StringAction action)
        {
            _property = property;
            _action = action;
        }

        public override string ToString()
        {
            if (_property != null)
                return _property.DeclaringType.Name + "." + _property.Name;
            return null;
        }

        public string GetValue(object obj)
        {
            if (_property != null && obj != null)
            {
                object value = _property.GetValue(obj, null);
                if (_action != null)
                    return _action(value == null ? null : value.ToString());
                return value == null ? null : value.ToString();
            }
            return null;
        }


        public delegate string StringAction(string o);
    }
}
