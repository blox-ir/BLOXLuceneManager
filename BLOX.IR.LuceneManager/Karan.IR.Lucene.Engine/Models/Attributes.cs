using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Karan.IR.Lucene.Engine
{
    public class LuceneTypeAttribute : Attribute
    {
        public LuceneTypeAttribute(bool store, int index, bool key)
        {
            this.Store = store;
            this.Index = index;
            this.Key = key;
        }
        public bool Store { get; set; }
        public bool Key { get; set; }
        /// <summary>
        /// 1 : Index.ANALYZED
        /// 2 : Index.ANALYZED_NO_NORMS
        /// 3 : Index.NO
        /// 4 : Index.NOT_ANALYZED
        /// 5 : Index.NOT_ANALYZED_NO_NORMS
        /// </summary>
        public int Index { get; set; }
    }
}