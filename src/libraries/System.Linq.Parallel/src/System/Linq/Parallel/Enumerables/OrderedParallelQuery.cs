// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// OrderedParallelQuery.cs
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq.Parallel;
using System.Diagnostics;

namespace System.Linq
{
    /// <summary>
    /// Represents a sorted, parallel sequence.
    /// </summary>
    public class OrderedParallelQuery<TSource> : ParallelQuery<TSource>
    {
        private readonly QueryOperator<TSource> _sortOp;
        private readonly IOrderedEnumerable<TSource> _browser;

        [System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
        internal OrderedParallelQuery(QueryOperator<TSource> sortOp)
            : base(sortOp.SpecifiedQuerySettings)
        {
            _sortOp = sortOp;
            _browser = null;
            Debug.Assert(!OperatingSystem.IsBrowser());
            Debug.Assert(sortOp is IOrderedEnumerable<TSource>);
        }

        internal OrderedParallelQuery(IOrderedEnumerable<TSource> inner)
            : base(default)
        {
            _sortOp = null;
            _browser = inner;
            Debug.Assert(OperatingSystem.IsBrowser());
        }

        [System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
        internal QueryOperator<TSource> SortOperator
        {
            get { return _sortOp; }
        }

        internal IOrderedEnumerable<TSource> OrderedEnumerable
        {
            get { return _browser ?? (IOrderedEnumerable<TSource>)_sortOp; }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the sequence.
        /// </summary>
        /// <returns>An enumerator that iterates through the sequence.</returns>
        public override IEnumerator<TSource> GetEnumerator()
        {
            return (_browser ?? _sortOp).GetEnumerator();
        }
    }
}
