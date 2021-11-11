// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Runtime.InteropServices.JavaScript.MarshalerGenerator
{
    internal static class Names
    {
        internal const string Attribute = "MarshalerAttribute";
        internal const string AttributeWithoutSuffix = "Marshaler";
        internal const string AttributeFull = "System.Runtime.InteropServices.JavaScript.MarshalerAttribute";
        internal const string AttributeFullWithoutSuffix = "System.Runtime.InteropServices.JavaScript.Marshaler";

        internal const string Interop = "Interop";
        internal const string InteropRuntime = "Runtime";

        internal const string InteropRuntimeInvokeJS = "InvokeJS";
    }
}
