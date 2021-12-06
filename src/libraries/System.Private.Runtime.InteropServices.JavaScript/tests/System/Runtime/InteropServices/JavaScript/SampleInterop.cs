using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Runtime.InteropServices.JavaScript
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ImportFromJSAttribute : Attribute
    {
        public string Location { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ExportToJSAttribute : Attribute
    { }
}

namespace System.Runtime.InteropServices.JavaScript.Tests
{
    //public partial class StaticInterop
    //{
    //    public static DateTime DateTime { get; set; }

    //    // C# -> JS
    //    [ImportFromJS(Location = "globalThis.setDateTime")]
    //    public static partial void ExportDateTime(DateTime dt);

    //    // JS -> C#
    //    [ExportToJS]
    //    public static void ImportDateTime(DateTime dt)
    //    {
    //        DateTime = dt;
    //    }
    //}

    public partial class InstanceInterop
    {
        public static DateTime DateTime { get; set; }

        // C# -> JS
        [ImportFromJS(Location = "this.setDateTime")]
        public partial void ExportDateTime(DateTime dt);

        // JS -> C#
        [ExportToJS]
        public void ImportDateTime(DateTime dt)
        {
            DateTime = dt;
        }
    }
}
